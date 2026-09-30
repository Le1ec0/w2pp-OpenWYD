using System.Text.Json;
using WydCdk.World;

internal static class AccountReviewStoreTests
{
    public static async Task MissingRecordIsUnmarked()
    {
        using var root = new TemporaryRoot();
        var store = new FileAccountReviewStore(root.Path);
        Check(await store.ReadStatusAsync("teste") == AccountReviewStatus.NotMarked,
            "A missing review sidecar did not remain unmarked.");
    }

    public static async Task MissingConfiguredRootFailsClosed()
    {
        using var parent = new TemporaryRoot();
        var reviewRoot = Path.Combine(parent.Path, "review");
        Directory.CreateDirectory(reviewRoot);
        var store = new FileAccountReviewStore(reviewRoot);
        Directory.Delete(reviewRoot);

        var rejected = false;
        try { _ = await store.ReadStatusAsync("TESTE"); }
        catch (IOException) { rejected = true; }
        Check(rejected, "A missing configured review root was treated like an unmarked account.");
    }

    public static async Task RoundTripAndPreserveFirstEvidence()
    {
        using var root = new TemporaryRoot();
        var first = Evidence("TESTE", "Herói", "127.0.0.1");
        var second = Evidence("TESTE", "Outro", "192.0.2.1");
        var store = new FileAccountReviewStore(root.Path);
        Check(await store.MarkForAnalysisAsync(first) == AccountReviewMarkResult.Marked,
            "The first review mark was not persisted.");

        var restartedStore = new FileAccountReviewStore(root.Path);
        Check(await restartedStore.ReadStatusAsync("teste") == AccountReviewStatus.Analysis,
            "A new store instance did not read the durable Analysis status.");
        Check(await restartedStore.MarkForAnalysisAsync(second) == AccountReviewMarkResult.AlreadyMarked,
            "A repeated review mark was not idempotent.");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root.Path, "TESTE.review.json")));
        Check(document.RootElement.GetProperty("evidence").GetProperty("characterName").GetString() == "Herói" &&
            document.RootElement.GetProperty("evidence").GetProperty("remoteAddress").GetString() == "127.0.0.1",
            "A repeated mark replaced the first detection evidence.");
    }

    public static async Task ConcurrentMarksAreIdempotentAcrossStoreInstances()
    {
        using var root = new TemporaryRoot();
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(index =>
            new FileAccountReviewStore(root.Path).MarkForAnalysisAsync(
                Evidence("RACE", $"Hero{index}", "127.0.0.1")).AsTask()));

        Check(results.Count(static result => result == AccountReviewMarkResult.Marked) == 1 &&
            results.Count(static result => result == AccountReviewMarkResult.AlreadyMarked) == 31,
            "Concurrent in-process marks did not produce exactly one durable first mark.");
        Check(await new FileAccountReviewStore(root.Path).ReadStatusAsync("RACE") == AccountReviewStatus.Analysis,
            "Concurrent marks did not leave the account in Analysis.");
    }

    public static async Task LockFileCountIsBoundedByStripeCount()
    {
        using var root = new TemporaryRoot();
        var store = new FileAccountReviewStore(root.Path);
        var accounts = Enumerable.Range(0, 160).Select(static index => $"A{index:D14}").ToArray();
        await Task.WhenAll(accounts.Select(account => store.ReadStatusAsync(account).AsTask()));

        var lockFiles = Directory.GetFiles(root.Path, ".account-review-lock-*.lck");
        Check(lockFiles.Length is > 0 and <= 128,
            $"Review-lock file count grew with account names instead of remaining bounded (found {lockFiles.Length}).");
    }

    public static async Task InvalidNamesCannotResolveOutsideRoot()
    {
        using var root = new TemporaryRoot();
        var store = new FileAccountReviewStore(root.Path);
        var outsideCandidate = Path.Combine(Path.GetDirectoryName(root.Path)!, "ESCAPE.review.json");
        foreach (var name in new[] { "", "../ESCAPE", "..\\ESCAPE", "A/B", "A:B", "ÉXITO", "ABCDEFGHIJKLMNOP" })
        {
            var rejected = false;
            try { _ = await store.ReadStatusAsync(name); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, $"Invalid account name '{name}' reached review storage.");
        }
        Check(!File.Exists(outsideCandidate), "Invalid account input created a sidecar outside the configured root.");
    }

    public static async Task InvalidRecordsFailClosed()
    {
        using var root = new TemporaryRoot();
        await AssertRecordRejected(root.Path, "TRUNCATED", "{\"version\":1");
        await AssertRecordRejected(root.Path, "VERSION", """
            {"version":2,"accountName":"VERSION","status":"Analysis","detectedAtUtc":"2026-09-28T10:00:00Z","evidence":{"accountName":"VERSION","reasonCode":"ActionSpeedViolationHighSeverity","characterName":null,"remoteAddress":null,"requestedSpeed":20,"allowedSpeed":5,"attackRun":5,"detectedAtUtc":"2026-09-28T10:00:00Z"}}
            """);
        await AssertRecordRejected(root.Path, "IDENTITY", """
            {"version":1,"accountName":"OTHER","status":"Analysis","detectedAtUtc":"2026-09-28T10:00:00Z","evidence":{"accountName":"OTHER","reasonCode":"ActionSpeedViolationHighSeverity","characterName":null,"remoteAddress":null,"requestedSpeed":20,"allowedSpeed":5,"attackRun":5,"detectedAtUtc":"2026-09-28T10:00:00Z"}}
            """);
    }

    public static async Task SidecarSymlinkCannotEscapeRoot()
    {
        using var root = new TemporaryRoot();
        var outside = Path.Combine(Path.GetDirectoryName(root.Path)!, $"review-outside-{Guid.NewGuid():N}.json");
        var link = Path.Combine(root.Path, "LINK.review.json");
        await File.WriteAllTextAsync(outside, "outside data");
        try
        {
            try
            {
                File.CreateSymbolicLink(link, outside);
            }
            catch (Exception error) when (error is PlatformNotSupportedException or UnauthorizedAccessException or IOException)
            {
                Console.WriteLine($"SKIP sidecar symlink capability unavailable: {error.GetType().Name}.");
                return;
            }

            var rejected = false;
            try { _ = await new FileAccountReviewStore(root.Path).ReadStatusAsync("LINK"); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "A review sidecar symlink was followed outside the configured root.");
            Check(await File.ReadAllTextAsync(outside) == "outside data", "The symlink target was modified.");
        }
        finally
        {
            if (File.Exists(link)) File.Delete(link);
            if (File.Exists(outside)) File.Delete(outside);
        }
    }

    public static Task AccountNameNormalizationMatchesAuthenticationContract()
    {
        Check(LegacyAccountNameRules.TryNormalize("test_1-", out var normalized) && normalized == "TEST_1-",
            "Valid account names were not normalized invariantly.");
        foreach (var invalid in new[] { "", "ABCDEFGHIJKLMNOP", "A/B", "ÉXITO" })
            Check(!LegacyAccountNameRules.TryNormalize(invalid, out _), $"Invalid account name '{invalid}' was accepted.");
        return Task.CompletedTask;
    }

    private static async Task AssertRecordRejected(string root, string accountName, string json)
    {
        await File.WriteAllTextAsync(Path.Combine(root, accountName + ".review.json"), json);
        var rejected = false;
        try { _ = await new FileAccountReviewStore(root).ReadStatusAsync(accountName); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, $"Invalid review document for {accountName} was treated as unmarked.");
    }

    private static AccountReviewEvidence Evidence(string account, string? character, string? address) => new(
        account, "ActionSpeedViolationHighSeverity", character, address, 20, 5, 5,
        new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TemporaryRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wyd-cdk-account-review-{Guid.NewGuid():N}");

        public TemporaryRoot() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
