using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using WydCdk.Protocol;
using WydCdk.World;

internal static class LegacyCharacterCheckpointFileStoreTests
{
    private const string AccountName = "SANDBOX";
    private const int DailyQuestOffset = 7_872;
    private const int DailyQuestLength = 56;
    private const int AccountDonateOffset = 7_808;
    private const string SidecarRootName = ".wyd-client-equipment-v1";
    private const string JournalName = ".wyd-cdk-atomic-state.journal";

    public static void Run()
    {
        CheckpointUpdatesLiveStateAndPreservesIndependentFields().GetAwaiter().GetResult();
        CharacterStateWriterPreservesBankCoin().GetAwaiter().GetResult();
        CharacterGuildWriterPreservesBankCoin().GetAwaiter().GetResult();
        AtomicTradeWriterPreservesBothBankCoins().GetAwaiter().GetResult();
        CheckpointValidatesAccountSlotLengthsAndCharacterState().GetAwaiter().GetResult();
        CheckpointPersistsEquipmentWithTheSameSnapshot().GetAwaiter().GetResult();
        CheckpointSerializesIndependentAccountWriters().GetAwaiter().GetResult();
        PreparedJournalRestoresAccountAndEquipmentBeforeState().GetAwaiter().GetResult();
        CommittedJournalReplaysAccountAndEquipmentAfterState().GetAwaiter().GetResult();
        VersionOneTwoAccountJournalRemainsReadable().GetAwaiter().GetResult();
    }

    private static async Task CheckpointUpdatesLiveStateAndPreservesIndependentFields()
    {
        using var fixture = new AccountFixture(AccountName);
        var before = await File.ReadAllBytesAsync(fixture.AccountPath);
        var request = CreateRequest();

        var result = await fixture.Store.TrySaveCharacterCheckpointAsync(request);

        Require(result == LegacyCharacterCheckpointSaveResult.Success, $"Expected checkpoint success, got {result}.");
        var after = await File.ReadAllBytesAsync(fixture.AccountPath);
        var expected = BuildExpectedAccount(before, request);
        Require(after.SequenceEqual(expected), "Checkpoint changed bytes outside the authoritative MOB, position, Extra, affects, and Coin fields.");
        Require(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 100,
            "Checkpoint replaced the account bank balance with the character's carried Gold.");
        Require(after.AsSpan(LegacyAccountSnapshot.CargoOffset, LegacyAccountSnapshot.AccountCoinOffset - LegacyAccountSnapshot.CargoOffset)
                .SequenceEqual(before.AsSpan(LegacyAccountSnapshot.CargoOffset, LegacyAccountSnapshot.AccountCoinOffset - LegacyAccountSnapshot.CargoOffset)),
            "Checkpoint overwrote Cargo while saving character state.");
        Require(after.AsSpan(LegacyAccountSnapshot.ShortSkillOffset, LegacyAccountSnapshot.CharacterCount * LegacyAccountSnapshot.ShortSkillStride)
                .SequenceEqual(before.AsSpan(LegacyAccountSnapshot.ShortSkillOffset, LegacyAccountSnapshot.CharacterCount * LegacyAccountSnapshot.ShortSkillStride)),
            "Checkpoint overwrote ShortSkill while saving character state.");
        Require(after.AsSpan(AccountDonateOffset, sizeof(int)).SequenceEqual(before.AsSpan(AccountDonateOffset, sizeof(int))),
            "Checkpoint overwrote the independently owned Donate value.");
        Require(after.AsSpan(DailyQuestOffset, DailyQuestLength).SequenceEqual(before.AsSpan(DailyQuestOffset, DailyQuestLength)),
            "Checkpoint overwrote the daily quest counters or other quest bytes.");

        var login = await fixture.Store.ReadCharacterLoginDataAsync(AccountName, 0);
        Require(login is not null && login.Mob.SequenceEqual(expected.AsSpan(LegacyAccountSnapshot.CharactersOffset, LegacyAccountSnapshot.CharacterStride).ToArray()),
            "Character login did not read back the checkpointed MOB.");
        Require(login!.MobExtra.SequenceEqual(request.MobExtra.ToArray()) && login.Affect.SequenceEqual(request.Affect.ToArray()),
            "Character login did not read back the checkpointed Extra and affects.");
    }

