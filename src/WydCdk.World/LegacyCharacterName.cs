namespace WydCdk.World;

/// <summary>
/// Character-name policy ported from TMSrv's <c>BASE_CheckValidString</c> (Basedef.cpp:2563) plus the reserved
/// command-name and device-name checks that <c>CFileDB::_MSG_DBCreateCharacter</c> repeats server-side
/// (CFileDB.cpp:901-924). The original's double-byte (Hangul) allowance for bytes with the high bit set has no
/// equivalent here: <see cref="Protocol.CreateCharacterRequest"/> already decodes the name as ASCII, so any
/// non-ASCII byte has already become <c>'?'</c> on the wire and is simply rejected below like any other
/// disallowed character.
/// </summary>
public static class LegacyCharacterName
{
    private const int MinLength = 4;
    private const int MaxLength = 15; // NAME_LENGTH - 1

    // BASE_CheckValidString: exact, case-sensitive matches against these literals (one, "Reino", is capitalized
    // in the original source; the rest are lowercase - not case-folded before the comparison).
    private static readonly string[] ClientReservedWords =
    [
        "Reino", "subcreate", "create", "gritar", "king", "kingdom", "getout", "gfame",
        "expulsar", "summonguild", "summon", "time", "relo", "stopally", "stopwar",
    ];

    // CFileDB::_MSG_DBCreateCharacter: comparison against these command names after uppercasing.
    private static readonly string[] ServerReservedCommandNames = ["KING", "KINGDOM", "GRITAR", "RELO"];

    public static bool IsValid(string name)
    {
        if (name.Length is < MinLength or > MaxLength) return false;
        if (Array.IndexOf(ClientReservedWords, name) >= 0) return false;

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-') return false;
        }

        var upper = name.ToUpperInvariant();
        return Array.IndexOf(ServerReservedCommandNames, upper) < 0 && !IsReservedDeviceName(upper);
    }

    private static bool IsReservedDeviceName(string upper) =>
        upper.Length == 4 && upper[3] is >= '0' and <= '9' &&
        ((upper[0] == 'C' && upper[1] == 'O' && upper[2] == 'M') || (upper[0] == 'L' && upper[1] == 'P' && upper[2] == 'T'));
}
