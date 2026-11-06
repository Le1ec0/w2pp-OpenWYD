using System.Buffers.Binary;
using System.Text.Json;
using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyNpcCargoSalePersistenceTests
{
    public static async Task FileCasChangesOnlyCargoAndSellerMobCoin()
    {
        var fixture = new Fixture();
        try
        {
            var store = new LegacyFileAccountStore(fixture.Root);
            var request = fixture.CreateRequest(expectedCoin: 100, newCoin: 900);
            Assert(await store.TryCommitNpcCargoSaleAsync(request) == LegacyNpcCargoSaleCommitResult.Success,
                "Valid NPC Cargo CAS did not commit.");
            var after = await File.ReadAllBytesAsync(fixture.AccountPath);
            Assert(after.AsSpan(LegacyAccountSnapshot.CargoOffset, request.NewCargo.Length).SequenceEqual(request.NewCargo),
                "Cargo CAS did not write the complete new Cargo array.");
            Assert(ReadCoin(after, 0) == 900, "Cargo CAS did not update the selling MOB.Coin.");
            Assert(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 246_810,
                "Cargo CAS changed the account bank balance.");
            Assert(OnlyCargoAndSellerCoinChanged(fixture.Before, after),
                "Cargo CAS changed unrelated bytes in the account blob.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    public static async Task FileCasConflictDoesNotWrite()
    {
        var fixture = new Fixture();
        try
        {
            var store = new LegacyFileAccountStore(fixture.Root);
            var stale = fixture.CreateRequest(expectedCoin: 99, newCoin: 900);
            Assert(await store.TryCommitNpcCargoSaleAsync(stale) == LegacyNpcCargoSaleCommitResult.Conflict,
                "Stale carried Gold did not return Conflict.");
            Assert((await File.ReadAllBytesAsync(fixture.AccountPath)).SequenceEqual(fixture.Before),
                "Conflicting Cargo CAS changed account bytes.");

            var staleCargo = fixture.CreateRequest(expectedCoin: 100, newCoin: 900);
            staleCargo.ExpectedCargo[0] ^= 0xFF;
            Assert(await store.TryCommitNpcCargoSaleAsync(staleCargo) == LegacyNpcCargoSaleCommitResult.Conflict,
                "Stale Cargo bytes did not return Conflict.");
            Assert((await File.ReadAllBytesAsync(fixture.AccountPath)).SequenceEqual(fixture.Before),
                "Cargo mismatch changed account bytes.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    public static async Task FileJournalRecoversPreparedAndCommittedCargoCas()
    {
        var fixture = new Fixture();
        try
        {
            var preparedAfter = fixture.Before.ToArray();
            fixture.NewCargo.CopyTo(preparedAfter, LegacyAccountSnapshot.CargoOffset);
            BinaryPrimitives.WriteInt32LittleEndian(
                preparedAfter.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset), 900);
            await WriteJournalAsync(fixture, "Prepared", preparedAfter);
            await File.WriteAllBytesAsync(fixture.AccountPath, preparedAfter);
            var store = new LegacyFileAccountStore(fixture.Root);
            var recoveredPrepared = await store.ReadSnapshotAsync("ALICE");
            Assert(recoveredPrepared is not null && (await File.ReadAllBytesAsync(fixture.AccountPath)).SequenceEqual(fixture.Before),
                "Prepared Cargo journal did not roll the account blob back to its before image.");

            var committedAfter = fixture.Before.ToArray();
            fixture.NewCargo.CopyTo(committedAfter, LegacyAccountSnapshot.CargoOffset);
            BinaryPrimitives.WriteInt32LittleEndian(
                committedAfter.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset), 900);
            await WriteJournalAsync(fixture, "Committed", committedAfter);
            await File.WriteAllBytesAsync(fixture.AccountPath, fixture.Before);
            var recoveredCommitted = await store.ReadSnapshotAsync("ALICE");
            Assert(recoveredCommitted is not null && (await File.ReadAllBytesAsync(fixture.AccountPath)).SequenceEqual(committedAfter),
                "Committed Cargo journal did not replay its after image.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static async Task WriteJournalAsync(Fixture fixture, string phase, byte[] after)
    {
        var document = new
        {
            Version = 3,
            Phase = phase,
            Files = new[] { new { Path = fixture.AccountPath, Before = fixture.Before, After = after } },
        };
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, ".wyd-cdk-atomic-state.journal"),
            JsonSerializer.Serialize(document));
    }

    private static int ReadCoin(byte[] blob, int slot) => BinaryPrimitives.ReadInt32LittleEndian(
        blob.AsSpan(LegacyAccountSnapshot.CharactersOffset + slot * LegacyAccountSnapshot.CharacterStride + LegacyAccountSnapshot.MobCoinOffset));

    private static bool OnlyCargoAndSellerCoinChanged(byte[] before, byte[] after)
    {
        for (var index = 0; index < before.Length; index++)
        {
            var inCargo = index >= LegacyAccountSnapshot.CargoOffset &&
                index < LegacyAccountSnapshot.CargoOffset + LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes;
            var inSellerCoin = index >= LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset &&
                index < LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset + sizeof(int);
            if (!inCargo && !inSellerCoin && before[index] != after[index])
                return false;
        }
        return true;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "wyd-npc-cargo-cas-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "A"));
            AccountPath = Path.Combine(Root, "A", "ALICE");
            Before = new byte[LegacyAccountSnapshot.RequiredFileLength];
            "ALICE"u8.CopyTo(Before.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobNameOffset));
            BinaryPrimitives.WriteInt32LittleEndian(
                Before.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset), 100);
            BinaryPrimitives.WriteInt32LittleEndian(Before.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 246_810);
            for (var index = 0; index < LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes; index++)
                Before[LegacyAccountSnapshot.CargoOffset + index] = (byte)(index % 251);
            NewCargo = Before.AsSpan(LegacyAccountSnapshot.CargoOffset,
                LegacyAccountSnapshot.CargoCount * LegacyItem.SizeInBytes).ToArray();
            NewCargo[0] ^= 0x55;
            NewCargo[^1] ^= 0xAA;
            File.WriteAllBytes(AccountPath, Before);
        }

        public string Root { get; }
        public string AccountPath { get; }
        public byte[] Before { get; }
        public byte[] NewCargo { get; }

        public LegacyNpcCargoSaleCommitRequest CreateRequest(int expectedCoin, int newCoin) => new(
            "ALICE",
            0,
            Before.AsSpan(LegacyAccountSnapshot.CargoOffset, NewCargo.Length).ToArray(),
            NewCargo.ToArray(),
            expectedCoin,
            newCoin);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
