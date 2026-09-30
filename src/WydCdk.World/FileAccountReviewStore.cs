using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WydCdk.World;

/// <summary>
/// Durable, versioned per-account review state. Same-account operations are
/// serialized across processes that share the configured root.
/// </summary>
public sealed class FileAccountReviewStore : IAccountReviewStore
{
    public const int CurrentVersion = 1;
    public const string HighSeverityActionSpeedReason = "ActionSpeedViolationHighSeverity";
    private const int MaximumDocumentBytes = 16 * 1024;
    private static readonly SemaphoreSlim[] AccountGates = Enumerable.Range(0, 128)
        .Select(static _ => new SemaphoreSlim(1, 1)).ToArray();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly TimeSpan CrossProcessLockRetryDelay = TimeSpan.FromMilliseconds(20);
    private readonly string root;

    public FileAccountReviewStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        this.root = Path.GetFullPath(root);
        EnsureExistingPathHasNoReparsePoints(this.root);
        Directory.CreateDirectory(this.root);
        EnsureReviewRootAvailable();
    }

    public async ValueTask<AccountReviewStatus> ReadStatusAsync(string accountName, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeOrThrow(accountName);
        var path = GetSidecarPath(normalized);
        var stripe = GetAccountStripe(normalized);
        var gate = AccountGates[stripe];
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var crossProcessGate = await AcquireCrossProcessGateAsync(stripe, cancellationToken);
            var document = await ReadDocumentAsync(path, normalized, cancellationToken);
            return document?.Status ?? AccountReviewStatus.NotMarked;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<AccountReviewMarkResult> MarkForAnalysisAsync(AccountReviewEvidence evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var normalized = NormalizeOrThrow(evidence.AccountName);
        if (!string.Equals(normalized, evidence.AccountName, StringComparison.Ordinal))
            throw new ArgumentException("Account-review evidence must use a normalized account name.", nameof(evidence));
        ValidateEvidence(evidence, normalized);

        var path = GetSidecarPath(normalized);
        var stripe = GetAccountStripe(normalized);
        var gate = AccountGates[stripe];
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var crossProcessGate = await AcquireCrossProcessGateAsync(stripe, cancellationToken);
            if (await ReadDocumentAsync(path, normalized, cancellationToken) is not null)
                return AccountReviewMarkResult.AlreadyMarked;

            var document = new AccountReviewDocument(CurrentVersion, normalized, AccountReviewStatus.Analysis,
                evidence.DetectedAtUtc, evidence);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            var temporaryPath = Path.Combine(root, $".{normalized}.{Guid.NewGuid():N}.tmp");
            try
            {
                EnsureReviewRootAvailable();
                await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(bytes, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(flushToDisk: true);
                }

                EnsureReviewRootAvailable();
                File.Move(temporaryPath, path);
                return AccountReviewMarkResult.Marked;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask<AccountReviewDocument?> ReadDocumentAsync(string path, string normalized, CancellationToken cancellationToken)
    {
        EnsureReviewRootAvailable();
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            EnsureReviewRootAvailable();
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            EnsureReviewRootAvailable();
            return null;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0)
            throw new InvalidDataException("The account-review sidecar is not a regular file inside the configured root.");

        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > MaximumDocumentBytes)
                throw new InvalidDataException("The account-review sidecar has an invalid length.");

            var document = await JsonSerializer.DeserializeAsync<AccountReviewDocument>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The account-review sidecar is empty.");
            ValidateDocument(document, normalized);
            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The account-review sidecar is malformed or uses an unsupported schema.", exception);
        }
    }

    private async ValueTask<FileStream> AcquireCrossProcessGateAsync(int stripe, CancellationToken cancellationToken)
    {
        var path = GetCrossProcessLockPath(stripe);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureReviewRootAvailable();
            EnsureExistingLockFileIsRegular(path);

            try
            {
                var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                    1, FileOptions.Asynchronous);
                try
                {
                    EnsureReviewRootAvailable();
                    EnsureExistingLockFileIsRegular(path);
                    return stream;
                }
                catch
                {
                    await stream.DisposeAsync();
                    throw;
                }
            }
            catch (IOException exception) when (IsCrossProcessLockContention(exception))
            {
                await Task.Delay(CrossProcessLockRetryDelay, cancellationToken);
            }
        }
    }

    private string GetCrossProcessLockPath(int stripe)
    {
        var path = Path.GetFullPath(Path.Combine(root, $".account-review-lock-{stripe:D3}.lck"));
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("The account-review lock resolves outside the configured root.", nameof(stripe));
        return path;
    }

    private static void EnsureExistingLockFileIsRegular(string path)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0)
            throw new InvalidDataException("The account-review lock is not a regular file inside the configured root.");
    }

    private static bool IsCrossProcessLockContention(IOException exception) =>
        (exception.HResult & 0xFFFF) is 11 or 32 or 33;

    private static void ValidateDocument(AccountReviewDocument document, string normalized)
    {
        if (document.Version != CurrentVersion ||
            !string.Equals(document.AccountName, normalized, StringComparison.Ordinal) ||
            document.Status != AccountReviewStatus.Analysis ||
            document.DetectedAtUtc.Offset != TimeSpan.Zero ||
            document.Evidence is null ||
            !string.Equals(document.Evidence.AccountName, normalized, StringComparison.Ordinal) ||
            document.Evidence.DetectedAtUtc != document.DetectedAtUtc)
            throw new InvalidDataException("The account-review sidecar version, identity, or state is invalid.");

        ValidateEvidence(document.Evidence, normalized);
    }

    private static void ValidateEvidence(AccountReviewEvidence evidence, string normalized)
    {
        if (!string.Equals(evidence.AccountName, normalized, StringComparison.Ordinal) ||
            !string.Equals(evidence.ReasonCode, HighSeverityActionSpeedReason, StringComparison.Ordinal) ||
            evidence.DetectedAtUtc.Offset != TimeSpan.Zero ||
            evidence.AttackRun < 0 ||
            evidence.AllowedSpeed != (evidence.AttackRun & 0xF) ||
            evidence.RequestedSpeed <= evidence.AllowedSpeed ||
            evidence.RequestedSpeed < 20 && evidence.AttackRun < 300 ||
            evidence.RemoteAddress is not null && !IPAddress.TryParse(evidence.RemoteAddress, out _))
            throw new ArgumentException("The account-review evidence is not a valid high-severity Action detection.", nameof(evidence));
    }

    private string GetSidecarPath(string normalized)
    {
        var path = Path.GetFullPath(Path.Combine(root, normalized + ".review.json"));
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("The account name resolves outside the configured account-review root.", nameof(normalized));
        return path;
    }

    private void EnsureReviewRootAvailable()
    {
        EnsureExistingPathHasNoReparsePoints(root);
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(root);
        }
        catch (FileNotFoundException exception)
        {
            throw new IOException("The configured account-review root is unavailable.", exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new IOException("The configured account-review root is unavailable.", exception);
        }

        if ((attributes & FileAttributes.Directory) == 0)
            throw new IOException("The configured account-review root is not a directory.");
    }

    private static string NormalizeOrThrow(string accountName)
    {
        if (!LegacyAccountNameRules.TryNormalize(accountName, out var normalized))
            throw new ArgumentException("The account name is invalid for account-review storage.", nameof(accountName));
        return normalized;
    }

    private static int GetAccountStripe(string normalized)
    {
        // Account names are validated ASCII and normalized to uppercase before hashing.
        // Unlike string.GetHashCode, FNV-1a is stable across the UP/PVP processes.
        uint hash = 2166136261;
        foreach (var character in normalized)
        {
            hash ^= character;
            hash *= 16777619;
        }
        return (int)(hash % (uint)AccountGates.Length);
    }

    private static void EnsureExistingPathHasNoReparsePoints(string fullPath)
    {
        var volumeRoot = Path.GetPathRoot(fullPath)
            ?? throw new ArgumentException("The account-review root must be an absolute path.", nameof(fullPath));
        var current = volumeRoot;
        var remainder = fullPath[volumeRoot.Length..];
        foreach (var segment in remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current); }
            catch (FileNotFoundException) { break; }
            catch (DirectoryNotFoundException) { break; }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The account-review path cannot traverse a symbolic link or reparse point.");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    private sealed record AccountReviewDocument(
        int Version,
        string AccountName,
        AccountReviewStatus Status,
        DateTimeOffset DetectedAtUtc,
        AccountReviewEvidence Evidence);
}
