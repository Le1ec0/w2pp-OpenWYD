using System.Buffers.Binary;
using System.IO;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerSoulAffectTests
{
    private const int CasterId = 1;
    private const int BaseHp = 1_000;
    private const int BaseMp = 500;
    private const int CurrentHp = 333;
    private const int CurrentMp = 222;
    private const int CelestialClassMaster = 3;
    private const byte IntelligenceSoul = 3;

    public static void Run()
    {
        Skill82IsRejectedByThePlayerAttackGateAsPassive();
        Skill102RebuildsAndExpiresType29PlayerScore();
    }

    private static void Skill82IsRejectedByThePlayerAttackGateAsPassive()
    {
        const string sourceRow =
            "82,69,0,15,6,0,0,0,0,0,29,1,8,19.0.0.24.0.0.0.0,19.0.0.8.0.0.0.0,0,0,0,1,0,0,1,0,Pericia_do_cacador";
        var table = LegacySkillDataTable.Load(new StringReader(sourceRow));
        var skill = table[82] ?? throw new InvalidOperationException("The 7.69 skill 82 row was not loaded.");
        Check(skill.Passive == 1, "The 7.69 skill 82 row is no longer marked passive.");

        var decision = LegacySkillAttackGate.Evaluate(
            table,
            skillIndex: 82,
            characterClass: 3,
            learnedSkill: 1u << (82 % 24),
            clientTick: 1_000,
            previousAttackTick: uint.MaxValue);
        Check(decision.Result == SkillAttackValidationResult.PassiveSkill,
            $"The player attack gate accepted passive skill 82: result={decision.Result}.");
    }

    private static void Skill102RebuildsAndExpiresType29PlayerScore()
    {
        var skill = LoadSkill(
            "102,0,0,75,1200,1,0,0,0,0,29,5,60,24.0.0.24.0.0.0.0,24.0.0.24.0.0.0.0,0,0,0,1,0,0,0,0,Limite_da_Alma");
        var world = CreateWorld(skill.Id, level: 400, special2: 0, out var itemData);

        var firstCast = ApplySelfCast(world, skill, itemData);
        Check(ReadAffectTime(firstCast.AffectSnapshot, 29) == 64,
            "Skill 102 did not preserve the source 7.69 class-master delay and AffectTime formula.");
        AssertSoulScore(firstCast.MobSnapshot, expectedMaxMp: 940,
            "Skill 102 did not apply the Type 29 Celestial Intelligence Soul score online.");

        var recast = ApplySelfCast(world, skill, itemData);
        Check(CountAffectType(recast.AffectSnapshot, 29) == 1 &&
              ReadAffectTime(recast.AffectSnapshot, 29) == 64,
            "Skill 102 recast stacked Type 29 instead of refreshing one affect.");
        AssertSoulScore(recast.MobSnapshot, expectedMaxMp: 940,
            "Skill 102 recast stacked the Soul MaxMp contribution.");

        AssertExpiryRestoresSoulBaseline(world, 64, "Skill 102");
    }

    private static (byte[] MobSnapshot, byte[] AffectSnapshot) ApplySelfCast(
        WorldHub world,
        LegacySkillDefinition skill,
        LegacyItemDataTable itemData)
    {
        var result = world.TryApplySkillAttack(
            CasterId, CasterId, skill, itemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted &&
              outcome?.TargetAffectSnapshot is { } affect && HasAffectType(affect, 29),
            $"Skill {skill.Id} did not apply its Type 29 affect to the player caster: result={result}.");
        return (outcome!.TargetMobSnapshot, outcome.TargetAffectSnapshot!);
    }

    private static WorldHub CreateWorld(
        int skillId,
        int level,
        short special2,
        out LegacyItemDataTable itemData)
    {
        itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(CasterId, $"SOUL_SKILL_{skillId}_{Guid.NewGuid():N}", (_, _) => ValueTask.CompletedTask),
            $"Skill {skillId} Soul caster could not be registered.");

        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes($"SOUL{skillId}").CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        var score = new LegacyScore(
            Level: level, Ac: 0, Damage: 0, Merchant: 0, AttackRun: 0x96, Direction: 0,
            ChaosRate: 0, MaxHp: BaseHp, MaxMp: BaseMp, Hp: CurrentHp, Mp: CurrentMp,
            Strength: 100, Intelligence: 100, Dexterity: 20, Constitution: 50,
            Special1: 0, Special2: special2, Special3: 0, Special4: 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 2;

        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        mobExtra[LegacyAccountSnapshot.MobExtraSoulOffset] = IntelligenceSoul;
        Check(world.SetCharacterState(
                CasterId, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                checked((short)CelestialClassMaster), new byte[LegacyAccountSnapshot.AffectStride], mobExtra),
            $"Skill {skillId} Soul caster state could not be installed.");
        return world;
    }

    private static void AssertExpiryRestoresSoulBaseline(WorldHub world, int duration, string skillName)
    {
        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < duration; tick++)
            expiry = world.TryProcessPlayerTimerAffects(CasterId);

        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 29),
            $"{skillName} Type 29 affect did not expire after {duration} ticks.");
        AssertSoulScore(expiry!.MobSnapshot, expectedMaxMp: BaseMp,
            $"{skillName} Type 29 expiry did not restore the caster's baseline score.");
    }

    private static void AssertSoulScore(ReadOnlySpan<byte> mob, int expectedMaxMp, string message)
    {
        var score = LegacyMobCombatState.Read(mob).CurrentScore;
        Check(score.MaxHp == BaseHp && score.MaxMp == expectedMaxMp &&
              score.Hp == CurrentHp && score.Mp == CurrentMp,
            $"{message} actual maxHp/maxMp/hp/mp={score.MaxHp}/{score.MaxMp}/{score.Hp}/{score.Mp}.");
    }

    private static LegacySkillDefinition LoadSkill(string row)
    {
        var table = LegacySkillDataTable.Load(new StringReader(row));
        var fields = row.Split(',');
        return table[int.Parse(fields[0])] ??
            throw new InvalidOperationException($"Skill row {fields[0]} was not loaded.");
    }

    private static uint ReadAffectTime(ReadOnlySpan<byte> affects, byte type)
    {
        for (var offset = 0; offset + 8 <= affects.Length; offset += 8)
            if (affects[offset] == type)
                return BinaryPrimitives.ReadUInt32LittleEndian(affects[(offset + 4)..]);
        return 0;
    }

    private static int CountAffectType(ReadOnlySpan<byte> affects, byte type)
    {
        var count = 0;
        for (var offset = 0; offset + 8 <= affects.Length; offset += 8)
            if (affects[offset] == type)
                count++;
        return count;
    }

    private static bool HasAffectType(ReadOnlySpan<byte> affects, byte type) =>
        CountAffectType(affects, type) != 0;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
