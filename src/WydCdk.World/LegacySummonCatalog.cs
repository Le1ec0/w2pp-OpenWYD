using WydCdk.Protocol;

namespace WydCdk.World;

/// <summary>
/// The 50 summon slots used by <c>CNPCSummon::Initialize</c>. The reference
/// release populates the first 40 slots; the remaining slots stay zeroed in
/// the legacy global array and are therefore intentionally unavailable here.
/// </summary>
public sealed class LegacySummonCatalog
{
    public const int SlotCount = 50;
    public const int LoadedSlotCount = 40;

    private static readonly string[] FileNames =
    [
        "Condor", "Javali", "Lobo", "Urso", "Tigre", "Gorila", "Dragao_Negro", "Succubus", "Porco", "Javali_",
        "Porco", "Javali", "Lobo", "Dragao_Menor", "Urso", "Dente_de_Sabre", "Sem_Sela_N", "Fantasma_N", "Leve_N", "Equip_N",
        "Andaluz_N", "Sem_Sela_B", "Fantasma_B", "Leve_B", "Equip_B", "Andaluz_B", "Fenrir", "Dragao", "FenrirSombra", "Tigre_de_Fogo",
        "Dragao_Vermelho", "Unicornio", "Pegasus", "Unisus", "Grifo", "Hipogrifo", "Grifo_Sangrento", "Svadilfire", "Sleipnir", "Pantera_Negra",
    ];

    // STRUCT_BEASTBONUS fields Unk..Unk6 from Basedef.cpp. Only these six
    // values participate in GenerateSummon; the rest of each C++ row is not
    // part of this path.
    private static readonly LegacySummonBonus[] Bonuses =
    [
        new(80, 300, 50, 75, 100, 400), new(80, 250, 50, 150, 125, 400), new(80, 400, 50, 125, 125, 400), new(80, 350, 50, 200, 150, 400),
        new(80, 500, 50, 175, 150, 400), new(80, 450, 50, 250, 175, 400), new(100, 500, 50, 250, 174, 400), new(130, 250, 60, 200, 180, 250),
    ];

    private readonly LegacySummonTemplate?[] templates;

    private LegacySummonCatalog(LegacySummonTemplate?[] templates) => this.templates = templates;

    public static LegacySummonCatalog Load(string baseSummonRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseSummonRoot);
        var root = Path.GetFullPath(baseSummonRoot);
        var templates = new LegacySummonTemplate?[SlotCount];
        for (var index = 0; index < LoadedSlotCount; index++)
        {
            var path = Path.Combine(root, FileNames[index]);
            var mob = File.ReadAllBytes(path);
            if (mob.Length != LegacyAccountSnapshot.CharacterStride)
                throw new InvalidDataException($"BaseSummon '{path}' must contain exactly {LegacyAccountSnapshot.CharacterStride} bytes.");

            templates[index] = new LegacySummonTemplate(index, FileNames[index], mob);
        }

        return new LegacySummonCatalog(templates);
    }

    public bool TryGet(int summonId, out LegacySummonTemplate? template)
    {
        template = null;
        if (summonId < 0 || summonId >= templates.Length) return false;
        template = templates[summonId];
        return template is not null;
    }

    public LegacySummonBonus GetBonus(int summonId) => summonId >= 0 && summonId < Bonuses.Length ? Bonuses[summonId] : default;
}

public sealed record LegacySummonTemplate(int Id, string SourceName, byte[] MobSnapshot);

public readonly record struct LegacySummonBonus(int MinDamage, int MaxDamage, int MinAc, int MaxAc, int MinHp, int MaxHp);
