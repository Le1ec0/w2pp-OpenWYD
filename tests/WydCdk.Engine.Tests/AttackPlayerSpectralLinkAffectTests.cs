using System.Buffers.Binary;
using System.IO;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerSpectralLinkAffectTests
{
    private const int AttackerId = 1;
    private const int TargetNpcId = 20_001;
    private const int BaseHp = 50_000;
    private const int CurrentSpecial3 = 20;

    public static void Run()
    {
        ScoreStageAddsCurrentSpecial3ForEveryActiveType37Record();
        Skill81AddsCurrentSpecial3ToTheForceDamageOfSubsequentAttacks();
    }

    private static void ScoreStageAddsCurrentSpecial3ForEveryActiveType37Record()
    {
        var affects = new byte[LegacyAccountSnapshot.AffectStride];
        affects[0] = 37;
        affects[8] = 37;

        var stage = LegacyCurrentScoreMath.CalculateSpectralLinkForceDamageStage(affects, CurrentSpecial3);

        Check(stage.ForceDamageAddition == CurrentSpecial3 * 2,
            $"Type 37 did not add Special3 once per active affect; actual={stage.ForceDamageAddition}.");
    }

    private static void Skill81AddsCurrentSpecial3ToTheForceDamageOfSubsequentAttacks()
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var skillData = LegacySkillDataTable.Load(new StringReader(
            "81,39,0,108,6,0,0,0,0,0,37,0,60,8.0.0.7.0.0.0.0,9.0.0.7.0.0.0.0,0,0,0,1,0,0,0,0,Ligacao_Espectral"));
        var world = new WorldHub(itemData: itemData, skillData: skillData);
        var attackerMob = CreateMob("SPECTRAL_ATTACKER", 5_000, damage: 1_000, special3: CurrentSpecial3);
        Check(world.Enter(AttackerId, "SPECTRAL_ATTACKER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  AttackerId, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob,
                  LegacyAccountSnapshot.ClassMasterMortal, new byte[LegacyAccountSnapshot.AffectStride],
                  new byte[LegacyAccountSnapshot.MobExtraStride]),
            "The Spectral Link caster fixture was not installed.");
        Check(world.EnterNpc(CreateMob("SPECTRAL_TARGET", BaseHp, damage: 0, special3: 0),
                  2_001, 2_000, requestedConnectionId: TargetNpcId, generateIndex: 1) == TargetNpcId,
            "The Spectral Link target NPC was not registered.");

        Check(world.TryApplyPhysicalAttack(
                  AttackerId, TargetNpcId, randomFactor: 99, out var baselineOutcome,
                  attackMissRoll: 99, itemData: itemData) == LegacyPhysicalAttackResult.Accepted &&
              baselineOutcome is { Damage: > 0, ForceDamage: 0 },
            "The baseline physical hit without Type 37 did not resolve cleanly.");

        var skill = skillData[81] ?? throw new InvalidOperationException("Skill 81 was not loaded.");
        Check(world.TryApplySkillAttack(
                  AttackerId, AttackerId, skill, itemData, weather: 0, randomFactor: 100,
                  out var buffOutcome) == LegacySkillAttackResult.Accepted &&
              buffOutcome?.TargetAffectSnapshot is { } affect && HasAffectType(affect, 37),
            "Skill 81 did not apply its Type 37 affect to the caster.");

        var appliedAffect = buffOutcome?.TargetAffectSnapshot ??
            throw new InvalidOperationException("Skill 81 omitted the Type 37 snapshot.");
        Check(GetAffectTime(appliedAffect, 37) == 19,
            "Skill 81 did not preserve the 7.69 Special2-derived delay and affect duration.");
        Check(world.TryApplySkillAttack(
                  AttackerId, AttackerId, skill, itemData, weather: 0, randomFactor: 100,
                  out var recastOutcome) == LegacySkillAttackResult.Accepted &&
              recastOutcome?.TargetAffectSnapshot is { } recastAffect && HasExactlyOneAffectType(recastAffect, 37) &&
              GetAffectTime(recastAffect, 37) == 19,
            "Recasting skill 81 did not refresh one Type 37 affect without stacking.");

        Check(world.TryApplyPhysicalAttack(
                  AttackerId, TargetNpcId, randomFactor: 99, out var affectedOutcome,
                  attackMissRoll: 99, itemData: itemData) == LegacyPhysicalAttackResult.Accepted &&
              affectedOutcome is { Damage: > 0, ForceDamage: CurrentSpecial3 } &&
              affectedOutcome.Damage == baselineOutcome!.Damage + CurrentSpecial3,
            $"Type 37 did not add Special3 to the physical ForceDamage stage; baseline={baselineOutcome?.Damage}, affected={affectedOutcome?.Damage}, force={affectedOutcome?.ForceDamage}.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < 19; tick++)
            expiry = world.TryProcessPlayerTimerAffects(AttackerId);
        Check(expiry is not null && !HasAffectType(expiry.AffectSnapshot, 37),
            "The Type 37 player affect did not expire after its 7.69 duration.");
        Check(world.TryApplyPhysicalAttack(
                  AttackerId, TargetNpcId, randomFactor: 99, out var expiredOutcome,
                  attackMissRoll: 99, itemData: itemData) == LegacyPhysicalAttackResult.Accepted &&
              expiredOutcome is { Damage: > 0, ForceDamage: 0 },
            $"An expired Type 37 affect continued to add ForceDamage to the caster's attack; actual={expiredOutcome?.ForceDamage}.");
    }

    private static byte[] CreateMob(string name, int hp, int damage, int special3)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15))
            .CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        var score = new LegacyScore(
            Level: 50, Ac: 0, Damage: damage, Merchant: 0, AttackRun: 0, Direction: 0,
            ChaosRate: 0, MaxHp: BaseHp, MaxMp: 5_000, Hp: hp, Mp: 5_000,
            Strength: 100, Intelligence: 50, Dexterity: 10, Constitution: 0,
            Special1: 0, Special2: 0, Special3: checked((short)special3), Special4: 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 2;
        return mob;
    }

    private static bool HasAffectType(ReadOnlySpan<byte> affects, byte affectType)
    {
        for (var offset = 0; offset < affects.Length; offset += 8)
        {
            if (affects[offset] == affectType)
                return true;
        }

        return false;
    }

    private static bool HasExactlyOneAffectType(ReadOnlySpan<byte> affects, byte affectType)
    {
        var count = 0;
        for (var offset = 0; offset < affects.Length; offset += 8)
        {
            if (affects[offset] == affectType)
                count++;
        }

        return count == 1;
    }

    private static uint GetAffectTime(ReadOnlySpan<byte> affects, byte affectType)
    {
        for (var offset = 0; offset + 8 <= affects.Length; offset += 8)
        {
            if (affects[offset] == affectType)
                return BinaryPrimitives.ReadUInt32LittleEndian(affects[(offset + 4)..]);
        }

        return 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
