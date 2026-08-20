using ProCycling.Core.Data;
using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class FlatStageSimulatorTests
{
    private static Stage FlatStage() => StageJsonLoader.Load("""
        {
          "id": "test_flat",
          "name": "Etapa de prueba",
          "season_id": 3,
          "type": "flat",
          "distance_km": 180.0,
          "sections": [
            { "km_from": 0, "km_to": 60, "terrains": ["flat"] },
            { "km_from": 60, "km_to": 120, "terrains": ["flat"] },
            { "km_from": 120, "km_to": 180, "terrains": ["flat"], "finish": true, "intermediate_sprint": { "km": 150, "points": [50,30,20,10] } }
          ],
          "climbs": []
        }
        """);

    private static List<Rider> Riders()
    {
        var riders = new List<Rider>();
        Action<int, int, int, string> add = (i, spr, fla, name) => riders.Add(new Rider
        {
            Id = i,
            Name = name,
            TeamId = (i % 10) + 1,
            Attributes = new Attributes
            {
                Flat = fla, Sprint = spr, Acceleration = spr, Endurance = 70,
                Attack = 70, MediumMountain = 65, Resistance = 65, Recovery = 65,
                Mountain = 55, Hill = 60, TimeTrial = 58, Prologue = 55,
                Cobbles = 60, Descent = 60
            },
            Roles = { Rider.ToString(spr >= 75 ? RiderSpecialty.Sprinter : RiderSpecialty.Rouleur) }
        });
        for (int i = 1; i <= 20; i++)
            add(i, i <= 4 ? 80 : 55, i <= 4 ? 80 : 62, $"Corredor{i}");
        return riders;
    }

    private static List<Team> Teams() =>
        Enumerable.Range(1, 10).Select(i => new Team { Id = i, Name = $"Team{i}", Abbr = $"T{i}", SeasonId = 3 }).ToList();

    private static RaceState Setup(ulong seed = 42) =>
        RaceSetup.Create(FlatStage(), Teams(), Riders(), seed);

    [Fact]
    public void MismaSeed_MismoResultado()
    {
        var r1 = new FlatStageSimulator(RulesConfig.Default(), 42).Run(Setup(42));
        var r2 = new FlatStageSimulator(RulesConfig.Default(), 42).Run(Setup(42));
        Assert.Equal(r1.Count, r2.Count);
        for (int i = 0; i < r1.Count; i++)
            Assert.Equal(r1[i].StageSeconds, r2[i].StageSeconds);
    }

    [Fact]
    public void DiferenteSeed_DiferentesTiemposOMenos()
    {
        var seedA = 42ul;
        var seedB = 999ul;
        var r1 = new FlatStageSimulator(RulesConfig.Default(), seedA).Run(Setup(seedA));
        var r2 = new FlatStageSimulator(RulesConfig.Default(), seedB).Run(Setup(seedB));

        // El ganador debe variar entre seeds (los sprinters son casi idénticos, el ruido decide).
        var podioA = r1.OrderBy(r => r.StageSeconds).Take(3).Select(r => r.RiderId).ToArray();
        var podioB = r2.OrderBy(r => r.StageSeconds).Take(3).Select(r => r.RiderId).ToArray();
        Assert.False(podioA.SequenceEqual(podioB), "El podio debería cambiar con la semilla");
    }

    [Fact]
    public void SprintEsGanadoPorUnEsprinter()
    {
        var results = new FlatStageSimulator(RulesConfig.Default(), 42).Run(Setup(42));
        var winner = results.OrderBy(r => r.StageSeconds).First();
        Assert.True(winner.RiderId <= 4, $"Ganador esperado esprinter (id≤4), fue #{winner.RiderId}");
    }

    [Fact]
    public void TodosLosCorredoresCruzanMeta()
    {
        var state = Setup(42);
        new FlatStageSimulator(RulesConfig.Default(), 42).Run(state);
        Assert.All(state.RiderStates, s => Assert.Equal(RiderStatus.Finished, s.Status));
        Assert.Equal(20, state.Classifications.GcStandings().Count);
    }

    [Fact]
    public void Clasificaciones_Y_Puntos_Correctos()
    {
        var state = Setup(42);
        var results = new FlatStageSimulator(RulesConfig.Default(), 42).Run(state);
        var gc = state.Classifications.GcStandings().OrderBy(c => c.GcSeconds).ToList();
        Assert.Equal(20, gc.Count);
        Assert.True(gc[0].GcSeconds <= gc[^1].GcSeconds);

        var points = state.Classifications.PointsStandings();
        Assert.True(points.Count > 0);
        Assert.True(points[0].Points >= points[^1].Points);
    }

    [Fact]
    public void LaFugaSeFormaYEsCazada()
    {
        var state = Setup(42);
        new FlatStageSimulator(RulesConfig.Default(), 42).Run(state);
        var breakaway = state.Groups.FirstOrDefault(g => g.Kind == GroupKind.Breakaway);
        Assert.NotNull(breakaway);
        Assert.True(breakaway!.MemberRiderIds.Count >= 1);
        // La etapa acaba con sprint masivo: el gap de la fuga es pequeño.
        Assert.True(breakaway.GapSeconds <= 30,
            $"Fuga debería estar cazada (gap={breakaway.GapSeconds:0})");
    }

    [Fact]
    public void ElGapNoCaePorDebajoDeCero()
    {
        for (ulong s = 1; s <= 10; s++)
        {
            var state = Setup(s);
            new FlatStageSimulator(RulesConfig.Default(), s).Run(state);
            foreach (var g in state.Groups)
                Assert.True(g.GapSeconds >= 0);
        }
    }

    [Fact]
    public void LaFatigaFinalEstaEntreCeroYMax()
    {
        var state = Setup(11);
        new FlatStageSimulator(RulesConfig.Default(), 11).Run(state);
        foreach (var rs in state.RiderStates)
            Assert.InRange(rs.Fatigue, 0, RulesConfig.Default().FatMax);
    }
}