namespace ProCycling.Core.Models;

public sealed class Rider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BirthDate { get; set; }   // ISO yyyy-MM-dd o null
    public string? Nationality { get; set; }
    public int TeamId { get; set; }
    public int SeasonId { get; set; }
    public int Number { get; set; }
    public HashSet<string> Roles { get; set; } = new();
    public Attributes Attributes { get; set; } = new();
    public RiderTeamRole TeamRole { get; set; } = RiderTeamRole.None;

    public int? AgeOn(int year)
    {
        if (string.IsNullOrEmpty(BirthDate)) return null;
        if (DateTime.TryParse(BirthDate, out var d))
            return year - d.Year;
        return null;
    }

    /// <summary>Sub-25 según la regla ciclista: nacido en o después del año (añoActual - 24).</summary>
    public bool IsYoungFor(int year)
    {
        if (string.IsNullOrEmpty(BirthDate)) return false;
        if (DateTime.TryParse(BirthDate, out var d)) return d.Year >= year - 24;
        return false;
    }

    public bool HasSpecialty(RiderSpecialty specialty) => Roles.Contains(ToString(specialty));

    public static string ToString(RiderSpecialty s) => s switch
    {
        RiderSpecialty.Sprinter => "sprinter",
        RiderSpecialty.Climber => "climber",
        RiderSpecialty.Puncheur => "puncheur",
        RiderSpecialty.Rouleur => "rouleur",
        RiderSpecialty.TimeTrialist => "time_trialist",
        RiderSpecialty.PrologueSpecialist => "prologue_specialist",
        RiderSpecialty.Paveur => "paveur",
        RiderSpecialty.Allrounder => "allrounder",
        _ => string.Empty
    };
}