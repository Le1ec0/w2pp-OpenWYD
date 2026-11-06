namespace WydCdk.World;

public sealed class LegacyConnectionSlotAllocator
{
    public const int FirstPlayerConnectionId = 1;
    public const int LastPlayerConnectionId = 999;

    private readonly object _gate = new();
    private readonly bool[] _allocated = new bool[LastPlayerConnectionId + 1];
    private int _nextCandidate = FirstPlayerConnectionId;

    public bool TryAllocate(out int connectionId)
    {
        lock (_gate)
        {
            for (var inspected = 0; inspected < LastPlayerConnectionId; inspected++)
            {
                var candidate = _nextCandidate;
                _nextCandidate = candidate == LastPlayerConnectionId
                    ? FirstPlayerConnectionId
                    : candidate + 1;

                if (_allocated[candidate])
                    continue;

                _allocated[candidate] = true;
                connectionId = candidate;
                return true;
            }
        }

        connectionId = 0;
        return false;
    }

    public bool Release(int connectionId)
    {
        if (connectionId is < FirstPlayerConnectionId or > LastPlayerConnectionId)
            return false;

        lock (_gate)
        {
            if (!_allocated[connectionId])
                return false;

            _allocated[connectionId] = false;
            return true;
        }
    }
}
