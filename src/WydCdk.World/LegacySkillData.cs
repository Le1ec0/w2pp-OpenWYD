using System.Globalization;

namespace WydCdk.World;

/// <summary>
/// One row from the legacy server's Common/SkillData.csv. The server-side
/// STRUCT_SPELL has one Act array; because the original sscanf writes both Act
/// columns into the same buffer, the second CSV action column is the effective
/// value used by the legacy server parser.
/// </summary>
public sealed record LegacySkillDefinition(
    int Id,
    int SkillPoint,
    int TargetType,
    int ManaSpent,
    int Delay,
    int Range,
    int InstanceType,
    int InstanceValue,
    int TickType,
    int TickValue,
    int AffectType,
    int AffectValue,
    int AffectTime,
    byte[] Action1,
    byte[] Action2,
    int InstanceAttribute,
    int TickAttribute,
    int Aggressive,
    int MaxTarget,
    int PartyCheck,
    int AffectResist,
    int Passive,
    int ClientForceDamage,
    string Name)
{
    /// <summary>Effective server action bytes after the legacy parser's buffer reuse.</summary>
    public ReadOnlySpan<byte> EffectiveActions => Action2;
}

/// <summary>Strict, deterministic loader for the legacy server skill table.</summary>
public sealed class LegacySkillDataTable
{
    public const int MaxSkillIndex = 248;

    private readonly LegacySkillDefinition?[] definitions;

    private LegacySkillDataTable(LegacySkillDefinition?[] definitions) => this.definitions = definitions;

    public int Count => definitions.Count(static definition => definition is not null);

    public LegacySkillDefinition? this[int skillId] =>
        skillId is >= 0 and < MaxSkillIndex ? definitions[skillId] : null;

    /// <summary>
    /// Loads the server CSV format used by BASE_InitializeSkill.
    /// Header/comment rows and out-of-range IDs are ignored like the legacy code;
    /// a malformed in-range row fails explicitly instead of silently producing a
    /// partially initialized spell definition.
    /// </summary>
    public static LegacySkillDataTable Load(TextReader reader, int maxSkillIndex = MaxSkillIndex)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (maxSkillIndex <= 0) throw new ArgumentOutOfRangeException(nameof(maxSkillIndex));

        var definitions = new LegacySkillDefinition?[maxSkillIndex];
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var fields = line.Split(',');
            if (fields.Length < 23 || !TryParseInt(fields[0], out var id)) continue;
            if (id < 0 || id >= maxSkillIndex) continue;

            var values = new int[20];
            for (var field = 1; field <= 12; field++)
                values[field - 1] = ParseInt(fields[field], lineNumber, field);

            for (var field = 15; field <= 21; field++)
                values[field - 3] = ParseInt(fields[field], lineNumber, field);

            // BASE_InitializeSkill stores AffectTime in server ticks, converting
            // the CSV value exactly as the legacy C++ loader does.
            values[11] /= 4;

            var action1 = ParseActions(fields[13], lineNumber);
            var action2 = ParseActions(fields[14], lineNumber);
            // The server CSV has no name column in the old 23-field variant.
            // The project base also carries the client-only ForceDamage field,
            // making 24 fields. The legacy server sscanf stops at Passive and
            // ignores that extra value; preserve it only for comparison/audit.
            var hasClientForceDamage = fields.Length >= 24;
            var clientForceDamage = hasClientForceDamage ? ParseInt(fields[22], lineNumber, 23) : 0;
            var name = fields[^1].Trim();
            definitions[id] = new LegacySkillDefinition(
                id,
                values[0], values[1], values[2], values[3], values[4], values[5], values[6],
                values[7], values[8], values[9], values[10], values[11],
                action1, action2,
                values[12], values[13], values[14], values[15], values[16], values[17], values[18],
                clientForceDamage,
                name);
        }

        return new LegacySkillDataTable(definitions);
    }

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static int ParseInt(string value, int lineNumber, int fieldNumber) =>
        TryParseInt(value, out var result)
            ? result
            : throw new FormatException($"Invalid integer in SkillData.csv line {lineNumber}, field {fieldNumber}.");

    private static byte[] ParseActions(string value, int lineNumber)
    {
        var actions = new byte[8];
        var parts = value.Trim().Split('.');
        for (var index = 0; index < Math.Min(parts.Length, 6); index++)
        {
            var parsed = ParseInt(parts[index], lineNumber, 14);
            actions[index] = unchecked((byte)parsed);
        }

        return actions;
    }
}
