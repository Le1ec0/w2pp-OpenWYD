using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// Reads the four legacy DBSrv class-starting STRUCT_MOB templates: <c>g_pBaseSet</c> in Server.cpp,
/// loaded once at startup from <c>./BaseMob/TK|FM|BM|HT</c> for classes 0-3 respectively (Server.cpp:450-508).
/// Applies the same in-memory fixup Server.cpp performs right after loading each template
/// (<c>g_pBaseSet[i].BaseScore = g_pBaseSet[i].CurrentScore</c>): that fixup happens before any character is
/// created and is therefore baked into every character produced afterwards via
/// <c>memcpy(mob, &amp;g_pBaseSet[cls], sizeof(STRUCT_MOB))</c> in CFileDB.cpp's _MSG_DBCreateCharacter handler.
/// </summary>
public sealed class LegacyCharacterTemplateStore(string baseMobRoot)
{
    public const int ClassCount = 4;
    private static readonly string[] FileNames = ["TK", "FM", "BM", "HT"];
    private readonly string baseMobRoot = Path.GetFullPath(baseMobRoot);

    public async ValueTask<byte[]?> ReadTemplateAsync(int characterClass, CancellationToken cancellationToken = default)
    {
        if (characterClass < 0 || characterClass >= ClassCount) return null;

        var path = Path.Combine(baseMobRoot, FileNames[characterClass]);
        var buffer = new byte[LegacyAccountSnapshot.CharacterStride];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan);
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
                if (read == 0) return null;
                offset += read;
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        buffer.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset, LegacyScore.SizeInBytes)
            .CopyTo(buffer.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset, LegacyScore.SizeInBytes));

        return buffer;
    }
}
