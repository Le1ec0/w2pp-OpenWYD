using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackFriendlyFireGateTests
{
    private const int AttackerId = 1;
    private const int TargetPlayerId = 2;
    private const int TargetNpcId = 20_101;
    private const int TargetHp = 10_000;

    public static void SamePartyPhysicalDamageRemainsActive()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);
        Check(world.TrySetParty(AttackerId, [AttackerId, TargetPlayerId]), "The same-party physical fixture could not be formed.");

        var result = world.TryApplyPhysicalAttack(AttackerId, TargetPlayerId, randomFactor: 99, out var outcome, attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: > 0, RemainingHp: < TargetHp },
            $"The skill-only friendly-fire gate incorrectly suppressed a same-party physical hit: result={result}, outcome={outcome}.");
    }

    public static void SameGuildPhysicalDamageRemainsActiveForPlayersAndNpcs()
    {
        var playerWorld = CreateWorld(attackerGuild: 77, targetGuild: 77);
        var playerResult = playerWorld.TryApplyPhysicalAttack(AttackerId, TargetPlayerId, randomFactor: 99, out var playerOutcome, attackMissRoll: 99);
        Check(playerResult == LegacyPhysicalAttackResult.Accepted && playerOutcome is { Damage: > 0, RemainingHp: < TargetHp },
            $"The skill-only friendly-fire gate incorrectly suppressed a same-guild player hit: result={playerResult}, outcome={playerOutcome}.");

        var npcWorld = CreateWorld(attackerGuild: 77, targetGuild: 0, includePlayerTarget: false);
        var npc = AddNpc(npcWorld, guildId: 77);
        var npcResult = npcWorld.TryApplyPhysicalAttack(AttackerId, npc, randomFactor: 99, out var npcOutcome, attackMissRoll: 99);
        Check(npcResult == LegacyPhysicalAttackResult.Accepted && npcOutcome is { Damage: > 0, RemainingHp: < TargetHp },
            $"The skill-only friendly-fire gate incorrectly suppressed a same-guild NPC hit: result={npcResult}, outcome={npcOutcome}.");
    }

    public static void GuildDisabledAttackerMayDamageSameGuildWithHostileSkill()
    {
        var world = CreateWorld(attackerGuild: 77, targetGuild: 77);
        Check(world.TrySetGuildDisabled(AttackerId, disabled: true) is not null, "The guild-disabled physical fixture could not disable the attacker guild.");
        var skill = CreateDamageSkill();
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

        var result = world.TryApplySkillAttack(
            AttackerId, TargetPlayerId, skill, itemData, weather: 0, randomFactor: 99, out var outcome,
            attackMissRoll: 99, targetIndex: 0, clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0, RemainingHp: < TargetHp },
            $"The guild-disabled attacker was still treated as same-guild: result={result}, outcome={outcome}.");
    }

    public static void BattleRoyaleOverrideAppliesOnlyInsideItsInclusiveArea()
    {
        var inside = CreateWorld(attackerGuild: 77, targetGuild: 77);
        inside.SetCombatWorldState(new LegacyCombatWorldState(BattleRoyaleState: 1, BattleRoyaleItem: 700));
        Check(inside.UpdatePosition(AttackerId, 2_604, 1_708), "The Battle Royale inside-boundary position was not set.");
        Check(inside.UpdatePosition(TargetPlayerId, 2_609, 1_708), "The Battle Royale inside-boundary target position was not set.");
        var skill = CreateDamageSkill();
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var insideResult = inside.TryApplySkillAttack(AttackerId, TargetPlayerId, skill, itemData, 0, 99, out var insideOutcome, 99, 0, 100, attackMissRoll: 99);
        Check(insideResult == LegacySkillAttackResult.Accepted && insideOutcome is { Damage: > 0, RemainingHp: < TargetHp },
            $"The active Battle Royale guild override did not permit same-guild damage inside the arena: result={insideResult}, outcome={insideOutcome}.");

        var outside = CreateWorld(attackerGuild: 77, targetGuild: 77);
        outside.SetCombatWorldState(new LegacyCombatWorldState(BattleRoyaleState: 1, BattleRoyaleItem: 700));
        Check(outside.UpdatePosition(AttackerId, 2_603, 1_708), "The Battle Royale outside-boundary position was not set.");
        Check(outside.UpdatePosition(TargetPlayerId, 2_609, 1_708), "The Battle Royale outside-boundary target position was not set.");
        var outsideResult = outside.TryApplySkillAttack(AttackerId, TargetPlayerId, skill, itemData, 0, 99, out var outsideOutcome, 99, 0, 100, attackMissRoll: 99);
        Check(outsideResult == LegacySkillAttackResult.Accepted && outsideOutcome is { Damage: 0, RemainingHp: TargetHp },
            $"The Battle Royale guild override escaped the inclusive arena boundary: result={outsideResult}, outcome={outsideOutcome}.");
    }

    public static void SamePartySkillDamageIsSuppressedWithoutBlockingTheSkillSlot()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);
        Check(world.TrySetParty(AttackerId, [AttackerId, TargetPlayerId]), "The same-party skill fixture could not be formed.");
        var skill = CreateDamageSkill();
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

        var result = world.TryApplySkillAttack(
            AttackerId,
            TargetPlayerId,
            skill,
            itemData,
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            targetIndex: 0,
            clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            $"A same-party skill target was rejected instead of resolving with zero damage: {result}.");
        Check(outcome!.Damage == 0 && outcome.RemainingHp == TargetHp,
            $"A skill damaged a member of the same party: damage={outcome.Damage}, hp={outcome.RemainingHp}.");
    }

    public static void SameGuildSkillDamageIsSuppressedForPlayersAndNpcs()
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var skill = CreateDamageSkill();

        var playerWorld = CreateWorld(attackerGuild: 77, targetGuild: 77);
        var playerResult = playerWorld.TryApplySkillAttack(
            AttackerId, TargetPlayerId, skill, itemData, weather: 0, randomFactor: 99, out var playerOutcome,
            attackMissRoll: 99, targetIndex: 0, clientTick: 100);
        Check(playerResult == LegacySkillAttackResult.Accepted && playerOutcome is { Damage: 0, RemainingHp: TargetHp },
            $"A same-guild skill hit did not resolve its slot with zero damage: result={playerResult}, outcome={playerOutcome}.");

        var npcWorld = CreateWorld(attackerGuild: 77, targetGuild: 0, includePlayerTarget: false);
        var npc = AddNpc(npcWorld, guildId: 77);
        var npcResult = npcWorld.TryApplySkillAttack(
            AttackerId, npc, skill, itemData, weather: 0, randomFactor: 99, out var npcOutcome,
            attackMissRoll: 99, targetIndex: 0, clientTick: 100);
        Check(npcResult == LegacySkillAttackResult.Accepted && npcOutcome is { Damage: 0, RemainingHp: TargetHp },
            $"A same-guild NPC received skill damage: result={npcResult}, outcome={npcOutcome}.");
    }

    private static WorldHub CreateWorld(int attackerGuild, int targetGuild, bool includePlayerTarget = true)
    {
        var world = new WorldHub();
        Check(world.Enter(AttackerId, "FRIENDLY_FIRE_ATTACKER", (_, _) => ValueTask.CompletedTask), "The friendly-fire attacker was not registered.");
        Check(world.SetCharacterState(
                AttackerId, 0, attackerGuild, 1, 0, 0, 2_000, 2_000,
                CreateMob("FRIENDLY_FIRE_ATTACKER", attackerGuild, hp: 5_000, damage: 2_000, magic: 2_000)),
            "The friendly-fire attacker state was not installed.");

        if (includePlayerTarget)
        {
            Check(world.Enter(TargetPlayerId, "FRIENDLY_FIRE_TARGET", (_, _) => ValueTask.CompletedTask), "The friendly-fire player target was not registered.");
            Check(world.SetCharacterState(
                    TargetPlayerId, 0, targetGuild, 2, 0, 0, 2_005, 2_000,
                    CreateMob("FRIENDLY_FIRE_TARGET", targetGuild, hp: TargetHp, damage: 0, magic: 0)),
                "The friendly-fire player target state was not installed.");
        }

        return world;
    }

    private static int AddNpc(WorldHub world, int guildId)
    {
        var npcMob = CreateMob("FRIENDLY_FIRE_NPC", guildId, hp: TargetHp, damage: 0, magic: 0);
        var npcId = world.EnterNpc(npcMob, 2_005, 2_000, requestedConnectionId: TargetNpcId, generateIndex: 1);
        Check(npcId == TargetNpcId, "The friendly-fire NPC was not registered at the requested ID.");
        return npcId;
    }

    private static byte[] CreateMob(string name, int guildId, int hp, int damage, int magic)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guildId));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), magic);
        new LegacyScore(50, 0, damage, 0, 100, 0, 0, hp, 1_000, hp, 1_000, 100, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static LegacySkillDefinition CreateDamageSkill() => new(
        10, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "FRIENDLY_FIRE_DAMAGE");

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
