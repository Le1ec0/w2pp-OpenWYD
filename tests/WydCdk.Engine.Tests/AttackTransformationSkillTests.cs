using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackTransformationSkillTests
{
    public static void Run()
    {
        TransformationCastsProjectType16EquipmentAndScoreState();
        TransformationCastWithoutItemDataDoesNotPartiallyApply();
    }

    private static void TransformationCastsProjectType16EquipmentAndScoreState()
    {
        var itemData = CreateNeutralItemData();
        foreach (var (skillId, expectedValue, expectedHeadIndex) in new[]
        {
            (64, 1, 22),
            (66, 2, 23),
            (68, 3, 24),
            (70, 4, 25),
            (71, 5, 32),
        })
        {
            var world = CreateWorld(itemData);
            var skill = CreateSkill(skillId);
            var result = world.TryApplySkillAttack(
                1, 1, skill, itemData, weather: 0, randomFactor: 0,
                out var outcome, targetIndex: 0);

            Check(result == LegacySkillAttackResult.Accepted && outcome?.TargetAffectSnapshot is not null &&
                outcome.TargetEquipmentChanged,
                $"Transformation skill {skillId} did not apply its Type 16 affect: result={result}.");
            Check(world.TryGetCharacterSnapshot(
                    1, out var mob, out _, out _, out _, out var affect, out var equipment) &&
                mob is not null && affect is not null && equipment is not null,
                $"Transformation skill {skillId} did not preserve a readable character snapshot.");

            var head = LegacyItem.Read(mob!.AsSpan(
                LegacyAccountSnapshot.MobEquipmentOffset,
                LegacyItem.SizeInBytes));
            Check(head.Index == expectedHeadIndex && equipment![0].Index == expectedHeadIndex,
                $"Transformation skill {skillId} did not project its head equipment: expected={expectedHeadIndex}, mob={head.Index}, client={equipment![0].Index}.");
            Check(affect![0] == 16 && affect[1] == expectedValue,
                $"Transformation skill {skillId} did not persist the matching Type 16 value.");
        }
    }

    private static void TransformationCastWithoutItemDataDoesNotPartiallyApply()
    {
        var world = CreateWorld(itemData: null);
        var skill = CreateSkill(64);
        var result = world.TryApplySkillAttack(
            1, 1, skill, itemData: null, weather: 0, randomFactor: 0,
            out var outcome, targetIndex: 0);

        Check(result == LegacySkillAttackResult.CombatStateUnavailable && outcome is null,
            $"Transformation skill without ItemData should fail before mutation: result={result}.");
        Check(world.TryGetCharacterSnapshot(
                1, out var mob, out _, out _, out _, out var affect, out var equipment) &&
            mob is not null && affect is not null && equipment is not null &&
            LegacyItem.Read(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes)).Index == 0 &&
            equipment[0].Index == 0 && affect[0] == 0,
            "Transformation skill without ItemData changed equipment or affects before rejecting.");
    }

    private static WorldHub CreateWorld(LegacyItemDataTable? itemData)
    {
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, $"TRANSFORM_CASTER_{Guid.NewGuid():N}", (_, _) => ValueTask.CompletedTask),
            "Transformation caster could not be registered.");

        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(
            50, 100, 100, 0, 0, 0, 0,
            1_000, 500, 1_000, 500,
            100, 100, 100, 100, 0, 0, 0, 10);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0x20000);
        Check(world.SetCharacterState(
                1, 0, 0, 0, 0, 0, 2_000, 2_000, mob,
                LegacyAccountSnapshot.ClassMasterMortal),
            "Transformation caster state could not be installed.");

        return world;
    }

    private static LegacySkillDefinition CreateSkill(int skillId) => new(
        skillId,
        SkillPoint: 33,
        TargetType: 0,
        ManaSpent: 0,
        Delay: 20,
        Range: 0,
        InstanceType: 0,
        InstanceValue: 0,
        TickType: 0,
        TickValue: 0,
        AffectType: 16,
        AffectValue: skillId switch
        {
            64 => 1,
            66 => 2,
            68 => 3,
            70 => 4,
            71 => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(skillId)),
        },
        AffectTime: 60,
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
        Name: $"Transformation_{skillId}");

    private static LegacyItemDataTable CreateNeutralItemData()
    {
        var encoded = new byte[LegacyItemDataTable.BodySizeInBytes];
        Array.Fill(encoded, (byte)0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
