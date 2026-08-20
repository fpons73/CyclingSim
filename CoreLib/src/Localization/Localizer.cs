namespace ProCycling.Core.Localization;

/// <summary>
/// Servicio de localización (PRD §38). Idiomas: Español, Inglés, Francés.
/// Los textos se cargan desde ficheros JSON (data/i18n/&lt;locale&gt;.json) para poder
/// traducirlos o modificarlos sin recompilar el juego.
/// Comportamiento:
///   - fallback: si una clave falta en el idioma activo se usa el Español;
///   - si falta también en Español se devuelve la propia clave;
///   - soporta interpolación con {0}, {1}, ... 
/// </summary>
public sealed class Localizer
{
    private readonly Dictionary<string, Dictionary<string, string>> _locales = new();
    private readonly Dictionary<string, string> _fallback = new();
    private string _locale = "es";

    public const string DefaultLocale = "es";
    public static IReadOnlyList<string> SupportedLocales { get; } = new[] { "es", "en", "fr" };

    public string Locale
    {
        get => _locale;
        set
        {
            if (!_locales.ContainsKey(value) && !SupportedLocales.Contains(value))
                throw new ArgumentException($"Idioma no soportado: {value}");
            // Si no está cargado, se cargará bajo demanda no: exigimos registro previo.
            if (!_locales.ContainsKey(value))
                throw new InvalidOperationException($"No se ha cargado el idioma '{value}'.");

            _locale = value;
        }
    }

    /// <summary>Registra (o reemplaza) el diccionario de un idioma.</summary>
    public Localizer Add(string locale, IReadOnlyDictionary<string, string> entries)
    {
        _locales[locale] = new Dictionary<string, string>(entries, StringComparer.Ordinal);
        if (locale == DefaultLocale)
        {
            _fallback.Clear();
            foreach (var kv in entries) _fallback[kv.Key] = kv.Value;
        }
        return this;
    }

    public bool IsLoaded(string locale) => _locales.ContainsKey(locale);

    /// <summary>Traduce una clave al idioma activo (con fallback a Español).</summary>
    public string T(string key)
    {
        if (_locales.TryGetValue(_locale, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        if (_fallback.TryGetValue(key, out var es))
            return es;
        return key;
    }

    /// <summary>Traduce e interpola {0}, {1}, ... con los argumentos dados.</summary>
    public string T(string key, params object[] args) =>
        string.Format(T(key), args);

    /// <summary>Carga los idiomas desde un directorio con ficheros &lt;locale&gt;.json.</summary>
    public static Localizer LoadFromDirectory(ILocalizationFileReader reader, string dir)
    {
        var loc = new Localizer();
        foreach (var locale in SupportedLocales)
        {
            if (reader.TryReadAllText(Path.Combine(dir, $"{locale}.json"), out var json))
            {
                var dict = System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();
                loc.Add(locale, dict);
            }
        }
        return loc;
    }
}

/// <summary>Abstracción para leer ficheros (permite testear Localizer sin disco).</summary>
public interface ILocalizationFileReader
{
    bool TryReadAllText(string path, out string content);
}

/// <summary>Lector basado en disco.</summary>
public sealed class FileLocalizationReader : ILocalizationFileReader
{
    public bool TryReadAllText(string path, out string content)
    {
        if (File.Exists(path)) { content = File.ReadAllText(path); return true; }
        content = string.Empty;
        return false;
    }
}