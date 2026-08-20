using Microsoft.Data.Sqlite;
using ProCycling.Core.Data;

namespace ProCycling.Core.Tests;

public class SqliteStoreTests
{
    [Fact]
    public void CargaTemporada2026_DesdeLaBDGenerada()
    {
        string root = ConfigAndStageTests.FindRepoRoot();
        string db = Path.Combine(root, "data", "pcrm.sqlite");
        Assert.True(File.Exists(db), "No existe data/pcrm.sqlite (ejecuta tools/import_data.py)");

        var (teams, riders) = SqliteStore.LoadSeason(db, 3);

        Assert.True(riders.Count >= 3320, $"Esperado ≥3320 corredores, obtenidos {riders.Count}");
        Assert.True(teams.Count >= 209, $"Esperado ≥209 equipos, obtenidos {teams.Count}");

        var pog = riders.First(r => r.Name.Contains("Pogacar", StringComparison.OrdinalIgnoreCase));
        Assert.True(pog.Attributes.Mountain >= 80, $"Pogacar MNT debería ser alto, es {pog.Attributes.Mountain}");
        Assert.Contains(pog.Roles, r => r == "climber");

        var sprinter = riders.OrderByDescending(r => r.Attributes.Sprint).First();
        Assert.True(sprinter.Attributes.Sprint >= 80);
    }

    [Fact]
    public void Edades_JovenesSub25_Detectadas()
    {
        string root = ConfigAndStageTests.FindRepoRoot();
        var (_, riders) = SqliteStore.LoadSeason(Path.Combine(root, "data", "pcrm.sqlite"), 3);

        int young = riders.Count(r => r.IsYoungFor(2026));
        Assert.True(young > 500, $"Esperado >500 sub-25 en 2026, obtenidos {young}");

        // Las fechas inválidas no rompen la carga
        Assert.True(riders.All(r => r.BirthDate == null || DateTime.TryParse(r.BirthDate, out _)));
    }

    [Fact]
    public void LosEquiposSinProblemaDeFichas_SeCarganConCategoriaDesconocida()
    {
        string root = ConfigAndStageTests.FindRepoRoot();
        var (teams, _) = SqliteStore.LoadSeason(Path.Combine(root, "data", "pcrm.sqlite"), 3);
        // 12 equipos sintéticos de plantillas sin ficha en Equipos_2026.xlsx
        Assert.Equal("Unknown", teams.First(t => t.Name == "KSPO").Category);
    }
}