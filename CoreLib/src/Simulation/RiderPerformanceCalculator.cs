using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

/// <summary>
/// Calcula el rendimiento efectivo de un corredor: blend ponderado de atributos,
/// modulado por fatiga (Aguante/Resistencia), contextos y RNG (PRD §6, §40).
/// </summary>
public sealed class RiderPerformanceCalculator
{
    private readonly RulesConfig _cfg;

    public RiderPerformanceCalculator(RulesConfig cfg) => _cfg = cfg;

    /// <summary>Blend ponderado de atributos según el perfil de acción.</summary>
    public double Raw(Rider rider, string profileKey)
    {
        var weights = _cfg.WeightsFor(profileKey);
        double sum = 0, wsum = 0;
        foreach (var (att, weight) in weights)
        {
            if (weight <= 0) continue;
            sum += rider.Attributes.Get(att) * weight;
            wsum += weight;
        }
        return wsum > 0 ? sum / wsum : 50;
    }

    /// <summary>Rendimiento efectivo con ruido RNG en [center ± noiseRange].</summary>
    public double Effective(Rider rider, RiderState state, string profileKey, double rngDraw01)
    {
        double raw = Raw(rider, profileKey);
        double penalty = FatigueCalculator.Penalty(state.Fatigue, rider.Attributes.Endurance,
            rider.Attributes.Resistance, _cfg);
        double noise = 1 + (rngDraw01 - _cfg.RngNoiseCenter) * 2 * _cfg.RngNoiseRange;
        return Math.Max(0, raw * (1 - penalty) * noise);
    }

    /// <summary>Rendimiento sin ruido (para comparaciones deterministas).</summary>
    public double EffectiveDeterministic(Rider rider, RiderState state, string profileKey) =>
        Effective(rider, state, profileKey, _cfg.RngNoiseCenter);
}