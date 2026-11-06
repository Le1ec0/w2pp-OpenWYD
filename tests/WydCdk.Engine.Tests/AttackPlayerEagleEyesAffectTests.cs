using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerEagleEyesAffectTests
{
    private const short WeaponItemIndex = 500;
    private const ushort DrainRsvFlag = 0x0002;
    private static readonly LegacyItemDataTable DrainWeaponItemData = CreateItemData(weaponType: 41);
    private static readonly LegacyItemDataTable OtherWeaponItemData = CreateItemData(weaponType: 40);

    public static void Run()
    {
        Type36ScoreProjectionUsesTheTargetWeaponGate();
        Skill92ProjectsAndExpiresDrainOnItsCaster();
    }

    private static void Type36ScoreProjectionUsesTheTargetWeaponGate()
    {
        var mob = CreateMob(hp: 700, mp: 250, WeaponItemIndex);
        var extra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var activeType36 = CreateType36Affect(level: 20, time: 1);
        var noAffect = new byte[LegacyAccountSnapshot.AffectStride];

        var drainWeapon = Project(mob, activeType36, extra, DrainWeaponItemData);
        Check((LegacyMobCombatState.Read(drainWeapon.Mob).Rsv & DrainRsvFlag) != 0,
            "The target score projection did not set RSV_DRAIN for active Type 36 and weapon type 41.");

        var otherWeapon = Project(mob, activeType36, extra, OtherWeaponItemData);
        Check((LegacyMobCombatState.Read(otherWeapon.Mob).Rsv & DrainRsvFlag) == 0,
            "Type 36 set RSV_DRAIN without the 7.69 weapon type 41 gate.");

        var withoutAffect = Project(mob, noAffect, extra, DrainWeaponItemData);
        Check((LegacyMobCombatState.Read(withoutAffect.Mob).Rsv & DrainRsvFlag) == 0,
            "Weapon type 41 set RSV_DRAIN without an active Type 36 affect.");
    }

    private static void Skill92ProjectsAndExpiresDrainOnItsCaster()
    {
        var world = new WorldHub(itemData: DrainWeaponItemData);
        var mob = CreateMob(hp: 700, mp: 250, WeaponItemIndex);
        var extra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, extra, DrainWeaponItemData);
        var baselineState = LegacyMobCombatState.Read(baseline.Mob);
        Check((baselineState.Rsv & DrainRsvFlag) == 0,
            "The Eagle Eyes baseline unexpectedly contains RSV_DRAIN.");

        Check(world.Enter(1, "EAGLE_EYES_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, extra),
            "Eagle Eyes player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, EagleEyes(), DrainWeaponItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 36),
            "Skill 92 did not apply Type 36 to its player caster.");

        var affected = LegacyMobCombatState.Read(outcome!.TargetMobSnapshot);
        Check((affected.Rsv & DrainRsvFlag) != 0 &&
              affected.CurrentScore.Hp == 700 && affected.CurrentScore.Mp == 250,
            $"Skill 92 did not project RSV_DRAIN with weapon type 41 while preserving HP/MP; rsv=0x{affected.Rsv:X4}, hp={affected.CurrentScore.Hp}, mp={affected.CurrentScore.Mp}.");

        var recast = world.TryApplySkillAttack(
            1, 1, EagleEyes(), DrainWeaponItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        var recastAffect = recastOutcome?.TargetAffectSnapshot;
        Check(recast == LegacySkillAttackResult.Accepted && recastAffect is not null &&
              HasExactlyOneAffectType(recastAffect, 36),
            "Recasting skill 92 did not refresh exactly one Type 36 record.");
        var confirmedAffect = recastAffect ?? throw new InvalidOperationException("The recast omitted its Type 36 affect snapshot.");
        var duration = GetAffectTime(confirmedAffect, 36);
        Check(duration == 4 && confirmedAffect[1] == 1 &&
              BinaryPrimitives.ReadUInt16LittleEndian(confirmedAffect.AsSpan(2)) == 20,
            $"Skill 92 Type 36 did not preserve the loaded 7.69 value/level/duration; value={confirmedAffect[1]}, level={BinaryPrimitives.ReadUInt16LittleEndian(confirmedAffect.AsSpan(2))}, time={duration}.");

        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < duration; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 36),
            "Player Type 36 expiry did not clear the affect with a score refresh.");
        var restored = LegacyMobCombatState.Read(expiry!.MobSnapshot);
        Check((restored.Rsv & DrainRsvFlag) == (baselineState.Rsv & DrainRsvFlag) &&
              restored.CurrentScore.Hp == 700 && restored.CurrentScore.Mp == 250,
            "Player Type 36 expiry did not restore baseline RSV_DRAIN and current HP/MP.");
    }

    private static LegacyCharacterLoginData Project(
        byte[] mob,
        byte[] affect,
        byte[] extra,
        LegacyItemDataTable itemData) =>
        LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                affect.ToArray(),
                extra.ToArray(),
                0,
                0,
                0),
            itemData);

    private static byte[] CreateMob(int hp, int mp, short weaponIndex)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(1_500, 100, 500, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 40, 30, 20, 10, 10) with { Special4 = 20 };
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 3;
        new LegacyItem(weaponIndex, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + 6 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        return mob;
    }

    private static byte[] CreateMobExtra(short classMaster)
    {
        var extra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(extra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        return extra;
    }

    private static LegacyItemDataTable CreateItemData(short weaponType)
    {
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        var decoded = new byte[LegacyItemDataTable.RecordSizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(decoded.AsSpan(80), LegacyItemEffect.WeaponType);
        BinaryPrimitives.WriteInt16LittleEndian(decoded.AsSpan(82), weaponType);
        var record = encoded.AsSpan(WeaponItemIndex * LegacyItemDataTable.RecordSizeInBytes, LegacyItemDataTable.RecordSizeInBytes);
        for (var index = 0; index < decoded.Length; index++)
            record[index] = (byte)(decoded[index] ^ 0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static byte[] CreateType36Affect(ushort level, uint time)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 36;
        affect[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), level);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), time);
        return affect;
    }

    private static LegacySkillDefinition EagleEyes() =>
        new(92, 75, 0, 30, 6, 0, 0, 0, 0, 0, 36, 1, 3,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 0, 1, 0, 2, 0, 0, "Olhos de Aguia");

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
    }

    private static bool HasExactlyOneAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        var count = 0;
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                count++;
        return count == 1;
    }

    private static uint GetAffectTime(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return BinaryPrimitives.ReadUInt32LittleEndian(affect.Slice(offset + 4, sizeof(uint)));
        return 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
