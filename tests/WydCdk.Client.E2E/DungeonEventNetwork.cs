using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class DungeonEventNetwork
{
    public static async Task RunAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        using var targetClient = new TcpClient();
        await targetClient.ConnectAsync(endpoint.Address, endpoint.Port);
        using var targetServer = await listener.AcceptTcpClientAsync();
        using var edgeClient = new TcpClient();
        await edgeClient.ConnectAsync(endpoint.Address, endpoint.Port);
        using var edgeServer = await listener.AcceptTcpClientAsync();
        using var outsideClient = new TcpClient();
        await outsideClient.ConnectAsync(endpoint.Address, endpoint.Port);
        using var outsideServer = await listener.AcceptTcpClientAsync();
        CheckLoopback(targetClient, targetServer);
        CheckLoopback(edgeClient, edgeServer);
        CheckLoopback(outsideClient, outsideServer);

        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[9]
            MinuteGenerate: 3800
            MaxNumMob: 20
            MinGroup: 0
            MaxGroup: 0
            Leader: EVENT_LEADER
            Follower: EVENT_FOLLOWER@
            """,
            new Dictionary<string, byte[]>
            {
                ["EVENT_LEADER"] = new byte[LegacyAccountSnapshot.CharacterStride],
                ["EVENT_FOLLOWER@"] = new byte[LegacyAccountSnapshot.CharacterStride],
            });
        var itemData = CreateNeutralItemData();
        var world = new WorldHub(itemData: itemData, npcGenerationCatalog: catalog);
        var targetWrites = new List<int>();
        var edgeWrites = new List<int>();
        var outsideWrites = new List<int>();
        AddPeer(world, 1, "DUNGEON_TARGET", 352, 3780, targetServer.GetStream(), targetWrites);
        AddPeer(world, 2, "DUNGEON_EDGE", 368, 3796, edgeServer.GetStream(), edgeWrites);
        AddPeer(world, 3, "DUNGEON_OUTSIDE", 369, 3796, outsideServer.GetStream(), outsideWrites);

        var codec = LegacyFrameCodec.CreateDefault();
        var runtime = new LegacyNpcGenerationRuntime(
            world,
            catalog,
            static (_, _) => ValueTask.CompletedTask,
            static maximum => maximum switch { 1000 => 12, 30 => 2, 5 => 0, 10 => 7, 4 => 3, _ => 0 },
            initialMinuteCounter: 9,
            itemData: itemData,
            relayDungeonItem: (spawn, token) => LegacyGroundItemRelay.RelaySpawnAsync(
                world, spawn, codec, token, clientTick: 17, keywordIndex: 19));

        var tick = await runtime.TickMinuteAsync();
        Check(tick.DungeonEventDrops.Count == 5, "The scheduled dungeon event did not create five loopback prizes.");
        Check(targetWrites.Count == 5 && edgeWrites.Count == 1 && outsideWrites.Count == 0,
            $"Captured-view writes differ: target=[{string.Join(',', targetWrites)}], edge=[{string.Join(',', edgeWrites)}], outside=[{string.Join(',', outsideWrites)}].");
        await AssertCreateItemFramesAsync(targetClient.GetStream(), codec, tick.DungeonEventDrops, "target");
        await AssertCreateItemFramesAsync(edgeClient.GetStream(), codec, tick.DungeonEventDrops.Take(1).ToArray(), "inclusive 33x33 edge peer");
        await AssertNoFrameAsync(edgeClient.GetStream());
        await AssertNoFrameAsync(outsideClient.GetStream());

        Console.WriteLine("DUNGEON_EVENT_SMOKE_PASS loopback=verified items=5 item=696 create-item=relayed view-scoped=true");
    }

    private static void AddPeer(WorldHub world, int id, string name, short x, short y, NetworkStream stream, List<int> itemWrites)
    {
        Check(world.Enter(id, name, (frame, token) =>
        {
            itemWrites.Add(frame.Length);
            return stream.WriteAsync(frame, token);
        }),
            $"Could not register dungeon-event loopback peer {id}.");
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(100, 10, 1000, 0, 0, 0, 0, 5000, 1000, 5000, 1000, 0, 0, 0, 0, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.SetCharacterState(id, 0, 0, 1, 0, 0, x, y, mob),
            $"Could not set dungeon-event loopback peer {id}'s position.");
    }

    private static async Task AssertCreateItemFramesAsync(
        NetworkStream stream,
        LegacyFrameCodec codec,
        IReadOnlyList<LegacyGroundItemSpawn> expectedDrops,
        string peerName)
    {
        for (var index = 0; index < expectedDrops.Count; index++)
        {
            var frame = await ReadFrameAsync(stream, codec);
            var payload = frame.Payload.Span;
            Check(frame.IsChecksumValid && frame.Header.Type == CreateItemConfirmation.MessageType &&
                  frame.Header.Size == CreateItemConfirmation.PacketSize && payload.Length == CreateItemConfirmation.PayloadSize,
                $"The {peerName} received an invalid dungeon prize frame.");
            var gridX = BinaryPrimitives.ReadUInt16LittleEndian(payload);
            var gridY = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..]);
            var wireItemId = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]);
            var item = LegacyItem.Read(payload.Slice(6, LegacyItem.SizeInBytes));
            var rotate = payload[6 + LegacyItem.SizeInBytes];
            var state = payload[7 + LegacyItem.SizeInBytes];
            var height = payload[8 + LegacyItem.SizeInBytes];
            var create = payload[9 + LegacyItem.SizeInBytes];
            var expected = expectedDrops[index];
            Check(gridX == expected.Item.PositionX && gridY == expected.Item.PositionY &&
                  wireItemId == expected.WireItemId && item.Index == expected.Item.Item.Index &&
                  rotate == expected.Item.Rotate && state == 1 && height == 0 && create == 0,
                $"The {peerName} received unexpected CreateItem fields: grid=({gridX},{gridY}), id={wireItemId}, item={item.Index}, rotate={rotate}, state={state}, height={height}, create={create}.");
        }
    }

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, timeout.Token);
        var header = PacketHeader.Read(headerBytes);
        Check(header.Size >= PacketHeader.SizeInBytes && header.Size <= PacketHeader.MaxPacketSize,
            $"Loopback produced invalid frame size {header.Size}.");
        var frame = new byte[header.Size];
        headerBytes.CopyTo(frame, 0);
        await stream.ReadExactlyAsync(frame.AsMemory(PacketHeader.SizeInBytes), timeout.Token);
        return codec.Decode(frame);
    }

    private static async Task AssertNoFrameAsync(NetworkStream stream)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var buffer = new byte[1];
        try
        {
            var read = await stream.ReadAsync(buffer, timeout.Token);
            Check(read == 0, "A loopback peer outside the captured dungeon item view received bytes.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
        }
    }

    private static void CheckLoopback(TcpClient client, TcpClient accepted)
    {
        Check(client.Client.LocalEndPoint is IPEndPoint local && IPAddress.IsLoopback(local.Address) &&
              client.Client.RemoteEndPoint is IPEndPoint remote && IPAddress.IsLoopback(remote.Address) &&
              accepted.Client.LocalEndPoint is IPEndPoint acceptedLocal && IPAddress.IsLoopback(acceptedLocal.Address) &&
              accepted.Client.RemoteEndPoint is IPEndPoint acceptedRemote && IPAddress.IsLoopback(acceptedRemote.Address),
            "A dungeon-event network smoke endpoint was not loopback-only.");
    }

    private static LegacyItemDataTable CreateNeutralItemData() =>
        LegacyItemDataTable.Load(Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
