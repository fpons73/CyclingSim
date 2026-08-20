using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

/// <summary>Recuperación entre etapas: fatiga_final → Recuperación → fatiga_residual (PRD §5).</summary>
public static class RecoveryCalculator
{
    public static double ApplyBetweenStages(double fatigue, int recoveryAttribute, RulesConfig cfg)
    {
        double nRec = Attributes.Normalized(recoveryAttribute);
        double recovered = cfg.RecoveryBase + cfg.RecoveryStatFactor * nRec;
        double keep = Math.Clamp(1 - recovered, 0, 1);
        return Math.Clamp(fatigue * keep, 0, cfg.FatMax);
    }
}