    private static async Task CharacterStateWriterPreservesBankCoin()
    {
        using var fixture = new AccountFixture(AccountName);
        var mob = CreateMob("HERO", 0x12345678);

        Require(await fixture.Store.TrySaveCharacterStateAsync(AccountName, 0, mob, 321, -123) == CharacterStateSaveResult.Success,
            "Character state writer failed before preserving the bank balance.");

        var after = await File.ReadAllBytesAsync(fixture.AccountPath);
        Require(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 100,
            "Character state writer replaced the bank balance with carried Gold.");
        Require(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset)) == 0x12345678,
            "Character state writer did not persist the character's carried Gold.");
    }

    private static async Task CharacterGuildWriterPreservesBankCoin()
    {
        using var fixture = new AccountFixture(AccountName);

        Require(await fixture.Store.TrySaveCharacterGuildAsync(AccountName, 0, 123, 7_777) == CharacterGuildSaveResult.Success,
            "Guild writer failed before preserving the bank balance.");

        var after = await File.ReadAllBytesAsync(fixture.AccountPath);
        Require(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 100,
            "Guild writer replaced the bank balance with carried Gold.");
        Require(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(LegacyAccountSnapshot.CharactersOffset + LegacyAccountSnapshot.MobCoinOffset)) == 7_777,
            "Guild writer did not persist the character's carried Gold.");
    }

    private static async Task AtomicTradeWriterPreservesBothBankCoins()
    {
        using var first = new AccountFixture(AccountName);
        using var second = new AccountFixture("OTHER", first.Root);
        var firstBefore = await File.ReadAllBytesAsync(first.AccountPath);
        var secondBefore = await File.ReadAllBytesAsync(second.AccountPath);
        BinaryPrimitives.WriteInt32LittleEndian(firstBefore.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 31_000);
        BinaryPrimitives.WriteInt32LittleEndian(secondBefore.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 42_000);
        await File.WriteAllBytesAsync(first.AccountPath, firstBefore);
        await File.WriteAllBytesAsync(second.AccountPath, secondBefore);

        var firstRequest = new LegacyCharacterStateSaveRequest(AccountName, 0, CreateMob("HERO", 111), 1, 2, new byte[LegacyAccountSnapshot.MobExtraStride]);
        var secondRequest = new LegacyCharacterStateSaveRequest("OTHER", 0, CreateMob("HERO", 222), 3, 4, new byte[LegacyAccountSnapshot.MobExtraStride]);
        var result = await first.Store.TrySaveCharacterStatesAtomicallyAsync(firstRequest, secondRequest);

        Require(result == AtomicCharacterStateSaveResult.Success, $"Atomic trade save failed with {result}.");
        var firstAfter = await File.ReadAllBytesAsync(first.AccountPath);
        var secondAfter = await File.ReadAllBytesAsync(second.AccountPath);
        Require(BinaryPrimitives.ReadInt32LittleEndian(firstAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 31_000 &&
                BinaryPrimitives.ReadInt32LittleEndian(secondAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset)) == 42_000,
            "Atomic trade save replaced one or both bank balances with carried Gold.");
    }

    private static async Task CheckpointValidatesAccountSlotLengthsAndCharacterState()
    {
        using var fixture = new AccountFixture(AccountName);
        var request = CreateRequest();

        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request with { AccountName = "MISSING" }) == LegacyCharacterCheckpointSaveResult.AccountNotFound,
            "Checkpoint did not report a missing account.");
        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request with { CharacterSlot = LegacyAccountSnapshot.CharacterCount }) == LegacyCharacterCheckpointSaveResult.SlotOutOfRange,
            "Checkpoint accepted a character slot beyond the account ABI.");
        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request with { Mob = new byte[LegacyAccountSnapshot.CharacterStride - 1] }) == LegacyCharacterCheckpointSaveResult.InvalidLength,
            "Checkpoint accepted a truncated MOB.");
        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request with { MobExtra = new byte[LegacyAccountSnapshot.MobExtraStride - 1] }) == LegacyCharacterCheckpointSaveResult.InvalidLength,
            "Checkpoint accepted a truncated MOBEXTRA.");
        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request with { Affect = new byte[LegacyAccountSnapshot.AffectStride - 1] }) == LegacyCharacterCheckpointSaveResult.InvalidLength,
            "Checkpoint accepted a truncated affect array.");
        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request with { ClientEquipment = new LegacyItem[ClientEquipmentStateV769.EquipmentCount - 1] }) == LegacyCharacterCheckpointSaveResult.InvalidLength,
            "Checkpoint accepted equipment without all eighteen client slots.");
        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request with { ClientEquipment = null! }) == LegacyCharacterCheckpointSaveResult.InvalidLength,
            "Checkpoint accepted a null client-equipment snapshot.");
        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request with { CharacterSlot = 1 }) == LegacyCharacterCheckpointSaveResult.CharacterNotAvailable,
            "Checkpoint accepted an empty character slot.");

        var accountBeforeShortFile = await File.ReadAllBytesAsync(fixture.AccountPath);
        await File.WriteAllBytesAsync(fixture.AccountPath, accountBeforeShortFile.AsSpan(0, 100).ToArray());
        Require(await fixture.Store.TrySaveCharacterCheckpointAsync(request) == LegacyCharacterCheckpointSaveResult.NotAvailable,
            "Checkpoint treated a truncated account file as available.");
    }

    private static async Task CheckpointPersistsEquipmentWithTheSameSnapshot()
    {
        using var fixture = new AccountFixture(AccountName);
        var request = CreateRequest();

        var result = await fixture.Store.TrySaveCharacterCheckpointAsync(request);
        var login = await fixture.Store.ReadCharacterLoginDataAsync(AccountName, 0);
        var equipment = await fixture.Store.ReadClientEquipmentAsync(AccountName, 0);
        var sidecarPath = GetSidecarPath(fixture.Root, AccountName, request.CharacterSlot);
        var rawSidecar = await File.ReadAllBytesAsync(sidecarPath);

        Require(result == LegacyCharacterCheckpointSaveResult.Success, "Composite checkpoint failed before writing equipment.");
        Require(equipment is not null && equipment.SequenceEqual(request.ClientEquipment), "The equipment reader did not return the checkpoint's eighteen-slot snapshot.");
        Require(login?.ClientEquipment is not null && login.ClientEquipment.SequenceEqual(request.ClientEquipment), "Character login combined the checkpoint with a different equipment state.");
        Require(rawSidecar.AsSpan(0, 8).SequenceEqual(Encoding.ASCII.GetBytes("WYDEQ769")) &&
                BinaryPrimitives.ReadInt32LittleEndian(rawSidecar.AsSpan(8, sizeof(int))) == 1 &&
                rawSidecar.AsSpan(12).SequenceEqual(new ClientEquipmentStateV769(request.ClientEquipment).ToBytes()),
            "The composite checkpoint did not persist a complete versioned 18-slot sidecar.");
    }

    private static async Task CheckpointSerializesIndependentAccountWriters()
    {
        using var fixture = new AccountFixture(AccountName);
        var request = CreateRequest();
        var before = await File.ReadAllBytesAsync(fixture.AccountPath);
        using var start = new ManualResetEventSlim(false);

        var checkpoint = Task.Run(async () =>
        {
            start.Wait();
            return await fixture.Store.TrySaveCharacterCheckpointAsync(request);
        });
        var donate = Task.Run(async () =>
        {
            start.Wait();
            return await fixture.Store.TrySaveDonateAsync(AccountName, 991);
        });

        start.Set();
        Require(await checkpoint == LegacyCharacterCheckpointSaveResult.Success,
            "Checkpoint failed while a Donate writer used the same account store.");
        Require(await donate == DonateBalanceSaveResult.Success,
            "Donate writer failed while a checkpoint used the same account store.");

        var after = await File.ReadAllBytesAsync(fixture.AccountPath);
        var expected = BuildExpectedAccount(before, request);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(AccountDonateOffset), 991);
        Require(after.SequenceEqual(expected),
            "Concurrent independent account writers lost either the checkpoint or Donate update.");
    }

    private static async Task PreparedJournalRestoresAccountAndEquipmentBeforeState()
    {
        using var fixture = new AccountFixture(AccountName);
        var beforeAccount = await File.ReadAllBytesAsync(fixture.AccountPath);
        var request = CreateRequest();
        var afterAccount = BuildExpectedAccount(beforeAccount, request);
        var beforeSidecar = EncodeSidecar(null);
        var afterSidecar = EncodeSidecar(request.ClientEquipment);
        var sidecarPath = GetSidecarPath(fixture.Root, AccountName, request.CharacterSlot);

        Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
        await File.WriteAllBytesAsync(fixture.AccountPath, afterAccount);
        await File.WriteAllBytesAsync(sidecarPath, afterSidecar);
        await WriteJournalAsync(fixture.Root, 2, "Prepared", (fixture.AccountPath, beforeAccount, afterAccount), (sidecarPath, beforeSidecar, afterSidecar));

        var login = await fixture.Store.ReadCharacterLoginDataAsync(AccountName, 0);
        var recoveredAccount = await File.ReadAllBytesAsync(fixture.AccountPath);

        Require(recoveredAccount.SequenceEqual(beforeAccount), "Prepared journal recovery did not restore the complete old account image.");
        Require(login is not null && login.ClientEquipment is null, "Prepared journal recovery exposed equipment from the incomplete checkpoint.");
        Require(!File.Exists(Path.Combine(fixture.Root, JournalName)), "Prepared journal was left behind after successful recovery.");
    }

    private static async Task CommittedJournalReplaysAccountAndEquipmentAfterState()
    {
        using var fixture = new AccountFixture(AccountName);
        var beforeAccount = await File.ReadAllBytesAsync(fixture.AccountPath);
        var request = CreateRequest();
        var afterAccount = BuildExpectedAccount(beforeAccount, request);
        var beforeSidecar = EncodeSidecar(null);
        var afterSidecar = EncodeSidecar(request.ClientEquipment);
        var sidecarPath = GetSidecarPath(fixture.Root, AccountName, request.CharacterSlot);

        Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
        await WriteJournalAsync(fixture.Root, 2, "Committed", (fixture.AccountPath, beforeAccount, afterAccount), (sidecarPath, beforeSidecar, afterSidecar));

        var login = await fixture.Store.ReadCharacterLoginDataAsync(AccountName, 0);
        var recoveredAccount = await File.ReadAllBytesAsync(fixture.AccountPath);

        Require(recoveredAccount.SequenceEqual(afterAccount), "Committed journal recovery did not replay the complete new account image.");
        Require(login?.ClientEquipment is not null && login.ClientEquipment.SequenceEqual(request.ClientEquipment), "Committed journal recovery did not replay the matching equipment sidecar.");
        Require(!File.Exists(Path.Combine(fixture.Root, JournalName)), "Committed journal was left behind after successful recovery.");
    }

    private static async Task VersionOneTwoAccountJournalRemainsReadable()
    {
        using var fixture = new AccountFixture(AccountName);
        var second = new AccountFixture("OTHER", fixture.Root);
        try
        {
            var firstBefore = await File.ReadAllBytesAsync(fixture.AccountPath);
            var secondBefore = await File.ReadAllBytesAsync(second.AccountPath);
            var firstAfter = firstBefore.ToArray();
            var secondAfter = secondBefore.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(firstAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 111);
            BinaryPrimitives.WriteInt32LittleEndian(secondAfter.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 222);

            await File.WriteAllBytesAsync(fixture.AccountPath, firstAfter);
            await File.WriteAllBytesAsync(second.AccountPath, secondAfter);
            await WriteJournalAsync(fixture.Root, 1, "Prepared", (fixture.AccountPath, firstBefore, firstAfter), (second.AccountPath, secondBefore, secondAfter));

            _ = await fixture.Store.ReadSnapshotAsync(AccountName);

            Require((await File.ReadAllBytesAsync(fixture.AccountPath)).SequenceEqual(firstBefore), "The store stopped recovering a prepared two-account v1 journal.");
            Require((await File.ReadAllBytesAsync(second.AccountPath)).SequenceEqual(secondBefore), "The store did not recover both account files from a v1 journal.");
        }
        finally
        {
            second.Dispose();
        }
    }

    private static LegacyCharacterCheckpointSaveRequest CreateRequest()
    {
        var mob = Enumerable.Range(0, LegacyAccountSnapshot.CharacterStride)
            .Select(static index => unchecked((byte)(index * 13 + 5)))
            .ToArray();
        mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16).Clear();
        Encoding.ASCII.GetBytes("HERO").CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, sizeof(uint)).Fill(0xE7);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), 0x12345678);

        var mobExtra = Enumerable.Range(0, LegacyAccountSnapshot.MobExtraStride)
            .Select(static index => unchecked((byte)(index * 7 + 9)))
            .ToArray();
        var affect = Enumerable.Range(0, LegacyAccountSnapshot.AffectStride)
            .Select(static index => unchecked((byte)(index * 5 + 3)))
            .ToArray();
        var equipment = Enumerable.Range(0, ClientEquipmentStateV769.EquipmentCount)
            .Select(static index => new LegacyItem((short)(1_000 + index), (byte)(index + 1), (byte)(index + 2), (byte)(index + 3), (byte)(index + 4), (byte)(index + 5), (byte)(index + 6)))
            .ToArray();

        return new LegacyCharacterCheckpointSaveRequest(AccountName, 0, mob, 321, -123, mobExtra, affect, equipment);
    }

    private static byte[] CreateMob(string name, int carriedGold)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob, LegacyAccountSnapshot.MobNameOffset);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCoinOffset), carriedGold);
        return mob;
    }

    private static byte[] BuildExpectedAccount(byte[] before, LegacyCharacterCheckpointSaveRequest request)
    {
        var expected = before.ToArray();
        var characterOffset = LegacyAccountSnapshot.CharactersOffset + (request.CharacterSlot * LegacyAccountSnapshot.CharacterStride);
        var mob = request.Mob.ToArray();
        before.AsSpan(characterOffset + LegacyAccountSnapshot.MobSkillBarOffset, sizeof(uint))
            .CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, sizeof(uint)));
        BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionXOffset), request.PositionX);
        BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobSavedPositionYOffset), request.PositionY);
        mob.CopyTo(expected, characterOffset);
        request.MobExtra.CopyTo(expected.AsMemory(LegacyAccountSnapshot.MobExtraOffset + request.CharacterSlot * LegacyAccountSnapshot.MobExtraStride));
        request.Affect.CopyTo(expected.AsMemory(LegacyAccountSnapshot.AffectOffset + request.CharacterSlot * LegacyAccountSnapshot.AffectStride));
        return expected;
    }

    private static byte[] EncodeSidecar(IReadOnlyList<LegacyItem>? equipment)
    {
        var payload = equipment is null ? Array.Empty<byte>() : new ClientEquipmentStateV769(equipment).ToBytes();
        var result = new byte[12 + payload.Length];
        Encoding.ASCII.GetBytes("WYDEQ769").CopyTo(result, 0);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8), 1);
        payload.CopyTo(result, 12);
        return result;
    }

    private static Task WriteJournalAsync(
        string root,
        int version,
        string phase,
        (string Path, byte[] Before, byte[] After) first,
        (string Path, byte[] Before, byte[] After) second)
    {
        object document = version == 2
            ? new
            {
                Version = version,
                Phase = phase,
                AccountName,
                CharacterSlot = 0,
                Files = new[]
                {
                    new { first.Path, first.Before, first.After },
                    new { second.Path, second.Before, second.After },
                },
            }
            : new
            {
                Version = version,
                Phase = phase,
                Files = new[]
                {
                    new { first.Path, first.Before, first.After },
                    new { second.Path, second.Before, second.After },
                },
            };
        var json = JsonSerializer.Serialize(document);
        return File.WriteAllTextAsync(Path.Combine(root, JournalName), json);
    }

    private static string GetSidecarPath(string root, string accountName, int slot) =>
        Path.Combine(root, SidecarRootName, accountName[..1], accountName, $"{slot:D1}.bin");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class AccountFixture : IDisposable
    {
        public AccountFixture(string accountName, string? existingRoot = null)
        {
            Root = existingRoot ?? Path.Combine(Path.GetTempPath(), $"wyd-cdk-character-checkpoint-{Guid.NewGuid():N}");
            AccountPath = Path.Combine(Root, accountName[..1], accountName);
            Directory.CreateDirectory(Path.GetDirectoryName(AccountPath)!);
            File.WriteAllBytes(AccountPath, CreateAccountBytes(accountName));
            Store = new LegacyFileAccountStore(Root);
        }

        public string Root { get; }
        public string AccountPath { get; }
        public LegacyFileAccountStore Store { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private static byte[] CreateAccountBytes(string accountName)
        {
            var bytes = new byte[LegacyAccountSnapshot.RequiredFileLength];
            Encoding.ASCII.GetBytes(accountName).CopyTo(bytes, 0);
            Encoding.ASCII.GetBytes("PASS").CopyTo(bytes, 16);

            var characterOffset = LegacyAccountSnapshot.CharactersOffset;
            Encoding.ASCII.GetBytes("HERO").CopyTo(bytes, characterOffset + LegacyAccountSnapshot.MobNameOffset);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(characterOffset + LegacyAccountSnapshot.MobCoinOffset), 100);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionXOffset), 10);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(characterOffset + LegacyAccountSnapshot.MobSavedPositionYOffset), 20);
            new byte[] { 0x11, 0x22, 0x33, 0x44 }.CopyTo(bytes, characterOffset + LegacyAccountSnapshot.MobSkillBarOffset);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(LegacyAccountSnapshot.AccountCoinOffset), 100);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(AccountDonateOffset), 777);

            for (var index = 0; index < LegacyAccountSnapshot.ShortSkillStride; index++)
                bytes[LegacyAccountSnapshot.ShortSkillOffset + index] = (byte)(index + 1);
            for (var index = 0; index < LegacyAccountSnapshot.CargoCount; index++)
                new LegacyItem((short)(300 + index), (byte)index, 2, (byte)(index + 1), 4, (byte)(index + 2), 6)
                    .Write(bytes.AsSpan(LegacyAccountSnapshot.CargoOffset + index * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
            for (var index = 0; index < LegacyAccountSnapshot.AffectStride; index++)
                bytes[LegacyAccountSnapshot.AffectOffset + index] = (byte)(0x40 + (index % 31));
            for (var index = 0; index < LegacyAccountSnapshot.MobExtraStride; index++)
                bytes[LegacyAccountSnapshot.MobExtraOffset + index] = (byte)(0x60 + (index % 31));
            for (var index = 0; index < DailyQuestLength; index++)
                bytes[DailyQuestOffset + index] = (byte)(0x80 + (index % 31));

            return bytes;
        }
    }
}
