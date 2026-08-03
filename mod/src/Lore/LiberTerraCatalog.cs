using System.Text.Json;
using Vintagestory.API.Common;

namespace LiberTerra.Lore;

public sealed class LiberTerraCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LiberTerraCatalog(IReadOnlyList<LiberTerraWork> works)
    {
        Works = works;
        ByCode = works.ToDictionary(work => work.Code, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<LiberTerraWork> Works { get; }

    public IReadOnlyDictionary<string, LiberTerraWork> ByCode { get; }

    public static LiberTerraCatalog Load(ICoreAPI api, string assetLocation)
    {
        var asset = api.Assets.TryGet(assetLocation)
            ?? throw new FileNotFoundException($"Missing Liber Terra catalog asset: {assetLocation}");
        var dto = JsonSerializer.Deserialize<CatalogDto>(asset.ToText(), JsonOptions)
            ?? throw new InvalidDataException($"Invalid Liber Terra catalog JSON: {assetLocation}");
        var works = (dto.Works ?? [])
            .Select(entry => new LiberTerraWork(
                entry.Code ?? throw new InvalidDataException("Work missing code"),
                entry.BaseCode ?? entry.Code!,
                entry.Title ?? entry.Code!,
                entry.Group ?? "unknown",
                entry.GutenbergId,
                entry.Volume,
                entry.VolumeCount,
                entry.PieceCount,
                entry.TitleCode ?? $"liberterra:lore-{entry.Code}-title",
                entry.TextCodes ?? []))
            .ToList();
        return new LiberTerraCatalog(works);
    }

    private sealed class CatalogDto
    {
        public int Version { get; set; }
        public List<WorkDto>? Works { get; set; }
    }

    private sealed class WorkDto
    {
        public string? Code { get; set; }
        public string? BaseCode { get; set; }
        public string? Title { get; set; }
        public string? Group { get; set; }
        public int GutenbergId { get; set; }
        public int Volume { get; set; }
        public int VolumeCount { get; set; }
        public int PieceCount { get; set; }
        public string? TitleCode { get; set; }
        public List<string>? TextCodes { get; set; }
    }
}

public sealed record LiberTerraWork(
    string Code,
    string BaseCode,
    string Title,
    string Group,
    int GutenbergId,
    int Volume,
    int VolumeCount,
    int PieceCount,
    string TitleCode,
    IReadOnlyList<string> TextCodes);
