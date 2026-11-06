using WydCdk.World;

internal static class LegacyAccountOperationGateTests
{
    public static async Task SameAccountReservationsAreExclusiveAndCaseCanonical()
    {
        var gate = new LegacyAccountOperationGate();
        await using var first = await gate.AcquireAsync(["alice"]);
        var attempting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contender = Task.Run(async () =>
        {
            attempting.SetResult();
            await using var second = await gate.AcquireAsync(["ALICE"]);
            acquired.SetResult();
        });

        await attempting.Task;
        var raced = await Task.WhenAny(acquired.Task, Task.Delay(TimeSpan.FromMilliseconds(100)));
        Assert(!ReferenceEquals(raced, acquired.Task), "Case variants of one account were not serialized.");
        await first.DisposeAsync();
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await contender;
    }

    public static async Task MultiAccountReservationsAreSortedAndDeduplicated()
    {
        var gate = new LegacyAccountOperationGate();
        await using var first = await gate.AcquireAsync(["BETA", "ALPHA", "ALPHA"]);
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contender = Task.Run(async () =>
        {
            await using var second = await gate.AcquireAsync(["alpha", "beta"]);
            acquired.SetResult();
        });

        var raced = await Task.WhenAny(acquired.Task, Task.Delay(TimeSpan.FromMilliseconds(100)));
        Assert(!ReferenceEquals(raced, acquired.Task), "A duplicate or differently ordered account set bypassed the lease.");
        await first.DisposeAsync();
        await acquired.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await contender;
    }

    public static async Task CancellationAndDisposalReleaseReservations()
    {
        var gate = new LegacyAccountOperationGate();
        await using var held = await gate.AcquireAsync(["BETA"]);
        using var cancellation = new CancellationTokenSource();
        var cancelled = gate.AcquireAsync(["ALPHA", "BETA"], cancellation.Token).AsTask();
        await Task.Yield();
        cancellation.Cancel();
        try
        {
            await cancelled.WaitAsync(TimeSpan.FromSeconds(2));
            throw new InvalidOperationException("A cancelled multi-account wait unexpectedly acquired a lease.");
        }
        catch (OperationCanceledException)
        {
        }

        await using var alpha = await gate.AcquireAsync(["ALPHA"]).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        await held.DisposeAsync();
        await alpha.DisposeAsync();

        await using var reacquired = await gate.AcquireAsync(["ALPHA", "BETA"]).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    public static async Task RecoveryBarrierBlocksUntilResolved()
    {
        var gate = new LegacyAccountOperationGate();
        await using (var held = await gate.AcquireAsync(["ALPHA"]))
            gate.MarkRecoveryRequired("alpha", "uncertain Cargo CAS");

        Assert(gate.IsRecoveryRequired("ALPHA"), "The account recovery barrier did not canonicalize its account key.");
        try
        {
            await gate.AcquireAsync(["alpha"]);
            throw new InvalidOperationException("The account operation gate allowed a quarantined account operation.");
        }
        catch (LegacyAccountRecoveryRequiredException)
        {
        }

        Assert(gate.ResolveRecovery("ALPHA") && !gate.IsRecoveryRequired("alpha"),
            "The authoritative recovery barrier did not clear after reconciliation.");
        await using var reacquired = await gate.AcquireAsync(["ALPHA"]);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
