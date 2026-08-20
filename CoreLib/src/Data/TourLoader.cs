using System.Text.Json;
using System.Text.Json.Serialization;
using ProCycling.Core.Models;

namespace ProCycling.Core.Data;

/// <summary>
/// Carga un Tour desde su manifiesto (grande_boucle_2026.json) expandiendo las
/// referencias de etapas (stage_refs) a objetos <see cref="Stage"/> completos.
/// </summary>
public static class TourLoader
{
    private sealed class Manifest
    {
        public string? Tour { get; set; }
        [JsonPropertyName("stage_refs")]
        public List<string>? StageRefs { get; set; }
    }

    public static (string Name, List<Stage> Stages) Load(IReadOnlyDictionary<string, Stage> index, string manifestPath)
    {
        string json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<Manifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var name = manifest?.Tour ?? "Tour";
        var stages = new List<Stage>();
        if (manifest?.StageRefs is not null)
        {
            foreach (var refId in manifest.StageRefs)
            {
                if (index.TryGetValue(refId.Trim(), out var stage))
                    stages.Add(stage);
                else
                    throw new InvalidOperationException($"Referencia de etapa no encontrada: {refId}");
            }
        }
        return (name, stages);
    }

    public static (string Name, List<Stage> Stages) Load(string manifestPath)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? ".";
        var index = StageJsonLoader.LoadAllFromDirectory(dir).ToDictionary(s => s.Id);
        return Load(index, manifestPath);
    }
}