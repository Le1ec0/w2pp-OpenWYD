using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class GuildDisableTests
{
    public static void Run()
    {
        GuildDisablePublishesOnlyChangedStateAndKeepsAccountGuild();
        GuildDisableBlocksTheGuildBypassForEitherPlayer();
        PartyMembershipStillAllowsTheSkillWhenGuildIsDisabled();
        GuildDisableSignalMatchesRetailWire();
    }

    private static void GuildDisablePublishesOnlyChangedStateAndKeepsAccountGuild()
    {
        var world = CreateWorld();
        var update = world.TrySetGuildDisabled(1, true);

        if (update is null)
            throw new InvalidOperationException("guildoff did not change state.");
        Check(update.Disabled && update.VisibleParticipantIds.Order().SequenceEqual(new[] { 1, 2 }),
            "guildoff did not capture the changed state and the visible-score recipients.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(update.ScoreMobSnapshot.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) == 0,
            "guildoff score projection did not hide the guild ID.");
        Check(world.TryGetCharacterSnapshot(1, out var savedMob, out _, out _) &&
            BinaryPrimitives.ReadUInt16LittleEndian(savedMob!.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) == 7,
            "guildoff modified the persisted character guild instead of keeping a transient projection.");
        Check(world.TrySetGuildDisabled(1, true) is null,
            "Repeating guildoff emitted a second state transition.");

        var on = world.TrySetGuildDisabled(1, false);
        Check(on is { Disabled: false } &&
            BinaryPrimitives.ReadUInt16LittleEndian(on.ScoreMobSnapshot.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) == 7,
            "guildon did not restore the authoritative guild in the score projection.");
        Check(world.TrySetGuildDisabled(1, false) is null,
            "Repeating guildon emitted a second state transition.");
    }

    private static void GuildDisableBlocksTheGuildBypassForEitherPlayer()
    {
        var attackerDisabled = CreateWorld();
        Check(attackerDisabled.TrySetGuildDisabled(1, true) is not null, "Attacker guildoff did not change state.");
        AssertPartySkillBlocked(attackerDisabled, "Attacker guildoff still allowed a same-guild target.");

        var targetDisabled = CreateWorld();
        Check(targetDisabled.TrySetGuildDisabled(2, true) is not null, "Target guildoff did not change state.");
        AssertPartySkillBlocked(targetDisabled, "Target guildoff still allowed a same-guild target.");

        var restored = CreateWorld();
        Check(restored.TrySetGuildDisabled(1, true) is not null && restored.TrySetGuildDisabled(1, false) is not null,
            "guildon did not transition the attacker back to guild-enabled state.");
        var result = restored.TryApplySkillAttack(1, 2, CreatePartySkill(), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);
        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: > 1000 },
            "guildon failed to restore the same-guild skill bypass.");
    }

    private static void PartyMembershipStillAllowsTheSkillWhenGuildIsDisabled()
    {
        var world = CreateWorld();
        Check(world.TrySetGuildDisabled(1, true) is not null && world.TrySetGuildDisabled(2, true) is not null,
            "Both party members must transition to guildoff for this case.");

        var outsider = world.TryApplySkillAttack(1, 2, CreatePartySkill(), null, 0, 90, out var outsiderOutcome, targetIndex: 0, clientTick: 100);
        Check(outsider == LegacySkillAttackResult.PartySkillBlocked && outsiderOutcome is null,
            "Two guildoff outsiders bypassed bParty even though the legacy zero-guild normalization must block them.");
        Check(world.TryGetCombatState(2, out var untouchedTarget) && untouchedTarget!.CurrentScore.Hp == 1000,
            "The unpartied GuildDisable rejection changed target HP.");

        Check(world.TrySetParty(1, [1, 2]), "GuildDisable party fixture could not be formed.");
        var result = world.TryApplySkillAttack(1, 2, CreatePartySkill(), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);
        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: > 1000 },
            "GuildDisable overrode the same-party eligibility path.");
    }

    private static void GuildDisableSignalMatchesRetailWire()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var decoded = codec.Decode(new GuildDisableConfirmation(1).ToFrame(codec, 123, 4));

        Check(decoded.IsChecksumValid && decoded.Header.Type == GuildDisableConfirmation.MessageType &&
            decoded.Header.Size == GuildDisableConfirmation.PacketSize && decoded.Header.Id == AttackConfirmation.SceneId &&
            BinaryPrimitives.ReadInt32LittleEndian(decoded.Payload.Span) == 1,
            "MSG_GuildDisable did not encode a 16-byte scene signal with Parm=1.");
    }

    private static void AssertPartySkillBlocked(WorldHub world, string message)
    {
        var result = world.TryApplySkillAttack(1, 2, CreatePartySkill(), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);
        Check(result == LegacySkillAttackResult.PartySkillBlocked && outcome is null, message);
        Check(world.TryGetCombatState(2, out var target) && target!.CurrentScore.Hp == 1000,
            "A GuildDisable-blocked party skill changed target HP.");
    }

    private static WorldHub CreateWorld()
    {
        var world = new WorldHub();
        Check(world.Enter(1, "GUILD_OFF_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "GUILD_OFF_TARGET", (_, _) => ValueTask.CompletedTask),
            "GuildDisable participants were not registered.");
        var attacker = CreateMob(5000);
        var target = CreateMob(1000);
        BinaryPrimitives.WriteUInt16LittleEndian(attacker.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 7);
        BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 7);
        Check(world.SetCharacterState(1, 0, 7, 1, 0, 0, 2000, 2000, attacker) &&
            world.SetCharacterState(2, 0, 7, 1, 0, 0, 2005, 2005, target),
            "GuildDisable character snapshots were not installed.");
        return world;
    }

    private static byte[] CreateMob(int hp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, hp, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static LegacySkillDefinition CreatePartySkill() => new(
        27, 0, 0, 0, 0, 0, 6, 100, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, 1, 0, 0, 0, "GUILD_DISABLE_HEAL");

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
