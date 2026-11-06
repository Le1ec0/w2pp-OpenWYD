namespace WydCdk.Server;

/// <summary>Tracks accepted connection handlers until the Host stops accepting and drains them.</summary>
public sealed class LegacyConnectionTaskDrain
{
    private readonly object gate = new();
    private readonly List<Task> tasks = [];
    private bool accepting = true;
    private Task? drainTask;

    public void Track(Task connectionTask)
    {
        ArgumentNullException.ThrowIfNull(connectionTask);
        lock (gate)
        {
            if (!accepting)
                throw new InvalidOperationException("The Host is no longer accepting connection tasks.");

            tasks.RemoveAll(static task => task.Status == TaskStatus.RanToCompletion);
            tasks.Add(connectionTask);
        }
    }

    public Task StopAcceptingAndDrainAsync()
    {
        lock (gate)
        {
            if (drainTask is not null)
                return drainTask;

            accepting = false;
            tasks.RemoveAll(static task => task.Status == TaskStatus.RanToCompletion);
            drainTask = Task.WhenAll(tasks.ToArray());
            return drainTask;
        }
    }
}
