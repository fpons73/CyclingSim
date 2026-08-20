using System.Text.Json;
using ProCycling.Core.Models;

namespace ProCycling.Core.Data;

/// <summary>Carga de etapas desde JSON/CSV sin recompilar (PRD §28, §3).</summary>
public static class StageJsonLoader
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Stage Load(string json) =>
        JsonSerializer.Deserialize<Stage>(json, Opts)
        ?? throw new InvalidOperationException("No se pudo deserializar la etapa JSON.");

    public static Stage LoadFile(string path) => Load(File.ReadAllText(path));

    public static List<Stage> LoadAllFromDirectory(string dir)
    {
        var stages = new List<Stage>();
        if (!Directory.Exists(dir)) return stages;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                stages.Add(LoadFile(file));
            }
            catch (JsonException)
            {
                // se ignoran ficheros no relacionados (p. ej. grande_boucle_2026.json)
            }
        }
        return stages;
    }
}