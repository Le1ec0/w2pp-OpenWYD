using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Intermediate values from the equipment-derived part of BASE_GetCurrentScore.
/// These are deliberately not the final combat values: later source blocks add
/// buffs, class/mount modifiers, and apply the remaining caps.
/// </summary>
public readonly record struct LegacyCurrentScoreAbilityStage(
    int SaveMana,
    int Magic,
    int RunSeed,
    int AttackSpeedSeed,
    int RegenHpSeed,
    int RegenMpSeed,
    int CriticalSeed);

/// <summary>Physical damage after the final face-gated stat bonus and multiplier.</summary>
public readonly record struct LegacyCurrentDamageStage(int Damage);

/// <summary>Clamped attack/run components and the packed legacy AttackRun byte.</summary>
public readonly record struct LegacyCurrentSpeedStage(int Attack, int Run, byte PackedAttackRun);

/// <summary>Values committed by the final scalar-clamp block of BASE_GetCurrentScore.</summary>
public readonly record struct LegacyCurrentScoreFinalStage(
    int Hp,
    int Mp,
    byte RegenHp,
    byte RegenMp,
    int Magic,
    int Critical);

/// <summary>Clamped resistance bytes in the legacy MOB order: holy, thunder, fire, ice.</summary>
public readonly record struct LegacyCurrentResistanceStage(
    byte Holy,
    byte Thunder,
    byte Fire,
    byte Ice);

/// <summary>Raw resistance totals after the active affect loop, before final 0..100 clamps.</summary>
public readonly record struct LegacyCurrentResistanceModifierStage(
    int Holy,
    int Thunder,
    int Fire,
    int Ice,
    int HeadItemIndex);

/// <summary>Run addition and MOB.Rsv bits contributed by active Haste affects.</summary>
public readonly record struct LegacyHasteAffectStage(int RunAddition, ushort RsvFlagsToAdd);

/// <summary>RSV_FROST contributed by an active Type=27 affect and a Type=101 weapon.</summary>
public readonly record struct LegacyIceEnchantAffectStage(ushort RsvFlagsToAdd);

/// <summary>RSV_DRAIN contributed by an active Type=36 affect and a Type=41 weapon.</summary>
public readonly record struct LegacyDrainAffectStage(ushort RsvFlagsToAdd);

/// <summary>ForceDamage added by active Type=37 affects during score composition.</summary>
public readonly record struct LegacySpectralLinkForceDamageStage(int ForceDamageAddition);

/// <summary>Damage addition from learned Hunter Proficiency with the source equipment gates.</summary>
public readonly record struct LegacyHunterProficiencyStage(bool IsActive, int DamageAddition);

/// <summary>ArmorClass multiplier and Critical addition from learned Transknight Armadura Critica.</summary>
public readonly record struct LegacyArmorCriticalStage(bool IsActive, int ArmorClass, int CriticalAddition);

/// <summary>Damage addition from learned Transknight Noção de Combate.</summary>
public readonly record struct LegacyCombatAwarenessStage(bool IsActive, int DamageAddition);

/// <summary>Score contributions from the selected 7.69 Hunter class passives.</summary>
public readonly record struct LegacyHunterClassPassivesStage(
    int AggressivenessDamageAddition,
    int LightningStormDamageAddition,
    int ShadowProtectionArmorClassAddition,
    int HunterVisionCriticalAddition);

/// <summary>Attack/run/intelligence deltas contributed by Type=1 Holy Touch affects.</summary>
public readonly record struct LegacyHolyTouchAffectStage(int RunDelta, int AttackDelta, int IntelligenceDelta);

/// <summary>MaxHp and Constitution after target Type=14 Possessed affects.</summary>
public readonly record struct LegacyPossessedAffectStage(int MaxHp, short Constitution);

/// <summary>
/// Target Type=13 Assault multiplier contribution and the untouched score
/// fields that W2PP mutates in the corresponding legacy branch.
/// </summary>
public readonly record struct LegacyAssaultAffectStage(
    int DamageMultiplierAdjustment,
    int Damage,
    int MaxHp);

/// <summary>Sequential ArmorClass value after target Type=24 Samaritan affects.</summary>
public readonly record struct LegacySamaritanArmorClassAffectStage(int ArmorClass);

/// <summary>ArmorClass addition contributed by active Type=11 Magic Shield affects.</summary>
public readonly record struct LegacyMagicShieldAffectStage(int ArmorClassAddition);

/// <summary>Damage, multiplier and Magic deltas contributed by Type=9 Magic Weapon affects.</summary>
public readonly record struct LegacyMagicWeaponAffectStage(
    int DamageAddition,
    int DamageMultiplierAdjustment,
    int MagicAddition);

/// <summary>Special-stat values after target 7.69 Type=15 Athena Touch affects.</summary>
public readonly record struct LegacyAthenaTouchAffectStage(
    int Special1,
    int Special2,
    int Special3,
    int Special4);

/// <summary>Sequential Dexterity value after Type=5 percentage reductions.</summary>
public readonly record struct LegacyFanaticismAffectStage(short Dexterity);

/// <summary>Sequential Dexterity value after Type=6 percentage affects.</summary>
public readonly record struct LegacyDexterityAffectStage(short Dexterity);

/// <summary>Sequential ArmorClass value after Type=12 percentage reductions.</summary>
public readonly record struct LegacyArmorClassReductionAffectStage(int ArmorClass);

/// <summary>Final ArmorClass after the selected ordered 7.69 affect passes.</summary>
public readonly record struct LegacyCurrentScoreArmorClassAffectCompositionStage(int ArmorClass);

/// <summary>Attack speed and intelligence deltas contributed by Type=7 Frozen Blade affects.</summary>
public readonly record struct LegacyFrozenBladeAffectStage(int AttackSpeedDelta, int IntelligenceDelta);

/// <summary>Damage reduction contributed by active Type=10 Weaken affects.</summary>
public readonly record struct LegacyWeakenAffectStage(int DamageReduction);

/// <summary>ArmorClass reduction and damage multiplier adjustment from Type=21 Meditation affects.</summary>
public readonly record struct LegacyMeditationAffectStage(
    int ArmorClassReduction,
    int DamageMultiplierAdjustment);

/// <summary>ArmorClass addition contributed by active Type=31 Golden Shield affects.</summary>
public readonly record struct LegacyGoldenShieldAffectStage(int ArmorClassAddition);

/// <summary>ArmorClass addition contributed by active Type=25 Elemental Protection affects.</summary>
public readonly record struct LegacyElementalProtectionArmorClassAffectStage(int ArmorClassAddition);

/// <summary>Sequential MaxHp and MaxMp values after Type=35 Vigor affects.</summary>
public readonly record struct LegacyVigorAffectStage(int MaxHp, int MaxMp);

/// <summary>
/// Score values after the active 7.69 Type=8 PvP-jewel bits in the CurrentScore loop.
/// Resistance and attack-only jewel effects remain in their separate stages.
/// </summary>
public readonly record struct LegacyPvpJewelAffectStage(
    int MaxHp,
    int MaxMp,
    int ArmorClass,
    int Damage,
    int Magic,
    int LearnedSkills,
    ushort RsvFlagsToAdd);

/// <summary>
/// Combined score projection for the selected Type=4, Type=34, Type=35, and
/// Type=8 branches, evaluated in affect-slot order. Other Type=8 branches
/// remain in their attack or resistance stages.
/// </summary>
public readonly record struct LegacyCurrentScoreAffectCompositionStage(
    int MaxHp,
    int MaxMp,
    int ArmorClass,
    int Damage,
    int Magic,
    int LearnedSkills,
    ushort RsvFlagsToAdd);

/// <summary>
/// Sequential score values after active Type=34 Divina affects in the 7.69 score loop.
/// Magic is the local accumulator; each affect derives its increment from the unchanged
/// source MOB.Magic value, and the later final Magic clamp is not part of this stage.
/// </summary>
public readonly record struct LegacyDivinaAffectStage(int MaxHp, int MaxMp, int Damage, int Magic);

/// <summary>Attack/run bonuses selected by the last valid transformation affect.</summary>
public readonly record struct LegacyTransformationSpeedAffectStage(
    bool HasActiveTransformation,
    int AttackSpeedBonus,
    int RunSpeedBonus);

/// <summary>Damage addition and additive multiplier delta from Type=16 affects.</summary>
public readonly record struct LegacyTransformationDamageAffectStage(
    bool HasActiveTransformation,
    int DamageAddition,
    int DamageMultiplierAdjustment);

/// <summary>Sequential CurrentScore.Ac mutations contributed by Type=16 affects.</summary>
public readonly record struct LegacyTransformationArmorClassAffectStage(
    bool HasActiveTransformation,
    int ArmorClass);

/// <summary>Sequential MaxHp mutations contributed by Type=16 affects.</summary>
public readonly record struct LegacyTransformationMaxHpAffectStage(
    bool HasActiveTransformation,
    int MaxHp);

/// <summary>Accumulated Critical contribution from valid Type=16 affects.</summary>
public readonly record struct LegacyTransformationCriticalAffectStage(
    bool HasActiveTransformation,
    int CriticalAddition);

/// <summary>Head item mutation produced by the last valid Type=16 affect.</summary>
public readonly record struct LegacyTransformationEquipmentAffectStage(
    bool HasActiveTransformation,
    LegacyItem HeadItem);

/// <summary>
/// Composition-only snapshot of the Type=16 effects already ported from the
/// 7.69 score loop. It deliberately exposes each independent stage instead of
/// mutating a MOB or committing a final score.
/// </summary>
public readonly record struct LegacyTransformationAffectCompositionStage(
    bool HasActiveTransformation,
    LegacyTransformationSpeedAffectStage Speed,
    LegacyTransformationDamageAffectStage Damage,
    LegacyTransformationArmorClassAffectStage ArmorClass,
    LegacyTransformationMaxHpAffectStage MaxHp,
    LegacyTransformationCriticalAffectStage Critical,
    LegacyTransformationEquipmentAffectStage Equipment,
    LegacyCurrentResistanceModifierStage Resistance);

/// <summary>
/// Snapshot of the score blocks that already have isolated ports. Earlier AC
/// fields are stage-local diagnostics; AfterScoreAffects carries the canonical
/// selected two-pass AC. Unsupported blocks remain visible as gaps, and no MOB
/// is mutated here.
/// </summary>
public readonly record struct LegacyCurrentScoreCoordinatorStage(
    LegacyMobCombatState BaseAndEquipment,
    LegacyCurrentScoreAbilityStage Abilities,
    LegacyMobCombatState AfterSpecial,
    LegacyMeditationAffectStage Meditation,
    LegacyTransformationAffectCompositionStage Transformation,
    LegacyMagicShieldAffectStage MagicShield,
    LegacyMobCombatState AfterTransformation,
    LegacyMobCombatState AfterMagicShield,
    LegacyMagicWeaponAffectStage MagicWeapon,
    LegacyMobCombatState AfterMagicWeapon,
    LegacyAthenaTouchAffectStage AthenaTouch,
    LegacyMobCombatState AfterAthenaTouch,
    LegacyHolyTouchAffectStage HolyTouch,
    LegacyMobCombatState AfterHolyTouch,
    LegacyFanaticismAffectStage Fanaticism,
    LegacyMobCombatState AfterFanaticism,
    LegacyPossessedAffectStage Possessed,
    LegacyMobCombatState AfterPossessed,
    LegacyAssaultAffectStage Assault,
    LegacyMobCombatState AfterAssault,
    LegacySamaritanArmorClassAffectStage Samaritan,
    LegacyMobCombatState AfterSamaritan,
    LegacyDexterityAffectStage Dexterity,
    LegacyMobCombatState AfterDexterity,
    LegacyArmorClassReductionAffectStage ArmorClassReduction,
    LegacyMobCombatState AfterArmorClassReduction,
    LegacyCurrentScoreAffectCompositionStage ScoreAffects,
    LegacyMobCombatState AfterScoreAffects,
    LegacyHunterClassPassivesStage HunterClassPassives,
    LegacyMobCombatState AfterHunterClassPassives,
    LegacyHunterProficiencyStage HunterProficiency,
    LegacyMobCombatState AfterHunterProficiency,
    LegacyArmorCriticalStage ArmorCritical,
    LegacyMobCombatState AfterArmorCritical,
    LegacyCombatAwarenessStage CombatAwareness,
    LegacyMobCombatState AfterCombatAwareness,
    LegacyMobCombatState AfterSoul,
    LegacyMobCombatState AfterKibita,
    LegacyHasteAffectStage Haste,
    LegacyCurrentResistanceStage Resistance,
    ushort RsvWithAffects);

