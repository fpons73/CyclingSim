using System.Text.Json;

namespace ProCycling.Core.Simulation;

/// <summary>
/// Configuración parametrizable del motor. Todos los pesos y parámetros
/// se pueden ajustar sin recompilar (PRD §40): se cargan desde JSON.
/// </summary>
public sealed class RulesConfig
{
    // --- RNG / ruido ---
    public double RngNoiseRange { get; set; } = 0.06;      // ±6% de variación por azar
    public double RngNoiseCenter { get; set; } = 0.5;

    // --- Fatiga ---
    public double KmFatiguePerKm { get; set; } = 0.055;
    public double ElevationFatiguePer100m { get; set; } = 0.14;
    public double EffortFatigue { get; set; } = 2.2;         // por ataque/persecución/etc
    public double EnduranceMitigation { get; set; } = 0.45;
    public double ResistanceMitigation { get; set; } = 0.28;
    public double LongStageKmThreshold { get; set; } = 190;
    public double ResistanceHardStageBonus { get; set; } = 0.25;
    public double FatMax { get; set; } = 100;
    public double FatigueCurve { get; set; } = 2.0;          // exponente de la curva de penalización
    public double MaxFatiguePenalty { get; set; } = 0.35;    // -35% máximo por fatiga

    // --- Recuperación entre etapas ---
    public double RecoveryBase { get; set; } = 0.55;         // fracción mínima recuperada
    public double RecoveryStatFactor { get; set; } = 0.45;   // aporte del atributo REC

    // --- Group Value / velocidad ---
    public double GvRef { get; set; } = 65.0;                 // GV de referencia → velocidad base
    public double GvKmhPerPoint { get; set; } = 0.85;         // km/h por punto de GV
    public double CohesionDropThreshold { get; set; } = 7.0;  // stdev (en GV) sobre el que la cohesión cae a 0
    public double WorkingRidersBreakaway { get; set; } = 3;   // nº de corredores que "ruedan" en una fuga
    public double WorkingRidersPeloton { get; set; } = 6;     // nº de corredores activos del pelotón (tempo)

    public Dictionary<string, double> TerrainBaseSpeedKmh { get; set; } = new()
    {
        ["flat"] = 44.0, ["rolling"] = 40.0, ["hill"] = 37.0,
        ["climb"] = 29.0, ["descent"] = 52.0, ["cobbles"] = 35.5, ["tt"] = 46.0
    };

    // --- Pesos por tipo de acción (atributo → peso) ---
    public Dictionary<string, Dictionary<string, double>> Profiles { get; set; } = DefaultProfiles();

    public static RulesConfig Default() => new();

    private static Dictionary<string, Dictionary<string, double>> DefaultProfiles() => new()
    {
        ["sprint_massive"] = new() { ["sprint"] = 0.6, ["acceleration"] = 0.4 },
        ["sprint_reduced"] = new() { ["sprint"] = 0.4, ["acceleration"] = 0.35, ["attack"] = 0.15, ["flat"] = 0.1 },
        ["sprint_explosive"] = new() { ["acceleration"] = 0.45, ["hill"] = 0.3, ["sprint"] = 0.25 },
        ["climb_attack"] = new() { ["mountain"] = 0.5, ["attack"] = 0.3, ["acceleration"] = 0.2 },
        ["pace_check"] = new() { ["mountain"] = 0.65, ["endurance"] = 0.35 },
        ["breakaway_flat"] = new() { ["attack"] = 0.35, ["flat"] = 0.45, ["endurance"] = 0.2 },
        ["breakaway_climb"] = new() { ["attack"] = 0.3, ["mountain"] = 0.5, ["endurance"] = 0.2 },
        ["cobbles_effort"] = new() { ["cobbles"] = 0.5, ["flat"] = 0.3, ["endurance"] = 0.2 },
        ["crosswind_echelon"] = new() { ["flat"] = 0.6, ["endurance"] = 0.25, ["resistance"] = 0.15 },
        ["tt_long"] = new() { ["ttr"] = 0.55, ["endurance"] = 0.25, ["resistance"] = 0.2 },
        ["tt_short"] = new() { ["ttr"] = 0.7, ["acceleration"] = 0.15, ["endurance"] = 0.15 },
        ["prologue"] = new() { ["prl"] = 0.7, ["acceleration"] = 0.2, ["endurance"] = 0.1 },
        ["ttt_team"] = new() { ["ttr"] = 0.5, ["flat"] = 0.25, ["endurance"] = 0.15, ["resistance"] = 0.1 },
        ["descent"] = new() { ["descent"] = 0.7, ["acceleration"] = 0.3 },
        ["flat_tempo"] = new() { ["flat"] = 0.6, ["endurance"] = 0.4 },
        ["hill_effort"] = new() { ["hill"] = 0.5, ["mm"] = 0.3, ["endurance"] = 0.2 },
        ["mm_effort"] = new() { ["mm"] = 0.5, ["hill"] = 0.25, ["endurance"] = 0.25 }
    };

    public IReadOnlyDictionary<string, double> WeightsFor(string profileKey)
    {
        if (Profiles.TryGetValue(profileKey, out var w) && w is not null) return w;
        return new Dictionary<string, double> { ["flat"] = 1.0 };
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static RulesConfig FromJson(string json) =>
        JsonSerializer.Deserialize<RulesConfig>(json) ?? Default();

    public static RulesConfig LoadFile(string path) => FromJson(File.ReadAllText(path));
}