using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Builds the client 7.69 <c>MSG_CreateMobTrade</c> visual for an active
/// autotrade listing. The source behavior is GetCreateMobTrade from the
/// community 7.69 TMSrv, with the W2PP MOB snapshot projected into the
/// independent 7.69 wire DTO.
/// </summary>
public static class LegacyAutoTradeVisualRelay
{
    private const int AffectSizeInBytes = 8;
    private const byte GuiltyMaximum = 50;

    /// <summary>
    /// Projects the W2PP 16-slot equipment state into the client 7.69
    /// <c>MSG_UpdateEquip</c> DTO. The calculation is shared with the
    /// CreateMobTrade visual so item appearance and secondary codes do not
    /// diverge between a spawn and a later equipment refresh.
    /// </summary>
    public static EquipmentAppearanceV769 BuildEquipmentAppearance(ReadOnlySpan<byte> mob)
        => BuildEquipmentAppearance(mob, clientEquipment: null);

    public static EquipmentAppearanceV769 BuildEquipmentAppearance(ReadOnlySpan<byte> mob, IReadOnlyList<LegacyItem>? clientEquipment)
    {
        if (mob.Length < LegacyAccountSnapshot.MobEquipmentOffset +
            (LegacyCharacterSelection.EquipmentCount * LegacyItem.SizeInBytes))
            throw new ArgumentException("A complete W2PP equipment snapshot is required.", nameof(mob));
        if (clientEquipment is not null && clientEquipment.Count != CharacterMobV769.EquipmentCount)
            throw new ArgumentException($"The client 7.69 appearance requires exactly {CharacterMobV769.EquipmentCount} equipment entries.", nameof(clientEquipment));

        var visualEquipment = new ushort[CharacterMobV769.EquipmentCount];
        var secondaryAppearance = new byte[CharacterMobV769.EquipmentCount];
        for (var index = 0; index < CharacterMobV769.EquipmentCount; index++)
        {
            var item = clientEquipment is not null
                ? clientEquipment[index]
                : index < LegacyCharacterSelection.EquipmentCount
                    ? LegacyItem.Read(mob.Slice(LegacyAccountSnapshot.MobEquipmentOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes))
                    : default;
            var visualCode = LegacyVisualItemCode(item, index);

            if (index == 14 && visualCode is >= 2360 and < 2390)
            {
                if (item.Value1 == 0)
                    visualCode = 0;
                else
                    visualCode += Math.Clamp(item.Effect2 / 10, 0, 13) << 12;
            }

            visualEquipment[index] = checked((ushort)visualCode);
            secondaryAppearance[index] = unchecked((byte)LegacyVisualAnctCode(item));
        }

        return W2ppEquipmentAppearanceV1Adapter.Adapt(visualEquipment, secondaryAppearance);
    }

    public static bool TryBuild(
        LegacyAutoTradeSnapshot snapshot,
        ReadOnlySpan<byte> mob,
        ReadOnlySpan<byte> affect,
        LegacyFrameCodec codec,
        uint clientTick,
        byte keywordIndex,
        out LegacyAutoTradeVisualRelayPlan? plan,
        IReadOnlyList<LegacyItem>? clientEquipment = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(codec);
        plan = null;

        var titleValid = TryEncodeAscii(snapshot.Title, CreateMobTradeConfirmationV769.DescriptionLength, out var description);
        if (snapshot.ConnectionId <= 0 || snapshot.ConnectionId > ushort.MaxValue ||
            mob.Length < LegacyAccountSnapshot.CharacterStride ||
            affect.Length != LegacyAccountSnapshot.AffectStride ||
            !titleValid)
            return false;

        var normalizedMob = LegacyCharacterStorageDefaults.EnsureKillMark(mob);
        var markerOffset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
        var marker = normalizedMob.AsSpan(markerOffset, LegacyItem.SizeInBytes);

        var mobName = new byte[CreateMobTradeConfirmationV769.MobNameLength];
        normalizedMob.AsSpan(LegacyAccountSnapshot.MobNameOffset, mobName.Length).CopyTo(mobName);
        var guilty = marker[4];
        mobName[12] = guilty is > 0 and <= GuiltyMaximum ? (byte)0 : marker[2];
        mobName[13] = marker[3];
        mobName[14] = marker[5];
        mobName[15] = marker[7];

        if (clientEquipment is not null && clientEquipment.Count != CharacterMobV769.EquipmentCount)
            return false;

        var visualEquipment = new ushort[CreateMobTradeConfirmationV769.EquipmentCount];
        var secondaryAppearance = new byte[CreateMobTradeConfirmationV769.SecondaryAppearanceLength];
        for (var index = 0; index < CreateMobTradeConfirmationV769.EquipmentCount; index++)
        {
            var item = clientEquipment is not null
                ? clientEquipment[index]
                : index < LegacyCharacterSelection.EquipmentCount
                    ? LegacyItem.Read(normalizedMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (index * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes))
                    : default;
            var visualCode = LegacyVisualItemCode(item, index);

            // GetCreateMobTrade hides a dead mount and applies the mount
            // sanctuary visual exactly after BASE_VisualItemCode.
            if (index == 14 && visualCode is >= 2360 and < 2390)
            {
                if (item.Value1 == 0)
                    visualCode = 0;
                else
                {
                    var sanctuary = Math.Clamp(item.Effect2 / 10, 0, 13);
                    visualCode += sanctuary << 12;
                }
            }

            visualEquipment[index] = checked((ushort)visualCode);
            secondaryAppearance[index] = unchecked((byte)LegacyVisualAnctCode(item));
        }

        var affects = new ushort[CreateMobTradeConfirmationV769.AffectCount];
        for (var index = 0; index < affects.Length; index++)
        {
            var affectOffset = index * AffectSizeInBytes;
            var type = affect[affectOffset];
            var time = BinaryPrimitives.ReadUInt32LittleEndian(affect.Slice(affectOffset + 4, sizeof(uint)));
            affects[index] = (ushort)(((ushort)type << 8) | (ushort)Math.Min(time, byte.MaxValue));
        }

        var guildLevel = normalizedMob[LegacyAccountSnapshot.MobGuildLevelOffset];
        ushort createType = 0;
        if (guildLevel == 9) createType |= 0x80;
        if (guildLevel != 0) createType |= 0x40;
        var score = AdaptScore(LegacyScore.Read(normalizedMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes)));
        var nick = new byte[CreateMobTradeConfirmationV769.NickLength];

