using System.Buffers.Binary;
using System.IO;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackSkill102DelayTests
{
    private const byte SoulAffectType = 29;
    private const byte SyntheticAffectType = 14;
    private const byte SyntheticTickType = 15;

    public static void Run()
    {
        PlayerAffectUsesTheLegacyClassMasterDelay();
        NpcTargetsReceiveTheSourceSoulAffectForSkill102();
        OtherSkillsKeepTheExistingGenericDelay();
    }

    private static void PlayerAffectUsesTheLegacyClassMasterDelay()
    {
        foreach (var (classMaster, level, expectedDelay) in new[]
        {
            ((short)LegacyAccountSnapshot.ClassMasterMortal, 39, 200),
            ((short)LegacyAccountSnapshot.ClassMasterArch, 39, 100),
            ((short)LegacyExperienceMath.ClassMasterCelestial, 38, 196),
            ((short)LegacyExperienceMath.ClassMasterCelestial, 39, 200),
            ((short)LegacyExperienceMath.ClassMasterCelestial, 198, 200),
            ((short)LegacyExperienceMath.ClassMasterCelestial, 199, 400),
        })
        {
            var (world, skill, _, itemData) = CreateWorld(classMaster, level, npcTarget: false);
            var result = world.TryApplySkillAttack(
                1, 2, skill, itemData, weather: 0, randomFactor: 0,
                out var outcome, targetIndex: 0);

            Check(result == LegacySkillAttackResult.Accepted && outcome?.TargetAffectSnapshot is not null,
                $"Skill 102 did not apply its effect to a player target: classMaster={classMaster}, level={level}, result={result}.");
            var expectedAffectTime = GetAffectTime(expectedDelay, affectTime: 15);
            Check(ReadAffectTime(outcome!.TargetAffectSnapshot!, SoulAffectType) == expectedAffectTime &&
                  ReadAffectTime(outcome.TargetAffectSnapshot, SyntheticTickType) == 0,
                $"Skill 102 source Type 29 duration was wrong for classMaster={classMaster}, level={level}: expected={expectedAffectTime}, affect={ReadAffectTime(outcome.TargetAffectSnapshot, SoulAffectType)}, unexpectedTick={ReadAffectTime(outcome.TargetAffectSnapshot, SyntheticTickType)}.");
        }
    }

    private static void NpcTargetsReceiveTheSourceSoulAffectForSkill102()
    {
        var (world, skill, npcId, itemData) = CreateWorld(
            (short)LegacyExperienceMath.ClassMasterCelestialCs,
            level: 38,
            npcTarget: true);
        var result = world.TryApplySkillAttack(
            1, npcId, skill, itemData, weather: 0, randomFactor: 0,
            out var outcome, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome?.TargetAffectSnapshot is not null,
            $"Skill 102 did not expose the updated NPC affect snapshot: result={result}.");
        var expectedAffectTime = GetAffectTime(196, affectTime: 15);
        Check(ReadAffectTime(outcome!.TargetAffectSnapshot!, SoulAffectType) == expectedAffectTime &&
              ReadAffectTime(outcome.TargetAffectSnapshot, SyntheticTickType) == 0,
            $"Skill 102 did not apply its level-38 source Type 29 duration to the NPC: expected={expectedAffectTime}, affect={ReadAffectTime(outcome.TargetAffectSnapshot, SoulAffectType)}.");
    }

    private static void OtherSkillsKeepTheExistingGenericDelay()
    {
        var (world, _, _, itemData) = CreateWorld(
            (short)LegacyAccountSnapshot.ClassMasterMortal,
            level: 39,
            npcTarget: false);
        var mob = CreateMob(level: 39, special3: 7);
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000, mob,
            (short)LegacyAccountSnapshot.ClassMasterMortal),
            "Generic-delay caster state could not be replaced.");
        var skill = CreateSkill(id: 10);
        var result = world.TryApplySkillAttack(
            1, 2, skill, itemData, weather: 0, randomFactor: 0,
            out var outcome, targetIndex: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome?.TargetAffectSnapshot is not null &&
            ReadAffectTime(outcome.TargetAffectSnapshot, SyntheticAffectType) == 107 &&
            ReadAffectTime(outcome.TargetAffectSnapshot, SyntheticTickType) == 107,
            $"A non-102 skill no longer uses the existing 100 + Special effect delay: result={result}, affect={(outcome?.TargetAffectSnapshot is null ? -1 : ReadAffectTime(outcome.TargetAffectSnapshot, SyntheticAffectType))}.");
    }

    private static (WorldHub World, LegacySkillDefinition Skill, int NpcId, LegacyItemDataTable ItemData) CreateWorld(
        short classMaster,
        int level,
        bool npcTarget)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, $"SKILL102_CASTER_{Guid.NewGuid():N}", (_, _) => ValueTask.CompletedTask),
            "Skill 102 caster could not be registered.");
        Check(world.SetCharacterState(1, 0, 0, 0, 0, 0, 2_000, 2_000,
            CreateMob(level), classMaster),
            "Skill 102 caster state could not be installed.");

        var npcId = 0;
        if (npcTarget)
        {
            npcId = world.EnterNpc(CreateMob(level: 100), 2_001, 2_000,
                new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: 20_102);
            Check(npcId == 20_102, "Skill 102 NPC target could not be registered.");
        }
        else
        {
            Check(world.Enter(2, $"SKILL102_TARGET_{Guid.NewGuid():N}", (_, _) => ValueTask.CompletedTask) &&
                world.SetCharacterState(2, 0, 0, 0, 0, 0, 2_001, 2_000,
                    CreateMob(level: 100), (short)LegacyAccountSnapshot.ClassMasterMortal),
                "Skill 102 player target state could not be installed.");
        }

        return (world, CreateSkill(id: 102), npcId, itemData);
    }

    private static LegacySkillDefinition CreateSkill(int id)
    {
        if (id == 102)
        {
            const string sourceRow = "102,0,0,75,1200,1,0,0,0,0,29,5,60,24.0.0.24.0.0.0.0,24.0.0.24.0.0.0.0,0,0,0,1,0,0,0,0,Limite_da_Alma";
            return LegacySkillDataTable.Load(new StringReader(sourceRow))[id] ??
                throw new InvalidOperationException("The source SkillData row for skill 102 was not loaded.");
        }

        return new LegacySkillDefinition(
            id,
            SkillPoint: 0,
            TargetType: 0,
            ManaSpent: 0,
            Delay: 17,
            Range: 4,
            InstanceType: 0,
            InstanceValue: 0,
            TickType: SyntheticTickType,
            TickValue: 8,
            AffectType: SyntheticAffectType,
            AffectValue: 6,
            AffectTime: 99,
            Action1: [],
            Action2: [],
            InstanceAttribute: 0,
            TickAttribute: 0,
            Aggressive: 0,
            MaxTarget: 1,
            PartyCheck: 0,
            AffectResist: 0,
            Passive: 0,
            ClientForceDamage: 0,
            Name: $"Synthetic_Skill_{id}");
    }

    private static uint GetAffectTime(int delay, int affectTime) =>
        checked((uint)(delay * (affectTime + 1) / 100));

    private static byte[] CreateMob(int level, short special3 = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(level, 0, 0, 0, 0, 0, 0, 10_000, 1_000, 10_000, 1_000,
            0, 0, 0, 0, 0, 0, 0, 0) with { Special3 = special3 };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        return mob;
    }

    private static uint ReadAffectTime(ReadOnlySpan<byte> snapshot, byte type)
    {
        for (var offset = 0; offset + 8 <= snapshot.Length; offset += 8)
            if (snapshot[offset] == type)
                return BinaryPrimitives.ReadUInt32LittleEndian(snapshot[(offset + 4)..]);

        return 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
