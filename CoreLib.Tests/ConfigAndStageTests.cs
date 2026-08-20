using ProCycling.Core.Data;
using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class ConfigAndStageTests
{
    [Fact]
    public void RulesConfig_Default_SerializaYCargaEquivalente()
    {
        var cfg = RulesConfig.Default();
        string json = cfg.ToJson();
        Assert.False(string.IsNullOrWhiteSpace(json));

        var loaded = RulesConfig.FromJson(json);
        Assert.Equal(cfg.GvRef, loaded.GvRef);
        Assert.Equal(cfg.MaxFatiguePenalty, loaded.MaxFatiguePenalty);
        Assert.Equal(cfg.Profiles.Keys, loaded.Profiles.Keys);
        Assert.Equal(cfg.Profiles["sprint_massive"], loaded.Profiles["sprint_massive"]);
    }

    [Fact]
    public void PerfilDesconocido_CaeAFlatPorDefecto()
    {
        var cfg = RulesConfig.Default();
        Assert.Equal(1.0, cfg.WeightsFor("no_existe")["flat"]);
    }

    [Fact]
    public void EtapaJSON_SeDeserializaCorrectamente()
    {
        const string json = """
            {
              "id": "test_flat",
              "name": "Etapa de prueba",
              "season_id": 3,
              "date": "2026-07-01",
              "type": "flat",
              "distance_km": 197.0,
              "time_factor": 1.0,
              "tempo_modifier": 0.0,
              "sections": [
                { "km_from": 0, "km_to": 40, "terrains": ["flat"], "gradient": 0, "cobbles": false,
                  "wind": { "direction": "tail", "strength": 1 }, "finish": false },
                { "km_from": 40, "km_to": 197, "terrains": ["flat"], "gradient": 0,
                  "intermediate_sprint": { "km": 150, "points": [20,17,15] }, "finish": true }
              ],
              "climbs": []
            }
            """;

        var stage = StageJsonLoader.Load(json);
        Assert.Equal("test_flat", stage.Id);
        Assert.Equal(197.0, stage.DistanceKm);
        Assert.Equal(StageType.Flat, stage.Type);
        Assert.Equal(2, stage.Sections.Count);
        Assert.Equal(Terrain.Flat, stage.Sections[0].DominantTerrain);
        Assert.Equal(WindDirection.Tail, stage.Sections[0].Wind!.Direction);
        Assert.True(stage.Sections[1].Finish);
        Assert.Equal(3, stage.Sections[1].IntermediateSprint!.Points.Length);
    }

    [Fact]
    public void LosFicherosDeEtapaGenerados_SeCaganTodos()
    {
        string root = FindRepoRoot();
        string stagesDir = Path.Combine(root, "data", "stages");
        Assert.True(Directory.Exists(stagesDir), "No existe data/stages (ejecuta tools/import_data.py)");

        var stages = StageJsonLoader.LoadAllFromDirectory(stagesDir);
        Assert.True(stages.Count >= 21, $"Esperado ≥21 etapas (catálogo+Tour), obtenido {stages.Count}");

        var flat = stages.First(s => s.Type == StageType.Flat);
        Assert.True(flat.DistanceKm > 100);
        Assert.All(flat.Sections, s => Assert.True(s.KmTo >= s.KmFrom));
    }

    internal static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repo.");
    }
}