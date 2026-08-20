namespace ProCycling.Core.Models;

/// <summary>Estado completo y serializable de una carrera (usado también para guardado).</summary>
public sealed class RaceState
{
    public ulong Seed { get; set; }
    public string StageId { get; set; } = string.Empty;
    public Stage? Stage { get; set; }
    public int SeasonYear { get; set; } = 2026;

    public Dictionary<int, Team> Teams { get; set; } = new();
    public Dictionary<int, Rider> Riders { get; set; } = new();
    public List<RiderState> RiderStates { get; set; } = new();

    public List<RiderGroup> Groups { get; set; } = new();
    public Classifications Classifications { get; set; } = new();

    public int CurrentSectionIndex { get; set; }
    public double KmCovered { get; set; }
    public List<string> ActionLog { get; set; } = new();

    /// <summary>Snapshot del estado del RNG (4×ulong) para reproducibilidad exacta.</summary>
    public ulong[] RngState { get; set; } = new ulong[4];
}