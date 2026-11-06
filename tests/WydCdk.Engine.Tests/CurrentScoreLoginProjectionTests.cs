using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WydCdk.Protocol;
using WydCdk.World;

internal static class CurrentScoreLoginProjectionTests
{
    private const string FixtureResource = "WydCdk.Engine.Tests.Fixtures.V769.current-score-login-projection.golden.json";
    private const int W2ppRegenHpOffset = 802;

    public static void Run()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(FixtureResource)
            ?? throw new InvalidOperationException($"Missing embedded fixture: {FixtureResource}.");
        using var fixture = JsonDocument.Parse(stream);
        var itemData = CreateNeutralItemData();

        foreach (var vector in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input = CreateInput(vector);
            var originalMob = input.Mob.ToArray();
            var originalShortSkill = input.ShortSkill.ToArray();
            var originalAffect = input.Affect.ToArray();
            var originalMobExtra = input.MobExtra.ToArray();
            var actual = Project(input, itemData);

            Check(input.Mob.AsSpan().SequenceEqual(originalMob), $"Vector '{vector.GetProperty("name").GetString()}' mutated its source MOB.");
            Check(input.ShortSkill.AsSpan().SequenceEqual(originalShortSkill), $"Vector '{vector.GetProperty("name").GetString()}' mutated its source ShortSkill.");
            Check(input.Affect.AsSpan().SequenceEqual(originalAffect), $"Vector '{vector.GetProperty("name").GetString()}' mutated its source Affect.");
            Check(input.MobExtra.AsSpan().SequenceEqual(originalMobExtra), $"Vector '{vector.GetProperty("name").GetString()}' mutated its source MOBEXTRA.");
            Check(!ReferenceEquals(actual.Mob, input.Mob) && !ReferenceEquals(actual.Affect, input.Affect) && !ReferenceEquals(actual.MobExtra, input.MobExtra),
                $"Vector '{vector.GetProperty("name").GetString()}' did not return copied account buffers.");

            AssertExpected(vector, actual);
            var repeated = Project(input, itemData);
            Check(actual.Mob.AsSpan().SequenceEqual(repeated.Mob), $"Vector '{vector.GetProperty("name").GetString()}' was not deterministic.");
            Check(actual.ClientEquipment is null || repeated.ClientEquipment is not null && actual.ClientEquipment.SequenceEqual(repeated.ClientEquipment),
                $"Vector '{vector.GetProperty("name").GetString()}' changed equipment nondeterministically.");
        }

