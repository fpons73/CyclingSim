using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class GroupValueTests
{
    private static Rider Make(int id, double bias) // bias satura hacia 85
    {
        int v = (int)(50 + bias * 35);
        var a = new Attributes
        {
            Flat = v, Mountain = v, Attack = v, Endurance = v,
            Sprint = v, Acceleration = v
        };
        return new Rider { Id = id, Name = $"r{id}", Attributes = a };
    }

    [Fact]
    public void GrupoFuerte_TieneMayorGV_QueGrupoDebil()
    {
        var cfg = RulesConfig.Default();
        var fuertes = new[] { Make(1, 0.85), Make(2, 0.80), Make(3, 0.78) };
        var debiles = new[] { Make(4, 0.2), Make(5, 0.15), Make(6, 0.1) };

        var gvCalc = new GroupValueCalculator(cfg, fuertes.Concat(debiles));
        var statesStrong = fuertes.Select(r => new RiderState { RiderId = r.Id }).ToList();
        var statesWeak = debiles.Select(r => new RiderState { RiderId = r.Id }).ToList();

        double gvStrong = gvCalc.ComputeGroupValue(statesStrong, "flat_tempo", workers: 3);
        double gvWeak = gvCalc.ComputeGroupValue(statesWeak, "flat_tempo", workers: 3);

        Assert.True(gvWeak < gvStrong);
    }

    [Fact]
    public void MenosTrabjeadores_ReducenElGV_BajaCohesionConHeterogeneidad()
    {
        var cfg = RulesConfig.Default();
        var riders = new[] { Make(1, 0.9), Make(2, 0.1), Make(3, 0.85) };
        var calc = new GroupValueCalculator(cfg, riders);
        var states = riders.Select(r => new RiderState { RiderId = r.Id, Fatigue = 20 }).ToList();

        double gv3 = calc.ComputeGroupValue(states, "flat_tempo", workers: 3);
        double gv1 = calc.ComputeGroupValue(states, "flat_tempo", workers: 1);
        Assert.True(gv1 > gv3); // solo el mejor rueda → GV más alto que media de los 3

        var values = states.Select(s => calc.EffectiveWorking(s, "flat_tempo")).ToList();
        double cohesion = calc.Cohesion(values, gv3);
        Assert.InRange(cohesion, 0, 1);
        Assert.True(cohesion < 0.5, $"Heterogéneo debería dar cohesión baja, obtuve {cohesion:F2}");
    }

    [Fact]
    public void VelocidadEstimada_LlanoMayorQueAdoquines()
    {
        var cfg = RulesConfig.Default();
        var calc = new GroupValueCalculator(cfg, Array.Empty<Rider>());
        double flat = calc.EstimateSpeed(65, Terrain.Flat);
        double cobbles = calc.EstimateSpeed(65, Terrain.Cobbles);
        Assert.True(flat > cobbles);
    }
}