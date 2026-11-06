using System.Globalization;
using System.Text;

namespace WydCdk.World;

/// <summary>Resolves global guild IDs using TMProject 7.69 Guilds.txt and fallback byte tables.</summary>
public sealed class LegacyGuildNameCatalog
{
    public const int ServerGroupCount = 10;
    public const int ServerCount = 16;
    public const int GuildsPerServer = 4096;
    public const int MaxNameBytes = 11;

    private static readonly byte[] GuildIndex =
    [
        0xA5, 0xB0, 0xA5, 0xB1, 0xA5, 0xB2, 0xA5, 0xB3, 0xA5, 0xB4, 0xA5, 0xB5, 0xA5, 0xB6, 0xA5, 0xB7,
        0xA5, 0xB8, 0xA5, 0xB9, 0xA3, 0xC1, 0xA3, 0xC2, 0xA3, 0xC3, 0xA3, 0xC4, 0xA3, 0xC5, 0xA3, 0xC6,
        0x00, 0x00, 0x00, 0x00,
    ];

    private static readonly byte[] GuildMark =
    [
        0xA5, 0xB0, 0xA5, 0xB1, 0xA5, 0xB2, 0xA5, 0xB3, 0xA5, 0xB4, 0xA5, 0xB5, 0xA5, 0xB6, 0xA5, 0xB7,
        0xA5, 0xB8, 0xA5, 0xB9, 0xA1, 0xE2, 0xA1, 0xE0, 0xA1, 0xDD, 0xA1, 0xD9, 0xA1, 0xBF, 0xA2, 0xB2,
        0xA1, 0xDE, 0xA6, 0xB6, 0xA1, 0xCC, 0xA5, 0xC8, 0xA2, 0xAF, 0xA1, 0xDB, 0xA2, 0xB7, 0xA2, 0xBF,
        0xA2, 0xC1, 0xA2, 0xC2, 0xA2, 0xC3, 0xA2, 0xC4, 0xA2, 0xD3, 0xA2, 0xD4, 0xA1, 0xC4, 0xA1, 0xD4,
        0xA1, 0xF3, 0xA2, 0xB3, 0xA8, 0xCD, 0xA3, 0xC1, 0xA5, 0xC6, 0xA5, 0xCE, 0xA2, 0xE7, 0xA2, 0xB9,
        0xA5, 0xE1, 0xA5, 0xE2, 0xA5, 0xE3, 0xA5, 0xE4, 0xA5, 0xE5, 0xA5, 0xE6, 0xA5, 0xE7, 0xA5, 0xE8,
        0xA5, 0xE9, 0xA5, 0xEA, 0xA5, 0xEB, 0xA5, 0xEC, 0xA5, 0xED, 0xA5, 0xEE, 0xA5, 0xEF, 0xA5, 0xF0,
        0xA5, 0xF1, 0xA5, 0xF2, 0xA5, 0xF3, 0xA5, 0xF4, 0xA5, 0xF5, 0xA5, 0xF6, 0xA5, 0xD7, 0xA5, 0xF8,
        0x00, 0x00, 0x00, 0x00,
    ];

    private readonly IReadOnlyDictionary<(int Group, int Server, int Guild), byte[]> names;

    private LegacyGuildNameCatalog(int serverGroup, IReadOnlyDictionary<(int Group, int Server, int Guild), byte[]> names)
    {
        if (serverGroup is < 0 or >= ServerGroupCount)
            throw new ArgumentOutOfRangeException(nameof(serverGroup), serverGroup, "ServerGroup must be between 0 and 9.");

        ServerGroup = serverGroup;
        this.names = names;
    }

    public int ServerGroup { get; }

    public static LegacyGuildNameCatalog FromLines(int serverGroup, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (serverGroup is < 0 or >= ServerGroupCount)
            throw new ArgumentOutOfRangeException(nameof(serverGroup), serverGroup, "ServerGroup must be between 0 and 9.");

        var names = new Dictionary<(int Group, int Server, int Guild), byte[]>();
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 4 ||
                !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var group) ||
                !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var server) ||
                !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guild) ||
                group is < 0 or >= ServerGroupCount || server is < 0 or >= ServerCount || guild is < 0 or >= GuildsPerServer)
            {
                throw new InvalidDataException($"Guilds.txt line {lineNumber} is invalid; expected '<group 0..9> <server 0..15> <guild 0..4095> <name>'.");
            }

            var rawName = Encoding.Latin1.GetBytes(fields[3]);
            if (rawName.Length == 0)
                throw new InvalidDataException($"Guilds.txt line {lineNumber} has an empty guild name.");

            var name = rawName.AsSpan(0, Math.Min(rawName.Length, MaxNameBytes)).ToArray();
            for (var index = 0; index < name.Length; index++)
                if (name[index] == (byte)'_')
                    name[index] = (byte)' ';

            names[(group, server, guild)] = name;
        }

        return new LegacyGuildNameCatalog(serverGroup, names);
    }

    public static async Task<LegacyGuildNameCatalog> LoadAsync(string? path, int serverGroup, CancellationToken cancellationToken = default)
    {
        if (serverGroup is < 0 or >= ServerGroupCount)
            throw new ArgumentOutOfRangeException(nameof(serverGroup), serverGroup, "ServerGroup must be between 0 and 9.");

        if (string.IsNullOrWhiteSpace(path))
            return FromLines(serverGroup, Array.Empty<string>());

        var lines = await File.ReadAllLinesAsync(path, Encoding.Latin1, cancellationToken).ConfigureAwait(false);
        return FromLines(serverGroup, lines);
    }

    public string GetGuildName(int guildId) => Encoding.Latin1.GetString(GetGuildNameBytes(guildId));

    public byte[] GetGuildNameBytes(int guildId)
    {
        if (guildId is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(guildId), guildId, "Guild ID must be between 0 and 65535.");

        var server = guildId / GuildsPerServer;
        var localGuild = guildId - (server * GuildsPerServer);
        if (names.TryGetValue((ServerGroup, server, localGuild), out var name))
            return name.ToArray();

        var markA = 2 * ((guildId / 64) & 63);
        var markB = 2 * (guildId & 63);
        return
        [
            GuildIndex[server * 2], GuildIndex[(server * 2) + 1], (byte)'-',
            GuildMark[markA], GuildMark[markA + 1], GuildMark[markB], GuildMark[markB + 1],
        ];
    }
}
