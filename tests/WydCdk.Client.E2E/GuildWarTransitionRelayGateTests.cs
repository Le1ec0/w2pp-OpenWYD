using WydCdk.Server;

internal static class GuildWarTransitionRelayGateTests
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var gate = new GuildWarTransitionRelayGate();
        var events = new List<string>();
        var firstRelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTransitionEntered = false;

        var first = RunFirstTransitionAndRelayAsync();
        await firstRelayStarted.Task.WaitAsync(cancellationToken);

        var second = RunSecondTransitionAndRelayAsync();
        Ensure(!secondTransitionEntered,
            "The second GuildWar transition entered before the first transition's relay batch completed.");

        releaseFirstRelay.TrySetResult();
        await Task.WhenAll(first, second);
        Ensure(events.SequenceEqual(new[]
        {
            "first-transition",
            "first-relay-start",
            "first-relay-end",
            "second-transition",
            "second-relay",
        }), "GuildWar transition and relay batches were not serialized.");

        Console.WriteLine("GUILD_WAR_RELAY_ORDER_PASS");
        return;

        async Task RunFirstTransitionAndRelayAsync()
        {
            using (await gate.AcquireAsync(cancellationToken))
            {
                events.Add("first-transition");
                events.Add("first-relay-start");
                firstRelayStarted.TrySetResult();
                await releaseFirstRelay.Task.WaitAsync(cancellationToken);
                events.Add("first-relay-end");
            }
        }

        async Task RunSecondTransitionAndRelayAsync()
        {
            using (await gate.AcquireAsync(cancellationToken))
            {
                secondTransitionEntered = true;
                events.Add("second-transition");
                events.Add("second-relay");
            }
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