/// <summary>Incremental ports of the 7.69 BASE_GetCurrentScore pipeline.</summary>
public static class LegacyCurrentScoreMath
{
    public const int TargetMaximumMagic = 190_000_000;
    public const int TargetMaximumLevel = 399;
    public const int TargetMaximumHp = 100_000;
    public const int TargetMaximumMp = 100_000;
    public const int TargetMaximumDamage = 1_000_000;
    public const ushort HasteRsvFlag = 0x0020;
    public const ushort FrostRsvFlag = 0x0001;
    public const ushort DrainRsvFlag = 0x0002;
    public const ushort ParryRsvFlag = 0x0008;
    public const ushort HideRsvFlag = 0x0010;
    public const ushort BlockRsvFlag = 0x0080;

    private const int TargetMaximumType4Hp = 400_000;
    private const int AffectTypeOffset = 0;
    private const int AffectSizeInBytes = 8;
    private const int AffectCount = 32;
    private const byte SoulAffectType = 29;
    private const byte Type4ScoreAffectType = 4;
    private const byte DivinaAffectType = 34;
    private const byte VigorAffectType = 35;
    private const byte ManaToHealthAffectType = 38;
    private const byte SoulSwapAffectType = 42;
    private const byte PursuitAffectType = 3;
    private const byte PvpJewelAffectType = 8;
    private const ushort PvpJewelVisionRsvFlag = 0x0004;
    private const int PvpJewelPrecisionLearnedSkillFlag = 1 << 28;
    private const byte ElementalProtectionAffectType = 25;
    private const byte TransformationAffectType = 16;
    private const byte SamaritanAffectType = 24;
    private const byte MagicShieldAffectType = 11;
    private const byte ArmorClassReductionAffectType = 12;
    private const byte HasteAffectType = 2;
    private const byte IceEnchantAffectType = 27;
    private const byte DrainAffectType = 36;
    private const byte SpectralLinkAffectType = 37;
    private const byte BlockAffectType = 19;
    private const byte EvasionAffectType = 26;
    private const byte InvisibilityAffectType = 28;
    private const byte FrozenBladeAffectType = 7;
    private const byte WeakenAffectType = 10;
    private const byte MeditationAffectType = 21;
    private const byte GoldenShieldAffectType = 31;
    private const int BeastMasterClass = 2;
    private const int MagicWeaponClass = 1;
    private const int HunterClass = 3;
    private const int HunterAggressivenessSkillId = 2;
    private const int HunterLightningStormSkillId = 7;
    private const int HunterVisionSkillId = 18;
    private const int HunterShadowProtectionSkillId = 22;
    private const int HunterProficiencySkillId = 82;
    private const int ClassSkillBookSize = 24;
    private const uint HunterAggressivenessLearnedFlag = 1u << HunterAggressivenessSkillId;
    private const uint HunterLightningStormLearnedFlag = 1u << HunterLightningStormSkillId;
    private const uint HunterVisionLearnedFlag = 1u << HunterVisionSkillId;
    private const uint HunterShadowProtectionLearnedFlag = 1u << HunterShadowProtectionSkillId;
    private const uint HunterProficiencyLearnedFlag = 1u << (HunterProficiencySkillId % ClassSkillBookSize);
    private const int HunterClawWeaponUnique = 43;
    private const int HunterBowWeaponUnique = 42;
    private const int TransknightClass = 0;
    private const int ArmorCriticalSkillId = 15;
    private const uint ArmorCriticalLearnedFlag = 1u << ArmorCriticalSkillId;
    private const int CombatAwarenessSkillId = 14;
    private const uint CombatAwarenessLearnedFlag = 1u << CombatAwarenessSkillId;

    /// <summary>
    /// Applies the target 7.69 terminal clamps after all score modifiers have
    /// been accumulated. This is a pure stage and does not mutate a MOB.
    /// </summary>
    public static LegacyCurrentScoreFinalStage ApplyFinalScalarClamps(
        int hp,
        int mp,
        int maxHp,
        int maxMp,
        int regenHp,
        int regenMp,
        int magic,
        int critical)
    {
        // Preserve the source's order: lower bound first, then current maximum.
        if (hp < 0)
            hp = 0;
        if (mp < 0)
            mp = 0;
        if (hp > maxHp)
            hp = maxHp;
        if (mp > maxMp)
            mp = maxMp;

        regenHp = Math.Clamp(regenHp, 0, byte.MaxValue);
        regenMp = Math.Clamp(regenMp, 0, byte.MaxValue);

        if (magic >= TargetMaximumMagic)
            magic = TargetMaximumMagic;
        if (critical >= byte.MaxValue)
            critical = byte.MaxValue;

        return new LegacyCurrentScoreFinalStage(
            hp,
            mp,
            (byte)regenHp,
            (byte)regenMp,
            magic,
            critical);
    }

    /// <summary>Applies the four 0..100 resistance bounds from the 7.69 final score block.</summary>
    public static LegacyCurrentResistanceStage ApplyResistanceClamps(int holy, int thunder, int fire, int ice) =>
        new(
            (byte)Math.Clamp(holy, 0, 100),
            (byte)Math.Clamp(thunder, 0, 100),
            (byte)Math.Clamp(fire, 0, 100),
            (byte)Math.Clamp(ice, 0, 100));

