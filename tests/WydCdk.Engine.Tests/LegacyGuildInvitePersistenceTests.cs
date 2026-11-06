using System.Buffers.Binary;
using System.Text;
using WydCdk.World;

internal static class LegacyGuildInvitePersistenceTests
{
    public static async Task SameAccountPairIsAtomicAndConflictDoesNotWrite()
    {
        var root = Path.Combine(Path.GetTempPath(), "wyd-guild-invite-pair-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "A"));
        var path = Path.Combine(root, "A", "ALICE");
        var before = CreateAccountBlob("SOURCE", 77, 1_000, "TARGET", 0, 2_000);
        await File.WriteAllBytesAsync(path, before);
        try
        {
            var store = new LegacyFileAccountStore(root);
            var request = CreateRequest("ALICE", 0, "ALICE", 1, 77, 1_000, 500, 0, 2_000, 2_000);
            Assert(await store.TrySaveGuildInvitePairAsync(request) == GuildInvitePairSaveResult.Success,
                "Same-account guild invite pair was not persisted atomically.");

            var after = await File.ReadAllBytesAsync(path);
            Assert(ReadGuild(after, 0) == 77 && ReadCoin(after, 0) == 500, "Source guild/Gold fields were not updated.");
            Assert(ReadGuild(after, 1) == 77 && ReadCoin(after, 1) == 2_000, "Target guild/Gold fields were not updated.");
            Assert(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 123_456,
                "Pair persistence changed the account bank balance.");
            Assert(OnlyGuildAndCoinFieldsChanged(before, after, [0, 1]),
                "Pair persistence changed unrelated account bytes.");

            var committed = after.ToArray();
            var stale = CreateRequest("ALICE", 0, "ALICE", 1, 77, 1_000, 500, 0, 2_000, 2_000);
            Assert(await store.TrySaveGuildInvitePairAsync(stale) == GuildInvitePairSaveResult.Conflict,
                "Stale same-account pair did not return Conflict.");
            Assert((await File.ReadAllBytesAsync(path)).SequenceEqual(committed), "Conflict wrote account bytes.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public static async Task DistinctAccountPairUpdatesAtomically()
    {
        var root = Path.Combine(Path.GetTempPath(), "wyd-guild-invite-pair-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "A"));
        Directory.CreateDirectory(Path.Combine(root, "B"));
        var firstPath = Path.Combine(root, "A", "ALICE");
        var secondPath = Path.Combine(root, "B", "BOB");
        var firstBefore = CreateAccountBlob("SOURCE", 77, 1_000, "", 0, 0);
        var secondBefore = CreateAccountBlob("TARGET", 0, 2_000, "", 0, 0);
        await File.WriteAllBytesAsync(firstPath, firstBefore);
        await File.WriteAllBytesAsync(secondPath, secondBefore);
        try
        {
            var store = new LegacyFileAccountStore(root);
            var request = CreateRequest("ALICE", 0, "BOB", 0, 77, 1_000, 500, 0, 2_000, 2_000);
            Assert(await store.TrySaveGuildInvitePairAsync(request) == GuildInvitePairSaveResult.Success,
                "Distinct-account guild invite pair was not persisted atomically.");
            var firstAfter = await File.ReadAllBytesAsync(firstPath);
            var secondAfter = await File.ReadAllBytesAsync(secondPath);
            Assert(ReadCoin(firstAfter, 0) == 500 && ReadGuild(secondAfter, 0) == 77,
                "Distinct-account pair fields were not updated.");
            Assert(BinaryPrimitives.ReadInt32LittleEndian(firstAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 123_456 &&
                   BinaryPrimitives.ReadInt32LittleEndian(secondAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 123_456,
                "Distinct-account pair changed bank balances.");
            Assert(OnlyGuildAndCoinFieldsChanged(firstBefore, firstAfter, [0]) && OnlyGuildAndCoinFieldsChanged(secondBefore, secondAfter, [0]),
                "Distinct-account pair changed unrelated bytes.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static GuildInvitePairSaveRequest CreateRequest(
        string sourceAccount,
        int sourceSlot,
        string targetAccount,
        int targetSlot,
        int guildId,
        int sourceExpectedCoin,
        int sourceNewCoin,
        int targetExpectedGuild,
        int targetExpectedCoin,
        int targetNewCoin) => new(
            new GuildInviteCharacterUpdate(sourceAccount, sourceSlot, guildId, sourceExpectedCoin, guildId, sourceNewCoin),
            new GuildInviteCharacterUpdate(targetAccount, targetSlot, targetExpectedGuild, targetExpectedCoin, guildId, targetNewCoin));

    private static byte[] CreateAccountBlob(
        string firstName,
        int firstGuild,
        int firstCoin,
        string secondName,
        int secondGuild,
        int secondCoin)
    {
        var blob = new byte[LegacyAccountSnapshot.RequiredFileLength];
        BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 123_456);
        WriteCharacter(blob, 0, firstName, firstGuild, firstCoin);
        if (secondName.Length != 0)
            WriteCharacter(blob, 1, secondName, secondGuild, secondCoin);
        return blob;
    }

    private static void WriteCharacter(byte[] blob, int slot, string name, int guild, int coin)
    {
        var offset = LegacyAccountSnapshot.CharactersOffset + slot * LegacyAccountSnapshot.CharacterStride;
        Encoding.ASCII.GetBytes(name).CopyTo(blob, offset + LegacyAccountSnapshot.MobNameOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(blob.AsSpan(offset + LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guild));
        BinaryPrimitives.WriteInt32LittleEndian(blob.AsSpan(offset + LegacyAccountSnapshot.MobCoinOffset), coin);
    }

    private static int ReadGuild(byte[] blob, int slot) => BinaryPrimitives.ReadUInt16LittleEndian(
        blob.AsSpan(LegacyAccountSnapshot.CharactersOffset + slot * LegacyAccountSnapshot.CharacterStride + LegacyAccountSnapshot.MobGuildOffset));

    private static int ReadCoin(byte[] blob, int slot) => BinaryPrimitives.ReadInt32LittleEndian(
        blob.AsSpan(LegacyAccountSnapshot.CharactersOffset + slot * LegacyAccountSnapshot.CharacterStride + LegacyAccountSnapshot.MobCoinOffset));

    private static bool OnlyGuildAndCoinFieldsChanged(byte[] before, byte[] after, int[] slots)
    {
        var allowed = new HashSet<int>();
        foreach (var slot in slots)
        {
            var offset = LegacyAccountSnapshot.CharactersOffset + slot * LegacyAccountSnapshot.CharacterStride;
            for (var index = 0; index < sizeof(ushort); index++) allowed.Add(offset + LegacyAccountSnapshot.MobGuildOffset + index);
            for (var index = 0; index < sizeof(int); index++) allowed.Add(offset + LegacyAccountSnapshot.MobCoinOffset + index);
        }
        for (var index = 0; index < before.Length; index++)
        {
            if (!allowed.Contains(index) && before[index] != after[index]) return false;
        }
        return true;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
