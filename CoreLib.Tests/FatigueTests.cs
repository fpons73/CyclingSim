using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class FatigueTests
{
    [Fact]
    public void LaFatigaNuncaSupera100()
    {
        var cfg = RulesConfig.Default();
        // serie enorme de acumulaciones siempre clampa a FatMax
        double f = 0;
        for (int i = 0; i < 200; i++)
            f = FatigueCalculator.AddFatigue(f, 200, 5000, 5, 60, 60, cfg);
        Assert.Equal(cfg.FatMax, f);
        Assert.InRange(f, 0, 100);
    }

    [Fact]
    public void LaPenalizacionCreceConLaFatiga()
    {
        var cfg = RulesConfig.Default();
        var low = FatigueCalculator.Penalty(20, 60, 60, cfg);
        var high = FatigueCalculator.Penalty(90, 60, 60, cfg);
        Assert.True(high > low);
    }

    [Fact]
    public void MejorAguanteYResistencia_ReducenLaPenalizacion()
    {
        var cfg = RulesConfig.Default();
        var mal = FatigueCalculator.Penalty(70, 55, 55, cfg);
        var bien = FatigueCalculator.Penalty(70, 85, 82, cfg);
        Assert.True(bien < mal);
    }

    [Fact]
    public void LaDistanciaAcumulaFatiga()
    {
        var cfg = RulesConfig.Default();
        var f = FatigueCalculator.AddFatigue(0, 100, 0, 0, 60, 60, cfg);
        Assert.True(f > 0);
    }

    [Fact]
    public void RecuperacionEntreEtapas_ReduceLaFatigaResidual()
    {
        var cfg = RulesConfig.Default();
        double f55 = RecoveryCalculator.ApplyBetweenStages(90, 55, cfg);
        double f80 = RecoveryCalculator.ApplyBetweenStages(90, 80, cfg);
        Assert.True(f80 < f55);
        Assert.InRange(f55, 0, 90);
        Assert.InRange(f80, 0, 90);
    }
}