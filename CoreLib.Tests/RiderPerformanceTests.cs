using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class RiderPerformanceTests
{
    private static Rider Make(string name, int mnt, int att, int acc = 60, int sta = 60)
    {
        var a = new Attributes { Mountain = mnt, Attack = att, Acceleration = acc, Endurance = sta, Flat = 60 };
        return new Rider { Id = name.GetHashCode(), Name = name, Attributes = a };
    }

    [Fact]
    public void EscaladorElite_SuperaAGregario_EnPerfilDeAtaqueEnPuerto()
    {
        var cfg = RulesConfig.Default();
        var calc = new RiderPerformanceCalculator(cfg);
        var pog = Make("Pogacar", 84, 82, 81, 85);
        var greg = Make("Gregario", 58, 55, 54, 58);
        var s0 = new RiderState { Fatigue = 10 };

        double top = calc.EffectiveDeterministic(pog, s0, "climb_attack");
        double low = calc.EffectiveDeterministic(greg, s0, "climb_attack");

        Assert.True(low < top, $"Esperado gregario baja ({low:F1}) << élite ({top:F1})");
        Assert.True(top - low > 15);
    }

    [Fact]
    public void LaFatigaReduceElRendimiento()
    {
        var cfg = RulesConfig.Default();
        var calc = new RiderPerformanceCalculator(cfg);
        var rider = Make("r", 70, 65, 62, 60);

        var fresco = new RiderState { Fatigue = 0 };
        var cansado = new RiderState { Fatigue = 80 };

        double vFresh = calc.EffectiveDeterministic(rider, fresco, "pace_check");
        double vTired = calc.EffectiveDeterministic(rider, cansado, "pace_check");
        Assert.True(vTired < vFresh);
    }

    [Fact]
    public void ElRuidoRNG_NoSacaDeRango()
    {
        var cfg = RulesConfig.Default();
        var calc = new RiderPerformanceCalculator(cfg);
        var rider = Make("r", 70, 60);

        var st = new RiderState { Fatigue = 20 };
        double vMin = calc.Effective(rider, st, "climb_attack", 0.0);
        double vMax = calc.Effective(rider, st, "climb_attack", 1.0);

        // el ruido simétrico es ± RngNoiseRange sobre el valor central
        double mid = (vMax + vMin) / 2;
        double relSwing = (vMax - vMin) / mid;   // ≈ 2*RngNoiseRange
        Assert.True(vMin <= vMax);
        Assert.True(relSwing <= cfg.RngNoiseRange * 2.5,
            $"Oscilación relativa {relSwing:F3} debería ser ≤ {cfg.RngNoiseRange * 2.5:F3}");
        Assert.True(relSwing >= cfg.RngNoiseRange * 1.0);
    }
}