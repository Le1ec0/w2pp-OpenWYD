using System.Diagnostics;
using System.Text.Json;
using WydCdk.World;

internal static class AccountReviewProcessTests
{
    private const int WorkerCount = 2;

    public static async Task RunAllAsync()
    {
        await ConcurrentMarksAcrossProcesses();
        await ReadWaitsForCrossProcessLock();
    }

    public static async Task ConcurrentMarksAcrossProcesses()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"wyd-cdk-account-review-process-{Guid.NewGuid():N}");
        var reviewRoot = Path.Combine(testRoot, "review");
        var coordinationRoot = Path.Combine(testRoot, "coordination");
        Directory.CreateDirectory(reviewRoot);
        Directory.CreateDirectory(coordinationRoot);

        const string accountName = "CROSSPROC";
        _ = await new FileAccountReviewStore(reviewRoot).ReadStatusAsync(accountName);
        var lockOwner = new FileStream(GetOnlyLockPath(reviewRoot), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var processes = new List<Process>();
        try
        {
            for (var index = 0; index < WorkerCount; index++)
                processes.Add(StartWorker("mark", reviewRoot, accountName, coordinationRoot, index));

            var readyPaths = Enumerable.Range(0, WorkerCount)
                .Select(index => Path.Combine(coordinationRoot, $"ready-{index}.signal")).ToArray();
            await WaitForSignalsAsync(readyPaths, processes);

            var gatePath = Path.Combine(coordinationRoot, "start.signal");
            await File.WriteAllTextAsync(gatePath, "start");
            var callingPaths = Enumerable.Range(0, WorkerCount)
                .Select(index => Path.Combine(coordinationRoot, $"calling-{index}.signal")).ToArray();
            await WaitForSignalsAsync(callingPaths, processes);

            await Task.Delay(TimeSpan.FromMilliseconds(200));
            Check(processes.All(static process => !process.HasExited) &&
                !File.Exists(Path.Combine(reviewRoot, $"{accountName}.review.json")),
                "A server process ignored the cross-process account lock instead of waiting for its owner.");

            lockOwner.Dispose();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                await Task.WhenAll(processes.Select(process => process.WaitForExitAsync(timeout.Token)));
            }
            catch (OperationCanceledException exception)
            {
                throw new TimeoutException("Account-review worker processes did not finish after the shared lock was released.", exception);
            }

            foreach (var process in processes)
            {
                var error = await process.StandardError.ReadToEndAsync();
                Check(process.ExitCode == 0, $"An account-review worker exited with code {process.ExitCode}: {error}");
            }

            var results = await Task.WhenAll(Enumerable.Range(0, WorkerCount).Select(async index =>
                await File.ReadAllTextAsync(Path.Combine(coordinationRoot, $"result-{index}.txt"))));
            Check(results.Count(static result => result == nameof(AccountReviewMarkResult.Marked)) == 1 &&
                results.Count(static result => result == nameof(AccountReviewMarkResult.AlreadyMarked)) == WorkerCount - 1,
                "Independent UP/PVP-like processes did not produce exactly one first mark and one idempotent result.");

            var winningIndex = Array.IndexOf(results, nameof(AccountReviewMarkResult.Marked));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(reviewRoot, $"{accountName}.review.json")));
            var winningEvidence = document.RootElement.GetProperty("evidence").GetProperty("characterName").GetString();
            Check(winningEvidence == $"World-{winningIndex}",
                "The persisted sidecar did not retain the evidence from the process that created the first mark.");
            Check(await new FileAccountReviewStore(reviewRoot).ReadStatusAsync(accountName) == AccountReviewStatus.Analysis,
                "The cross-process mark did not leave the account durably in Analysis.");
        }
        finally
        {
            lockOwner.Dispose();
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                    }
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                process.Dispose();
            }

            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    public static async Task ReadWaitsForCrossProcessLock()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"wyd-cdk-account-review-read-process-{Guid.NewGuid():N}");
        var reviewRoot = Path.Combine(testRoot, "review");
        var coordinationRoot = Path.Combine(testRoot, "coordination");
        Directory.CreateDirectory(reviewRoot);
        Directory.CreateDirectory(coordinationRoot);

        const string accountName = "READPROC";
        var seedEvidence = new AccountReviewEvidence(accountName, FileAccountReviewStore.HighSeverityActionSpeedReason,
            "Seed", "127.0.0.1", 20, 5, 5, DateTimeOffset.UtcNow);
        await new FileAccountReviewStore(reviewRoot).MarkForAnalysisAsync(seedEvidence);

        var lockOwner = new FileStream(GetOnlyLockPath(reviewRoot), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var process = StartWorker("read", reviewRoot, accountName, coordinationRoot, 0);
        try
        {
            await WaitForSignalsAsync(
                [Path.Combine(coordinationRoot, "ready-0.signal")], [process]);
            var gatePath = Path.Combine(coordinationRoot, "start.signal");
            await File.WriteAllTextAsync(gatePath, "start");
            var resultPath = Path.Combine(coordinationRoot, "result-0.txt");
            await WaitForSignalsAsync(
                [Path.Combine(coordinationRoot, "calling-0.signal")], [process]);

            await Task.Delay(TimeSpan.FromMilliseconds(200));
            Check(!process.HasExited && !File.Exists(resultPath),
                "A login-style status read ignored the cross-process account lock.");

            lockOwner.Dispose();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException exception)
            {
                throw new TimeoutException("The account-review reader did not finish after the shared lock was released.", exception);
            }

            var error = await process.StandardError.ReadToEndAsync();
            Check(process.ExitCode == 0, $"The account-review reader exited with code {process.ExitCode}: {error}");
            Check(await File.ReadAllTextAsync(resultPath) == nameof(AccountReviewStatus.Analysis),
                "The serialized cross-process read did not observe the durable Analysis status.");
        }
        finally
        {
            lockOwner.Dispose();
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            process.Dispose();
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, recursive: true);
        }
    }

    public static async Task<int> RunWorkerAsync(string[] args)
    {
        if (args.Length != 7)
        {
            Console.Error.WriteLine("Invalid account-review process worker arguments.");
            return 2;
        }

        var operation = args[1];
        var reviewRoot = args[2];
        var accountName = args[3];
        var coordinationRoot = args[4];
        var gatePath = args[5];
        var workerIndex = int.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture);
        var store = new FileAccountReviewStore(reviewRoot);
        await File.WriteAllTextAsync(Path.Combine(coordinationRoot, $"ready-{workerIndex}.signal"), "ready");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!File.Exists(gatePath))
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);

        await File.WriteAllTextAsync(Path.Combine(coordinationRoot, $"calling-{workerIndex}.signal"), "calling");
        try
        {
            string result;
            if (operation == "mark")
            {
                var evidence = new AccountReviewEvidence(accountName,
                    FileAccountReviewStore.HighSeverityActionSpeedReason, $"World-{workerIndex}", "127.0.0.1",
                    RequestedSpeed: 20, AllowedSpeed: 5, AttackRun: 5, DetectedAtUtc: DateTimeOffset.UtcNow);
                result = (await store.MarkForAnalysisAsync(evidence, timeout.Token)).ToString();
            }
            else if (operation == "read")
            {
                result = (await store.ReadStatusAsync(accountName, timeout.Token)).ToString();
            }
            else
            {
                throw new ArgumentException("Unknown account-review worker operation.", nameof(operation));
            }

            await File.WriteAllTextAsync(Path.Combine(coordinationRoot, $"result-{workerIndex}.txt"), result);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 3;
        }
    }

    private static Process StartWorker(string operation, string reviewRoot, string accountName, string coordinationRoot, int index)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("The test process path is unavailable.");
        var startInfo = new ProcessStartInfo(processPath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            startInfo.ArgumentList.Add(typeof(TestAssemblyMarker).Assembly.Location);

        startInfo.ArgumentList.Add("--account-review-process-worker");
        startInfo.ArgumentList.Add(operation);
        startInfo.ArgumentList.Add(reviewRoot);
        startInfo.ArgumentList.Add(accountName);
        startInfo.ArgumentList.Add(coordinationRoot);
        startInfo.ArgumentList.Add(Path.Combine(coordinationRoot, "start.signal"));
        startInfo.ArgumentList.Add(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start an account-review worker process.");
    }

    private static string GetOnlyLockPath(string reviewRoot)
    {
        var lockPaths = Directory.GetFiles(reviewRoot, ".account-review-lock-*.lck");
        Check(lockPaths.Length == 1, $"Expected one account-review lock stripe, found {lockPaths.Length}.");
        return lockPaths[0];
    }

    private static async Task WaitForSignalsAsync(IReadOnlyList<string> paths, IReadOnlyList<Process> processes)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (paths.Any(static path => !File.Exists(path)))
        {
            for (var index = 0; index < processes.Count; index++)
            {
                if (processes[index].HasExited && !File.Exists(paths[index]))
                {
                    var error = await processes[index].StandardError.ReadToEndAsync();
                    throw new InvalidOperationException($"Account-review worker {index} exited before its signal ({processes[index].ExitCode}): {error}");
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
