using ProCycling.Core.Data;

namespace ProCycling.Core.Tests;

public class SeasonImporterTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"pcrm_test_{Guid.NewGuid():N}.sqlite");

    private static ImportTeam Team(string name, int season, string category = "WorldTour") =>
        new(name, name[..Math.Min(3, name.Length)], "FRA", category, season);

    private static ImportRider Rider(string name, string team, int season, int spr = 60, int mnt = 60, int number = 1) =>
        new(name, "1998-05-01", "FRA", team, season, number,
            new[] { spr >= 80 ? "sprinter" : mnt >= 80 ? "climber" : "rouleur" },
            spr + 10, mnt + 10, 60, 60, 60, 60, 60, spr, 60, 60, 60, 60, 60, 60);

    [Fact]
    public void ImportaDosTemporadas_YLasCargaIndependientes()
    {
        using (var imp = SeasonImporter.Open(_db, overwrite: true))
        {
            imp.UpsertSeason(2, "Season 2025", 2025);
            imp.ImportSeason(
                new[] { Team("Alpha", 2), Team("Bravo", 2) },
                new[] { Rider("A1", "Alpha", 2, spr: 85), Rider("B1", "Bravo", 2, mnt: 88) });

            imp.UpsertSeason(3, "Season 2026", 2026);
            imp.ImportSeason(
                new[] { Team("Alpha", 3), Team("Charlie", 3) },
                new[] { Rider("A2", "Alpha", 3, spr: 78), Rider("C1", "Charlie", 3) });
        }

        var seasons = SqliteStore.LoadSeasons(_db);
        Assert.Equal(2, seasons.Count);
        Assert.Contains(seasons, s => s.Year == 2025);
        Assert.Contains(seasons, s => s.Year == 2026);

        var (teams25, riders25) = SqliteStore.LoadSeason(_db, 2);
        Assert.Equal(2, teams25.Count);
        Assert.Equal(2, riders25.Count);
        Assert.All(riders25, r => Assert.Equal(2, r.SeasonId));
        var a1 = riders25.First(r => r.Name == "A1");
        Assert.Equal(85, a1.Attributes.Sprint);
        Assert.Contains(a1.Roles, role => role == "sprinter");

        var (_, riders26) = SqliteStore.LoadSeason(_db, 3);
        Assert.Equal(2, riders26.Count);
        Assert.All(riders26, r => Assert.Equal(3, r.SeasonId));
        Assert.Contains(riders26, r => r.Name == "C1");
        Assert.DoesNotContain(riders26, r => r.Name == "A1" || r.Name == "B1");
    }

    [Fact]
    public void ReimportarLaMismaTemporada_ReemplazaContenido()
    {
        using (var imp = SeasonImporter.Open(_db, overwrite: true))
        {
            imp.UpsertSeason(3, "Season 2026", 2026);
            imp.ImportSeason(new[] { Team("Alpha", 3) }, new[] { Rider("A", "Alpha", 3) });
            imp.ImportSeason(new[] { Team("Alpha", 3) }, new[] { Rider("A2", "Alpha", 3), Rider("A3", "Alpha", 3) });
        }

        var (_, riders) = SqliteStore.LoadSeason(_db, 3);
        Assert.Equal(2, riders.Count);
        Assert.DoesNotContain(riders, r => r.Name == "A");
    }

    [Fact]
    public void EquipoDesconocido_EsRechazado()
    {
        using var imp = SeasonImporter.Open(_db, overwrite: true);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            imp.ImportSeason(new[] { Team("Alpha", 3) }, new[] { Rider("A", "Salmoncorp", 3) }));
        Assert.Contains("Salmoncorp", ex.Message);
    }

    [Fact]
    public void EnsureSchemaNoDamage_SobreBDExistenteDelProyecto()
    {
        // El esquema debe ser compatible con la BD generada por import_data.py.
        string root = ConfigAndStageTests.FindRepoRoot();
        string db = Path.Combine(root, "data", "pcrm.sqlite");
        if (!File.Exists(db)) return;

        using (var imp = SeasonImporter.Open(db, overwrite: false))
        {
        }
        using var con = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={db}");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM seasons";
        Assert.True((long)cmd.ExecuteScalar()! >= 1);
    }

    public void Dispose()
    {
        if (File.Exists(_db)) File.Delete(_db);
    }
}