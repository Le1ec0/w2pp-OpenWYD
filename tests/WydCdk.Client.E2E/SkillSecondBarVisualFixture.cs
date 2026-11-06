using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class SkillSecondBarVisualFixture
{
    private const string FixtureDirectoryName = "SkillSecondBarVisual";
    private const string BloodHandsFixtureDirectoryName = "SkillSecondBarBloodHandsBook";
    private const ushort FixtureConnectionId = 11;
    private const string AccountName = "teste";
    private const string Password = "teste";
    private const string CharacterName = "SKILLBAR";
    private const int FixtureServerPort = 18_384;
    private const int FixtureGuildId = 1;
    private const int FixtureCharacterSlot = 0;
    private const int FixtureCharacterClass = 0;
    private const int FixtureCharacterLevel = 100;
    private const int NumericPasswordOffset = 202;
    private const int NumericPasswordLength = 6;

    public static async Task PrepareAsync(string fixtureRoot, uint baseLearnedSkill, uint secondaryLearnedSkill, CancellationToken cancellationToken = default)
    {
        var checkoutRoot = FindCheckoutRoot();
        EnsureInsideDedicatedBuildDirectory(checkoutRoot, fixtureRoot, FixtureDirectoryName);
        if (Directory.Exists(fixtureRoot) && Directory.EnumerateFileSystemEntries(fixtureRoot).Any())
            throw new IOException($"Refusing to overwrite the non-empty second-bar fixture directory: {fixtureRoot}");

        Directory.CreateDirectory(fixtureRoot);
        var fixture = await GuildWarFixtureFactory.CreateAsync(
            fixtureRoot, FixtureServerPort, FixtureConnectionId, AccountName, Password, CharacterName, guildId: FixtureGuildId, guildLevel: 0, cancellationToken);
        var account = await File.ReadAllBytesAsync(fixture.AccountPath, cancellationToken);
        Array.Clear(account, NumericPasswordOffset, NumericPasswordLength);
        System.Text.Encoding.ASCII.GetBytes("0000").CopyTo(account, NumericPasswordOffset);
        var mobOffset = LegacyAccountSnapshot.CharactersOffset;
        var mobExtraOffset = LegacyAccountSnapshot.MobExtraOffset;
        BinaryPrimitives.WriteUInt32LittleEndian(
            account.AsSpan(mobOffset + LegacyAccountSnapshot.MobLearnedSkillOffset), baseLearnedSkill);
        BinaryPrimitives.WriteUInt32LittleEndian(
            account.AsSpan(mobExtraOffset + LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset), secondaryLearnedSkill);
        await File.WriteAllBytesAsync(fixture.AccountPath, account, cancellationToken);
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "review"));

        // Keep every trial on the same valid guild spawn. The synthetic character is level 100,
        // above the local FREEEXP=35 training-area gate; this fixture does not exercise newbie entry.
        var guildData = new int[]
        {
            1,
            FixtureGuildId, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            10, 10, 10, 10, 10,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
        };
        var guildDataPath = Path.Combine(fixtureRoot, "Guild.txt");
        await File.WriteAllTextAsync(guildDataPath, string.Join(" ", guildData), cancellationToken);

        Console.WriteLine(
            $"SKILL_SECOND_BAR_FIXTURE_READY root={fixtureRoot} account={AccountName.ToUpperInvariant()} character={CharacterName} slot={FixtureCharacterSlot} class={FixtureCharacterClass} level={FixtureCharacterLevel} host=127.0.0.1:{FixtureServerPort} base=0x{baseLearnedSkill:X8} mobextra-sec-learned-skill=0x{secondaryLearnedSkill:X8} word1=wire-proxy-only position-mode=charged-guild-zone-0 guild-data=\"{guildDataPath}\" credentials=local-test-only");
    }

    public static async Task PrepareBloodHandsBookAsync(string fixtureRoot, CancellationToken cancellationToken = default)
    {
        var checkoutRoot = FindCheckoutRoot();
        EnsureInsideDedicatedBuildDirectory(checkoutRoot, fixtureRoot, BloodHandsFixtureDirectoryName);
        if (Directory.Exists(fixtureRoot) && Directory.EnumerateFileSystemEntries(fixtureRoot).Any())
            throw new IOException($"Refusing to overwrite the non-empty Blood Hands book fixture directory: {fixtureRoot}");

        Directory.CreateDirectory(fixtureRoot);
        var fixture = await GuildWarFixtureFactory.CreateAsync(
            fixtureRoot, FixtureServerPort, FixtureConnectionId, AccountName, Password, CharacterName, guildId: FixtureGuildId, guildLevel: 0, cancellationToken);
        var account = await File.ReadAllBytesAsync(fixture.AccountPath, cancellationToken);
        Array.Clear(account, NumericPasswordOffset, NumericPasswordLength);
        Encoding.ASCII.GetBytes("0000").CopyTo(account, NumericPasswordOffset);

        var mobOffset = LegacyAccountSnapshot.CharactersOffset;
        var mobExtraOffset = LegacyAccountSnapshot.MobExtraOffset;
        BinaryPrimitives.WriteInt32LittleEndian(account.AsSpan(mobOffset + LegacyAccountSnapshot.MobBaseScoreOffset), 200);
        BinaryPrimitives.WriteInt32LittleEndian(account.AsSpan(mobOffset + LegacyAccountSnapshot.MobCurrentScoreOffset), 200);
        account[mobOffset + LegacyAccountSnapshot.MobClassOffset] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(
            account.AsSpan(mobOffset + LegacyAccountSnapshot.MobLearnedSkillOffset), 1u << 15);
        BinaryPrimitives.WriteInt16LittleEndian(
            account.AsSpan(mobExtraOffset + LegacyAccountSnapshot.MobExtraClassMasterOffset), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(
            account.AsSpan(mobExtraOffset + LegacyAccountSnapshot.MobExtraSecondaryLearnedSkillOffset), 0);
        new LegacyItem(5504, 61, 1, 0, 0, 0, 0).Write(
            account.AsSpan(mobOffset + LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
        await File.WriteAllBytesAsync(fixture.AccountPath, account, cancellationToken);
        Directory.CreateDirectory(Path.Combine(fixtureRoot, "review"));

        var guildData = new int[]
        {
            1,
            FixtureGuildId, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            10, 10, 10, 10, 10,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
        };
        var guildDataPath = Path.Combine(fixtureRoot, "Guild.txt");
        await File.WriteAllTextAsync(guildDataPath, string.Join(" ", guildData), cancellationToken);

        Console.WriteLine(
            $"SKILL_SECOND_BAR_BLOOD_HANDS_FIXTURE_READY root={fixtureRoot} account={AccountName.ToUpperInvariant()} character={CharacterName} slot={FixtureCharacterSlot} class=Trans class-master=Celestial level=200 host=127.0.0.1:{FixtureServerPort} base-learned=0x00008000 mobextra-sec-learned-skill=0x00000000 book=5504 inventory-slot=0 credentials=local-test-only guild-data=\"{guildDataPath}\"");
    }

    public static uint ParseUInt32(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parsed = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var hexadecimal)
            : uint.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out hexadecimal);
        return parsed ? hexadecimal : throw new ArgumentException($"Expected a decimal or 0x-prefixed uint32 value, received '{value}'.", nameof(value));
    }

    private static string FindCheckoutRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WydCdk.Engine.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the WYD CDK checkout root from the E2E output directory.");
    }

    private static void EnsureInsideDedicatedBuildDirectory(string checkoutRoot, string path, string fixtureDirectoryName)
    {
        var workspaceRoot = Directory.GetParent(checkoutRoot)?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the workspace root above the Server checkout.");
        var buildRoot = Path.GetFullPath(Path.Combine(workspaceRoot, "Backup", "Build"));
        var fullPath = Path.GetFullPath(path);
        var prefix = Path.EndsInDirectorySeparator(buildRoot) ? buildRoot : buildRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullPath).Equals(fixtureDirectoryName, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Refusing to prepare the fixture outside its dedicated Backup/Build/{fixtureDirectoryName} directory: {fullPath}.");
    }
}
