using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackRenascimentoTests
{
    private const string SkillRow = "31,281,1,0,5,5,8,0,0,0,0,0,0,7.0.0.24.0.0.0.0,24.0.0.8.0.0.0.0,0,0,0,1,0,0,0,0,Renascimento";

    public static void Run()
    {
        SuccessfulRenascimentoRevivesDeadPlayerAndAppliesType55();
        FailedRenascimentoChanceStillZerosCasterCurrentMana();
        ExistingType55DoesNotPreventRevivalOrGetReplaced();
        FullAffectTableDoesNotPreventRevival();
    }

    private static void SuccessfulRenascimentoRevivesDeadPlayerAndAppliesType55()
    {
        var world = CreateWorld(out var skill);

        var result = world.TryApplySkillAttack(
            1, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var outcome,
            resurrectionRoll: 69, resurrectionHpRoll: 9, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is not null,
            $"Renascimento did not accept its dead player target: {result}.");
        var applied = outcome ?? throw new InvalidOperationException("Renascimento returned no outcome after accepting the target.");
        Check(applied.RemainingHp == 1_900 && applied.TargetResourceChanged && applied.AttackerResourceChanged,
            $"Renascimento did not restore 19 percent of the caster MaxHP or expose resource updates: HP={applied.RemainingHp}, targetChanged={applied.TargetResourceChanged}, casterChanged={applied.AttackerResourceChanged}.");
        Check(applied.TargetAffectSnapshot is { Length: LegacyAccountSnapshot.AffectStride } affect &&
            affect[0] == 55 && affect[1] == 0 && BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 0 &&
            BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(4)) == 2,
            "Renascimento did not install the 7.69 Type 55 affect in the first empty slot.");
        Check(world.TryGetResourceState(1, out var caster, out _, out var requestedMp) &&
            caster is { CurrentMana: 0 } && requestedMp == 400,
            $"Renascimento did not zero current caster MP while preserving the 7.69 requested-MP value: current={caster?.CurrentMana}, requested={requestedMp}.");
        Check(world.TryGetResourceState(2, out var target, out _, out _) && target?.CurrentScore.Hp == 1_900,
            $"Renascimento did not store the restored target HP: {target?.CurrentScore.Hp}.");
    }

    private static void FailedRenascimentoChanceStillZerosCasterCurrentMana()
    {
        var world = CreateWorld(out var skill);

        var result = world.TryApplySkillAttack(
            1, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var outcome,
            resurrectionRoll: 70, resurrectionHpRoll: 0, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: 0, AttackerResourceChanged: true, TargetResourceChanged: false, TargetAffectSnapshot: null },
            $"A failed Renascimento roll changed the target or skipped the caster MP mutation: result={result}, hp={outcome?.RemainingHp}, targetChanged={outcome?.TargetResourceChanged}.");
        Check(world.TryGetResourceState(1, out var caster, out _, out var requestedMp) && caster is { CurrentMana: 0 } && requestedMp == 400,
            $"A failed Renascimento roll did not zero current MP while preserving requested MP: current={caster?.CurrentMana}, requested={requestedMp}.");
        Check(world.TryGetResourceState(2, out var target, out _, out _) && target?.CurrentScore.Hp == 0,
            $"A failed Renascimento roll changed the dead target HP: {target?.CurrentScore.Hp}.");
    }

    private static void ExistingType55DoesNotPreventRevivalOrGetReplaced()
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 55;
        affect[1] = 9;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 88);
        var before = affect.ToArray();
        var world = CreateWorld(out var skill, affect);

        var result = world.TryApplySkillAttack(
            1, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var outcome,
            resurrectionRoll: 69, resurrectionHpRoll: 0, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: 1_000, TargetResourceChanged: true, TargetAffectSnapshot: null },
            $"An existing Type 55 prevented Renascimento from restoring HP or was reported as a new affect: result={result}, hp={outcome?.RemainingHp}.");
        Check(world.TryGetCharacterSnapshot(2, out _, out _, out _, out _, out var actualAffect) && actualAffect is not null && actualAffect.SequenceEqual(before),
            "Renascimento overwrote an existing Type 55 affect.");
    }

    private static void FullAffectTableDoesNotPreventRevival()
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        for (var slot = 0; slot < LegacyAccountSnapshot.AffectStride / 8; slot++)
            affect[slot * 8] = checked((byte)(slot + 1));
        var world = CreateWorld(out var skill, affect);

        var result = world.TryApplySkillAttack(
            1, 2, skill, itemData: null, weather: 0, randomFactor: 90, out var outcome,
            resurrectionRoll: 69, resurrectionHpRoll: 0, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: 1_000, TargetResourceChanged: true, TargetAffectSnapshot: null },
            $"A full affect table prevented Renascimento from restoring HP: result={result}, hp={outcome?.RemainingHp}.");
    }

    private static WorldHub CreateWorld(out LegacySkillDefinition skill, byte[]? targetAffect = null)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "RENASCIMENTO_CASTER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "RENASCIMENTO_TARGET", (_, _) => ValueTask.CompletedTask),
            "Renascimento participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, CreateMob(10_000, 1_000, 800, 400)) &&
            world.SetCharacterState(2, 0, 0, 0, 0, 0, 2_002, 2_000, CreateMob(8_000, 500, 0, 200),
                LegacyAccountSnapshot.ClassMasterMortal, targetAffect ?? new byte[LegacyAccountSnapshot.AffectStride]),
            "Renascimento character states were not installed.");

        skill = LegacySkillDataTable.Load(new StringReader(SkillRow))[31]
            ?? throw new InvalidOperationException("The Renascimento skill fixture did not load.");
        return world;
    }

    private static byte[] CreateMob(int maxHp, int maxMp, int hp, int mp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(50, 0, 0, 0, 0, 0, 0, maxHp, maxMp, hp, mp,
            0, 0, 0, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
