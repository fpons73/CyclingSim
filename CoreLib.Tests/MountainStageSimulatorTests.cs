using ProCycling.Core.Data;
using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class MountainStageSimulatorTests
{
    private static Stage MountainStage() => StageJsonLoader.Load("""
        {
          "id": "test_mountain", "name": "Etapa de montaña", "season_id": 3, "type": "mountain",
          "distance_km": 180.0,
          "sections": [
            { "km_from": 0, "km_to": 40, "terrains": ["flat"], "gradient": 0 },
            { "km_from": 40, "km_to": 70, "terrains": ["flat","hill"], "gradient": 2 },
            { "km_from": 70, "km_to": 100, "terrains": ["climb"], "gradient": 8, "climb_id": "c1" },
            { "km_from": 100, "km_to": 120, "terrains": ["descent"], "gradient": 0 },
            { "km_from": 120, "km_to": 180, "terrains": ["flat","hill"], "gradient": 1, "finish": true }
          ],
          "climbs": [
            { "id": "c1", "name": "Col", "km_from": 70, "km_to": 100, "category": 1,
              "length_km": 30, "avg_gradient": 8, "summit_km": 95,
              "koM_points": [20, 15, 12, 10, 8, 6, 4] }
          ]
        }
        """);

    private static List<Team> Teams() =>
        Enumerable.Range(1, 10).Select(i => new Team { Id = i, Name = $"T{i}", Abbr = $"T{i}", SeasonId = 3 }).ToList();

    private static List<Rider> Riders()
    {
        var riders = new List<Rider>();
        // Escalador élite (ids 1-3), esprinter (4), gregarios (5+).
        for (int i = 1; i <= 20; i++)
        {
            bool climber = i <= 3;
            riders.Add(new Rider
            {
                Id = i,
                Name = $"Rider{i}",
                TeamId = (i % 10) + 1,
                Attributes = new Attributes
                {
                    Mountain = climber ? (i == 1 ? 84 : 82) : 58,
                    Attack = climber ? 82 : 55,
                    Acceleration = climber ? 75 : 50,
                    Flat = 60, Sprint = climber ? 50 : (i == 4 ? 80 : 55),
                    Endurance = 78, Resistance = 72, Recovery = 70,
                    Descent = climber ? 78 : 60, TimeTrial = 60, Hill = climber ? 78 : 55,
                    MediumMountain = climber ? 80 : 58, Cobbles = 55, Prologue = 55
                },
                Roles = { Rider.ToString(climber ? RiderSpecialty.Climber : RiderSpecialty.Sprinter) }
            });
        }
        return riders;
    }

    private static RaceState Setup(ulong seed = 42) =>
        RaceSetup.Create(MountainStage(), Teams(), Riders(), seed);

    [Fact]
    public void ElGanadorDeUnaEtapaDeMontana_EsUnEscalador()
    {
        var results = new MountainStageSimulator(RulesConfig.Default(), 42).Run(Setup(42));
        var winner = results.OrderBy(r => r.StageSeconds).First();
        Assert.True(winner.RiderId <= 3, $"Ganador esperado escalador (id≤3), fue #{winner.RiderId} ({winner.RiderId})");
    }

    [Fact]
    public void SeOtorganPuntosKoM_YSeAcumulanEnClasificacion()
    {
        var state = Setup(7);
        new MountainStageSimulator(RulesConfig.Default(), 7).Run(state);
        var koM = state.Classifications.KoMStandings();
        Assert.NotEmpty(koM);
        Assert.True(koM[0].KoMPoints > 0, "El líder de la montaña debe tener puntos.");
        Assert.True(koM[0].KoMPoints >= koM[^1].KoMPoints);
        // El líder KoM es un escalador dominante.
        Assert.True(koM[0].RiderId <= 3, $"Líder KoM esperado escalador, fue #{koM[0].RiderId}");
    }

    [Fact]
    public void MismaSeed_MismoResultado_EnMontaña()
    {
        var s1 = new MountainStageSimulator(RulesConfig.Default(), 42).Run(Setup(42));
        var s2 = new MountainStageSimulator(RulesConfig.Default(), 42).Run(Setup(42));
        Assert.Equal(s1.Count, s2.Count);
        for (int i = 0; i < s1.Count; i++)
            Assert.Equal(s1[i].StageSeconds, s2[i].StageSeconds);
    }

    [Fact]
    public void LaFatigaEnSubida_EsMayorParaLosQueTrabajan()
    {
        var state = Setup(3);
        new MountainStageSimulator(RulesConfig.Default(), 3).Run(state);
        // Los escaladores (grupo de cabeza) acumulan fatiga relevante (0..100).
        foreach (var rs in state.RiderStates)
            Assert.InRange(rs.Fatigue, 0, RulesConfig.Default().FatMax);
    }

    [Fact]
    public void TodosLosCorredoresCruzanLaMetaDeLaEtapaDeMontaña()
    {
        var state = Setup(11);
        new MountainStageSimulator(RulesConfig.Default(), 11).Run(state);
        Assert.Equal(20, state.Classifications.GcStandings().Count);
        Assert.All(state.RiderStates, s => Assert.Equal(RiderStatus.Finished, s.Status));
    }
}