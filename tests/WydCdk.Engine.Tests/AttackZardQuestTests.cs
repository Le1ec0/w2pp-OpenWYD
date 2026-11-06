using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackZardQuestTests
{
    private const int AttackerId = 1;
    private const int ZardId = 20_501;
    private const short AttackerX = 2_000;
    private const short AttackerY = 2_000;

    public static void Run()
    {
        PhysicalZardHitAdvancesProgressWithoutChangingDamageOrAbsorbingHp();
        ZardSkillHitAdvancesProgressWithoutAbsorbingHp();
        EightiethZardHitGrantsRuneAndResetsProgress();
        FullCarryAtEightiethHitReportsNoSpaceAndResetsProgress();
        LethalEightiethHitRetainsRuneAcrossNpcDeathFinalization();
    }

    private static void PhysicalZardHitAdvancesProgressWithoutChangingDamageOrAbsorbingHp()
    {
        var world = CreateWorld(counter: 0, fillCarry: false, out var affects);

        var result = world.TryApplyPhysicalAttack(
            AttackerId,
            ZardId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            itemData: CreateItemData());

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: > 0 },
            $"The physical hit on Zard did not resolve: result={result}, damage={outcome?.Damage}.");
        var resolved = outcome!;
        var attackerMob = ReadAttackerMob(world);
        var questItem = ReadQuestItem(attackerMob);
        Check(questItem.Effect1 == 51 && questItem.Value1 == 1,
            $"The Zard counter was not stored in equipment slot 12: {questItem}.");
        Check(resolved.ZardMobLeftCount == 1, $"The first Zard progress signal was not 1: {resolved.ZardMobLeftCount}.");
        Check(resolved.RemainingHp == 100_000 - resolved.Damage,
            "The quest branch changed the physical damage applied to Zard.");
        Check(resolved.AttackerHpAbsorbed == 0,
            $"The Type 8 HP absorb ran against Zard: absorbed={resolved.AttackerHpAbsorbed}.");
        Check(world.TryGetCombatState(AttackerId, out var state) && state!.CurrentScore.Hp == 50,
            $"The Type 8 HP absorb changed the attacker's HP: hp={state?.CurrentScore.Hp}.");
        Check(affects[0] == 8, "The Type 8 test affect was not installed.");
    }

    private static void ZardSkillHitAdvancesProgressWithoutAbsorbingHp()
    {
        var world = CreateWorld(counter: 4, fillCarry: false, out _);
        var result = world.TryApplySkillAttack(
            AttackerId,
            ZardId,
            CreateSkill(),
            CreateItemData(),
            weather: 0,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 },
            $"The skill hit on Zard did not resolve: result={result}, damage={outcome?.Damage}.");
        var resolved = outcome!;
        var questItem = ReadQuestItem(ReadAttackerMob(world));
        Check(questItem.Effect1 == 51 && questItem.Value1 == 5,
            $"The Zard skill hit did not advance equipment slot 12: {questItem}.");
        Check(resolved.ZardMobLeftCount == 5, $"The Zard skill progress signal was not 5: {resolved.ZardMobLeftCount}.");
        Check(world.TryGetCombatState(AttackerId, out var state) && state!.CurrentScore.Hp == 50,
            $"The Type 8 HP absorb ran on a Zard skill hit: hp={state?.CurrentScore.Hp}.");
    }

    private static void EightiethZardHitGrantsRuneAndResetsProgress()
    {
        var world = CreateWorld(counter: 79, fillCarry: false, out _);
        var result = world.TryApplyPhysicalAttack(
            AttackerId,
            ZardId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            itemData: CreateItemData());

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: > 0 },
            "The eightieth physical Zard hit did not resolve.");
        var resolved = outcome!;
        var questItem = ReadQuestItem(ReadAttackerMob(world));
        Check(questItem.Effect1 == 51 && questItem.Value1 == 0,
            $"The Zard counter did not reset after the reward threshold: {questItem}.");
        Check(resolved.ZardMobLeftCount == 80, $"The reward threshold signal did not send 80 before reset: {resolved.ZardMobLeftCount}.");
        Check(resolved.ItemDrops is { Count: 1 } drops && drops[0].InventorySlot == 0 &&
            drops[0].Item == new LegacyItem(475, 0, 0, 0, 0, 0, 0),
            $"The eightieth Zard hit did not award the zeroed rune 475 in the first carry slot: {resolved.ItemDrops?.Count} drops.");
        var attackerMob = ReadAttackerMob(world);
        var runeOffset = LegacyAccountSnapshot.MobCarryOffset;
        Check(LegacyItem.Read(attackerMob.AsSpan(runeOffset, LegacyItem.SizeInBytes)) == new LegacyItem(475, 0, 0, 0, 0, 0, 0),
            "The rune reward was not inserted into the attacker's carry.");
    }

    private static void FullCarryAtEightiethHitReportsNoSpaceAndResetsProgress()
    {
        var world = CreateWorld(counter: 79, fillCarry: true, out _);
        var result = world.TryApplyPhysicalAttack(
            AttackerId,
            ZardId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            itemData: CreateItemData());

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: > 0 },
            "The eightieth Zard hit with a full carry did not resolve.");
        var resolved = outcome!;
        var questItem = ReadQuestItem(ReadAttackerMob(world));
        Check(questItem.Effect1 == 51 && questItem.Value1 == 0,
            $"The Zard counter did not reset after the failed reward insertion: {questItem}.");
        Check(resolved.ZardMobLeftCount == 80, $"The full-carry threshold signal did not send 80: {resolved.ZardMobLeftCount}.");
        Check(resolved.ItemDrops is null or { Count: 0 },
            "The full carry produced a rune item relay.");
        Check(resolved.PrivateNotices is { Count: 1 } notices &&
            notices[0] == new LegacyPrivateNotice(AttackerId, "Não há espaço no inventário para realizar transações."),
            $"The full carry did not return the localized no-space notice: {resolved.PrivateNotices?.Count} notices.");
    }

    private static void LethalEightiethHitRetainsRuneAcrossNpcDeathFinalization()
    {
        var world = CreateWorld(counter: 79, fillCarry: false, out _, zardHp: 1);
        var result = world.TryApplyPhysicalAttack(
            AttackerId,
            ZardId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99,
            itemData: CreateItemData());

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { TargetRemoved: true, Damage: > 0 },
            "The lethal eightieth Zard hit did not complete NPC death finalization.");
        var resolved = outcome!;
        Check(resolved.ZardMobLeftCount == 80 && resolved.ItemDrops is { } drops &&
            drops.Any(drop => drop.Item == new LegacyItem(475, 0, 0, 0, 0, 0, 0)),
            "NPC death finalization discarded the Zard rune reward or progress signal.");
        var attackerMob = ReadAttackerMob(world);
        Check(LegacyItem.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes)) ==
            new LegacyItem(475, 0, 0, 0, 0, 0, 0),
            "The lethal Zard reward was not kept in the attacker's carry.");
    }

    private static WorldHub CreateWorld(int counter, bool fillCarry, out byte[] affects, int zardHp = 100_000)
    {
        affects = new byte[LegacyAccountSnapshot.AffectStride];
        affects[0] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(affects.AsSpan(2), 1 << 3);

        var attackerMob = CreateMob("ZARD_QUEST_ATTACKER", currentHp: 50, maximumHp: 1_000, damage: 1_000);
        var questItem = new LegacyItem(0, 0, checked((byte)counter), 0, 0, 0, 0);
        questItem.Write(attackerMob.AsSpan(EquipmentSlotOffset(12), LegacyItem.SizeInBytes));
        if (fillCarry)
        {
            for (var slot = 0; slot < LegacyAccountSnapshot.MobCarryCount; slot++)
                new LegacyItem(1, 0, 0, 0, 0, 0, 0).Write(
                    attackerMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset + slot * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        }

        var world = new WorldHub();
        Check(world.Enter(AttackerId, "ZARD_QUEST_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.SetCharacterState(AttackerId, 0, guildId: 0, clan: 1, guildLevel: 0, coin: 0,
                AttackerX, AttackerY, attackerMob, LegacyAccountSnapshot.ClassMasterMortal, affects),
            "The Zard quest attacker was not initialized.");

        var zard = CreateMob("Zard", currentHp: zardHp, maximumHp: 100_000, damage: 100);
        var registeredId = world.EnterNpc(zard, AttackerX + 5, AttackerY + 5,
            requestedConnectionId: ZardId, generateIndex: 1);
        Check(registeredId == ZardId, $"Zard was not registered with the requested ID: {registeredId}.");
        return world;
    }

    private static byte[] CreateMob(string name, int currentHp, int maximumHp, int damage)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        new LegacyScore(100, 0, damage, 0, 0, 0, 0, maximumHp, 1_000, currentHp, 1_000,
            0, 0, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static byte[] ReadAttackerMob(WorldHub world)
    {
        Check(world.TryGetCharacterSnapshot(AttackerId, out var mob, out _, out _) && mob is not null,
            "The Zard quest attacker snapshot is unavailable.");
        return mob!;
    }

    private static LegacyItem ReadQuestItem(byte[] mob) =>
        LegacyItem.Read(mob.AsSpan(EquipmentSlotOffset(12), LegacyItem.SizeInBytes));

    private static int EquipmentSlotOffset(int slot) =>
        LegacyAccountSnapshot.MobEquipmentOffset + slot * LegacyItem.SizeInBytes;

    private static LegacyItemDataTable CreateItemData() =>
        LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    private static LegacySkillDefinition CreateSkill() =>
        new(79, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 0, 0, 0, 0, 0, "ZARD_QUEST_SKILL");

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
