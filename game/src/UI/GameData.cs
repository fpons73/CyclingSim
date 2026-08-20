using Godot;
using ProCycling.Core.Data;
using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Game.UI;

/// <summary>
/// Servicio de datos del juego: carga SQLite + catálogo de etapas (res://data/),
/// construye un RaceState y ejecuta el simulador de etapa llana. Verificable headless.
/// </summary>
public static class GameData
{
    public static List<Rider>? Riders;
    public static List<Team>? Teams;
    public static List<Stage>? Stages;
    public static Dictionary<int, Rider> RidersById = new();

    public static bool Load(string dataDir)
    {
        string dbPath = ProjectSettings.GlobalizePath($"{dataDir}/pcrm.sqlite");
        if (!System.IO.File.Exists(dbPath))
        {
            GD.PrintErr($"[GameData] No existe {dbPath}");
            return false;
        }

        (Teams, Riders) = SqliteStore.LoadSeason(dbPath, 3);
        RidersById = Riders.ToDictionary(r => r.Id);

        // Etapas individuales (json en data/stages/ en el proyecto real).
        string stagesDir = ProjectSettings.GlobalizePath($"{dataDir}/stages");
        Stages = System.IO.Directory.Exists(stagesDir)
            ? StageJsonLoader.LoadAllFromDirectory(stagesDir)
            : LoadStagesFromRes(dataDir);

        GD.Print($"[GameData] Cargados {Riders.Count} corredores, {Teams.Count} equipos, {Stages.Count} etapas.");
        return true;
    }

    private static List<Stage> LoadStagesFromRes(string dataDir)
    {
        var list = new List<Stage>();
        foreach (var f in new[]
        {
            "flat_01_2026.json", "flat_02_2026.json", "flat_03_2026.json",
            "flat_04_2026.json", "flat_05_2026.json", "medium_mountain_01_2026.json",
            "mountain_01_2026.json", "itt_01_2026.json", "prologue_01_2026.json"
        })
        {
            using var file = Godot.FileAccess.Open($"{dataDir}/{f}", Godot.FileAccess.ModeFlags.Read);
            if (file is null) continue;
            try
            {
                list.Add(StageJsonLoader.Load(file.GetAsText()));
            }
            catch (System.Text.Json.JsonException) { }
        }
        return list;
    }

    // --- Tour Mode ---
    public static List<Stage>? TourStages;
    public static string TourName = string.Empty;

    /// <summary>Carga la Grande Boucle (manifiesto + etapas) desde el data dir.</summary>
    public static bool LoadTour(string dataDir)
    {
        string dir = ProjectSettings.GlobalizePath(dataDir);
        string manifest = System.IO.Path.Combine(dir, "grande_boucle_2026.json");
        if (!System.IO.File.Exists(manifest)) return false;

        var index = StageJsonLoader.LoadAllFromDirectory(dir)
            .Where(s => !string.IsNullOrEmpty(s.Id) && s.Sections.Count > 0)
            .ToDictionary(s => s.Id);
        try
        {
            (TourName, TourStages) = TourLoader.Load(index, manifest);
            GD.Print($"[TourLoader] {TourName}: {TourStages.Count} etapas cargadas.");
            return true;
        }
        catch (System.InvalidOperationException e)
        {
            GD.PrintErr($"[TourLoader] {e.Message}");
            return false;
        }
    }

    public static List<StageResultRider>? RunTour(List<Stage> stages, List<Team> teams, List<Rider> riders, ulong seed)
    {
        var tour = new TourSimulator(RulesConfig.Default(), seed);
        var classifications = tour.Run(stages, teams, riders);
        return classifications
            .GcStandings()
            .OrderBy(c => c.GcSeconds)
            .Select(c => new StageResultRider(c.RiderId, 0, c.GcSeconds, c.Points, c.KoMPoints, c.IsYoung))
            .ToList();
    }

    /// <summary>Selecciona un pelotón manejable: N equipos (preferencia WorldTour) × sus corredores.</summary>
    public static (uint Count, List<Team> Teams, List<Rider> Riders) BuildStartList(int teamCount, int perTeam = 8)
    {
        if (Teams is null || Riders is null) return (0, new List<Team>(), new List<Rider>());

        var byCategory = Teams
            .OrderByDescending(t => t.Category == "WorldTour")
            .ThenBy(t => t.Name)
            .Take(Math.Min(teamCount, Teams.Count))
            .ToList();

        var selectedRiders = new List<Rider>();
        foreach (var team in byCategory)
        {
            var roster = Riders.Where(r => r.TeamId == team.Id)
                .OrderByDescending(r => r.Attributes.Sprint)
                .Take(perTeam)
                .ToList();
            selectedRiders.AddRange(roster);
        }
        return ((uint)selectedRiders.Count, byCategory, selectedRiders);
    }

    public static RaceState NewRace(Stage stage, List<Team> teams, List<Rider> riders,
        RulesConfig cfg, ulong seed)
    {
        var state = RaceSetup.Create(stage, teams, riders, seed);
        state.Riders = riders.ToDictionary(r => r.Id);
        return state;
    }

    public static List<StageResultRider> RunFlat(RaceState state, ulong seed)
    {
        var sim = new FlatStageSimulator(RulesConfig.Default(), seed);
        return sim.Run(state);
    }

    public static RulesConfig LoadConfig()
    {
        string configPath = ProjectSettings.GlobalizePath("res://data/rules.json");
        return System.IO.File.Exists(configPath)
            ? RulesConfig.LoadFile(configPath)
            : RulesConfig.Default();
    }
}