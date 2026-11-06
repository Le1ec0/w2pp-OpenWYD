using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>Applies the legacy Battle Royale identity mask to a client 7.69 CreateMob frame.</summary>
public static class LegacyBattleRoyaleCreateMobVisualRelay
{
    public const short MinX = 2_604;
    public const short MaxX = 2_648;
    public const short MinY = 1_708;
    public const short MaxY = 1_744;
    private const int HiddenEquipmentSlot = 15;
    private static readonly byte[] HiddenName = Encoding.ASCII.GetBytes("??????");

    public static byte[] Apply(byte[] frame, int battleRoyaleState, LegacyFrameCodec codec)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(codec);
        if (battleRoyaleState == 0)
            return frame;

        var decoded = codec.Decode(frame);
        if (!decoded.IsChecksumValid || decoded.Header.Type != CreateMobConfirmationV769.MessageType ||
            decoded.Payload.Length != CreateMobConfirmationV769.PayloadSize)
            return frame;

        var payload = decoded.Payload.ToArray();
        var x = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(CreateMobConfirmationV769.PositionXOffset));
        var y = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(CreateMobConfirmationV769.PositionYOffset));
        if (x < MinX || x > MaxX || y < MinY || y > MaxY)
            return frame;

        payload.AsSpan(CreateMobConfirmationV769.MobNameOffset, CreateMobConfirmationV769.MobNameLength).Clear();
        HiddenName.CopyTo(payload.AsSpan(CreateMobConfirmationV769.MobNameOffset));
        payload.AsSpan(
            CreateMobConfirmationV769.EquipmentOffset + (HiddenEquipmentSlot * sizeof(ushort)),
            sizeof(ushort)).Clear();
        payload.AsSpan(CreateMobConfirmationV769.GuildOffset, sizeof(ushort)).Clear();

        return codec.Encode(
            decoded.Header.Type,
            decoded.Header.Id,
            decoded.Header.ClientTick,
            payload,
            decoded.Header.KeywordIndex);
    }
}
