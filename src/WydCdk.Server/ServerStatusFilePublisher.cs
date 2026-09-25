using System.Globalization;
using System.Text;

/// <summary>
/// Publishes one channel's status into the legacy ten-line servtest.htm file.
/// Line zero is the legacy control value; channels use lines one through nine.
/// </summary>
public sealed class ServerStatusFilePublisher
{
    public const int SlotCount = 10;
    private const string LegacySeparator = "\\n";

    private readonly string path;
    private readonly int slot;
    private readonly string heartbeatPath;

    public ServerStatusFilePublisher(string path, int slot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (slot is < 1 or >= SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Status slot must be between 1 and {SlotCount - 1}.");

        this.path = Path.GetFullPath(path);
        this.slot = slot;
        heartbeatPath = Path.Combine(Path.GetDirectoryName(this.path)!, ".wyd-status", $"slot-{slot}.heartbeat");
    }

    public void PublishOnline(int playerCount)
    {
        WriteHeartbeat();
        PublishValue(Math.Max(0, playerCount));
    }

    public void PublishOffline()
    {
        PublishValue(-1);
        if (File.Exists(heartbeatPath))
            File.Delete(heartbeatPath);
    }

    public string HeartbeatPath => heartbeatPath;

    private void WriteHeartbeat()
    {
        var directory = Path.GetDirectoryName(heartbeatPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = heartbeatPath + $".{Environment.ProcessId}.tmp";
        File.WriteAllText(temporaryPath, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n", new UTF8Encoding(false));
        File.Move(temporaryPath, heartbeatPath, true);
    }

    private void PublishValue(int value)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException($"Status file path has no directory: {path}");

        Directory.CreateDirectory(directory);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var fileLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                var values = ReadValues();
                values[slot] = value;

                var temporaryPath = path + $".{Environment.ProcessId}.tmp";
                File.WriteAllText(temporaryPath, string.Join(LegacySeparator, values) + LegacySeparator, new UTF8Encoding(false));
                File.Move(temporaryPath, path, true);
                return;
            }
            catch (IOException) when (attempt < 19)
            {
                Thread.Sleep(50);
            }
        }

        throw new IOException($"Could not acquire status file lock after retries: {path}.lock");
    }

    private int[] ReadValues()
    {
        if (!File.Exists(path))
            return DefaultValues();

        var raw = File.ReadAllText(path);
        var lines = raw.Contains(LegacySeparator, StringComparison.Ordinal)
            ? raw.Split(LegacySeparator, StringSplitOptions.RemoveEmptyEntries)
            : raw.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var values = Enumerable.Repeat(-1, SlotCount).ToArray();
        for (var index = 0; index < Math.Min(lines.Length, SlotCount); index++)
        {
            values[index] = int.TryParse(lines[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : -1;
        }

        values[0] = 1;

        return values;
    }

    private static int[] DefaultValues()
    {
        var values = Enumerable.Repeat(-1, SlotCount).ToArray();
        values[0] = 1;
        return values;
    }
}