        var confirmation = new CreateMobTradeConfirmationV769(
            snapshot.PositionX,
            snapshot.PositionY,
            checked((ushort)snapshot.ConnectionId),
            mobName,
            visualEquipment,
            affects,
            BinaryPrimitives.ReadUInt16LittleEndian(normalizedMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)),
            guildLevel,
            score,
            createType,
            secondaryAppearance,
            nick,
            description,
            server: 0);

        plan = new LegacyAutoTradeVisualRelayPlan(
            snapshot.ConnectionId,
            confirmation.ToFrame(codec, clientTick, keywordIndex));
        return true;
    }

    private static ClientScoreV769 AdaptScore(LegacyScore score) => new(
        checked((short)score.Level),
        score.Ac,
        score.Damage,
        Reserved: 0,
        score.AttackRun,
        score.MaxHp,
        score.MaxMp,
        score.Hp,
        score.Mp,
        score.Strength,
        score.Intelligence,
        score.Dexterity,
        score.Constitution,
        unchecked((ushort)score.Special1),
        unchecked((ushort)score.Special2),
        unchecked((ushort)score.Special3),
        unchecked((ushort)score.Special4));

    private static int LegacyVisualItemCode(LegacyItem item, int equipmentSlot)
    {
        if (item.Index <= 0)
            return 0;

        var value = 0;
        if (equipmentSlot == 14)
        {
            // The reference's impossible sIndex range is preserved; mount
            // sanctuary is added by GetCreateMobTrade below.
            return (ushort)item.Index | (value * 0x1000);
        }

        if (item.Effect1 == 43)
            value = item.Value1;
        else if (item.Effect2 == 43)
            value = item.Value2;
        else if (item.Effect3 == 43)
            value = item.Value3;
        else if (HasEffectInRange(item, 116, 125))
            return (ushort)item.Index | (12 * 0x1000);
        else
            return item.Index;

        value = value < 230 ? value % 10
            : value < 234 ? 10
            : value < 238 ? 11
            : value < 242 ? 12
            : value < 246 ? 13
            : value < 250 ? 14
            : value < 254 ? 15
            : 16;
        return (ushort)item.Index | (value * 0x1000);
    }

    private static int LegacyVisualAnctCode(LegacyItem item)
    {
        var value = 0;
        if (item.Index is >= 2360 and <= 2390 && item.Value1 > 0)
        {
            return item.Value3 switch
            {
                11 => 0x10B,
                12 => 0x10C,
                13 => 0x10D,
                14 => 0x10E,
                15 => 0x10F,
                16 => 0x110,
                17 => 0x111,
                18 => 0x112,
                19 => 0x113,
                20 => 0x114,
                35 when item.Index is 2363 or 2377 => 0x115,
                _ => 0,
            };
        }

        if (item.Effect1 == 43)
            value = item.Value1;
        else if (item.Effect2 == 43)
            value = item.Value2;
        else if (item.Effect3 == 43)
            value = item.Value3;

        var sanctifiedEffect = FirstEffectInRange(item, 116, 125);
        if (sanctifiedEffect != 0)
            return sanctifiedEffect - 3;
        if (value == 0)
            return 0;
        if (value < 230)
            return 43;

        var remainder = value % 4;
        return remainder switch
        {
            0 => 0x30,
            1 => 0x40,
            2 => 0x10,
            _ => 0x20,
        };
    }

    private static bool HasEffectInRange(LegacyItem item, byte minimum, byte maximum) =>
        item.Effect1 >= minimum && item.Effect1 <= maximum ||
        item.Effect2 >= minimum && item.Effect2 <= maximum ||
        item.Effect3 >= minimum && item.Effect3 <= maximum;

    private static byte FirstEffectInRange(LegacyItem item, byte minimum, byte maximum)
    {
        if (item.Effect1 >= minimum && item.Effect1 <= maximum) return item.Effect1;
        if (item.Effect2 >= minimum && item.Effect2 <= maximum) return item.Effect2;
        if (item.Effect3 >= minimum && item.Effect3 <= maximum) return item.Effect3;
        return 0;
    }

    private static bool TryEncodeAscii(string value, int capacity, out byte[] encoded)
    {
        encoded = [];
        if (value.Any(static character => character == '\0' || character > 0x7F))
            return false;

        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length > capacity - 2)
            return false;
        encoded = new byte[capacity];
        bytes.CopyTo(encoded, 0);
        return true;
    }
}

public sealed record LegacyAutoTradeVisualRelayPlan(int ShopConnectionId, byte[] Frame);
