using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;

internal sealed record GuildWarFixture(E2eOptions Options, AccountHeader Header, string AccountPath);

internal static class GuildWarFixtureFactory
{
    private const int AccountFileLength = 7945;
    private const int AccountNameOffset = 0;
    private const int AccountPasswordOffset = 16;
    private const int NumericTokenOffset = 202;
    private const int CharactersOffset = 216;
    private const int CharacterStride = 816;
    private const int MobExtraOffset = 5600;
    private const int MobGuildOffset = 18;
    private const int MobClassOffset = 20;
    private const int MobCoinOffset = 28;
    private const int MobSavedPositionXOffset = 40;
    private const int MobBaseScoreOffset = 44;
    private const int MobCurrentScoreOffset = 92;
    private const int MobGuildLevelOffset = 800;

    public static async Task<GuildWarFixture> CreateAsync(
        string root,
        int port,
        ushort connectionId,
        string accountName,
        string password,
        string characterName,
        int guildId,
        int guildLevel,
        CancellationToken cancellationToken = default)
    {
        if (accountName.Length is < 3 or > 16 || accountName.Any(static value => !char.IsAsciiLetterOrDigit(value)))
            throw new ArgumentException("Synthetic account name must contain 3..16 ASCII letters or digits.", nameof(accountName));
        if (password.Length is < 1 or > 12 || password.Any(static value => !char.IsAsciiLetterOrDigit(value)))
            throw new ArgumentException("Synthetic account password must contain 1..12 ASCII letters or digits.", nameof(password));
        if (guildId is < 1 or > ushort.MaxValue || guildLevel is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(guildId));

        var normalizedAccount = accountName.ToUpperInvariant();
        var accountDirectory = Path.Combine(root, "accounts", normalizedAccount[..1]);
        Directory.CreateDirectory(accountDirectory);
        var accountPath = Path.Combine(accountDirectory, normalizedAccount);
        var account = new byte[AccountFileLength];
        WriteCString(account.AsSpan(AccountNameOffset, 16), normalizedAccount);
        WriteCString(account.AsSpan(AccountPasswordOffset, 12), password);
        WriteCString(account.AsSpan(NumericTokenOffset, 6), "123456");

        var mob = account.AsSpan(CharactersOffset, CharacterStride);
        WriteCString(mob[..16], characterName.ToUpperInvariant());
        BinaryPrimitives.WriteUInt16LittleEndian(mob[MobGuildOffset..], checked((ushort)guildId));
        mob[MobClassOffset] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(mob[MobCoinOffset..], 1000);
        BinaryPrimitives.WriteInt16LittleEndian(mob[MobSavedPositionXOffset..], 2000);
        BinaryPrimitives.WriteInt16LittleEndian(mob[(MobSavedPositionXOffset + 2)..], 2000);
        var score = new LegacyScore(100, 10, 10, 0, 0, 0, 0, 1000, 1000, 1000, 1000, 100, 100, 100, 100, 0, 0, 0, 0);
        score.Write(mob.Slice(MobBaseScoreOffset, LegacyScore.SizeInBytes));
        score.Write(mob.Slice(MobCurrentScoreOffset, LegacyScore.SizeInBytes));
        mob[MobGuildLevelOffset] = checked((byte)guildLevel);
        BinaryPrimitives.WriteInt16LittleEndian(account.AsSpan(MobExtraOffset), 2); // W2PP MOBEXTRA.ClassMaster = MORTAL.
        await File.WriteAllBytesAsync(accountPath, account, cancellationToken);

        var options = new E2eOptions(
            "127.0.0.1", port, accountPath, 0, connectionId, TimeSpan.FromSeconds(8),
            null, null, null, null, null, false,
            null, null, null, null,
            false, false, false, false, false);
        return new GuildWarFixture(options, new AccountHeader(normalizedAccount, password, Encoding.ASCII.GetBytes("123456")), accountPath);
    }

    private static void WriteCString(Span<byte> destination, string value)
    {
        destination.Clear();
        Encoding.ASCII.GetBytes(value.AsSpan(), destination);
    }
}
