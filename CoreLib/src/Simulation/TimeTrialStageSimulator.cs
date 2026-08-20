using ProCycling.Core.Models;
using ProCycling.Core.Replay;

namespace ProCycling.Core.Simulation;

/// <summary>
/// CRI, Prólogo y TTT (PRD §13–§15). Resolución directa del tiempo por perfiles
/// TTR/PRL sobre la distancia total. El TTT usa el peor del equipo (regla configurable).
/// Menos fases que una gran etapa: el rendimiento domina sobre la táctica.
/// </summary>
public sealed class TimeTrialStageSimulator
{
    private readonly RulesConfig _cfg;
    private readonly SeededRandom _rng;

    public TimeTrialStageSimulator(RulesConfig cfg, ulong seed)
    {
        _cfg = cfg;
        _rng = new SeededRandom(seed);
    }

    public List<StageResultRider> Run(RaceState state) => Run(state, null);

    public List<StageResultRider> Run(RaceState state, IRaceRecorder? recorder)
    {
        var stage = state.Stage ?? throw new InvalidOperationException("Sin etapa.");
        _rng.RestoreState(state.RngState);
        state.CurrentSectionIndex = 0;
        state.KmCovered = stage.DistanceKm;
        state.ActionLog.Clear();

        double distance = Math.Max(1, stage.DistanceKm);
        string profile = stage.Type switch
        {
            StageType.Prologue => "prologue",
            StageType.TeamTimeTrial => "ttt_team",
            _ => distance > 35 ? "tt_long" : "tt_short"
        };

        var ordered = state.RiderStates
            .Where(s => s.Status == RiderStatus.Active)
            .ToList();

        // TTT: tiempo por equipo = peor corredor (regla de arrastre).
        if (stage.Type == StageType.TeamTimeTrial)
        {
            var orderedIds = ordered.OrderBy(s => s.RiderId).Select(s => s.RiderId).ToList();
            var byTeam = ordered.GroupBy(s => state.Riders[s.RiderId].TeamId);
            foreach (var team in byTeam)
            {
                double worst = team.Max(s => EffectiveTt(state, s, "ttt_team"));
                double speed = MaxTtSpeed(state, "ttt_team");
                double teamTime = SecondsForKm(distance, speed * (worst / _cfg.GvRef));
                foreach (var s in team)
                {
                    s.StageTimeSeconds = teamTime;
                    s.StageRiddenSeconds = teamTime;
                    ApplyFatigue(state, s, distance, "ttt_team");
                }
                state.ActionLog.Add($"[PCRM] TTT equipo {team.Key} → {RiderCardTime(teamTime)}");
            }
            recorder?.RecordSection(state);
            return ResultsFrom(state, orderedIds);
        }

        // CRI / Prólogo: cada corredor rueda solo.
        var ranked = ordered
            .Select(s =>
            {
                double per = EffectiveTt(state, s, profile);
                double time = SecondsForKm(distance, MaxTtSpeed(state, profile) * 0.9
                    + (per - _cfg.GvRef) * 0.05);
                return (s, Time: time);
            })
            .OrderBy(x => x.Time)
            .ToList();

        foreach (var (s, time) in ranked)
        {
            s.StageTimeSeconds = time;
            s.StageRiddenSeconds = time;
            ApplyFatigue(state, s, distance, profile);
        }

        state.RngState = _rng.GetState();
        recorder?.RecordSection(state);
        return ResultsFrom(state, ranked.Select(x => x.s.RiderId).ToList());
    }

    private double EffectiveTt(RaceState state, RiderState s, string profile)
    {
        var r = state.Riders[s.RiderId];
        double raw = BlendFor(r, profile);
        double penalty = FatigueCalculator.Penalty(s.Fatigue, r.Attributes.Endurance,
            r.Attributes.Resistance, _cfg);
        return raw * (1 - penalty);
    }

    private double MaxTtSpeed(RaceState state, string profile)
    {
        var values = state.RiderStates
            .Where(x => x.Status == RiderStatus.Active)
            .Select(x => EffectiveTt(state, x, profile))
            .OrderByDescending(v => v)
            .Take(8)
            .DefaultIfEmpty(_cfg.GvRef)
            .Average();
        return Math.Max(40, _cfg.TerrainBaseSpeedKmh["tt"] + (values - _cfg.GvRef) * 0.03);
    }

    private void ApplyFatigue(RaceState state, RiderState s, double km, string profile)
    {
        var r = state.Riders[s.RiderId];
        s.Fatigue = FatigueCalculator.AddFatigue(s.Fatigue, km, 0,
            profile.Contains("long") ? 0.5 : 0.1,
            r.Attributes.Endurance, r.Attributes.Resistance, _cfg);
    }

    private List<StageResultRider> ResultsFrom(RaceState state, List<int> order)
    {
        var results = new List<StageResultRider>();
        int pos = 0;
        foreach (var id in order)
        {
            var s = state.RiderStates.First(x => x.RiderId == id);
            var r = state.Riders[id];
            s.Status = RiderStatus.Finished;
            results.Add(new StageResultRider(id, r.TeamId, s.StageTimeSeconds,
                pos < 3 ? (3 - pos) * 4 : 0, 0, r.IsYoungFor(2026)));
            pos++;
        }
        return results;
    }

    private double BlendFor(Rider r, string profile)
    {
        double sum = 0, wsum = 0;
        foreach (var (att, w) in _cfg.WeightsFor(profile))
        {
            if (w <= 0) continue;
            sum += r.Attributes.Get(att) * w;
            wsum += w;
        }
        return wsum > 0 ? sum / wsum : 50;
    }

    private static double SecondsForKm(double km, double speedKmh) =>
        speedKmh <= 0 ? 0 : km / speedKmh * 3600;

    internal static string RiderCardTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{t.Hours:D1}h {t.Minutes:D2}m {t.Seconds:D2}s" : $"{t.Minutes:D2}m {t.Seconds:D2}s";
    }
}