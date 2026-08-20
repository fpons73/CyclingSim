using ProCycling.Core.Localization;

namespace ProCycling.Core.Tests;

public class LocalizerTests
{
    private static Localizer Sample() => new Localizer()
        .Add("es", new Dictionary<string, string>
        {
            ["greet"] = "Hola {0}",
            ["only_es"] = "solo español",
            ["missing_fr"] = "presente en es"
        })
        .Add("en", new Dictionary<string, string>
        {
            ["greet"] = "Hello {0}"
        })
        .Add("fr", new Dictionary<string, string>
        {
            ["greet"] = "Bonjour {0}",
            ["fr_only"] = "único francés"
        });

    [Fact]
    public void IdiomaPorDefecto_EsEspanol()
    {
        var l = Sample();
        Assert.Equal("es", l.Locale);
    }

    [Fact]
    public void TraduccionBasica_Y_Interpolacion()
    {
        var l = Sample();
        l.Locale = "en";
        Assert.Equal("Hello Paco", l.T("greet", "Paco"));
        l.Locale = "es";
        Assert.Equal("Hola Paco", l.T("greet", "Paco"));
    }

    [Fact]
    public void FaltaDeClave_EnIdiomaActivo_UsaFallbackEspanol()
    {
        var l = Sample();
        l.Locale = "fr";
        Assert.Equal("solo español", l.T("only_es"));
        Assert.Equal("presente en es", l.T("missing_fr"));
    }

    [Fact]
    public void ClaveInexistente_DevuelveLaPropiaClave()
    {
        var l = Sample();
        Assert.Equal("no_existe", l.T("no_existe"));
    }

    [Fact]
    public void IdiomaNoSoportado_EsRechazado()
    {
        var l = Sample();
        Assert.Throws<ArgumentException>(() => l.Locale = "de");
    }

    [Fact]
    public void TodosLosIdiomasSoportados_PuedenRegistrarse()
    {
        var l = Sample();
        foreach (var code in Localizer.SupportedLocales)
            Assert.True(l.IsLoaded(code), $"No registrado: {code}");
    }

    [Fact]
    public void LoadFromDirectory_LeeLosTresIdiomas()
    {
        var reader = new DictionaryLocalizationReader(new Dictionary<string, string>
        {
            ["es.json"] = "{\"key\":\"valor\"}",
            ["en.json"] = "{\"key\":\"value\"}",
            ["fr.json"] = "{\"key\":\"valeur\"}"
        });

        var l = Localizer.LoadFromDirectory(reader, "@temp");
        l.Locale = "en";
        Assert.Equal("value", l.T("key"));
        l.Locale = "fr";
        Assert.Equal("valeur", l.T("key"));
        l.Locale = "es";
        Assert.Equal("valor", l.T("key"));
    }

    [Fact]
    public void LoadFromDirectory_ConFicherosRealesDelRepositorio()
    {
        string root = FindRepoRoot();
        string dir = Path.Combine(root, "data", "i18n");
        var l = Localizer.LoadFromDirectory(new FileLocalizationReader(), dir);
        Assert.True(l.IsLoaded("es") && l.IsLoaded("en") && l.IsLoaded("fr"));

        l.Locale = "en";
        Assert.Equal("PRO CYCLING REPLAY MANAGER", l.T("app.title"));
        Assert.Equal("Stage", l.T("prestage.stage"));

        l.Locale = "fr";
        Assert.Equal("Étape", l.T("prestage.stage"));

        l.Locale = "es";
        Assert.Equal("12 corredores en start list (max 8/equipo).",
            l.T("prestage.startlist", 12));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "README.md")))
            dir = dir.Parent;
        if (dir is not null && File.Exists(Path.Combine(dir.FullName, "README.md")))
            return dir.FullName;

        // fallback: subir buscando data/i18n
        dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "i18n")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró data/i18n desde el directorio de pruebas.");
    }
}

public class DictionaryLocalizationReader : ILocalizationFileReader
{
    private readonly Dictionary<string, string> _files;
    public DictionaryLocalizationReader(Dictionary<string, string> files) => _files = files;

    public bool TryReadAllText(string path, out string content)
    {
        if (_files.TryGetValue(Path.GetFileName(path), out var c)) { content = c; return true; }
        content = string.Empty;
        return false;
    }
}