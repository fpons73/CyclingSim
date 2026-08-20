namespace ProCycling.Core.Models;

public sealed class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbr { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string Category { get; set; } = "Unknown";
    public int SeasonId { get; set; }
}