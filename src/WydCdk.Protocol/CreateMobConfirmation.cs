using System.Buffers.Binary;
using System.Text;

namespace WydCdk.Protocol;

/// <summary>Minimal player-presence snapshot matching legacy <c>MSG_CreateMob</c>.</summary>
public sealed class CreateMobConfirmation
{
    public const ushort MessageType = 0x0364; // 100 | FLAG_GAME2CLIENT | FLAG_CLIENT2GAME
    public const ushort SceneId = 30000;
    public const int PacketSize = 232;
    private const int NameLength = 16;
    private const int EquipmentCount = 16;
    private const int AffectCount = 32;
    private const int ScoreSize = 48;
    private const int MobCarryOffset = 268;
    private const int KillMarkSlot = 63;
    private const int KillMarkOffset = MobCarryOffset + (KillMarkSlot * LegacyItem.SizeInBytes);
    private readonly short positionX;
    private readonly short positionY;
    private readonly ushort mobId;
    private readonly byte[] mob;
    private readonly byte[]? affect;
    private readonly ushort createType;

    public CreateMobConfirmation(ushort mobId, short positionX, short positionY, ReadOnlySpan<byte> mob)
        : this(mobId, positionX, positionY, mob, ReadOnlySpan<byte>.Empty, npc: false)
    {
    }

    public CreateMobConfirmation(ushort mobId, short positionX, short positionY, ReadOnlySpan<byte> mob, ReadOnlySpan<byte> affect, bool npc, bool summon = false)
    {
        if (mob.Length < LegacyAccountMobSize) throw new ArgumentException("A STRUCT_MOB snapshot is required.", nameof(mob));
        if (!affect.IsEmpty && affect.Length != 256) throw new ArgumentException("A legacy affect snapshot must contain 256 bytes.", nameof(affect));
        this.mobId = mobId;
        this.positionX = positionX;
        this.positionY = positionY;
        this.mob = mob[..LegacyAccountMobSize].ToArray();
        this.affect = affect.IsEmpty ? null : affect.ToArray();
        this.createType = npc ? (summon ? (ushort)3 : (ushort)2) : (ushort)0;
    }

    public byte[] ToPayload()
    {
        var payload = new byte[PacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteInt16LittleEndian(payload, positionX);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(2), positionY);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), mobId);
        mob.AsSpan(0, NameLength).CopyTo(payload.AsSpan(6, NameLength));

        // GetCreateMob overwrites the last four name bytes with the player
        // metadata consumed by TMHuman: chaos/CP, current kills and total
        // kills.  Without this, other clients interpret trailing zeroes as
        // chaos level zero and render the name as permanently negative.
        if (createType == 0 && mobId > 0 && mobId < 1000)
        {
            payload[6 + 12] = mob[KillMarkOffset + 2];
            payload[6 + 13] = mob[KillMarkOffset + 3];
            payload[6 + 14] = mob[KillMarkOffset + 5];
            payload[6 + 15] = mob[KillMarkOffset + 7];
        }

        for (var index = 0; index < EquipmentCount; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(22 + (index * 2)), BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(140 + (index * LegacyItem.SizeInBytes))));

        if (affect is not null)
        {
            for (var index = 0; index < AffectCount; index++)
            {
                var affectOffset = index * 8;
                var type = affect[affectOffset];
                var time = BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(affectOffset + 4));
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(54 + (index * 2)), (ushort)(((ushort)type << 8) | (ushort)(time & 0xFF)));
            }
        }

        // MSG_CreateMob offsets: Equip=22, Affect=54, Guild=118, Unknow=121, Score=124.
        mob.AsSpan(18, 2).CopyTo(payload.AsSpan(118, 2)); // Guild
        mob.AsSpan(92, ScoreSize).CopyTo(payload.AsSpan(124, ScoreSize)); // CurrentScore
        if (createType != 0)
        {
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(128), 0); // GetCreateMob forces NPC AC to zero for Clan 4.
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(172), createType); // NPC=2; GenerateSummon=3.
        }
        return payload;
    }

    public byte[] ToFrame(LegacyFrameCodec codec, uint clientTick, byte keywordIndex)
    {
        ArgumentNullException.ThrowIfNull(codec);
        return codec.Encode(MessageType, SceneId, clientTick, ToPayload(), keywordIndex);
    }

    private const int LegacyAccountMobSize = 816;
}
