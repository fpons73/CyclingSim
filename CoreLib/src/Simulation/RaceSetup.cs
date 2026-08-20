using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

/// <summary>
/// Construye un <see cref="RaceState"/> inicializado: RNG sembrado, corredores,
/// fatigas a 0, pelotón único con todos los participantes y estado del RNG
/// persistent para reproducibilidad (PRD §8, §32).
/// </summary>
public static class RaceSetup
{
    public static RaceState Create(Stage stage, IEnumerable<Team> teams, IEnumerable<Rider> riders,
        ulong seed)
    {
        var teamsById = teams.ToDictionary(t => t.Id);
        var riderList = riders.ToList();

        var state = new RaceState
        {
            Seed = seed,
            StageId = stage.Id,
            Stage = stage,
        };

        foreach (var t in teams) state.Teams[t.Id] = t;
        foreach (var r in riderList) state.Riders[r.Id] = r;

        var rng = new SeededRandom(seed);

        var riderStates = riderList.Select(r => new RiderState
        {
            RiderId = r.Id,
            Fatigue = 0,
            Status = RiderStatus.Active,
            GroupId = 1
        }).ToList();
        state.RiderStates = riderStates;

        state.Groups.Add(new RiderGroup
        {
            Id = 1,
            Kind = GroupKind.Peloton,
            MemberRiderIds = riderStates.Select(rs => rs.RiderId).ToList(),
            FrontKmPos = 0,
            GapSeconds = 0,
            Cohesion = 1
        });

        // Estado inicial del RNG persistido para reproducción exacta.
        state.RngState = rng.GetState();
        state.ActionLog.Add($"[PCRM] Etapa '{stage.Name}' con {riderList.Count} corredores ({riderList.Count / Math.Max(1, teamsById.Count)} equipos). Seed={seed}.");

        return state;
    }
}