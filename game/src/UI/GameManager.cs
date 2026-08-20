using Godot;
using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Game.UI;

/// <summary>
/// Estado global de la partida: datos cargados, start list, etapa activa,
/// estado de carrera (RaceState), resultados y exportación.
/// </summary>
public static class GameManager
{
    public static bool DataLoaded;
    public static Stage? Stage;
    public static List<Team> SelectedTeams = new();
    public static List<Rider> SelectedRiders = new();
    public static ulong Seed = 42;
    public static RulesConfig Config = RulesConfig.Default();

    public static RaceState? State;
    public static List<StageResultRider>? Results;

    public static bool LoadData()
    {
        bool ok = GameData.Load("res://data");
        DataLoaded = ok;
        return ok;
    }

    public static bool PrepareRace(string stageId, int teamCount, ulong seed)
    {
        Stage = GameData.Stages?.FirstOrDefault(s => s.Id == stageId);
        if (Stage is null) return false;

        Seed = seed;
        var (_, teams, riders) = GameData.BuildStartList(teamCount);
        SelectedTeams = teams;
        SelectedRiders = riders;

        State = RaceSetup.Create(Stage, teams, riders, seed);
        return true;
    }

    public static void RunRace()
    {
        if (State is null) return;
        Results = GameData.RunFlat(State, Seed);
    }

    public static List<string> Classifications()
    {
        if (Results is null || State is null) return new List<string> { "Sin resultados." };
        var rows = new List<string>
        {
            "GENERAL (Tiempo por etapa) — top 10",
            "Pos | Corredor | Equipo | Tiempo | Puntos"
        };
        foreach (var r in Results.OrderBy(r => r.StageSeconds).Take(10))
            rows.Add($"{Results.OrderBy(x => x.StageSeconds).ToList().IndexOf(r) + 1} | {RiderName(r.RiderId)} | {TeamName(r.TeamId)} | {RiderCard.FormatTime(r.StageSeconds)} | {r.PointsEarned}");

        rows.Add("");
        rows.Add("GANADOR: " + RiderName(Results.OrderBy(r => r.StageSeconds).First().RiderId));
        return rows;
    }

    public static string RiderName(int riderId) =>
        State is not null && State.Riders.TryGetValue(riderId, out var r) && !string.IsNullOrEmpty(r.Name)
            ? r.Name : $"#{riderId}";

    public static string TeamName(int teamId) =>
        State is not null && State.Teams.TryGetValue(teamId, out var t) && !string.IsNullOrEmpty(t.Name)
            ? t.Name : $"Equipo {teamId}";
}
