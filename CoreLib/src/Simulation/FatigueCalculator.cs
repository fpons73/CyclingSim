using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

/// <summary>
/// Gestión de fatiga (0–100). Aumenta por km/desnivel/ritmo/esfuerzos y se ve
/// mitigada por Aguante (STA) y Resistencia (RES) (PRD §5).
/// </summary>
public static class FatigueCalculator
{
    /// <summary>Penalización de rendimiento (0..MaxFatiguePenalty) según la fatiga acumulada.</summary>
    public static double Penalty(double fatigue, int endurance, int resistance, RulesConfig cfg)
    {
        double nEnd = Attributes.Normalized(endurance);
        double nRes = Attributes.Normalized(resistance);
        double mitigation = Math.Max(0.25,
            1 - cfg.EnduranceMitigation * nEnd - cfg.ResistanceMitigation * nRes);
        double ratio = Math.Pow(Math.Clamp(fatigue, 0, cfg.FatMax) / cfg.FatMax, cfg.FatigueCurve);
        return Math.Clamp(ratio * cfg.MaxFatiguePenalty * mitigation, 0, cfg.MaxFatiguePenalty);
    }

    /// <summary>Acumula fatiga manteniéndola en [0, FatMax].</summary>
    public static double AddFatigue(double current, double km, double elevationMeters,
        double effortScore, int endurance, int resistance, RulesConfig cfg)
    {
        double raw = km * cfg.KmFatiguePerKm
                   + (elevationMeters / 100.0) * cfg.ElevationFatiguePer100m
                   + effortScore * cfg.EffortFatigue;
        double nEnd = Attributes.Normalized(endurance);
        double nRes = Attributes.Normalized(resistance);
        double mitigation = Math.Max(0.25,
            1 - cfg.EnduranceMitigation * nEnd - cfg.ResistanceMitigation * nRes);
        if (km >= cfg.LongStageKmThreshold)
        {
            // Resistencia cobra más peso en etapas largas/duras
            mitigation *= Math.Max(0.25, 1 - cfg.ResistanceHardStageBonus * nRes);
        }
        return Math.Min(cfg.FatMax, Math.Max(0, current + Math.Max(0, raw * mitigation)));
    }
}