    /// <summary>
    /// Extracts the Type=2 Haste contributions from the 7.69 affect loop.
    /// Each active record adds its Value to Run and requests RSV_HASTE; caps are
    /// applied later by the terminal AttackRun stage.
    /// </summary>
    public static LegacyHasteAffectStage CalculateHasteAffectStage(ReadOnlySpan<byte> affectSnapshot)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var runAddition = 0;
        ushort rsvFlags = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != HasteAffectType)
                continue;

            runAddition = unchecked(runAddition + affectSnapshot[offset + 1]);
            rsvFlags |= HasteRsvFlag;
        }

        return new LegacyHasteAffectStage(runAddition, rsvFlags);
    }

    /// <summary>
    /// Applies the target 7.69 Type=27 RSV_FROST rule. The source requires the
    /// character's weapon in equipment slot 6 to have EF_WTYPE 101.
    /// </summary>
    public static LegacyIceEnchantAffectStage CalculateIceEnchantAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        ReadOnlySpan<LegacyItem> equipment,
        LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(itemData);
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var hasIceEnchant = false;
        for (var index = 0; index < AffectCount; index++)
        {
            if (affectSnapshot[index * AffectSizeInBytes + AffectTypeOffset] == IceEnchantAffectType)
            {
                hasIceEnchant = true;
                break;
            }
        }

        if (!hasIceEnchant)
            return new LegacyIceEnchantAffectStage(0);
        if (equipment.Length <= 6)
            throw new ArgumentException("Type 27 score projection requires equipment slot 6.", nameof(equipment));

        var rsvFlags = hasIceEnchant &&
            itemData.GetItemAbility(equipment[6], LegacyItemEffect.WeaponType) == 101
                ? FrostRsvFlag
                : (ushort)0;
        return new LegacyIceEnchantAffectStage(rsvFlags);
    }

    /// <summary>
    /// Applies the target 7.69 Type=36 RSV_DRAIN rule. The source requires the
    /// character's weapon in equipment slot 6 to have EF_WTYPE 41; W2PP omits
    /// this gate.
    /// </summary>
    public static LegacyDrainAffectStage CalculateDrainAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        ReadOnlySpan<LegacyItem> equipment,
        LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(itemData);
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var hasDrain = false;
        for (var index = 0; index < AffectCount; index++)
        {
            if (affectSnapshot[index * AffectSizeInBytes + AffectTypeOffset] == DrainAffectType)
            {
                hasDrain = true;
                break;
            }
        }

        if (!hasDrain)
            return new LegacyDrainAffectStage(0);
        if (equipment.Length <= 6)
            throw new ArgumentException("Type 36 score projection requires equipment slot 6.", nameof(equipment));

        var rsvFlags = itemData.GetItemAbility(equipment[6], LegacyItemEffect.WeaponType) == 41
            ? DrainRsvFlag
            : (ushort)0;
        return new LegacyDrainAffectStage(rsvFlags);
    }

    /// <summary>
    /// Adds the source 7.69 Type=37 contribution to the separate ForceDamage
    /// accumulator. Its local <c>special2</c> is CurrentScore.Special3; the
    /// legacy loop adds it once per active Type=37 record after attack damage
    /// is calculated.
    /// </summary>
    public static LegacySpectralLinkForceDamageStage CalculateSpectralLinkForceDamageStage(
        ReadOnlySpan<byte> affectSnapshot,
        int currentSpecial3)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var forceDamageAddition = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            if (affectSnapshot[index * AffectSizeInBytes + AffectTypeOffset] == SpectralLinkAffectType)
                forceDamageAddition = unchecked(forceDamageAddition + currentSpecial3);
        }

        return new LegacySpectralLinkForceDamageStage(forceDamageAddition);
    }

    /// <summary>
    /// Extracts target 7.69 Type=1 Holy Touch deltas in affect-slot order.
    /// Each affect subtracts its Value from Run and 30 from Attack; a head
    /// item above index 50 also loses 40 Intelligence per affect.
    /// </summary>
    public static LegacyHolyTouchAffectStage CalculateHolyTouchAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int headItemIndex)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var runDelta = 0;
        var attackDelta = 0;
        var intelligenceDelta = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 1)
                continue;

            runDelta = unchecked(runDelta - affectSnapshot[offset + 1]);
            attackDelta = unchecked(attackDelta - 30);
            if (headItemIndex > 50)
                intelligenceDelta = unchecked(intelligenceDelta - 40);
        }

        return new LegacyHolyTouchAffectStage(runDelta, attackDelta, intelligenceDelta);
    }

    /// <summary>
    /// Applies target 7.69 Type=14 Possessed mutations in affect-slot order.
    /// Mortal/Arch characters use a value multiplier of two; other classes use
    /// three. Each value adds twice to MaxHp and once to Constitution.
    /// </summary>
    public static LegacyPossessedAffectStage CalculatePossessedAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int classMaster,
        int startingMaxHp,
        int startingConstitution)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var maxHp = startingMaxHp;
        var constitution = startingConstitution;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 14)
                continue;

            var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort))) * 3 / 4 + affectSnapshot[offset + 1];
            value *= classMaster is LegacyAccountSnapshot.ClassMasterArch or LegacyAccountSnapshot.ClassMasterMortal
                ? 2
                : 3;

            maxHp = unchecked(maxHp + value * 2);
            constitution = unchecked((short)(constitution + value));
        }

        return new LegacyPossessedAffectStage(maxHp, unchecked((short)constitution));
    }

    /// <summary>
    /// Applies target 7.69 Type=13 Assault affects in affect-slot order.
    /// The target branch contributes only Level / 10 + Value to the shared
    /// damage multiplier; unlike W2PP, it does not mutate Damage or MaxHp.
    /// </summary>
    public static LegacyAssaultAffectStage CalculateAssaultAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingDamage,
        int startingMaxHp)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var damageMultiplierAdjustment = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 13)
                continue;

            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            damageMultiplierAdjustment = unchecked(
                damageMultiplierAdjustment + level / 10 + affectSnapshot[offset + 1]);
        }

        return new LegacyAssaultAffectStage(
            damageMultiplierAdjustment,
            startingDamage,
            startingMaxHp);
    }

    /// <summary>
    /// Applies target 7.69 Type=24 Samaritan affects in affect-slot order.
    /// Each affect adds one quarter of the current ArmorClass plus Value;
    /// integer truncation and sequential accumulation are preserved.
    /// </summary>
    public static LegacySamaritanArmorClassAffectStage CalculateSamaritanArmorClassAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingArmorClass)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var armorClass = startingArmorClass;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 24)
                continue;

            var value = affectSnapshot[offset + 1];
            var addition = unchecked(armorClass / 4 + value);
            armorClass = unchecked(armorClass + addition);
        }

        return new LegacySamaritanArmorClassAffectStage(armorClass);
    }

    /// <summary>
    /// Extracts the target 7.69 Type=11 Magic Shield AC addition. Each active
    /// record contributes `Level / 5 + Value`; the later AC clamp/commit remains
    /// outside this stage.
    /// </summary>
    public static LegacyMagicShieldAffectStage CalculateMagicShieldAffectStage(ReadOnlySpan<byte> affectSnapshot)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var armorClassAddition = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 11)
                continue;

            var value = affectSnapshot[offset + 1];
            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            armorClassAddition = unchecked(armorClassAddition + level / 5 + value);
        }

        return new LegacyMagicShieldAffectStage(armorClassAddition);
    }

    /// <summary>
    /// Extracts the target 7.69 Type=9 Magic Weapon damage and multiplier
    /// deltas. The target does not add Magic in this branch; W2PP's separate
    /// comparison stage documents its legacy +5-per-affect behavior.
    /// </summary>
    public static LegacyMagicWeaponAffectStage CalculateMagicWeaponAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int characterClass,
        int learnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var damageAddition = 0;
        var damageMultiplierAdjustment = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 9)
                continue;

            var value = affectSnapshot[offset + 1];
            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            var add = unchecked(level * 5 / 20 + value);
            add = unchecked(add * 3 / 2);
            damageMultiplierAdjustment = unchecked(damageMultiplierAdjustment + 5);

            if (characterClass == MagicWeaponClass && (learnedSkills & 0x80000) != 0)
            {
                add = unchecked(add * 3);
                damageMultiplierAdjustment = unchecked(damageMultiplierAdjustment + 10);
            }

            damageAddition = unchecked(damageAddition + add);
        }

        return new LegacyMagicWeaponAffectStage(damageAddition, damageMultiplierAdjustment, 0);
    }

    /// <summary>
    /// Applies the target 7.69 Type=15 Special additions. Each affect adds
    /// Level / 10 + Value to all four Special values and clamps each result to
    /// 255. The W2PP Special1 cap of 200 is kept in the comparison fixture,
    /// rather than silently copied into the target stage.
    /// </summary>
    public static LegacyAthenaTouchAffectStage CalculateAthenaTouchAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingSpecial1,
        int startingSpecial2,
        int startingSpecial3,
        int startingSpecial4)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var special1 = startingSpecial1;
        var special2 = startingSpecial2;
        var special3 = startingSpecial3;
        var special4 = startingSpecial4;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 15)
                continue;

            var value = affectSnapshot[offset + 1];
            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            var addition = level / 10 + value;
            special1 = Math.Min(255, unchecked(special1 + addition));
            special2 = Math.Min(255, unchecked(special2 + addition));
            special3 = Math.Min(255, unchecked(special3 + addition));
            special4 = Math.Min(255, unchecked(special4 + addition));
        }

        return new LegacyAthenaTouchAffectStage(special1, special2, special3, special4);
    }

    /// <summary>
    /// Applies target 7.69 Type=5 Fanaticism mutations in affect-slot order.
    /// The source uses a single-precision reduction factor and narrows every
    /// result to the signed short Dexterity field; no clamp is present.
    /// </summary>
    public static LegacyFanaticismAffectStage CalculateFanaticismAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingDexterity)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var dexterity = startingDexterity;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 5)
                continue;

            var factor = (100 - affectSnapshot[offset + 1]) / 100.0f;
            dexterity = unchecked((short)(dexterity * factor));
        }

        return new LegacyFanaticismAffectStage(unchecked((short)dexterity));
    }

    /// <summary>
    /// Applies target 7.69 Type=6 Dexterity mutations in affect-slot order.
    /// The source uses a single-precision factor and narrows the result to the
    /// signed short field; no clamp is present in this branch.
    /// </summary>
    public static LegacyDexterityAffectStage CalculateDexterityAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingDexterity)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var dexterity = startingDexterity;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 6)
                continue;

            var factor = (affectSnapshot[offset + 1] + 100) / 100.0f;
            dexterity = unchecked((short)(dexterity * factor));
        }

        return new LegacyDexterityAffectStage(unchecked((short)dexterity));
    }

    /// <summary>
    /// Applies target 7.69 Type=12 ArmorClass reductions in affect-slot order.
    /// The source uses a single-precision factor and truncates the result to an
    /// integer on every affect; no clamp is present in this branch.
    /// </summary>
    public static LegacyArmorClassReductionAffectStage CalculateArmorClassReductionAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingArmorClass)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var armorClass = startingArmorClass;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != 12)
                continue;

            var factor = (100 - affectSnapshot[offset + 1]) / 100.0f;
            armorClass = (int)(armorClass * factor);
        }

        return new LegacyArmorClassReductionAffectStage(armorClass);
    }

    /// <summary>
    /// Applies the selected 7.69 ArmorClass branches in their source pass order.
    /// The first slot pass handles Type=24, 31, 25, 16, 11, and 21; the later pass
    /// interleaves Type=12 and Type=8 bit 4 by affect slot. Other unported AC
    /// branches remain outside this selected projection.
    /// </summary>
    public static LegacyCurrentScoreArmorClassAffectCompositionStage CalculateCurrentScoreArmorClassAffectCompositionStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingArmorClass,
        int characterClass,
        int learnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var armorClass = startingArmorClass;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            var type = affectSnapshot[offset + AffectTypeOffset];
            var value = affectSnapshot[offset + 1];
            switch (type)
            {
                case SamaritanAffectType:
                    var samaritanAddition = unchecked(armorClass / 4 + value);
                    armorClass = unchecked(armorClass + samaritanAddition);
                    break;
                case GoldenShieldAffectType:
                    var goldenShieldLevel = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                        affectSnapshot.Slice(offset + 2, sizeof(ushort)));
                    armorClass = unchecked(armorClass + goldenShieldLevel / 2 + value);
                    break;
                case ElementalProtectionAffectType:
                    ApplyElementalProtectionArmorClassAffect(affectSnapshot, offset, ref armorClass);
                    break;
                case TransformationAffectType:
                    _ = ApplyTransformationArmorClassAffect(
                        affectSnapshot,
                        offset,
                        characterClass,
                        learnedSkills,
                        ref armorClass);
                    break;
                case MagicShieldAffectType:
                    var magicShieldLevel = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                        affectSnapshot.Slice(offset + 2, sizeof(ushort)));
                    armorClass = unchecked(armorClass + magicShieldLevel / 5 + value);
                    break;
                case MeditationAffectType:
                    armorClass = unchecked(armorClass - CalculateMeditationArmorClassReductionAffect(affectSnapshot, offset));
                    break;
            }
        }

        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            var type = affectSnapshot[offset + AffectTypeOffset];
            if (type == ArmorClassReductionAffectType)
            {
                var factor = (100 - affectSnapshot[offset + 1]) / 100.0f;
                armorClass = (int)(armorClass * factor);
            }
            else if (type == PvpJewelAffectType)
            {
                var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                    affectSnapshot.Slice(offset + 2, sizeof(ushort)));
                if ((level & (1 << 4)) == 0)
                    continue;

                var addition = unchecked(armorClass * 10 / 100);
                armorClass = unchecked(armorClass + addition);
            }
        }

        return new LegacyCurrentScoreArmorClassAffectCompositionStage(armorClass);
    }

    /// <summary>
    /// Calculates the attack speed and intelligence deltas from active Type=7
    /// Frozen Blade affects in the 7.69 CurrentScore loop. The attack slow applies
    /// unconditionally, while the intelligence reduction applies only when the head
    /// item index exceeds 50 (monster face / head index).
    /// </summary>
    public static LegacyFrozenBladeAffectStage CalculateFrozenBladeAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int headItemIndex)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var attackSpeedDelta = 0;
        var intelligenceDelta = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != FrozenBladeAffectType)
                continue;

            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            var tmp = level / 10 + 10;
            attackSpeedDelta = unchecked(attackSpeedDelta - tmp);

            if (headItemIndex > 50)
                intelligenceDelta = unchecked(intelligenceDelta - (tmp + 10));
        }

        return new LegacyFrozenBladeAffectStage(attackSpeedDelta, intelligenceDelta);
    }

    /// <summary>
    /// Calculates the damage reduction contributed by active Type=10 Weaken affects
    /// in the 7.69 CurrentScore loop. Formula matches W2PP: sub = Level / 5 + Value.
    /// </summary>
    public static LegacyWeakenAffectStage CalculateWeakenAffectStage(
        ReadOnlySpan<byte> affectSnapshot)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var damageReduction = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != WeakenAffectType)
                continue;

            var value = affectSnapshot[offset + 1];
            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            var sub = level / 5 + value;
            damageReduction = unchecked(damageReduction + sub);
        }

        return new LegacyWeakenAffectStage(damageReduction);
    }

    /// <summary>
    /// Calculates the sequential armor class reduction and damage multiplier adjustment
    /// contributed by active Type=21 Meditation affects in the 7.69 CurrentScore loop.
    /// ArmorClass reduction matches W2PP (Level / 3 + 10) and is shared with the ordered
    /// coordinator AC pass. The multiplier addition is consumed by the login projection using
    /// target 7.69's Level / 25 + Value formula; W2PP diverges with Level / 10 + Value.
    /// </summary>
    public static LegacyMeditationAffectStage CalculateMeditationAffectStage(
        ReadOnlySpan<byte> affectSnapshot)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var armorClassReduction = 0;
        var damageMultiplierAdjustment = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != MeditationAffectType)
                continue;

            var value = affectSnapshot[offset + 1];
            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));

            var add = CalculateMeditationArmorClassReductionAffect(affectSnapshot, offset);
            armorClassReduction = unchecked(armorClassReduction + add);

            var tv = level / 25 + value;
            damageMultiplierAdjustment = unchecked(damageMultiplierAdjustment + tv);
        }

        return new LegacyMeditationAffectStage(armorClassReduction, damageMultiplierAdjustment);
    }

    /// <summary>
    /// Calculates the armor class addition contributed by active Type=31 Golden Shield
    /// affects in the 7.69 CurrentScore loop. Target 7.69 adds Level / 2 + Value.
    /// W2PP uses the same formula in its single slot-order buff loop; 7.69 applies
    /// this stage in a separate pass.
    /// </summary>
    public static LegacyGoldenShieldAffectStage CalculateGoldenShieldAffectStage(
        ReadOnlySpan<byte> affectSnapshot)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var armorClassAddition = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != GoldenShieldAffectType)
                continue;

            var value = affectSnapshot[offset + 1];
            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));

            var add = level / 2 + value;
            armorClassAddition = unchecked(armorClassAddition + add);
        }

        return new LegacyGoldenShieldAffectStage(armorClassAddition);
    }

    /// <summary>
    /// Calculates the armor class addition contributed by active Type=25 Elemental Protection
    /// affects in the 7.69 CurrentScore loop: Level / 2 + Value (+30 if Level >= 255).
    /// In W2PP, Type=25 mutates only resistances (Fire, Thunder, Ice), leaving ArmorClass untouched.
    /// </summary>
    public static LegacyElementalProtectionArmorClassAffectStage CalculateElementalProtectionArmorClassAffectStage(
        ReadOnlySpan<byte> affectSnapshot)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var armorClassAddition = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != ElementalProtectionAffectType)
                continue;

            ApplyElementalProtectionArmorClassAffect(affectSnapshot, offset, ref armorClassAddition);
        }

        return new LegacyElementalProtectionArmorClassAffectStage(armorClassAddition);
    }

    private static void ApplyElementalProtectionArmorClassAffect(
        ReadOnlySpan<byte> affectSnapshot,
        int offset,
        ref int armorClass)
    {
        var value = affectSnapshot[offset + 1];
        var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
            affectSnapshot.Slice(offset + 2, sizeof(ushort)));
        var addition = level / 2 + value;
        if (level >= 255)
            addition += 30;

        armorClass = unchecked(armorClass + addition);
    }

    private static int CalculateMeditationArmorClassReductionAffect(
        ReadOnlySpan<byte> affectSnapshot,
        int offset)
    {
        var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
            affectSnapshot.Slice(offset + 2, sizeof(ushort)));
        return unchecked(level / 3 + 10);
    }

    /// <summary>
    /// Applies active Type=35 Vigor affects in slot order using the common 7.69 score-loop formula.
    /// MaxHp gains 15 percent and MaxMp gains 7 percent, with integer truncation after multiplying;
    /// each affect observes the prior result. The 7.69 branch compares the increment to 100,000
    /// and assigns that value only when the increment reaches the threshold, so the result can exceed it.
    /// </summary>
    public static LegacyVigorAffectStage CalculateVigorAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingMaxHp,
        int startingMaxMp)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var maxHp = startingMaxHp;
        var maxMp = startingMaxMp;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != VigorAffectType)
                continue;

            ApplyVigorAffect(ref maxHp, ref maxMp);
        }

        return new LegacyVigorAffectStage(maxHp, maxMp);
    }

    /// <summary>
    /// Applies active Type=34 Divina affects in slot order using the 7.69 score-loop formula.
    /// HP, MP and Damage are updated sequentially; each Magic increment uses sourceMobMagic,
    /// while startingMagic is the local accumulator. This stage intentionally leaves the
    /// later final scalar clamp to its own score block.
    /// </summary>
    public static LegacyDivinaAffectStage CalculateDivinaAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingMaxHp,
        int startingMaxMp,
        int startingDamage,
        int sourceMobMagic,
        int startingMagic)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var maxHp = startingMaxHp;
        var maxMp = startingMaxMp;
        var damage = startingDamage;
        var magic = startingMagic;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != DivinaAffectType)
                continue;

            ApplyDivinaAffect(ref maxHp, ref maxMp, ref damage, sourceMobMagic, ref magic);
        }

        return new LegacyDivinaAffectStage(maxHp, maxMp, damage, magic);
    }

    /// <summary>
    /// Applies the remaining score-loop bits from active Type=8 PvP-jewel affects in 7.69
    /// slot order. Bit 1 resistance is handled by ApplyResistanceAffectStage; bits 0 and 3
    /// belong to attack processing. Magic increments use the unchanged source MOB.Magic.
    /// </summary>
    public static LegacyPvpJewelAffectStage CalculatePvpJewelAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingMaxHp,
        int startingMaxMp,
        int startingArmorClass,
        int startingDamage,
        int sourceMobMagic,
        int startingMagic,
        int startingLearnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var maxHp = startingMaxHp;
        var maxMp = startingMaxMp;
        var armorClass = startingArmorClass;
        var damage = startingDamage;
        var magic = startingMagic;
        var learnedSkills = startingLearnedSkills;
        ushort rsvFlagsToAdd = 0;

        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != PvpJewelAffectType)
                continue;

            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            ApplyPvpJewelAffect(
                level,
                sourceMobMagic,
                ref maxHp,
                ref maxMp,
                ref armorClass,
                ref damage,
                ref magic,
                ref learnedSkills,
                ref rsvFlagsToAdd,
                includeArmorClass: true);
        }

        return new LegacyPvpJewelAffectStage(
            maxHp,
            maxMp,
            armorClass,
            damage,
            magic,
            learnedSkills,
            rsvFlagsToAdd);
    }

    /// <summary>
    /// Evaluates the selected score-changing branches while scanning the 32
    /// affect records once. Unsupported Type=8 bits stay in their dedicated
    /// attack/resistance paths, and unrelated affect types are skipped.
    /// </summary>
    public static LegacyCurrentScoreAffectCompositionStage CalculateCurrentScoreAffectCompositionStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingMaxHp,
        int startingMaxMp,
        int startingArmorClass,
        int startingDamage,
        int sourceMobMagic,
        int startingMagic,
        int startingLearnedSkills,
        int currentLevel = 0,
        int classMaster = LegacyAccountSnapshot.ClassMasterMortal)
        => CalculateCurrentScoreAffectCompositionStage(
            affectSnapshot,
            startingMaxHp,
            startingMaxMp,
            startingArmorClass,
            startingDamage,
            sourceMobMagic,
            startingMagic,
            startingLearnedSkills,
            includePvpJewelArmorClass: true,
            currentLevel,
            classMaster);

    private static LegacyCurrentScoreAffectCompositionStage CalculateCurrentScoreAffectCompositionStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingMaxHp,
        int startingMaxMp,
        int startingArmorClass,
        int startingDamage,
        int sourceMobMagic,
        int startingMagic,
        int startingLearnedSkills,
        bool includePvpJewelArmorClass,
        int currentLevel,
        int classMaster)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var maxHp = startingMaxHp;
        var maxMp = startingMaxMp;
        var armorClass = startingArmorClass;
        var damage = startingDamage;
        var magic = startingMagic;
        var learnedSkills = startingLearnedSkills;
        ushort rsvFlagsToAdd = 0;

        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            switch (affectSnapshot[offset + AffectTypeOffset])
            {
                case Type4ScoreAffectType:
                    ApplyType4Affect(ref maxHp, ref maxMp, ref damage, sourceMobMagic, ref magic);
                    break;
                case DivinaAffectType:
                    ApplyDivinaAffect(ref maxHp, ref maxMp, ref damage, sourceMobMagic, ref magic);
                    break;
                case VigorAffectType:
                    ApplyVigorAffect(ref maxHp, ref maxMp);
                    break;
                case ManaToHealthAffectType:
                    ApplyManaToHealthAffect(ref maxHp, ref maxMp);
                    break;
                case SoulSwapAffectType:
                    var affectLevel = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                        affectSnapshot.Slice(offset + 2, sizeof(ushort)));
                    ApplySoulSwapAffect(ref maxHp, ref maxMp, currentLevel, classMaster, affectLevel);
                    break;
                case PvpJewelAffectType:
                    var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                        affectSnapshot.Slice(offset + 2, sizeof(ushort)));
                    ApplyPvpJewelAffect(
                        level,
                        sourceMobMagic,
                        ref maxHp,
                        ref maxMp,
                        ref armorClass,
                        ref damage,
                        ref magic,
                        ref learnedSkills,
                        ref rsvFlagsToAdd,
                        includeArmorClass: includePvpJewelArmorClass);
                    break;
                case BlockAffectType:
                    rsvFlagsToAdd |= BlockRsvFlag;
                    break;
                case EvasionAffectType:
                    rsvFlagsToAdd |= ParryRsvFlag;
                    break;
                case InvisibilityAffectType:
                    rsvFlagsToAdd |= HideRsvFlag;
                    break;
            }
        }

        return new LegacyCurrentScoreAffectCompositionStage(
            maxHp,
            maxMp,
            armorClass,
            damage,
            magic,
            learnedSkills,
            rsvFlagsToAdd);
    }

    /// <summary>
    /// Extracts only the AttackSpeedBonus/RunSpeedBonus assignments made by
    /// valid Type=16 transformations in the selected 7.69 BASE_GetCurrentScore.
    /// A later valid transformation replaces an earlier one; invalid affects
    /// leave the prior assignment unchanged. Other transformation side effects
    /// are intentionally outside this stage.
    /// </summary>
    public static LegacyTransformationSpeedAffectStage CalculateTransformationSpeedAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int characterClass,
        int learnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var result = new LegacyTransformationSpeedAffectStage(false, 0, 0);
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != TransformationAffectType)
                continue;

            var transformation = affectSnapshot[offset + 1] - 1;
            if (transformation < 0 || transformation >= 5 || characterClass != BeastMasterClass)
                continue;

            var (baseAttackSpeed, runSpeed) = transformation switch
            {
                0 => (15, 1),
                1 => (60, 0),
                2 => (115, 1),
                3 => (155, 0),
                4 => (155, 3),
                _ => throw new InvalidOperationException("Transformation index passed its source gate."),
            };

            var attackSpeedAddition = transformation switch
            {
                1 when (learnedSkills & 0x80000) != 0 => 20,
                2 when (learnedSkills & 0x200000) != 0 => 20,
                3 => 10,
                4 => 25,
                _ => 0,
            };

            result = new LegacyTransformationSpeedAffectStage(
                true,
                baseAttackSpeed + attackSpeedAddition,
                runSpeed);
        }

        return result;
    }

    /// <summary>
    /// Extracts only the damage mutations from valid Type=16 affects in the
    /// selected 7.69 score loop. DamageAddition is applied before the eventual
    /// accumulated multiplier; DamageMultiplierAdjustment is the sum of each
    /// source `multi - 100`, not a standalone percentage multiplier.
    /// </summary>
    public static LegacyTransformationDamageAffectStage CalculateTransformationDamageAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int characterClass,
        int learnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var hasActiveTransformation = false;
        var damageAddition = 0;
        var damageMultiplierAdjustment = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != TransformationAffectType)
                continue;

            var transformation = affectSnapshot[offset + 1] - 1;
            if (transformation < 0 || transformation >= 5 || characterClass != BeastMasterClass)
                continue;

            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            var (baseMinimum, baseMaximum, damageAdd) = transformation switch
            {
                0 => (110, 130, (learnedSkills & 0x20000) != 0 ? 25 : 0),
                1 => (80, 100, 0),
                2 => (100, 120, (learnedSkills & 0x200000) != 0 ? 10 : 0),
                3 => (90, 110, 0),
                4 => (105, 120, 20),
                _ => throw new InvalidOperationException("Transformation index passed its source gate."),
            };

            var minimum = damageAdd + baseMinimum;
            var maximum = damageAdd + baseMaximum;
            var multiplier = (maximum - minimum) * level / 200 + minimum;
            damageMultiplierAdjustment = unchecked(damageMultiplierAdjustment + multiplier - 100);
            if (transformation == 0)
                damageAddition = unchecked(damageAddition + 10);
            hasActiveTransformation = true;
        }

        return new LegacyTransformationDamageAffectStage(
            hasActiveTransformation,
            damageAddition,
            damageMultiplierAdjustment);
    }

    /// <summary>
    /// Applies only the sequential ArmorClass mutations from valid Type=16
    /// affects in the selected 7.69 score loop. Each affect scales the AC
    /// produced by prior affects, truncates at the source division, then Wolf
    /// adds five. Other transformation outputs and score stages are excluded.
    /// </summary>
    public static LegacyTransformationArmorClassAffectStage CalculateTransformationArmorClassAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingArmorClass,
        int characterClass,
        int learnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var hasActiveTransformation = false;
        var armorClass = startingArmorClass;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != TransformationAffectType)
                continue;

            hasActiveTransformation |= ApplyTransformationArmorClassAffect(
                affectSnapshot,
                offset,
                characterClass,
                learnedSkills,
                ref armorClass);
        }

        return new LegacyTransformationArmorClassAffectStage(hasActiveTransformation, armorClass);
    }

    private static bool ApplyTransformationArmorClassAffect(
        ReadOnlySpan<byte> affectSnapshot,
        int offset,
        int characterClass,
        int learnedSkills,
        ref int armorClass)
    {
        var transformation = affectSnapshot[offset + 1] - 1;
        if (transformation < 0 || transformation >= 5 || characterClass != BeastMasterClass)
            return false;

        var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
            affectSnapshot.Slice(offset + 2, sizeof(ushort)));
        var armorClassAddition = transformation switch
        {
            0 => 0,
            1 => 0,
            2 when (learnedSkills & 0x200000) != 0 => 5,
            2 => 0,
            3 => 20,
            4 => 10,
            _ => throw new InvalidOperationException("Transformation index passed its source gate."),
        };
        var (baseMinimum, baseMaximum) = transformation switch
        {
            0 => (95, 105),
            1 => (100, 110),
            2 => (105, 115),
            3 => (110, 125),
            4 => (110, 120),
            _ => throw new InvalidOperationException("Transformation index passed its source gate."),
        };

        var minimum = armorClassAddition + baseMinimum;
        var delta = baseMaximum - baseMinimum;
        var multiplier = delta * level / 200 + minimum;
        armorClass = unchecked(armorClass * multiplier) / 100;
        if (transformation == 0)
            armorClass = unchecked(armorClass + 5);
        return true;
    }

    /// <summary>
    /// Applies only the sequential MaxHp mutations from valid Type=16 affects
    /// in the selected 7.69 score loop. Each affect scales the MaxHp left by
    /// prior affects and truncates at the source integer division. Resistance
    /// additions from the branch are already represented by the separate
    /// resistance-affect stage and are intentionally not duplicated here.
    /// </summary>
    public static LegacyTransformationMaxHpAffectStage CalculateTransformationMaxHpAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int startingMaxHp,
        int characterClass,
        int learnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var hasActiveTransformation = false;
        var maxHp = startingMaxHp;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != TransformationAffectType)
                continue;

            var transformation = affectSnapshot[offset + 1] - 1;
            if (transformation < 0 || transformation >= 5 || characterClass != BeastMasterClass)
                continue;

            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                affectSnapshot.Slice(offset + 2, sizeof(ushort)));
            var hpAddition = transformation switch
            {
                0 => 0,
                1 when (learnedSkills & 0x80000) != 0 => 10,
                2 when (learnedSkills & 0x200000) != 0 => 5,
                3 => 5,
                4 => 10,
                _ => 0,
            };
            var (baseMinimum, baseMaximum) = transformation switch
            {
                0 => (95, 105),
                1 => (110, 140),
                2 => (100, 120),
                3 => (105, 110),
                4 => (105, 115),
                _ => throw new InvalidOperationException("Transformation index passed its source gate."),
            };

            var minimum = hpAddition + baseMinimum;
            var delta = baseMaximum - baseMinimum;
            var multiplier = delta * level / 200 + minimum;
            maxHp = unchecked(maxHp * multiplier) / 100;
            hasActiveTransformation = true;
        }

        return new LegacyTransformationMaxHpAffectStage(hasActiveTransformation, maxHp);
    }

    /// <summary>
    /// Extracts the Critical mutations from valid Type=16 affects in the
    /// selected 7.69 score loop. Critical additions accumulate per affect;
    /// the existing Critical seed and final clamp are separate stages.
    /// </summary>
    public static LegacyTransformationCriticalAffectStage CalculateTransformationCriticalAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        int characterClass,
        int learnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var hasActiveTransformation = false;
        var criticalAddition = 0;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != TransformationAffectType)
                continue;

            var transformation = affectSnapshot[offset + 1] - 1;
            if (transformation < 0 || transformation >= 5 || characterClass != BeastMasterClass)
                continue;

            var addition = transformation switch
            {
                0 when (learnedSkills & 0x20000) != 0 => 5,
                4 => 10,
                _ => 0,
            };
            criticalAddition = unchecked(criticalAddition + addition);
            hasActiveTransformation = true;
        }

        return new LegacyTransformationCriticalAffectStage(hasActiveTransformation, criticalAddition);
    }

    /// <summary>
    /// Reproduces the Type=16 equipment mutation without mutating the caller's
    /// item. Each valid affect replaces the head index (22, 23, 24, 25, or 32)
    /// and overwrites effect 0 with EF_SANC. The character level is distinct
    /// from Affect.Level: it is the MOB.CurrentScore.Level used by the source.
    /// `special3` is expected to be the already capped value from the prior
    /// Cur Special stage.
    /// </summary>
    public static LegacyTransformationEquipmentAffectStage CalculateTransformationEquipmentAffectStage(
        ReadOnlySpan<byte> affectSnapshot,
        LegacyItem startingHeadItem,
        int characterClass,
        int characterLevel,
        int classMaster,
        int special3)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var hasActiveTransformation = false;
        var headItem = startingHeadItem;
        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            if (affectSnapshot[offset + AffectTypeOffset] != TransformationAffectType)
                continue;

            var transformation = affectSnapshot[offset + 1] - 1;
            if (transformation < 0 || transformation >= 5 || characterClass != BeastMasterClass)
                continue;

            var itemIndex = transformation == 4 ? 32 : transformation + 22;
            var baseSanctuary = transformation == 0 ? 15 : 100;
            var levelTerm = classMaster is not LegacyAccountSnapshot.ClassMasterArch
                and not LegacyAccountSnapshot.ClassMasterMortal
                ? characterLevel + TargetMaximumLevel
                : characterLevel;
            var sanctuary = (special3 + (levelTerm * 2)) / 3;
            sanctuary = (sanctuary - baseSanctuary) / 12;
            sanctuary = Math.Clamp(sanctuary, 0, 9);

            headItem = headItem with
            {
                Index = checked((short)itemIndex),
                Effect1 = checked((byte)LegacyItemEffect.Sanctuary),
                Value1 = checked((byte)sanctuary),
            };
            hasActiveTransformation = true;
        }

        return new LegacyTransformationEquipmentAffectStage(hasActiveTransformation, headItem);
    }

    /// <summary>
    /// Composes the already-audited Type=16 score stages for one immutable
    /// affect snapshot. This method is an orchestration fixture boundary: it
    /// does not change the active login encoder, mutate a MOB, or infer any
    /// score blocks that have not been ported yet.
    /// </summary>
    public static LegacyTransformationAffectCompositionStage CalculateTransformationAffectCompositionStage(
        ReadOnlySpan<byte> affectSnapshot,
        LegacyItem startingHeadItem,
        int startingArmorClass,
        int startingMaxHp,
        int holy,
        int thunder,
        int fire,
        int ice,
        int characterClass,
        int learnedSkills,
        int characterLevel,
        int classMaster,
        int special3)
    {
        var speed = CalculateTransformationSpeedAffectStage(affectSnapshot, characterClass, learnedSkills);
        var damage = CalculateTransformationDamageAffectStage(affectSnapshot, characterClass, learnedSkills);
        var armorClass = CalculateTransformationArmorClassAffectStage(
            affectSnapshot, startingArmorClass, characterClass, learnedSkills);
        var maxHp = CalculateTransformationMaxHpAffectStage(
            affectSnapshot, startingMaxHp, characterClass, learnedSkills);
        var critical = CalculateTransformationCriticalAffectStage(affectSnapshot, characterClass, learnedSkills);
        var equipment = CalculateTransformationEquipmentAffectStage(
            affectSnapshot, startingHeadItem, characterClass, characterLevel, classMaster, special3);
        var resistance = ApplyResistanceAffectStage(
            holy,
            thunder,
            fire,
            ice,
            affectSnapshot,
            characterClass,
            startingHeadItem.Index,
            learnedSkills);

        return new LegacyTransformationAffectCompositionStage(
            speed.HasActiveTransformation,
            speed,
            damage,
            armorClass,
            maxHp,
            critical,
            equipment,
            resistance);
    }

    /// <summary>
    /// Coordinates the currently audited score blocks. Final AC is recomposed
    /// in two 7.69 passes (Type=24/31/25/16/11/21, then Type=12/Type=8 bit 4) and is
    /// carried by AfterScoreAffects; earlier AC fields remain stage-local
    /// diagnostics. Other selected Type=4/34/35/8 score branches are projected
    /// before Soul. Critical, Attack/Run, equipment and multiplier deltas remain
    /// explicit in their stage snapshots because their final commit blocks are
    /// not ported.
    /// </summary>
    public static LegacyCurrentScoreCoordinatorStage CalculateCurrentScoreCoordinatorStage(
        LegacyMobCombatState mob,
        ReadOnlySpan<LegacyItem> equipment,
        LegacyItemDataTable itemData,
        ReadOnlySpan<byte> affectSnapshot,
        int classMaster,
        byte soul,
        bool isSummon,
        int holy,
        int thunder,
        int fire,
        int ice)
    {
        ArgumentNullException.ThrowIfNull(mob);
        ArgumentNullException.ThrowIfNull(itemData);
        if (equipment.Length == 0)
            throw new ArgumentException("The score coordinator requires equipment slot 0.", nameof(equipment));

        var baseAndEquipment = RebuildBaseAndEquipmentScore(mob, equipment, itemData);
        var abilities = CalculateAbilityStage(baseAndEquipment, equipment, itemData);
        var afterSpecial = ApplySpecialAbilityStage(baseAndEquipment, equipment, itemData);
        var meditation = CalculateMeditationAffectStage(affectSnapshot);
        var armorClassAffects = CalculateCurrentScoreArmorClassAffectCompositionStage(
            affectSnapshot,
            afterSpecial.CurrentScore.Ac,
            afterSpecial.CharacterClass,
            unchecked((int)afterSpecial.LearnedSkill));
        var transformation = CalculateTransformationAffectCompositionStage(
            affectSnapshot,
            equipment[0],
            afterSpecial.CurrentScore.Ac,
            afterSpecial.CurrentScore.MaxHp,
            holy,
            thunder,
            fire,
            ice,
            afterSpecial.CharacterClass,
            unchecked((int)afterSpecial.LearnedSkill),
            afterSpecial.CurrentScore.Level,
            classMaster,
            afterSpecial.CurrentScore.Special3);
        var magicShield = CalculateMagicShieldAffectStage(affectSnapshot);

        var afterTransformation = afterSpecial with
        {
            CurrentScore = afterSpecial.CurrentScore with
            {
                Damage = unchecked(afterSpecial.CurrentScore.Damage + transformation.Damage.DamageAddition),
                Ac = transformation.ArmorClass.ArmorClass,
                MaxHp = transformation.MaxHp.MaxHp,
            },
        };
        var haste = CalculateHasteAffectStage(affectSnapshot);
        var iceEnchant = CalculateIceEnchantAffectStage(affectSnapshot, equipment, itemData);
        var drain = CalculateDrainAffectStage(affectSnapshot, equipment, itemData);
        var afterMagicShield = afterTransformation with
        {
            CurrentScore = afterTransformation.CurrentScore with
            {
                Ac = unchecked(afterTransformation.CurrentScore.Ac + magicShield.ArmorClassAddition),
            },
        };
        var magicWeapon = CalculateMagicWeaponAffectStage(
            affectSnapshot,
            afterMagicShield.CharacterClass,
            unchecked((int)afterMagicShield.LearnedSkill));
        var afterMagicWeapon = afterMagicShield with
        {
            CurrentScore = afterMagicShield.CurrentScore with
            {
                Damage = unchecked(afterMagicShield.CurrentScore.Damage + magicWeapon.DamageAddition),
            },
        };
        var athenaTouch = CalculateAthenaTouchAffectStage(
            affectSnapshot,
            afterMagicWeapon.CurrentScore.Special1,
            afterMagicWeapon.CurrentScore.Special2,
            afterMagicWeapon.CurrentScore.Special3,
            afterMagicWeapon.CurrentScore.Special4);
        var afterAthenaTouch = afterMagicWeapon with
        {
            CurrentScore = afterMagicWeapon.CurrentScore with
            {
                Special1 = checked((short)athenaTouch.Special1),
                Special2 = checked((short)athenaTouch.Special2),
                Special3 = checked((short)athenaTouch.Special3),
                Special4 = checked((short)athenaTouch.Special4),
            },
        };
        var holyTouch = CalculateHolyTouchAffectStage(affectSnapshot, equipment[0].Index);
        var afterHolyTouch = afterAthenaTouch with
        {
            CurrentScore = afterAthenaTouch.CurrentScore with
            {
                Intelligence = unchecked((short)(afterAthenaTouch.CurrentScore.Intelligence + holyTouch.IntelligenceDelta)),
            },
        };
        var fanaticism = CalculateFanaticismAffectStage(
            affectSnapshot,
            afterHolyTouch.CurrentScore.Dexterity);
        var afterFanaticism = afterHolyTouch with
        {
            CurrentScore = afterHolyTouch.CurrentScore with
            {
                Dexterity = fanaticism.Dexterity,
            },
        };
        var possessed = CalculatePossessedAffectStage(
            affectSnapshot,
            classMaster,
            afterFanaticism.CurrentScore.MaxHp,
            afterFanaticism.CurrentScore.Constitution);
        var afterPossessed = afterFanaticism with
        {
            CurrentScore = afterFanaticism.CurrentScore with
            {
                MaxHp = possessed.MaxHp,
                Constitution = possessed.Constitution,
            },
        };
        var assault = CalculateAssaultAffectStage(
            affectSnapshot,
            afterPossessed.CurrentScore.Damage,
            afterPossessed.CurrentScore.MaxHp);
        // Assault only accumulates DamageMultiplierAdjustment; target 7.69 does not mutate Damage/MaxHp here.
        var afterAssault = afterPossessed;
        var samaritan = CalculateSamaritanArmorClassAffectStage(
            affectSnapshot,
            afterAssault.CurrentScore.Ac);
        var afterSamaritan = afterAssault with
        {
            CurrentScore = afterAssault.CurrentScore with
            {
                Ac = samaritan.ArmorClass,
            },
        };
        var dexterity = CalculateDexterityAffectStage(
            affectSnapshot,
            afterSamaritan.CurrentScore.Dexterity);
        var afterDexterity = afterSamaritan with
        {
            CurrentScore = afterSamaritan.CurrentScore with
            {
                Dexterity = dexterity.Dexterity,
            },
        };
        var armorClassReduction = CalculateArmorClassReductionAffectStage(
            affectSnapshot,
            afterDexterity.CurrentScore.Ac);
        var afterArmorClassReduction = afterDexterity with
        {
            CurrentScore = afterDexterity.CurrentScore with
            {
                Ac = armorClassReduction.ArmorClass,
            },
        };
        var scoreAffects = CalculateCurrentScoreAffectCompositionStage(
            affectSnapshot,
            afterArmorClassReduction.CurrentScore.MaxHp,
            afterArmorClassReduction.CurrentScore.MaxMp,
            armorClassAffects.ArmorClass,
            afterArmorClassReduction.CurrentScore.Damage,
            mob.Magic,
            abilities.Magic,
            unchecked((int)afterArmorClassReduction.LearnedSkill),
            includePvpJewelArmorClass: false,
            currentLevel: afterArmorClassReduction.CurrentScore.Level,
            classMaster: classMaster);
        var afterScoreAffects = afterArmorClassReduction with
        {
            Magic = scoreAffects.Magic,
            LearnedSkill = unchecked((uint)scoreAffects.LearnedSkills),
            Rsv = (ushort)(afterArmorClassReduction.Rsv | scoreAffects.RsvFlagsToAdd | iceEnchant.RsvFlagsToAdd | drain.RsvFlagsToAdd),
            CurrentScore = afterArmorClassReduction.CurrentScore with
            {
                MaxHp = scoreAffects.MaxHp,
                MaxMp = scoreAffects.MaxMp,
                Ac = scoreAffects.ArmorClass,
                Damage = scoreAffects.Damage,
            },
        };
        var hunterClassPassives = CalculateHunterClassPassivesStage(afterScoreAffects, equipment, itemData);
        var afterHunterClassPassives = afterScoreAffects with
        {
            CurrentScore = afterScoreAffects.CurrentScore with
            {
                Damage = unchecked(afterScoreAffects.CurrentScore.Damage +
                    hunterClassPassives.AggressivenessDamageAddition +
                    hunterClassPassives.LightningStormDamageAddition),
                Ac = unchecked(afterScoreAffects.CurrentScore.Ac + hunterClassPassives.ShadowProtectionArmorClassAddition),
            },
        };
        var hunterProficiency = CalculateHunterProficiencyStage(afterSpecial, equipment);
        var afterHunterProficiency = afterHunterClassPassives with
        {
            CurrentScore = afterHunterClassPassives.CurrentScore with
            {
                Damage = unchecked(afterHunterClassPassives.CurrentScore.Damage + hunterProficiency.DamageAddition),
            },
        };
        var armorCritical = CalculateArmorCriticalStage(
            afterSpecial,
            afterHunterProficiency.CurrentScore.Ac);
        var afterArmorCritical = afterHunterProficiency with
        {
            CurrentScore = afterHunterProficiency.CurrentScore with
            {
                Ac = armorCritical.ArmorClass,
            },
        };
        var combatAwareness = CalculateCombatAwarenessStage(afterArmorCritical);
        var afterCombatAwareness = afterArmorCritical with
        {
            CurrentScore = afterArmorCritical.CurrentScore with
            {
                Damage = unchecked(afterArmorCritical.CurrentScore.Damage + combatAwareness.DamageAddition),
            },
        };
        var afterSoul = ApplySoulHealthManaStage(afterCombatAwareness, affectSnapshot, classMaster, soul, isSummon);
        var afterKibita = ApplyKibitaSoulStage(afterSoul, affectSnapshot, classMaster, isSummon);
        var resistance = ApplyResistanceClamps(
            transformation.Resistance.Holy,
            transformation.Resistance.Thunder,
            transformation.Resistance.Fire,
            transformation.Resistance.Ice);

        return new LegacyCurrentScoreCoordinatorStage(
            baseAndEquipment,
            abilities,
            afterSpecial,
            meditation,
            transformation,
            magicShield,
            afterTransformation,
            afterMagicShield,
            magicWeapon,
            afterMagicWeapon,
            athenaTouch,
            afterAthenaTouch,
            holyTouch,
            afterHolyTouch,
            fanaticism,
            afterFanaticism,
            possessed,
            afterPossessed,
            assault,
            afterAssault,
            samaritan,
            afterSamaritan,
            dexterity,
            afterDexterity,
            armorClassReduction,
            afterArmorClassReduction,
            scoreAffects,
            afterScoreAffects,
            hunterClassPassives,
            afterHunterClassPassives,
            hunterProficiency,
            afterHunterProficiency,
            armorCritical,
            afterArmorCritical,
            combatAwareness,
            afterCombatAwareness,
            afterSoul,
            afterKibita,
            haste,
            resistance,
            (ushort)(afterKibita.Rsv | scoreAffects.RsvFlagsToAdd | haste.RsvFlagsToAdd | iceEnchant.RsvFlagsToAdd | drain.RsvFlagsToAdd));
    }

    /// <summary>
    /// Ports the 7.69 Hunter Proficiency class pass. The source writes 1 &lt;&lt; 82
    /// against a 32-bit LearnedSkill field, which is undefined C++; the class-skill
    /// gate uses skillIndex % 24, so the clean port uses that same deterministic bit.
    /// </summary>
    public static LegacyHunterProficiencyStage CalculateHunterProficiencyStage(
        LegacyMobCombatState mob,
        ReadOnlySpan<LegacyItem> equipment)
    {
        ArgumentNullException.ThrowIfNull(mob);
        if (equipment.Length <= 7)
            throw new ArgumentException("Hunter Proficiency requires equipment slots 6 and 7.", nameof(equipment));

        var isActive = mob.CharacterClass == HunterClass &&
            (mob.LearnedSkill & HunterProficiencyLearnedFlag) != 0 &&
            equipment[6].Index != 0 && equipment[7].Index == 0;
        return new LegacyHunterProficiencyStage(
            isActive,
            isActive ? mob.CurrentScore.Special2 + 12 : 0);
    }

    /// <summary>
    /// Ports the 7.69 Hunter class passives using the already-composed score
    /// specials and ItemList weapon unique values.
    /// </summary>
    public static LegacyHunterClassPassivesStage CalculateHunterClassPassivesStage(
        LegacyMobCombatState mob,
        ReadOnlySpan<LegacyItem> equipment,
        LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(mob);
        ArgumentNullException.ThrowIfNull(itemData);
        if (equipment.Length <= 7)
            throw new ArgumentException("Hunter class passives require equipment slots 6 and 7.", nameof(equipment));

        if (mob.CharacterClass != HunterClass)
            return default;

        var weaponIndex = equipment[6].Index;
        var weaponUnique = weaponIndex != 0 ? itemData[weaponIndex]?.Unique ?? 0 : 0;
        var aggressivenessActive =
            (mob.LearnedSkill & HunterAggressivenessLearnedFlag) != 0 &&
            weaponUnique is HunterClawWeaponUnique or HunterBowWeaponUnique;
        var lightningStormActive = (mob.LearnedSkill & HunterLightningStormLearnedFlag) != 0;
        var shadowProtectionActive =
            (mob.LearnedSkill & HunterShadowProtectionLearnedFlag) != 0 &&
            weaponUnique == HunterClawWeaponUnique;
        var hunterVisionActive = (mob.LearnedSkill & HunterVisionLearnedFlag) != 0;

        return new LegacyHunterClassPassivesStage(
            aggressivenessActive ? mob.CurrentScore.Special2 + 10 : 0,
            lightningStormActive ? unchecked(mob.CurrentScore.Special2 * 2) : 0,
            shadowProtectionActive ? (mob.CurrentScore.Special4 / 3) + 10 : 0,
            hunterVisionActive ? mob.CurrentScore.Dexterity / 50 : 0);
    }

    /// <summary>
    /// Ports the 7.69 Transknight passive: learned skill 15 multiplies the
    /// current ArmorClass by 1.1f and adds 25 to Critical.
    /// </summary>
    public static LegacyArmorCriticalStage CalculateArmorCriticalStage(
        LegacyMobCombatState mob,
        int armorClass)
    {
        ArgumentNullException.ThrowIfNull(mob);
        var isActive = mob.CharacterClass == TransknightClass &&
            (mob.LearnedSkill & ArmorCriticalLearnedFlag) != 0;
        return new LegacyArmorCriticalStage(
            isActive,
            isActive ? (int)(armorClass * 1.1f) : armorClass,
            isActive ? 25 : 0);
    }

    /// <summary>
    /// Ports the 7.69 Transknight passive: learned skill 14 adds half of the
    /// composed CurrentScore.Special3 to damage using integer division.
    /// </summary>
    public static LegacyCombatAwarenessStage CalculateCombatAwarenessStage(LegacyMobCombatState mob)
    {
        ArgumentNullException.ThrowIfNull(mob);
        var isActive = mob.CharacterClass == TransknightClass &&
            (mob.LearnedSkill & CombatAwarenessLearnedFlag) != 0;
        return new LegacyCombatAwarenessStage(
            isActive,
            isActive ? mob.CurrentScore.Special3 / 2 : 0);
    }

    /// <summary>
    /// Applies only the active resistance mutations in the 7.69 CurrentScore
    /// affect loop. Returns raw totals for the later clamp and the locally
    /// transformed head index needed to preserve affect ordering. The source's
    /// EF_RESISTALL equipment loop is commented out and intentionally omitted.
    /// </summary>
    public static LegacyCurrentResistanceModifierStage ApplyResistanceAffectStage(
        int holy,
        int thunder,
        int fire,
        int ice,
        ReadOnlySpan<byte> affectSnapshot,
        int characterClass,
        int headItemIndex,
        int learnedSkills)
    {
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        for (var index = 0; index < AffectCount; index++)
        {
            var offset = index * AffectSizeInBytes;
            var type = affectSnapshot[offset + AffectTypeOffset];
            if (type == 0)
                continue;

            var value = affectSnapshot[offset + 1];
            var level = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(affectSnapshot.Slice(offset + 2, sizeof(ushort)));

            if (type == PursuitAffectType)
            {
                var reduction = headItemIndex < 50 ? value / 2 : value - 10;
                holy -= reduction;
                thunder -= reduction;
                fire -= reduction;
                ice -= reduction;
            }
            else if (type == ElementalProtectionAffectType)
            {
                var addition = (value + level / 4) / 10;
                if (level >= 255)
                    addition += 20;

                holy += addition;
                thunder += addition;
                fire += addition;
                ice += addition;
            }
            else if (type == TransformationAffectType)
            {
                var transformation = value - 1;
                if (transformation < 0 || transformation >= 5 || characterClass != BeastMasterClass)
                    continue;

                headItemIndex = transformation == 4 ? 32 : transformation + 22;
                var addition = headItemIndex switch
                {
                    22 when (learnedSkills & 0x20000) != 0 => 5,
                    23 when (learnedSkills & 0x80000) != 0 => 20,
                    24 when (learnedSkills & 0x200000) != 0 => 15,
                    25 => 20,
                    32 => 20,
                    _ => 0,
                };

                holy += addition;
                thunder += addition;
                fire += addition;
                ice += addition;
            }
            else if (type == PvpJewelAffectType && (level & (1 << 1)) != 0)
            {
                holy += 25;
                thunder += 25;
                fire += 25;
                ice += 25;
            }
        }

        return new LegacyCurrentResistanceModifierStage(holy, thunder, fire, ice, headItemIndex);
    }

    /// <summary>
    /// Applies only the final face-gated physical-damage addition and accumulated
    /// multiplier from BASE_GetCurrentScore. Earlier class/buff damage changes
    /// must already be reflected in the inputs.
    /// </summary>
    public static LegacyCurrentDamageStage ApplyFinalDamageStage(
        int damage,
        int strength,
        int dexterity,
        int special1,
        int level,
        int face,
        int classMaster,
        int damageMultiplier)
    {
        if (face < 4)
        {
            var levelBonus = classMaster is LegacyAccountSnapshot.ClassMasterArch or LegacyAccountSnapshot.ClassMasterMortal
                ? level
                : unchecked(level + TargetMaximumLevel);
            damage = unchecked(damage + strength / 2 + dexterity / 3 + special1 + levelBonus);
        }

        if (damageMultiplier != 100)
            damage = unchecked(damage * damageMultiplier) / 100;

        return new LegacyCurrentDamageStage(damage);
    }

    /// <summary>
    /// Applies the final 7.69 AttackRun composition. Inputs are the accumulated
    /// pre-final values; the cached face is the source value captured before
    /// transformations. The caller resolves mount index/effect/table rules and
    /// passes zero when the source mount gate is inactive.
    /// </summary>
    public static LegacyCurrentSpeedStage ApplyFinalAttackRunStage(
        int attackSpeedBeforeFinal,
        int runBeforeFinal,
        int attackSpeedBonus,
        int runSpeedBonus,
        int dexterity,
        int face,
        int eligibleMountRunFloor)
    {
        var attack = unchecked(attackSpeedBeforeFinal + attackSpeedBonus + dexterity / 5);
        var run = unchecked(runBeforeFinal + runSpeedBonus);

        if (face <= 4 && eligibleMountRunFloor > run)
            run = eligibleMountRunFloor;

        run = Math.Clamp(run, 0, 6);
        attack = Math.Clamp(attack, 0, 150);
        attack /= 10;

        return new LegacyCurrentSpeedStage(attack, run, checked((byte)(attack * 16 + run)));
    }

    /// <summary>
    /// Ports the 7.69 Cur HP/MP block's equipment-derived inputs. Later blocks
    /// still modify Run, AttackSpeed, regeneration, Magic, and Critical before
    /// committing them to MOB; this method therefore returns a stage snapshot.
    /// </summary>
    public static LegacyCurrentScoreAbilityStage CalculateAbilityStage(
        LegacyMobCombatState mob,
        ReadOnlySpan<LegacyItem> equipment,
        LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(mob);
        ArgumentNullException.ThrowIfNull(itemData);

        var learnedSkill = mob.LearnedSkill;
        var characterClass = mob.CharacterClass;
        var saveMana = LegacyMobAbilityMath.GetAbility(equipment, characterClass, learnedSkill, LegacyItemEffect.SaveMana, itemData);
        var magic = LegacyMobAbilityMath.GetAbility(equipment, characterClass, learnedSkill, LegacyItemEffect.Magic, itemData)
            + LegacyMobAbilityMath.GetAbility(equipment, characterClass, learnedSkill, LegacyItemEffect.MagicAdd, itemData);
        magic = (magic + 1) / 4;

        var run = (mob.CurrentScore.AttackRun & 15)
            + LegacyMobAbilityMath.GetAbility(equipment, characterClass, learnedSkill, LegacyItemEffect.RunSpeed, itemData);
        if (run > 6)
            run = 6;

        var attackSpeed = mob.BaseScore.AttackRun / 16 * 10
            + LegacyMobAbilityMath.GetAbility(equipment, characterClass, learnedSkill, LegacyItemEffect.AttackSpeed, itemData);
        var regenHp = LegacyMobAbilityMath.GetAbility(equipment, characterClass, learnedSkill, LegacyItemEffect.RegenHp, itemData);
        var regenMp = LegacyMobAbilityMath.GetAbility(equipment, characterClass, learnedSkill, LegacyItemEffect.RegenMp, itemData);
        var critical = LegacyMobAbilityMath.GetAbility(equipment, characterClass, learnedSkill, LegacyItemEffect.Critical, itemData) / 4;

        return new LegacyCurrentScoreAbilityStage(saveMana, magic, run, attackSpeed, regenHp, regenMp, critical);
    }

    /// <summary>
    /// Ports the initial base/equipment stage: reset Rsv, restore BaseScore,
    /// preserve current HP/MP, then apply the equipment-derived score abilities.
    /// Later affect, soul, class, and derived-stat stages remain separate.
    /// </summary>
    public static LegacyMobCombatState RebuildBaseAndEquipmentScore(
        LegacyMobCombatState mob,
        ReadOnlySpan<LegacyItem> equipment,
        LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(mob);
        ArgumentNullException.ThrowIfNull(itemData);

        var score = mob.BaseScore with
        {
            Hp = mob.CurrentScore.Hp,
            Mp = mob.CurrentScore.Mp,
        };

        var acBonus = LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Ac, itemData)
            + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.AcAdd, itemData);
        score = score with
        {
            Ac = score.Ac + acBonus,
            Damage = score.Damage + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Damage, itemData),
            MaxHp = score.MaxHp + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Hp, itemData),
            MaxMp = score.MaxMp + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Mp, itemData),
            Strength = unchecked((short)(score.Strength + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Strength, itemData))),
            Intelligence = unchecked((short)(score.Intelligence + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Intelligence, itemData))),
            Dexterity = unchecked((short)(score.Dexterity + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Dexterity, itemData))),
            Constitution = unchecked((short)(score.Constitution + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Constitution, itemData))),
        };

        return mob with { CurrentScore = score, Rsv = 0 };
    }

    /// <summary>Ports the 7.69 Cur Special stage, including its upper cap of 255.</summary>
    public static LegacyMobCombatState ApplySpecialAbilityStage(
        LegacyMobCombatState mob,
        ReadOnlySpan<LegacyItem> equipment,
        LegacyItemDataTable itemData)
    {
        ArgumentNullException.ThrowIfNull(mob);
        ArgumentNullException.ThrowIfNull(itemData);

        var score = mob.CurrentScore;
        var special1 = Math.Min(255, score.Special1 + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Special1, itemData));
        var special2 = Math.Min(255, score.Special2 + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Special2, itemData)
            + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.SpecialAll, itemData));
        var special3 = Math.Min(255, score.Special3 + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Special3, itemData)
            + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.SpecialAll, itemData));
        var special4 = Math.Min(255, score.Special4 + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.Special4, itemData)
            + LegacyMobAbilityMath.GetAbility(equipment, mob.CharacterClass, mob.LearnedSkill, LegacyItemEffect.SpecialAll, itemData));

        score = score with
        {
            Special1 = unchecked((short)special1),
            Special2 = unchecked((short)special2),
            Special3 = unchecked((short)special3),
            Special4 = unchecked((short)special4),
        };
        return mob with { CurrentScore = score };
    }

    /// <summary>
    /// Ports the 7.69 Soul HP/MP section after the equipment-derived score stage.
    /// The source iterates 32 eight-byte affects and applies one Soul contribution
    /// for every active affect of type 29; summons skip this section.
    /// </summary>
    public static LegacyMobCombatState ApplySoulHealthManaStage(
        LegacyMobCombatState mob,
        ReadOnlySpan<byte> affectSnapshot,
        int classMaster,
        byte soul,
        bool isSummon = false)
    {
        ArgumentNullException.ThrowIfNull(mob);
        if (isSummon)
            return mob;
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));

        var score = mob.CurrentScore;
        var intelligence = (int)score.Intelligence;
        var constitution = (int)score.Constitution;
        var maxHp = score.MaxHp;
        var maxMp = score.MaxMp;
        var isMortal = classMaster == LegacyAccountSnapshot.ClassMasterMortal;
        var isKibitaSoul = isMortal && score.Level < 369;
        var hasSoulMultiplier = classMaster != LegacyAccountSnapshot.ClassMasterArch;

        for (var index = 0; index < AffectCount; index++)
        {
            if (affectSnapshot[index * AffectSizeInBytes + AffectTypeOffset] != SoulAffectType)
                continue;

            if (isMortal)
            {
                if (!isKibitaSoul)
                    ApplyMortalSoul(soul, ref intelligence, ref constitution, ref maxHp, ref maxMp);
            }
            else if (hasSoulMultiplier)
            {
                ApplyCelestialSoul(soul, ref intelligence, ref constitution, ref maxHp, ref maxMp);
            }
        }

        return mob with
        {
            CurrentScore = score with { MaxHp = maxHp, MaxMp = maxMp },
        };
    }

    /// <summary>
    /// Applies the late 7.69 Kibita Soul override after ordinary score modifiers.
    /// The source detects it from a Mortal Type-29 affect below level 369; this
    /// stage intentionally remains separate from the earlier Soul HP/MP pass.
    /// </summary>
    public static LegacyMobCombatState ApplyKibitaSoulStage(
        LegacyMobCombatState mob,
        ReadOnlySpan<byte> affectSnapshot,
        int classMaster,
        bool isSummon = false)
    {
        ArgumentNullException.ThrowIfNull(mob);
        if (isSummon)
            return mob;
        if (affectSnapshot.Length < AffectCount * AffectSizeInBytes)
            throw new ArgumentException("A legacy affect snapshot must contain 32 eight-byte records.", nameof(affectSnapshot));
        if (classMaster != LegacyAccountSnapshot.ClassMasterMortal || mob.CurrentScore.Level >= 369)
            return mob;

        var hasSoulAffect = false;
        for (var index = 0; index < AffectCount; index++)
        {
            if (affectSnapshot[index * AffectSizeInBytes + AffectTypeOffset] == SoulAffectType)
            {
                hasSoulAffect = true;
                break;
            }
        }

        if (!hasSoulAffect)
            return mob;

        var score = mob.CurrentScore with
        {
            Strength = 2000,
            Intelligence = 2000,
            MaxHp = 10000,
        };
        var itemInt = score.Intelligence - mob.BaseScore.Intelligence > 2000
            ? 2000
            : mob.BaseScore.Intelligence;
        var maxMp = unchecked(score.MaxMp + itemInt * 2);

        return mob with { CurrentScore = score with { MaxMp = maxMp } };
    }

    private static void ApplyVigorAffect(ref int maxHp, ref int maxMp)
    {
        var hpAddition = unchecked(maxHp * 15 / 100);
        maxHp = hpAddition >= TargetMaximumHp
            ? TargetMaximumHp
            : unchecked(maxHp + hpAddition);

        var mpAddition = unchecked(maxMp * 7 / 100);
        maxMp = mpAddition >= TargetMaximumMp
            ? TargetMaximumMp
            : unchecked(maxMp + mpAddition);
    }

    private static void ApplyManaToHealthAffect(ref int maxHp, ref int maxMp)
    {
        var mana = maxMp / 2;
        maxHp = unchecked(maxHp + mana);
        maxMp = unchecked(maxMp - mana);
    }

    private static void ApplySoulSwapAffect(
        ref int maxHp,
        ref int maxMp,
        int currentLevel,
        int classMaster,
        int affectLevel)
    {
        var effectiveLevel = classMaster is LegacyAccountSnapshot.ClassMasterArch or LegacyAccountSnapshot.ClassMasterMortal
            ? currentLevel
            : unchecked(currentLevel + TargetMaximumLevel);
        var mana = unchecked(maxMp / 5 + (effectiveLevel + affectLevel) / 2);
        maxHp = unchecked(maxHp + mana);
        maxMp = unchecked(maxMp - mana);
    }

    private static void ApplyType4Affect(
        ref int maxHp,
        ref int maxMp,
        ref int damage,
        int sourceMobMagic,
        ref int magic)
    {
        var hpAddition = unchecked(maxHp * 15 / 100);
        maxHp = hpAddition >= TargetMaximumType4Hp
            ? TargetMaximumType4Hp
            : unchecked(maxHp + hpAddition);

        var mpAddition = unchecked(maxMp * 10 / 100);
        maxMp = mpAddition >= TargetMaximumMp
            ? TargetMaximumMp
            : unchecked(maxMp + mpAddition);

        var damageAddition = unchecked(damage * 30 / 100);
        damage = damageAddition >= TargetMaximumDamage
            ? TargetMaximumDamage
            : unchecked(damage + damageAddition);

        var magicAddition = unchecked(sourceMobMagic * 20 / 100);
        magic = magicAddition >= TargetMaximumMagic
            ? TargetMaximumMagic
            : unchecked(magic + magicAddition);
    }

    private static void ApplyDivinaAffect(
        ref int maxHp,
        ref int maxMp,
        ref int damage,
        int sourceMobMagic,
        ref int magic)
    {
        var totalHp = unchecked(maxHp * 20 / 100);
        maxHp = totalHp >= TargetMaximumHp
            ? TargetMaximumHp
            : unchecked(maxHp + totalHp);

        var totalMp = unchecked(maxMp * 20 / 100);
        maxMp = totalMp >= TargetMaximumMp
            ? TargetMaximumMp
            : unchecked(maxMp + totalMp);

        var totalDamage = unchecked(damage * 10 / 100);
        damage = totalDamage >= TargetMaximumDamage
            ? TargetMaximumDamage
            : unchecked(damage + totalDamage);

        var totalMagic = unchecked(sourceMobMagic * 10 / 100);
        magic = totalMagic >= TargetMaximumMagic
            ? TargetMaximumMagic
            : unchecked(magic + totalMagic);
    }

    private static void ApplyPvpJewelAffect(
        ushort level,
        int sourceMobMagic,
        ref int maxHp,
        ref int maxMp,
        ref int armorClass,
        ref int damage,
        ref int magic,
        ref int learnedSkills,
        ref ushort rsvFlagsToAdd,
        bool includeArmorClass)
    {
        if ((level & (1 << 2)) != 0)
            rsvFlagsToAdd |= PvpJewelVisionRsvFlag;

        if ((level & (1 << 4)) != 0)
        {
            var hpAddition = unchecked(maxHp * 10 / 100);
            var armorClassAddition = unchecked(armorClass * 10 / 100);
            if (maxHp >= TargetMaximumHp)
                hpAddition = 0;

            maxHp = unchecked(maxHp + hpAddition);
            if (includeArmorClass)
                armorClass = unchecked(armorClass + armorClassAddition);
        }

        if ((level & (1 << 5)) != 0)
        {
            var hpAddition = unchecked(maxHp * 10 / 100);
            var damageAddition = unchecked(damage * 20 / 100);
            var magicAddition = unchecked(sourceMobMagic * 5 / 100);
            if (maxHp >= TargetMaximumHp)
                hpAddition = 0;

            maxHp = unchecked(maxHp + hpAddition);
            damage = damageAddition >= TargetMaximumDamage
                ? TargetMaximumDamage
                : unchecked(damage + damageAddition);
            magic = magicAddition >= TargetMaximumMagic
                ? TargetMaximumMagic
                : unchecked(magic + magicAddition);
        }

        if ((level & (1 << 6)) != 0)
            learnedSkills |= PvpJewelPrecisionLearnedSkillFlag;

        if ((level & (1 << 7)) != 0)
        {
            var mana = maxMp / 2;
            var totalHp = unchecked(maxHp + mana);
            maxHp = totalHp >= TargetMaximumHp
                ? TargetMaximumHp
                : unchecked(maxHp + mana);
            maxMp = mana >= TargetMaximumMp ? TargetMaximumMp : mana;
        }
    }

    private static void ApplyMortalSoul(byte soul, ref int intelligence, ref int constitution, ref int maxHp, ref int maxMp)
    {
        switch ((LegacySoulKind)soul)
        {
            case LegacySoulKind.Intelligence:
                intelligence = Scale(intelligence, 1.8f);
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.Constitution:
                constitution = Scale(constitution, 1.8f);
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.FoemaIntelligence:
                intelligence = Scale(intelligence, 1.4f);
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.FoemaConstitution:
                constitution = Scale(constitution, 1.4f);
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.IntelligenceFoema:
            case LegacySoulKind.IntelligenceDexterity:
                intelligence = Scale(intelligence, 1.6f);
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.IntelligenceConstitution:
                intelligence = Scale(intelligence, 1.6f);
                constitution = Scale(constitution, 1.4f);
                maxMp += intelligence * 2;
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.DexterityIntelligencePair:
                intelligence = Scale(intelligence, 1.4f);
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.DexterityConstitution:
                constitution = Scale(constitution, 1.4f);
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.ConstitutionFoema:
            case LegacySoulKind.ConstitutionDexterity:
                constitution = Scale(constitution, 1.6f);
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.ConstitutionIntelligence:
                constitution = Scale(constitution, 1.6f);
                intelligence = Scale(intelligence, 1.4f);
                maxHp += constitution * 2;
                maxMp += intelligence * 2;
                break;
        }
    }

    private static void ApplyCelestialSoul(byte soul, ref int intelligence, ref int constitution, ref int maxHp, ref int maxMp)
    {
        switch ((LegacySoulKind)soul)
        {
            case LegacySoulKind.Intelligence:
                intelligence = Scale(intelligence, 2.2f);
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.Constitution:
                constitution = Scale(constitution, 2.2f);
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.FoemaIntelligence:
                intelligence = Scale(intelligence, 1.4f);
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.FoemaConstitution:
                constitution = Scale(constitution, 1.4f);
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.IntelligenceFoema:
            case LegacySoulKind.IntelligenceDexterity:
                intelligence = Scale(intelligence, 1.8f);
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.IntelligenceConstitution:
                intelligence = Scale(intelligence, 1.8f);
                constitution = Scale(constitution, 1.4f);
                maxMp += intelligence * 2;
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.DexterityIntelligencePair:
                intelligence = Scale(intelligence, 1.4f);
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.DexterityConstitution:
                constitution = Scale(constitution, 1.4f);
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.ConstitutionFoema:
                constitution = Scale(constitution, 1.8f);
                maxHp += constitution * 2;
                break;
            case LegacySoulKind.ConstitutionIntelligence:
                constitution = Scale(constitution, 1.8f);
                intelligence = Scale(intelligence, 1.4f);
                maxHp += constitution * 2;
                maxMp += intelligence * 2;
                break;
            case LegacySoulKind.ConstitutionDexterity:
                constitution = Scale(constitution, 1.8f);
                maxHp += constitution * 2;
                break;
        }
    }

    private static int Scale(int value, float multiplier) => (int)(value * multiplier);

    // Numeric values follow SOUL_* in the selected 7.69 Basedef.h.
    private enum LegacySoulKind : byte
    {
        FoemaIntelligence = 6,
        FoemaConstitution = 8,
        Intelligence = 3,
        Constitution = 5,
        IntelligenceFoema = 9,
        IntelligenceDexterity = 10,
        IntelligenceConstitution = 11,
        DexterityIntelligencePair = 13,
        DexterityConstitution = 14,
        ConstitutionFoema = 15,
        ConstitutionIntelligence = 16,
        ConstitutionDexterity = 17,
    }
}
