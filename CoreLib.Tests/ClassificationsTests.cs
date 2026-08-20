using ProCycling.Core.Models;

namespace ProCycling.Core.Tests;

public class ClassificationsTests
{
    [Fact]
    public void AcumulaGeneralPuntosKmYEquipos()
    {
        var cls = new Classifications();
        cls.RegisterStage(new[]
        {
            new StageResultRider(1, 10, 10000, 50, 20, false),
            new StageResultRider(2, 10, 10020, 30, 15, true),
            new StageResultRider(3, 11, 10040, 20, 0, false),
            new StageResultRider(4, 11, 10060, 10, 0, false),
            new StageResultRider(5, 11, 10100, 5, 0, false),
        });

        var gc = cls.GcStandings();
        Assert.Equal(1, gc[0].RiderId);
        Assert.True(gc[0].GcSeconds <= gc[1].GcSeconds);

        var points = cls.PointsStandings();
        Assert.Equal(1, points[0].RiderId);
        Assert.Equal(50, points[0].Points);

        var kom = cls.KoMStandings();
        Assert.Equal(1, kom[0].RiderId);

        var young = cls.YoungStandings();
        Assert.Single(young);
        Assert.Equal(2, young[0].RiderId);

        // Equipos: suma de los 3 mejores (team 10: 10000+10020; team 11: 10040+10060+10100)
        Assert.Equal(10020 + 10000, cls.TeamGcSeconds[10], 3);
        Assert.Equal(10040 + 10060 + 10100, cls.TeamGcSeconds[11], 3);
    }

    [Fact]
    public void VariasEtapas_AcumulanCorrectamente()
    {
        var cls = new Classifications();
        cls.RegisterStage(new[] { new StageResultRider(1, 5, 9000, 0, 0, false) });
        cls.RegisterStage(new[] { new StageResultRider(1, 5, 8000, 10, 0, false) });
        Assert.Equal(17000, cls.GcStandings()[0].GcSeconds, 3);
        Assert.Equal(10, cls.PointsStandings()[0].Points);
    }
}