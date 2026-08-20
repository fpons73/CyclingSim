using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

public enum DirectorMode
{
    Player,     // las decisiones del equipo del jugador pausan y muestran opciones
    Assistant,  // sugerencias, requerimiento táctico es manual
    Directed    // auto-ejecución de todas las decisiones (PRD §23)
}

public enum ChaseIntensity
{
    None,   // no se persigue (la fuga puede crecer / aguantar)
    Light,  // ritmo normal
    Strong  // equipos de sprinter/GC persiguen con dureza
}

/// <summary>Decisión táctica emitida por el motor (IA). Log del "por qué" (PRD §21).</summary>
public sealed record RaceDecision(string Kind, int? TeamId, string Reason);

/// <summary>
/// IA básica del pelotón (PRD §21, §23): decide qué equipos persiguen la fuga
/// según la composición del pelotón (equipos con sprinter fuerte persiguen
/// hasta el final; equipos de GC dejan crecer el gap para finales duros).
/// Director Mode: las decisiones se ejecutan automáticamente y se registran.
/// </summary>
public sealed class RaceDecisionEngine
{
    private readonly RulesConfig _cfg;
    private readonly Dictionary<int, Team> _teams;
    private readonly Dictionary<int, Rider> _riders;
    private readonly Dictionary<int, RiderState> _states;

    public DirectorMode Mode { get; }

    public RaceDecisionEngine(RulesConfig cfg, RaceState state, DirectorMode mode = DirectorMode.Directed)
    {
        _cfg = cfg;
        Mode = mode;
        _teams = state.Teams;
        _riders = state.Riders;
        _states = state.RiderStates.ToDictionary(s => s.RiderId);
    }

    /// <summary>Intensidad de persecución según el estado de carrera.</summary>
    public ChaseIntensity DecideChase(RiderGroup? breakaway, double kmFront, double stageKm)
    {
        if (breakaway is null) return ChaseIntensity.None;
        if (breakaway.MemberRiderIds.Count == 0) return ChaseIntensity.None;

        double kmToFinish = Math.Max(0, stageKm - kmFront);
        double gap = breakaway.GapSeconds;

        // Sprinters fuertes en el pelotón → perseguir duro en los últimos 50 km.
        bool hasSprinter = StrongestSprintOutside(breakaway) is not null;
        if (hasSprinter && kmToFinish <= 50)
            return ChaseIntensity.Strong;

        // Sin sprinter: dejar crecer si queda mucho; control ligero en el tramo final.
        if (kmToFinish <= 15 || gap > _cfg.BreakawayMaxGapSeconds)
            return ChaseIntensity.Light;

        return ChaseIntensity.None;
    }

    /// <summary>El mejor esprinter del pelotón (que no está en la fuga).</summary>
    public Rider? StrongestSprintOutside(RiderGroup? breakaway)
    {
        var inBreak = breakaway?.MemberRiderIds.ToHashSet() ?? new HashSet<int>();
        Rider? best = null;
        int bestSprint = 0;
        foreach (var (id, rider) in _riders)
        {
            if (inBreak.Contains(id)) continue;
            if (!_states.TryGetValue(id, out var st) || st.Status != RiderStatus.Active) continue;
            if (rider.Attributes.Sprint > bestSprint)
            {
                bestSprint = rider.Attributes.Sprint;
                best = rider;
            }
        }
        return best;
    }

    /// <summary>
    /// Decide y registra la persecución. Devuelve el ajuste de velocidad del pelotón
    /// (km/h) derivado de la intensidad. En modo Player devuelve 0 (la decisión
    /// quedaría para el jugador); en Directed/Assistant se ejecuta y registra.
    /// </summary>
    public double ApplyChase(RaceState state, RiderGroup? breakaway, double kmFront, double stageKm)
    {
        var intensity = DecideChase(breakaway, kmFront, stageKm);
        if (Mode == DirectorMode.Player)
        {
            state.ActionLog.Add($"[PCRM] [JUGADOR] ¿Perseguir fuga ({intensity})? (Director Mode: decisión pendiente)");
            return state.RiderStates.Count > 0 ? 0 : 0;
        }

        double baseAdjust;
        string reason;
        switch (intensity)
        {
            case ChaseIntensity.Strong:
                baseAdjust = 3.5;
                reason = "equipos de sprint cazan la fuga.";
                break;
            case ChaseIntensity.Light:
                baseAdjust = 1.2;
                reason = "control ligero del pelotón.";
                break;
            default:
                baseAdjust = 0;
                reason = "la fuga no representa amenaza hoy.";
                break;
        }
        state.ActionLog.Add($"[PCRM] [IA] Persecución {intensity}: {reason}");
        return baseAdjust;
    }
}