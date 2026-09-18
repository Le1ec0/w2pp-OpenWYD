using System.Threading.Channels;

namespace WydCdk.World;

/// <summary>Single-owner world loop. Network and persistence code submit commands; they never mutate state directly.</summary>
public sealed class WorldLoop
{
    private readonly Channel<IWorldCommand> commands = Channel.CreateUnbounded<IWorldCommand>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Dictionary<int, PlayerSession> sessions = [];
    public int SessionCount => sessions.Count;
    public ValueTask EnqueueAsync(IWorldCommand command, CancellationToken cancellationToken = default) => commands.Writer.WriteAsync(command, cancellationToken);
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var command in commands.Reader.ReadAllAsync(cancellationToken)) command.Apply(sessions);
    }
}

public sealed record PlayerSession(int ConnectionId, string AccountName);
public interface IWorldCommand { void Apply(IDictionary<int, PlayerSession> sessions); }
public sealed record OpenSession(int ConnectionId, string AccountName) : IWorldCommand { public void Apply(IDictionary<int, PlayerSession> sessions) => sessions.Add(ConnectionId, new PlayerSession(ConnectionId, AccountName)); }
public sealed record CloseSession(int ConnectionId) : IWorldCommand { public void Apply(IDictionary<int, PlayerSession> sessions) => sessions.Remove(ConnectionId); }
