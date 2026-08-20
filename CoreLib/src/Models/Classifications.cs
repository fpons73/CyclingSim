namespace ProCycling.Core.Models;

public sealed record StageResultRider(
    int RiderId,
    int TeamId,
    double StageSeconds,
    int PointsEarned,
    int KoMPointsEarned,
    bool IsYoung);

public sealed class RiderClassification
{
    public int RiderId { get; init; }
    public double GcSeconds { get; set; }
    public double StageSeconds { get; set; }
    public int Points { get; set; }
    public int KoMPoints { get; set; }
    public bool IsYoung { get; set; }
}

/// <summary>Clasificaciones acumuladas: General, Puntos, Montaña, Jóvenes y Equipos.</summary>
public sealed class Classifications
{
    private readonly Dictionary<int, RiderClassification> _riders = new();
    private readonly Dictionary<int, double> _teamGcSeconds = new();

    public IReadOnlyDictionary<int, RiderClassification> Riders => _riders;
    public IReadOnlyDictionary<int, double> TeamGcSeconds => _teamGcSeconds;

    public void RegisterStage(IReadOnlyCollection<StageResultRider> results)
    {
        foreach (var r in results)
        {
            if (!_riders.TryGetValue(r.RiderId, out var rc))
            {
                rc = new RiderClassification { RiderId = r.RiderId };
                _riders[r.RiderId] = rc;
            }
            rc.GcSeconds += r.StageSeconds;
            rc.StageSeconds = r.StageSeconds;
            rc.Points += r.PointsEarned;
            rc.KoMPoints += r.KoMPointsEarned;
            rc.IsYoung = r.IsYoung;
        }

        foreach (var g in results.GroupBy(r => r.TeamId))
        {
            double best3 = g.OrderBy(x => x.StageSeconds).Take(3).Sum(x => x.StageSeconds);
            _teamGcSeconds.TryGetValue(g.Key, out var acc);
            _teamGcSeconds[g.Key] = acc + best3;
        }
    }

    public IReadOnlyList<RiderClassification> GcStandings() => _riders.Values.OrderBy(r => r.GcSeconds).ToList();
    public IReadOnlyList<RiderClassification> PointsStandings() => _riders.Values.OrderByDescending(r => r.Points).ToList();
    public IReadOnlyList<RiderClassification> KoMStandings() => _riders.Values.OrderByDescending(r => r.KoMPoints).ToList();
    public IReadOnlyList<RiderClassification> YoungStandings() => _riders.Values.Where(r => r.IsYoung).OrderBy(r => r.GcSeconds).ToList();
    public IReadOnlyList<KeyValuePair<int, double>> TeamStandings() => _teamGcSeconds.OrderBy(kv => kv.Value).ToList();
}