using System.Text.Json.Serialization;

namespace ProCycling.Core.Models;

public sealed class Stage
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("season_id")] public int SeasonId { get; set; }
    [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;

    [JsonPropertyName("type")] public string TypeRaw { get; set; } = "flat";
    [JsonIgnore] public StageType Type => ParseType(TypeRaw);

    [JsonPropertyName("distance_km")] public double DistanceKm { get; set; }
    [JsonPropertyName("time_factor")] public double TimeFactor { get; set; } = 1.0;
    [JsonPropertyName("tempo_modifier")] public double TempoModifier { get; set; }

    [JsonPropertyName("sections")] public List<StageSection> Sections { get; set; } = new();
    [JsonPropertyName("climbs")] public List<Climb> Climbs { get; set; } = new();

    public static StageType ParseType(string raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "flat" => StageType.Flat,
        "flat_hilly" or "hilly" => StageType.FlatHilly,
        "flat_cobbles" or "cobbles" => StageType.FlatCobbles,
        "medium_mountain" => StageType.MediumMountain,
        "mountain" => StageType.Mountain,
        "itt" => StageType.IndividualTimeTrial,
        "ttt" => StageType.TeamTimeTrial,
        "crosswind" => StageType.Crosswind,
        "prologue" => StageType.Prologue,
        "rest" => StageType.Rest,
        _ => StageType.Flat
    };
}

public sealed class StageSection
{
    [JsonPropertyName("km_from")] public double KmFrom { get; set; }
    [JsonPropertyName("km_to")] public double KmTo { get; set; }

    [JsonPropertyName("terrains")] public List<string> TerrainsRaw { get; set; } = new();
    [JsonIgnore] public IReadOnlyList<Terrain> Terrains => TerrainsRaw
        .Select(t => t.Trim().ToLowerInvariant() switch
        {
            "flat" => Terrain.Flat,
            "rolling" => Terrain.Rolling,
            "hill" => Terrain.Hill,
            "climb" => Terrain.Climb,
            "descent" => Terrain.Descent,
            "cobbles" => Terrain.Cobbles,
            "tt" => Terrain.TimeTrial,
            _ => Terrain.Flat
        })
        .ToList();

    [JsonPropertyName("gradient")] public double GradientPct { get; set; }

    [JsonPropertyName("cobbles")] public bool Cobbles { get; set; }

    [JsonPropertyName("wind")] public WindInfo? Wind { get; set; }

    [JsonPropertyName("intermediate_sprint")]
    public SprintInfo? IntermediateSprint { get; set; }

    [JsonPropertyName("climb_id")] public string? ClimbId { get; set; }

    [JsonPropertyName("finish")] public bool Finish { get; set; }

    public double LengthKm => Math.Max(0, KmTo - KmFrom);

    [JsonIgnore] public Terrain DominantTerrain =>
        Terrains.Count == 0 ? Terrain.Flat :
        Terrains.GroupBy(t => t).OrderByDescending(g => g.Count()).First().Key;
}

public sealed class Climb
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("km_from")] public double KmFrom { get; set; }
    [JsonPropertyName("km_to")] public double KmTo { get; set; }
    [JsonPropertyName("category")] public int Category { get; set; }
    [JsonPropertyName("length_km")] public double LengthKm { get; set; }
    [JsonPropertyName("avg_gradient")] public double AvgGradient { get; set; }
    [JsonPropertyName("summit_km")] public double SummitKm { get; set; }
    [JsonPropertyName("koM_points")] public int[] KoM_Points { get; set; } = Array.Empty<int>();
}

public sealed class WindInfo
{
    [JsonPropertyName("direction")] public string DirectionRaw { get; set; } = "none";
    [JsonIgnore] public WindDirection Direction => DirectionRaw.Trim().ToLowerInvariant() switch
    {
        "tail" => WindDirection.Tail,
        "head" => WindDirection.Head,
        "cross" => WindDirection.Cross,
        _ => WindDirection.Tail
    };

    [JsonPropertyName("strength")] public int Strength { get; set; }
}

public sealed class SprintInfo
{
    [JsonPropertyName("km")] public double Km { get; set; }
    [JsonPropertyName("points")] public int[] Points { get; set; } = Array.Empty<int>();
}