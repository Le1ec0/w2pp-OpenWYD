using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPlayerIceEnchantAffectTests
{
    private const short WeaponItemIndex = 500;
    private const ushort FrostRsvFlag = 0x0001;
    private static readonly LegacyItemDataTable FrostWeaponItemData = CreateItemData(weaponType: 101);
    private static readonly LegacyItemDataTable OtherWeaponItemData = CreateItemData(weaponType: 100);

    public static void Run()
    {
        Type27ScoreProjectionUsesTheTargetWeaponGate();
        Skill75ProjectsAndExpiresFrostOnItsCaster();
    }

    private static void Type27ScoreProjectionUsesTheTargetWeaponGate()
    {
        var mob = CreateMob(hp: 700, mp: 250);
        var extra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var activeType27 = CreateType27Affect();
        var withoutType27 = new byte[LegacyAccountSnapshot.AffectStride];

        var frostWeaponScore = Project(mob, activeType27, extra, FrostWeaponItemData);
        Check((LegacyMobCombatState.Read(frostWeaponScore.Mob).Rsv & FrostRsvFlag) != 0,
            "The target score projection did not set RSV_FROST for active Type 27 and weapon type 101.");

        var otherWeaponScore = Project(mob, activeType27, extra, OtherWeaponItemData);
        Check((LegacyMobCombatState.Read(otherWeaponScore.Mob).Rsv & FrostRsvFlag) == 0,
            "Type 27 set RSV_FROST without the 7.69 weapon type 101 gate.");

        var noAffectScore = Project(mob, withoutType27, extra, FrostWeaponItemData);
        Check((LegacyMobCombatState.Read(noAffectScore.Mob).Rsv & FrostRsvFlag) == 0,
            "Weapon type 101 set RSV_FROST without an active Type 27 affect.");
    }

    private static void Skill75ProjectsAndExpiresFrostOnItsCaster()
    {
        var world = new WorldHub(itemData: FrostWeaponItemData);
        var mob = CreateMob(hp: 700, mp: 250);
        var mobExtra = CreateMobExtra(LegacyAccountSnapshot.ClassMasterMortal);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = Project(mob, emptyAffect, mobExtra, FrostWeaponItemData);
        var baselineState = LegacyMobCombatState.Read(baseline.Mob);
        Check((baselineState.Rsv & FrostRsvFlag) == 0,
            "The Ice Enchant baseline unexpectedly contains RSV_FROST.");

        Check(world.Enter(1, "ICE_ENCHANT_PLAYER", static (_, _) => ValueTask.CompletedTask) &&
              world.SetCharacterState(
                  1, 0, 0, 1, 0, 0, 2_000, 2_000, mob,
                  LegacyAccountSnapshot.ClassMasterMortal, emptyAffect, mobExtra),
            "Ice Enchant player fixture was not installed.");

        var result = world.TryApplySkillAttack(
            1, 1, IceEnchant(), FrostWeaponItemData, weather: 0, randomFactor: 100, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.Damage == 0 &&
              outcome.TargetAffectSnapshot is { } firstAffect && HasAffectType(firstAffect, 27),
            "Skill 75 did not apply Type 27 to its player caster.");

        var affectedScore = LegacyMobCombatState.Read(outcome!.TargetMobSnapshot);
        Check((affectedScore.Rsv & FrostRsvFlag) != 0 &&
              affectedScore.CurrentScore.Hp == 700 && affectedScore.CurrentScore.Mp == 250,
            $"Skill 75 did not project RSV_FROST while preserving current HP/MP; rsv=0x{affectedScore.Rsv:X4}, hp={affectedScore.CurrentScore.Hp}, mp={affectedScore.CurrentScore.Mp}.");

        var recastResult = world.TryApplySkillAttack(
            1, 1, IceEnchant(), FrostWeaponItemData, weather: 0, randomFactor: 100, out var recastOutcome);
        var recastAffect = recastOutcome?.TargetAffectSnapshot;
        Check(recastResult == LegacySkillAttackResult.Accepted &&
              recastAffect is not null && HasAffectType(recastAffect, 27),
            "Recasting skill 75 did not refresh Type 27.");
        var recastScore = LegacyMobCombatState.Read(recastOutcome!.TargetMobSnapshot);
        Check((recastScore.Rsv & FrostRsvFlag) != 0 &&
              recastScore.CurrentScore.Hp == 700 && recastScore.CurrentScore.Mp == 250,
            "Recasting skill 75 lost RSV_FROST or changed current HP/MP.");

        var duration = GetAffectTime(recastAffect!, 27);
        Check(recastAffect![1] == 1 && BinaryPrimitives.ReadUInt16LittleEndian(recastAffect.AsSpan(2)) == 20 && duration == 19,
            $"Skill 75 Type 27 affect did not preserve the 7.69 value/level/duration inputs; value={recastAffect[1]}, level={BinaryPrimitives.ReadUInt16LittleEndian(recastAffect.AsSpan(2))}, time={duration}.");
        LegacyPlayerAffectTimerOutcome? expiry = null;
        for (var tick = 0; tick < duration; tick++)
            expiry = world.TryProcessPlayerTimerAffects(1);

        Check(expiry is { ScoreRefreshRequired: true } && !HasAffectType(expiry.AffectSnapshot, 27),
            "Player Type 27 expiry did not clear the affect with a score refresh.");
        var restored = LegacyMobCombatState.Read(expiry!.MobSnapshot);
        Check((restored.Rsv & FrostRsvFlag) == (baselineState.Rsv & FrostRsvFlag) &&
              restored.CurrentScore.Hp == 700 && restored.CurrentScore.Mp == 250,
            "Player Type 27 expiry did not restore baseline RSV and current HP/MP.");
    }

    private static LegacyCharacterLoginData Project(
        byte[] mob,
        byte[] affect,
        byte[] mobExtra,
        LegacyItemDataTable itemData) =>
        LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                mob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                affect.ToArray(),
                mobExtra.ToArray(),
                0,
                0,
                0),
            itemData);

    private static byte[] CreateMob(int hp, int mp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(1_500, 100, 500, 0, 0x96, 0, 0,
            1_000, 500, hp, mp, 0, 100, 0, 40, 30, 20, 10, 10);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 3;
        new LegacyItem(WeaponItemIndex, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + 6 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        return mob;
    }

    private static byte[] CreateMobExtra(short classMaster)
    {
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        return mobExtra;
    }

    private static LegacyItemDataTable CreateItemData(short weaponType)
    {
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        Span<byte> decoded = stackalloc byte[LegacyItemDataTable.RecordSizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(decoded[80..], LegacyItemEffect.WeaponType);
        BinaryPrimitives.WriteInt16LittleEndian(decoded[82..], weaponType);
        var encodedRecord = encoded.AsSpan(WeaponItemIndex * LegacyItemDataTable.RecordSizeInBytes, LegacyItemDataTable.RecordSizeInBytes);
        for (var index = 0; index < decoded.Length; index++)
            encodedRecord[index] = (byte)(decoded[index] ^ 0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static byte[] CreateType27Affect()
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 27;
        affect[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 50);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        return affect;
    }

    private static LegacySkillDefinition IceEnchant() =>
        new(75, 51, 0, 30, 6, 0, 0, 0, 0, 0, 27, 1, 15,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 0, 1, 0, 2, 0, 0, "Encantar Gelo");

    private static bool HasAffectType(ReadOnlySpan<byte> affect, byte type)
    {
        for (var offset = 0; offset + 8 <= affect.Length; offset += 8)
            if (affect[offset] == type)
                return true;
        return false;
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
