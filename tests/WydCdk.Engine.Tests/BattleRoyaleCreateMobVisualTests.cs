using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class BattleRoyaleCreateMobVisualTests
{
    public static void WorldCreateMobMasksOnlyActiveArenaPlayers()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        foreach (var testCase in new[]
        {
            (State: 1, Item: 700, X: (short)2_604, Y: (short)1_708, ExpectedMasked: true),
            (State: 2, Item: 700, X: (short)2_621, Y: (short)1_726, ExpectedMasked: true),
            (State: 3, Item: 700, X: (short)2_648, Y: (short)1_744, ExpectedMasked: true),
            (State: 1, Item: 0, X: (short)2_621, Y: (short)1_726, ExpectedMasked: true),
            (State: 0, Item: 700, X: (short)2_621, Y: (short)1_726, ExpectedMasked: false),
            (State: 2, Item: 700, X: (short)2_603, Y: (short)1_726, ExpectedMasked: false),
            (State: 2, Item: 700, X: (short)2_648, Y: (short)1_745, ExpectedMasked: false),
        })
        {
            var world = new WorldHub();
            AddPlayer(world, 1, "BR_VIEWER", testCase.X, testCase.Y, guildId: 1);
            var mob = CreateMob("BR_PLAYER_SECRET");
            BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), 77);
            new LegacyItem(1_234, 0, 0, 0, 0, 0, 0).Write(
                mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var originalMob = mob.ToArray();
            AddPlayer(world, 2, "BR_PLAYER_SECRET", testCase.X, testCase.Y, guildId: 77, mob);
            world.SetBattleRoyaleState(testCase.State, testCase.Item);

            Check(world.TryBuildCreateMobFrame(2, codec, 33, 16, out var frame) && frame is not null,
                "The Battle Royale CreateMob fixture could not build its player frame.");
            var decoded = codec.Decode(frame!);
            var payload = decoded.Payload.Span;
            var name = payload.Slice(CreateMobConfirmationV769.MobNameOffset, 16);
            var nameMatches = testCase.ExpectedMasked
                ? name.SequenceEqual(new byte[] { 63, 63, 63, 63, 63, 63, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })
                : name[..9].SequenceEqual(Encoding.ASCII.GetBytes("BR_PLAYER"));
            var visualEquipment = BinaryPrimitives.ReadUInt16LittleEndian(
                payload[(CreateMobConfirmationV769.EquipmentOffset + (15 * sizeof(ushort)))..]);
            var guild = BinaryPrimitives.ReadUInt16LittleEndian(payload[CreateMobConfirmationV769.GuildOffset..]);
            Check(decoded.IsChecksumValid && nameMatches &&
                visualEquipment == (testCase.ExpectedMasked ? 0 : 1_234) && guild == (testCase.ExpectedMasked ? 0 : 77),
                $"Battle Royale CreateMob visual mask did not match state={testCase.State}, item={testCase.Item}, position=({testCase.X},{testCase.Y}).");
            Check(mob.SequenceEqual(originalMob),
                "Building a masked Battle Royale CreateMob frame mutated the caller's authoritative MOB snapshot.");

            if (testCase.ExpectedMasked)
            {
                var targetSnapshot = world.GetBattleRoyaleGridRefreshSnapshots(2_604, 1_708, 2_648, 1_744)
                    .SelectMany(static recipient => recipient.Mobs)
                    .Single(snapshot => snapshot.ConnectionId == 2);
                Check(BinaryPrimitives.ReadUInt16LittleEndian(targetSnapshot.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) == 77 &&
                    LegacyItem.Read(targetSnapshot.MobSnapshot.AsSpan(
                        LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes)).Index == 1_234,
                    "Battle Royale CreateMob masking changed the authoritative WorldHub guild or equipment snapshot.");
            }
        }
    }

    private static void AddPlayer(WorldHub world, int connectionId, string name, short x, short y, int guildId, byte[]? mob = null)
    {
        mob ??= CreateMob(name);
        Check(world.Enter(connectionId, name, static (_, _) => ValueTask.CompletedTask),
            $"Battle Royale CreateMob participant {connectionId} could not be registered.");
        Check(world.SetCharacterState(connectionId, 0, guildId, 1, 0, 0, x, y, mob),
            $"Battle Royale CreateMob participant {connectionId} state could not be initialized.");
    }

    private static byte[] CreateMob(string name)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob, 0);
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 1_000, 1_000, 1_000, 250, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
