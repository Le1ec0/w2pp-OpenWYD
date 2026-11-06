using WydCdk.Server;

internal static class LegacyConnectionTaskRegistryTests
{
    public static void Run()
    {
        DrainWaitsForEveryTrackedHandler().GetAwaiter().GetResult();
        DrainReportsFaultedHandler().GetAwaiter().GetResult();
    }

    private static async Task DrainWaitsForEveryTrackedHandler()
    {
        var registry = new LegacyConnectionTaskDrain();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        registry.Track(first.Task);
        registry.Track(second.Task);

        var drain = registry.StopAcceptingAndDrainAsync();
        Require(!drain.IsCompleted, "Shutdown drain returned while connection handlers were still running.");

        var rejectedLateHandler = false;
        try
        {
            registry.Track(Task.CompletedTask);
        }
        catch (InvalidOperationException)
        {
            rejectedLateHandler = true;
        }
        Require(rejectedLateHandler, "Shutdown registry accepted a connection handler after draining began.");

        first.SetResult();
        await Task.Yield();
        Require(!drain.IsCompleted, "Shutdown drain returned after only one of two handlers completed.");

        second.SetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task DrainReportsFaultedHandler()
    {
        var registry = new LegacyConnectionTaskDrain();
        registry.Track(Task.FromException(new InvalidOperationException("connection failure")));

        try
        {
            await registry.StopAcceptingAndDrainAsync();
            throw new InvalidOperationException("Shutdown drain hid a failed connection handler.");
        }
        catch (InvalidOperationException error) when (error.Message == "connection failure")
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