        RejectsMalformedAndUnrepresentableInput(fixture.RootElement.GetProperty("cases")[0], itemData);
        FrozenBladeProjectionUsesRawAttackSpeedAndHeadGate(fixture.RootElement.GetProperty("cases")[0], itemData);
        TormentShieldPassiveAddsOneSeventhOfSecondaryShieldArmorClass(fixture.RootElement.GetProperty("cases")[0]);
    }

    private static void FrozenBladeProjectionUsesRawAttackSpeedAndHeadGate(JsonElement baseline, LegacyItemDataTable itemData)
    {
        var source = CreateInput(baseline);
        var mob = source.Mob.ToArray();
        var baseScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)) with { AttackRun = 0xA6 };
        baseScore.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyItem(60, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var affect = source.Affect.ToArray();
        affect[0] = 7;
        affect[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 50);

        var projected = Project(source with { Mob = mob, Affect = affect }, itemData);
        var score = LegacyScore.Read(projected.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(score.AttackRun == 0x96 && score.Intelligence == -5,
            $"Type 7 was not composed before final AttackRun packing and the head-gated Intelligence delta; AttackRun=0x{score.AttackRun:X2}, Int={score.Intelligence}.");

        new LegacyItem(50, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var lowHead = Project(source with { Mob = mob, Affect = affect }, itemData);
        var lowHeadScore = LegacyScore.Read(lowHead.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(lowHeadScore.AttackRun == 0x96 && lowHeadScore.Intelligence == 20,
            $"Type 7 changed Intelligence at the exact head index 50 boundary; AttackRun=0x{lowHeadScore.AttackRun:X2}, Int={lowHeadScore.Intelligence}.");
    }

    private static void TormentShieldPassiveAddsOneSeventhOfSecondaryShieldArmorClass(JsonElement baseline)
    {
        const uint learnedSkillFlag = 1u << 19;
        var shieldData = CreateTormentShieldItemData(position: 128);
        var unlearnedAc = ProjectedArmorClass(baseline, shieldData, characterClass: 2, learnedSkill: 0);
        var learnedAc = ProjectedArmorClass(baseline, shieldData, characterClass: 2, learnedSkill: learnedSkillFlag);
        Check(learnedAc == unlearnedAc + 4,
            $"Beastmaster Escudo do Tormento did not add (offhand AC 28 + 1) / 7: learned AC={learnedAc}, unlearned AC={unlearnedAc}.");

        var otherClassWithoutSkillAc = ProjectedArmorClass(baseline, shieldData, characterClass: 0, learnedSkill: 0);
        var otherClassWithSkillAc = ProjectedArmorClass(baseline, shieldData, characterClass: 0, learnedSkill: learnedSkillFlag);
        Check(otherClassWithSkillAc == otherClassWithoutSkillAc,
            "Escudo do Tormento changed armor class outside the Beastmaster class.");

        var wrongSlotData = CreateTormentShieldItemData(position: 64);
        var wrongSlotWithoutSkillAc = ProjectedArmorClass(baseline, wrongSlotData, characterClass: 2, learnedSkill: 0);
        var wrongSlotWithSkillAc = ProjectedArmorClass(baseline, wrongSlotData, characterClass: 2, learnedSkill: learnedSkillFlag);
        Check(wrongSlotWithSkillAc == wrongSlotWithoutSkillAc,
            "Escudo do Tormento changed armor class when the secondary item was not a shield.");
    }

    private static int ProjectedArmorClass(JsonElement baseline, LegacyItemDataTable itemData, int characterClass, uint learnedSkill)
    {
        var source = CreateInput(baseline);
        var mob = source.Mob.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobClassOffset), checked((short)characterClass));
        BinaryPrimitives.WriteUInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), learnedSkill);
        new LegacyItem(100, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (7 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        var projected = Project(source with { Mob = mob }, itemData);
        return LegacyScore.Read(projected.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes)).Ac;
    }

    private static void RejectsMalformedAndUnrepresentableInput(JsonElement baseline, LegacyItemDataTable itemData)
    {
        var source = CreateInput(baseline);
        ExpectFailure(source with { Mob = new byte[LegacyAccountSnapshot.CharacterStride - 1] }, itemData,
            typeof(ArgumentException), "MOB");
        ExpectFailure(source with { Affect = new byte[LegacyAccountSnapshot.AffectStride - 1] }, itemData,
            typeof(ArgumentException), "Affect");
        ExpectFailure(source with { MobExtra = new byte[LegacyAccountSnapshot.MobExtraStride - 1] }, itemData,
            typeof(ArgumentException), "MOBEXTRA");
        ExpectFailure(source with { ClientEquipment = new LegacyItem[CharacterMobV769.EquipmentCount - 1] }, itemData,
            typeof(ArgumentException), "equipment");

        var overflowMob = source.Mob.ToArray();
        new LegacyItem(100, 0, 0, 0, 0, 0, 0).Write(overflowMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var overflowInput = source with { Mob = overflowMob };
        var projectionFailure = typeof(LegacyCharacterLoginData).Assembly.GetType("WydCdk.World.LegacyCurrentScoreProjectionException")
            ?? throw new InvalidOperationException("LegacyCurrentScoreProjectionException is missing.");
        ExpectFailure(overflowInput, CreateCriticalOverflowItemData(), projectionFailure, "Critical");
    }

    private static void ExpectFailure(
        LegacyCharacterLoginData input,
        LegacyItemDataTable itemData,
        Type expectedException,
        string expectedMessage)
    {
        var mob = input.Mob.ToArray();
        var shortSkill = input.ShortSkill.ToArray();
        var affect = input.Affect.ToArray();
        var mobExtra = input.MobExtra.ToArray();
        var failedAsExpected = false;
        try
        {
            _ = Project(input, itemData);
        }
        catch (Exception exception) when (expectedException.IsInstanceOfType(exception) && exception.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase))
        {
            failedAsExpected = true;
            Check(input.Mob.AsSpan().SequenceEqual(mob) && input.ShortSkill.AsSpan().SequenceEqual(shortSkill)
                && input.Affect.AsSpan().SequenceEqual(affect) && input.MobExtra.AsSpan().SequenceEqual(mobExtra),
                "A rejected CurrentScore projection mutated an input buffer.");
        }
        Check(failedAsExpected, $"CurrentScore projection did not reject invalid input with {expectedException.Name} containing '{expectedMessage}'.");
    }

    private static LegacyCharacterLoginData Project(LegacyCharacterLoginData input, LegacyItemDataTable itemData)
    {
        var projectorType = typeof(LegacyCharacterLoginData).Assembly.GetType("WydCdk.World.LegacyCurrentScoreLoginProjector");
        if (projectorType is null)
            throw new InvalidOperationException("The terminal CurrentScore login projector has not been implemented yet.");
        var project = projectorType.GetMethod("Project", BindingFlags.Public | BindingFlags.Static, [typeof(LegacyCharacterLoginData), typeof(LegacyItemDataTable)])
            ?? throw new InvalidOperationException("LegacyCurrentScoreLoginProjector.Project(LegacyCharacterLoginData, LegacyItemDataTable) is missing.");
        try
        {
            return (LegacyCharacterLoginData)(project.Invoke(null, [input, itemData])
                ?? throw new InvalidOperationException("The CurrentScore projector returned null."));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static LegacyCharacterLoginData CreateInput(JsonElement vector)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        var head = ReadItem(vector.GetProperty("headItem"));
        head.Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobClassOffset), (short)vector.GetProperty("characterClass").GetInt32());
        mob[LegacyAccountSnapshot.MobMerchantOffset] = (byte)vector.GetProperty("merchant").GetInt32();
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), (ushort)vector.GetProperty("rsv").GetInt32());
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), vector.GetProperty("magic").GetInt32());
        mob[LegacyAccountSnapshot.MobCriticalOffset] = (byte)vector.GetProperty("critical").GetInt32();
        mob[LegacyAccountSnapshot.MobSaveManaOffset] = (byte)vector.GetProperty("saveMana").GetInt32();
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(W2ppRegenHpOffset), (ushort)vector.GetProperty("regenHp").GetInt32());
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRegenMpOffset), (ushort)vector.GetProperty("regenMp").GetInt32());
        var resist = vector.GetProperty("resist").EnumerateArray().Select(static item => item.GetByte()).ToArray();
        resist.CopyTo(mob, LegacyAccountSnapshot.MobResistOffset);
        ReadScore(vector.GetProperty("baseScore")).Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        ReadScore(vector.GetProperty("staleCurrentScore")).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteInt16LittleEndian(mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), (short)vector.GetProperty("classMaster").GetInt32());
        mobExtra[LegacyAccountSnapshot.MobExtraSoulOffset] = (byte)vector.GetProperty("soul").GetInt32();

        foreach (var record in vector.GetProperty("affects").EnumerateArray())
        {
            var offset = record.GetProperty("slot").GetInt32() * 8;
            affect[offset] = (byte)record.GetProperty("type").GetInt32();
            affect[offset + 1] = (byte)record.GetProperty("value").GetInt32();
            BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(offset + 2), (ushort)record.GetProperty("level").GetInt32());
        }

        return new LegacyCharacterLoginData(
            mob,
            new byte[16],
            affect,
            mobExtra,
            0,
            2100,
            2100);
    }

    private static void AssertExpected(JsonElement vector, LegacyCharacterLoginData actual)
    {
        var expected = vector.GetProperty("expected");
        var name = vector.GetProperty("name").GetString() ?? "unnamed";
        var score = LegacyScore.Read(actual.Mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes));
        Check(score.Level == expected.GetProperty("level").GetInt32(), "Projected CurrentScore.Level differed from the golden vector.");
        Check(score.Ac == expected.GetProperty("ac").GetInt32(),
            $"Vector '{name}' projected CurrentScore.Ac={score.Ac}; expected {expected.GetProperty("ac").GetInt32()}.");
        Check(score.Damage == expected.GetProperty("damage").GetInt32(), "Projected CurrentScore.Damage differed from the golden vector.");
        Check(score.Merchant == expected.GetProperty("merchant").GetByte(), "Projected CurrentScore.Merchant differed from the golden vector.");
        Check(score.AttackRun == expected.GetProperty("attackRun").GetByte(), "Projected CurrentScore.AttackRun differed from the golden vector.");
        Check(score.MaxHp == expected.GetProperty("maxHp").GetInt32() && score.MaxMp == expected.GetProperty("maxMp").GetInt32(), "Projected maxima differed from the golden vector.");
        Check(score.Hp == expected.GetProperty("hp").GetInt32() && score.Mp == expected.GetProperty("mp").GetInt32(), "Projected current HP/MP differed from the golden vector.");
        Check(score.Strength == expected.GetProperty("strength").GetInt16() && score.Intelligence == expected.GetProperty("intelligence").GetInt16()
            && score.Dexterity == expected.GetProperty("dexterity").GetInt16() && score.Constitution == expected.GetProperty("constitution").GetInt16(),
            "Projected attributes differed from the golden vector.");
        Check(score.Special1 == expected.GetProperty("special1").GetInt16() && score.Special2 == expected.GetProperty("special2").GetInt16()
            && score.Special3 == expected.GetProperty("special3").GetInt16() && score.Special4 == expected.GetProperty("special4").GetInt16(),
            "Projected specials differed from the golden vector.");
        var actualMagic = BinaryPrimitives.ReadInt32LittleEndian(actual.Mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset));
        Check(actualMagic == expected.GetProperty("magic").GetInt32(), $"Vector '{name}' projected Magic={actualMagic}; expected {expected.GetProperty("magic").GetInt32()}.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(actual.Mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset)) == expected.GetProperty("rsv").GetUInt16(), "Projected RSV differed from the golden vector.");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(actual.Mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset)) == expected.GetProperty("learnedSkill").GetUInt32(), "Projected LearnedSkill differed from the golden vector.");
        Check(actual.Mob[LegacyAccountSnapshot.MobCriticalOffset] == expected.GetProperty("critical").GetByte(), "Projected Critical differed from the golden vector.");
        Check(actual.Mob[LegacyAccountSnapshot.MobSaveManaOffset] == expected.GetProperty("saveMana").GetByte(), "Projected SaveMana differed from the golden vector.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(actual.Mob.AsSpan(W2ppRegenHpOffset)) == expected.GetProperty("regenHp").GetUInt16(), "Projected RegenHP differed from the golden vector.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(actual.Mob.AsSpan(LegacyAccountSnapshot.MobRegenMpOffset)) == expected.GetProperty("regenMp").GetUInt16(), "Projected RegenMP differed from the golden vector.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(actual.Mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset)) == expected.GetProperty("headIndex").GetInt16(), "Projected head equipment differed from the golden vector.");
        var actualResist = actual.Mob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).ToArray();
        var expectedResist = expected.GetProperty("resist").EnumerateArray().Select(static item => item.GetByte()).ToArray();
        Check(actualResist.SequenceEqual(expectedResist), $"Vector '{name}' projected resistance=[{string.Join(',', actualResist)}]; expected [{string.Join(',', expectedResist)}].");
    }

    private static LegacyScore ReadScore(JsonElement value) => new(
        value.GetProperty("level").GetInt32(), value.GetProperty("ac").GetInt32(), value.GetProperty("damage").GetInt32(),
        value.GetProperty("merchant").GetByte(), value.GetProperty("attackRun").GetByte(), value.GetProperty("direction").GetByte(), value.GetProperty("chaosRate").GetByte(),
        value.GetProperty("maxHp").GetInt32(), value.GetProperty("maxMp").GetInt32(), value.GetProperty("hp").GetInt32(), value.GetProperty("mp").GetInt32(),
        value.GetProperty("strength").GetInt16(), value.GetProperty("intelligence").GetInt16(), value.GetProperty("dexterity").GetInt16(), value.GetProperty("constitution").GetInt16(),
        value.GetProperty("special1").GetInt16(), value.GetProperty("special2").GetInt16(), value.GetProperty("special3").GetInt16(), value.GetProperty("special4").GetInt16());

    private static LegacyItem ReadItem(JsonElement value) => new(
        (short)value.GetProperty("index").GetInt32(), (byte)value.GetProperty("effect1").GetInt32(), (byte)value.GetProperty("value1").GetInt32(),
        (byte)value.GetProperty("effect2").GetInt32(), (byte)value.GetProperty("value2").GetInt32(), (byte)value.GetProperty("effect3").GetInt32(), (byte)value.GetProperty("value3").GetInt32());

    private static LegacyItemDataTable CreateNeutralItemData() => LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    private static LegacyItemDataTable CreateCriticalOverflowItemData()
    {
        const int itemIndex = 100;
        const int staticEffectsOffset = 80;
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        WriteXoredInt16(encoded, (itemIndex * LegacyItemDataTable.RecordSizeInBytes) + staticEffectsOffset, LegacyItemEffect.Critical);
        WriteXoredInt16(encoded, (itemIndex * LegacyItemDataTable.RecordSizeInBytes) + staticEffectsOffset + sizeof(short), 512);
        return LegacyItemDataTable.Load(encoded);
    }

    private static LegacyItemDataTable CreateTormentShieldItemData(int position)
    {
        const int itemIndex = 100;
        const int staticEffectsOffset = 80;
        const int positionOffset = 136;
        var encoded = Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray();
        var itemOffset = itemIndex * LegacyItemDataTable.RecordSizeInBytes;
        WriteXoredInt16(encoded, itemOffset + staticEffectsOffset, LegacyItemEffect.Ac);
        WriteXoredInt16(encoded, itemOffset + staticEffectsOffset + sizeof(short), 28);
        WriteXoredInt32(encoded, itemOffset + positionOffset, position);
        return LegacyItemDataTable.Load(encoded);
    }

    private static void WriteXoredInt32(Span<byte> destination, int offset, int value)
    {
        Span<byte> decoded = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(decoded, value);
        for (var index = 0; index < decoded.Length; index++)
            destination[offset + index] = (byte)(decoded[index] ^ 0x5A);
    }

    private static void WriteXoredInt16(Span<byte> destination, int offset, int value)
    {
        Span<byte> decoded = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(decoded, checked((short)value));
        destination[offset] = (byte)(decoded[0] ^ 0x5A);
        destination[offset + 1] = (byte)(decoded[1] ^ 0x5A);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
