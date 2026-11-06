using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class TinyCombineRelayTests
{
    public static void Run()
    {
        TinyChanceSettings();

        Assert(ServerOptions.Parse([]).TinyChanceBase == 15 &&
            ServerOptions.Parse(["--tiny-chance-base", "0"]).TinyChanceBase == 0 &&
            ServerOptions.Parse(["--tiny-chance-base", "100"]).TinyChanceBase == 100,
            "Tiny CHANCEBASE did not preserve the 7.69 default and configured range.");
        AssertThrows<ArgumentException>(() => ServerOptions.Parse(["--tiny-chance-base", "101"]),
            "An out-of-range Tiny CHANCEBASE was accepted.");

        var codec = LegacyFrameCodec.CreateDefault();
        var etcFrame = codec.Encode(0x7777, 1, 10, ReadOnlySpan<byte>.Empty, 0);
        var target = new LegacyItem(100, 116, 7, 1, 2, 3, 4);
        var success = CreateOutcome(
            succeeded: true,
            [
                new(2, default), new(3, default), new(5, default),
                new(1, default), new(2, default), new(0, target),
            ]);
        var failure = CreateOutcome(
            succeeded: false,
            [new(2, default), new(3, default), new(5, default), new(2, default)]);

        if (!LegacyTinyCombineRelay.TryBuild(success, etcFrame, codec, 41, 17, 7,
                "Processo de combinação foi concluído.", "Combinação de item falhou.", out var successFrames) || successFrames is null)
            throw new InvalidOperationException("The success relay rejected a valid Tiny outcome.");
        Assert(Types(codec, successFrames).SequenceEqual(new ushort[]
            { 0x0182, 0x0182, 0x0182, 0x0182, 0x0182, 0x7777, 0x0101, 0xC0A7, 0x0182 }),
            "Tiny success did not send item updates, Etc, message, completion, and target in 7.69 order.");
        Assert(Slots(codec, successFrames.Take(5)).SequenceEqual(new short[] { 2, 3, 5, 1, 2 }) &&
            Slots(codec, successFrames.Skip(8)).SequenceEqual(new short[] { 0 }) &&
            BinaryPrimitives.ReadInt32LittleEndian(codec.Decode(successFrames[7]).Payload.Span) == 1,
            "Tiny success changed its repeated slot updates, final target slot, or completion value.");

        if (!LegacyTinyCombineRelay.TryBuild(failure, etcFrame, codec, 42, 18, 7,
                "Processo de combinação foi concluído.", "Combinação de item falhou.", out var failureFrames) || failureFrames is null)
            throw new InvalidOperationException("The failure relay rejected a valid Tiny outcome.");
        Assert(Types(codec, failureFrames).SequenceEqual(new ushort[]
            { 0x0182, 0x0182, 0x0182, 0x0182, 0x0101, 0xC0A7, 0x7777 }) &&
            Slots(codec, failureFrames.Take(4)).SequenceEqual(new short[] { 2, 3, 5, 2 }) &&
            BinaryPrimitives.ReadInt32LittleEndian(codec.Decode(failureFrames[5]).Payload.Span) == 2,
            "Tiny failure did not send the repeated slot, message, completion, and Etc in 7.69 order.");
    }

    private static void TinyChanceSettings()
    {
        var parsed = LegacyTinyChanceConfiguration.ParseCompRate(
        [
            "Ehre Pacote_Ori 100",
            "",
            "Tiny ChanceBase 21",
            "tiny CHANCEBASE 33",
        ]);
        Assert(parsed == 33, "CompRate Tiny ChanceBase parsing did not accept the shipped casing or last value.");
        Assert(LegacyTinyChanceConfiguration.ParseCompRate(["Shany ChanceBase 20"]) == 15,
            "CompRate parsing changed the 7.69 default when Tiny ChanceBase was absent.");
        Assert(LegacyTinyChanceConfiguration.ResolveBase(null, parsed) == 33 &&
            LegacyTinyChanceConfiguration.ResolveBase(7, parsed) == 7,
            "The explicit command-line Tiny override did not take precedence over CompRate.txt.");
        AssertThrows<InvalidDataException>(() => LegacyTinyChanceConfiguration.ParseCompRate(["Tiny ChanceBase 101"]),
            "Out-of-range CompRate Tiny ChanceBase was accepted.");
        AssertThrows<InvalidDataException>(() => LegacyTinyChanceConfiguration.ParseCompRate(["Tiny ChanceBase nope"]),
            "Malformed CompRate Tiny ChanceBase was accepted.");

        var options = ServerOptions.Parse(["--comp-rate", "Common/Settings/CompRate.txt"]);
        Assert(options.CompRatePath == Path.GetFullPath("Common/Settings/CompRate.txt") &&
            ServerOptions.Parse(["--tiny-chance-base", "7", "--comp-rate", "rate.txt"]).TinyChanceBase == 7,
            "Server options did not preserve the CompRate path and explicit Tiny override.");
    }

    private static LegacyTinyCombineOutcome CreateOutcome(bool succeeded, IReadOnlyList<LegacyTinyCombineItemUpdate> updates)
    {
        var positions = updates.Select(static update => update.Position).ToArray();
        return new LegacyTinyCombineOutcome(
            7, succeeded, succeeded ? 15 : 100, succeeded ? 15 : 85, 15,
            100_000_000, 0, positions, updates,
            new byte[LegacyAccountSnapshot.CharacterStride],
            new byte[LegacyAccountSnapshot.CharacterStride]);
    }

    private static IEnumerable<ushort> Types(LegacyFrameCodec codec, IEnumerable<byte[]> frames) =>
        frames.Select(frame => codec.Decode(frame).Header.Type);

    private static IEnumerable<short> Slots(LegacyFrameCodec codec, IEnumerable<byte[]> frames) =>
        frames.Select(frame => BinaryPrimitives.ReadInt16LittleEndian(codec.Decode(frame).Payload.Span.Slice(2, sizeof(short))));

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
