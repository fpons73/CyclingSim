using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

/// <summary>
/// Group Value (GV) y Cohesion Level de un grupo. Solo cuentan los corredores
/// que efectivamente ruedan (PRD §10, §19, §20).
/// </summary>
public sealed class GroupValueCalculator
{
    private readonly RulesConfig _cfg;
    private readonly RiderPerformanceCalculator _perf;
    private readonly Dictionary<int, Rider> _riders;

    public GroupValueCalculator(RulesConfig cfg, IEnumerable<Rider> riders)
    {
        _cfg = cfg;
        _perf = new RiderPerformanceCalculator(cfg);
        _riders = riders.ToDictionary(r => r.Id);
    }

    public double EffectiveWorking(RiderState state, string profile) =>
        _riders.TryGetValue(state.RiderId, out var rider)
            ? _perf.EffectiveDeterministic(rider, state, profile)
            : 50;

    /// <summary>GV = media del rendimiento de los N mejores corredores que ruedan.</summary>
    public double ComputeGroupValue(IEnumerable<RiderState> states, string profile, int workers)
    {
        var values = states.Select(s => EffectiveWorking(s, profile)).OrderByDescending(v => v).ToList();
        if (values.Count == 0) return 0;
        int count = Math.Clamp(workers, 1, values.Count);
        return values.Take(count).Average();
    }

    /// <summary>Cohesion Level: 1 = homogéneo, 0 = riesgo máximo de rotura.</summary>
    public double Cohesion(IReadOnlyCollection<double> effectiveValues, double gv)
    {
        if (effectiveValues.Count == 0) return 1;
        double mean = effectiveValues.Average();
        double variance = effectiveValues.Average(v => (v - mean) * (v - mean));
        double stdev = Math.Sqrt(variance);
        return Math.Clamp(1 - stdev / Math.Max(1e-9, _cfg.CohesionDropThreshold), 0, 1);
    }

    /// <summary>Velocidad estimada del grupo según GV y terreno dominante.</summary>
    public double EstimateSpeed(double gv, Terrain terrain)
    {
        string key = terrain switch
        {
            Terrain.Flat => "flat",
            Terrain.Rolling => "rolling",
            Terrain.Hill => "hill",
            Terrain.Climb => "climb",
            Terrain.Descent => "descent",
            Terrain.Cobbles => "cobbles",
            Terrain.TimeTrial => "tt",
            _ => "flat"
        };
        double baseSpeed = _cfg.TerrainBaseSpeedKmh.TryGetValue(key, out var b) ? b : 40;
        return baseSpeed + (gv - _cfg.GvRef) * _cfg.GvKmhPerPoint;
    }
}