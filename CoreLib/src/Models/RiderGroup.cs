namespace ProCycling.Core.Models;

public sealed class RiderGroup
{
    public int Id { get; set; }
    public GroupKind Kind { get; set; }
    public List<int> MemberRiderIds { get; set; } = new();

    /// <summary>Velocidad actual del grupo (km/h).</summary>
    public double SpeedKmh { get; set; }

    /// <summary>Tiempo del grupo respecto al grupo de cabeza (0 para el grupo que va primero).</summary>
    public double GapSeconds { get; set; }

    /// <summary>Kilómetro de la etapa donde se encuentra el frente del grupo.</summary>
    public double FrontKmPos { get; set; }

    /// <summary>Group Value: rendimiento efectivo agregado de los que ruedan.</summary>
    public double GroupValue { get; set; }

    /// <summary>Cohesion Level 0..1 (1 = totalmente cohesionado).</summary>
    public double Cohesion { get; set; }

    public bool IsTempoSet { get; set; }

    public override string ToString() => $"{Kind} #{Id} @{FrontKmPos:0.0} km gap {GapSeconds:0} s";
}