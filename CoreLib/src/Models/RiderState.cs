namespace ProCycling.Core.Models;

/// <summary>Estado dinámico de un corredor durante la carrera (no es atributo permanente).</summary>
public sealed class RiderState
{
    public int RiderId { get; set; }

    /// <summary>Fatiga 0–100. 0 = fresco, 100 = agotamiento extremo.</summary>
    public double Fatigue { get; set; }

    public RiderStatus Status { get; set; } = RiderStatus.Active;

    /// <summary>Tiempo de la etapa actual (segundos).</summary>
    public double StageTimeSeconds { get; set; }

    /// <summary>Tiempo acumulado de General (segundos).</summary>
    public double GcSeconds { get; set; }

    public int GroupId { get; set; } = -1;
    public int RacePosition { get; set; }

    /// <summary>Desnivel acumulado en la etapa (metros) — alimenta la fatiga.</summary>
    public double StageElevationMeters { get; set; }

    /// <summary>Esfuerzos puntuales acumulados (ataques, persecuciones...) — alimentan la fatiga.</summary>
    public double StageEffortScore { get; set; }

    /// <summary>Tiempo invertido en la etapa (suma real de secciones).</summary>
    public double StageRiddenSeconds { get; set; }
}