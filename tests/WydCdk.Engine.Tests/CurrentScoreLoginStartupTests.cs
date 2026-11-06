using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

internal static class CurrentScoreLoginStartupTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

    public static void Run()
    {
        var serverRoot = FindServerRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent?.Name ?? "Debug";
        var serverDll = Path.Combine(serverRoot, "src", "WydCdk.Server", "bin", configuration, "net10.0", "WydCdk.Server.Core.dll");
        Ensure(File.Exists(serverDll), $"The Server host was not built at the expected path: {serverDll}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "wyd-cdk-current-score-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            var accountsRoot = Path.Combine(testRoot, "accounts");
            var reviewRoot = Path.Combine(testRoot, "review");
            Directory.CreateDirectory(accountsRoot);
            Directory.CreateDirectory(reviewRoot);

            var missingTablePort = ReservePort();
            var missingTable = RunToExit(serverDll,
                "--bind", "127.0.0.1", "--port", missingTablePort.ToString(),
                "--accounts-root", accountsRoot, "--account-review-root", reviewRoot);
            Ensure(missingTable.ExitCode != 0, "Authenticated startup without --item-data unexpectedly exited successfully.");
            Ensure(missingTable.StandardError.Contains("--item-data", StringComparison.OrdinalIgnoreCase) ||
                missingTable.StandardOutput.Contains("--item-data", StringComparison.OrdinalIgnoreCase),
                "Authenticated startup without --item-data did not report the missing option.");
            EnsureCanBind(missingTablePort, "authenticated startup without ItemList");

            var unreadableTablePort = ReservePort();
            var unreadableTable = RunToExit(serverDll,
                "--bind", "127.0.0.1", "--port", unreadableTablePort.ToString(),
                "--accounts-root", accountsRoot, "--account-review-root", reviewRoot,
                "--item-data", Path.Combine(testRoot, "missing-ItemList.bin"));
            Ensure(unreadableTable.ExitCode != 0, "Authenticated startup with a missing ItemList unexpectedly exited successfully.");
            EnsureCanBind(unreadableTablePort, "authenticated startup with an unreadable ItemList");

            var diagnosticPort = ReservePort();
            var diagnostic = Start(serverDll, "--bind", "127.0.0.1", "--port", diagnosticPort.ToString());
            using (diagnostic.Process)
            {
                try
                {
                    var connected = WaitForListener(diagnostic.Process, diagnosticPort, StartupTimeout);
                    Ensure(connected, "Unauthenticated diagnostic startup without ItemList did not open its loopback listener.");
                }
                finally
                {
                    Stop(diagnostic.Process);
                    _ = diagnostic.StandardOutput.GetAwaiter().GetResult();
                    _ = diagnostic.StandardError.GetAwaiter().GetResult();
                }
            }
            EnsureCanBind(diagnosticPort, "diagnostic host shutdown");
        }
        finally
        {
            DeleteTemporaryRoot(testRoot);
        }
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunToExit(string serverDll, params string[] arguments)
    {
        var started = Start(serverDll, arguments);
        using var process = started.Process;
        if (!process.WaitForExit(checked((int)StartupTimeout.TotalMilliseconds)))
        {
            var port = ReadPort(arguments);
            Stop(process);
            _ = started.StandardOutput.GetAwaiter().GetResult();
            _ = started.StandardError.GetAwaiter().GetResult();
            throw new InvalidOperationException($"Server startup did not reject its invalid configuration before binding port {port}.");
        }
        process.WaitForExit();
        var standardOutput = started.StandardOutput.GetAwaiter().GetResult();
        var standardError = started.StandardError.GetAwaiter().GetResult();
        return (process.ExitCode, standardOutput, standardError);
    }

    private static StartedProcess Start(string serverDll, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = FindServerRoot(),
        };
        start.ArgumentList.Add(serverDll);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the WydCdk.Server host process.");
        return new StartedProcess(
            process,
            process.StandardOutput.ReadToEndAsync(),
            process.StandardError.ReadToEndAsync());
    }

    private static bool WaitForListener(Process process, int port, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until && !process.HasExited)
        {
            using var client = new TcpClient();
            try
            {
                client.Connect(IPAddress.Loopback, port);
                return true;
            }
            catch (SocketException)
            {
                Thread.Sleep(100);
            }
        }
        return false;
    }

    private static void Stop(Process process)
    {
        if (process.HasExited) return;
        try { process.StandardInput.WriteLine("stop"); }
        catch (IOException) { }
        if (!process.WaitForExit(8_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
    }

    private sealed record StartedProcess(Process Process, Task<string> StandardOutput, Task<string> StandardError);

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void EnsureCanBind(int port, string scenario)
    {
        using var listener = new TcpListener(IPAddress.Loopback, port);
        try { listener.Start(); }
        catch (SocketException error)
        {
            throw new InvalidOperationException($"Port {port} remained bound after {scenario}.", error);
        }
    }

    private static int ReadPort(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index + 1 < arguments.Count; index++)
            if (arguments[index] == "--port" && int.TryParse(arguments[index + 1], out var port))
                return port;
        return 0;
    }

    private static string FindServerRoot()
    {
        foreach (var origin in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(Path.GetFullPath(origin));
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "WydCdk.Engine.slnx")) &&
                    File.Exists(Path.Combine(current.FullName, "src", "WydCdk.Server", "WydCdk.Server.csproj")))
                    return current.FullName;
                current = current.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not find the Server checkout root for the startup tests.");
    }

    private static void DeleteTemporaryRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var tempPrefix = Path.EndsInDirectorySeparator(tempRoot) ? tempRoot : tempRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(fullPath))
            throw new InvalidOperationException("Startup test cleanup target is not a child directory of the system temp root.");
        Directory.Delete(fullPath, recursive: true);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
