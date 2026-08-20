using ProCycling.Core.Data;
using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class TourSimulatorTests
{
    /// <summary>Mini-tour representativo: prólogo + llana + montaña + CRI + llana final.</summary>
    private static readonly Stage[] Stages =
    {
        StageJsonLoader.Load("""{"id":"s01","name":"Prólogo","type":"prologue","distance_km":6,"sections":[{"km_from":0,"km_to":6,"terrains":["flat"],"finish":true}],"climbs":[]}"""),
        StageJsonLoader.Load("""{"id":"s02","name":"Llana","type":"flat","distance_km":180,"sections":[{"km_from":0,"km_to":180,"terrains":["flat"],"finish":true}],"climbs":[]}"""),
        StageJsonLoader.Load("""
            {"id":"s03","name":"Montaña","type":"mountain","distance_km":190,"sections":[
            {"km_from":0,"km_to":60,"terrains":["flat"]},
            {"km_from":60,"km_to":100,"terrains":["climb"],"gradient":7,"climb_id":"c1"},
            {"km_from":100,"km_to":190,"terrains":["flat"],"finish":true}],
            "climbs":[{"id":"c1","name":"Col","km_from":60,"km_to":100,"category":1,"length_km":40,"avg_gradient":7,"summit_km":85,"koM_points":[20,15,12,10,8,6,4]}]}
            """),
        StageJsonLoader.Load("""{"id":"s04","name":"CRI","type":"itt","distance_km":30,"sections":[{"km_from":0,"km_to":30,"terrains":["flat"],"finish":true}],"climbs":[]}"""),
        StageJsonLoader.Load("""{"id":"s05","name":"Final","type":"flat","distance_km":140,"sections":[{"km_from":0,"km_to":140,"terrains":["flat"],"finish":true}],"climbs":[]}""")
    };

    private static (List<Team>, List<Rider>) Roster()
    {
        var teams = Enumerable.Range(1, 10).Select(i => new Team { Id = i, Name = $"T{i}", Abbr = $"T{i}", SeasonId = 3 }).ToList();
        var riders = new List<Rider>();
        for (int i = 1; i <= 32; i++)
        {
            bool climber = i <= 4;
            riders.Add(new Rider
            {
                Id = i, Name = $"R{i}", TeamId = (i % 10) + 1,
                Attributes = new Attributes
                {
                    Flat = 65, Mountain = climber ? 82 : 58, Attack = climber ? 80 : 55,
                    Acceleration = climber ? 72 : 55, Sprint = climber ? 48 : (i <= 8 ? 78 : 55),
                    Endurance = 78, Resistance = 72, Recovery = 70, TimeTrial = 68,
                    Descent = 65, Hill = climber ? 76 : 55, MediumMountain = climber ? 78 : 58,
                    Prologue = 66, Cobbles = 60
                },
                Roles = { Rider.ToString(climber ? RiderSpecialty.Climber : RiderSpecialty.Rouleur) }
            });
        }
        return (teams, riders);
    }

    [Fact]
    public void ElTour_SimulaTodasLasEtapas_YGeneraClasificaciones()
    {
        var (teams, riders) = Roster();
        var tour = new TourSimulator(RulesConfig.Default(), 42).Run(Stages, teams, riders);

        Assert.Equal(32, tour.GcStandings().Count);
        Assert.Equal(32, tour.PointsStandings().Count);
        Assert.NotEmpty(tour.KoMStandings());
        Assert.NotEmpty(tour.TeamStandings());
        // Jóvenes: todos nacen en rango no determinable (sin birth_date) → vacío es válido.
    }

    [Fact]
    public void ElGCSeAcumula_Progresivamente()
    {
        var (teams, riders) = Roster();
        var tour = new TourSimulator(RulesConfig.Default(), 42).Run(Stages, teams, riders);

        // El GC acumula los tiempos de las 5 etapas: el líder debe tener < 60 min reales de ventaja nula.
        var gc = tour.GcStandings();
        double maxGap = gc[^1].GcSeconds - gc[0].GcSeconds;
        Assert.True(maxGap > 0, "Debe haber diferencias de GC.");
        Assert.True(maxGap < 5 * 3600, $"Gap de GC {maxGap:0}s implausible para 5 etapas.");
    }

    [Fact]
    public void MismaSeed_MismoTour()
    {
        var (t1, r1) = Roster();
        var (t2, r2) = Roster();
        var a = new TourSimulator(RulesConfig.Default(), 42).Run(Stages, t1, r1);
        var b = new TourSimulator(RulesConfig.Default(), 42).Run(Stages, t2, r2);

        var gcA = a.GcStandings().Select(c => (c.RiderId, c.GcSeconds)).ToList();
        var gcB = b.GcStandings().Select(c => (c.RiderId, c.GcSeconds)).ToList();
        Assert.Equal(gcA, gcB);
    }

    [Fact]
    public void ElLiderDelGC_Final_EsElQueMenosTiempoAcumula()
    {
        var (teams, riders) = Roster();
        var tour = new TourSimulator(RulesConfig.Default(), 42).Run(Stages, teams, riders);
        var gc = tour.GcStandings();
        Assert.True(gc[0].GcSeconds <= gc[^1].GcSeconds);
        // Con 4 escaladores dominantes, lo normal es que el GC lo lidere un escalador.
        Assert.Contains(gc[0].RiderId, new[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void TourCompletoDeLaGrandeBoucle_CorreSinErrores()
    {
        // Ruta real de datos (skip si no existe en CI).
        string manifest = Path.Combine(Directory.GetCurrentDirectory(), "../../../../data/stages/grande_boucle_2026.json");
        // fallback: buscar en el repo
        if (!File.Exists(manifest))
        {
            var candidates = new[]
            {
                "data/stages/grande_boucle_2026.json",
                "../../../../data/stages/grande_boucle_2026.json"
            };
            manifest = candidates.FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException("Manifiesto de la Grande Boucle no encontrado.");
        }

        var (name, stages) = TourLoader.Load(manifest);
        Assert.Equal(21, stages.Count);
        var (teams, riders) = Roster();
        var tour = new TourSimulator(RulesConfig.Default(), 99).Run(stages, teams, riders);
        Assert.True(tour.GcStandings().Count > 0);
    }
}