using System.Text.Json;
using Godot;
using ProCycling.Core.Localization;

namespace ProCycling.Game.UI;

/// <summary>
/// Localización para la UI del juego (PRD §38): carga los diccionarios
/// es/en/fr desde res://data/i18n/<locale>.json usando el Localizer de CoreLib.
/// Antes de usarla hay que llamar a Load() (tras GameManager.LoadData()).
/// </summary>
public static class GameLocalizer
{
    private static readonly Localizer Inner = new();
    private static string _locale = Localizer.DefaultLocale;

    public static bool Loaded { get; private set; }

    public static string Locale
    {
        get => _locale;
        set { Inner.Locale = value; _locale = value; }
    }

    public static string T(string key) => Inner.T(key);

    public static string T(string key, params object[] args) => Inner.T(key, args);

    /// <summary>Carga res://data/i18n/*.json. Devuelve false si no encuentra al menos un idioma.</summary>
    public static bool Load(string dataDir = "res://data")
    {
        foreach (var code in Localizer.SupportedLocales)
        {
            using var file = Godot.FileAccess.Open($"{dataDir}/i18n/{code}.json", Godot.FileAccess.ModeFlags.Read);
            if (file is null) continue;
            try
            {
                Inner.Add(code, JsonSerializer.Deserialize<Dictionary<string, string>>(file.GetAsText())
                               ?? new Dictionary<string, string>());
            }
            catch (JsonException)
            {
                // diccionario corrupto: se ignora, se usará el fallback
            }
        }
        Loaded = Inner.IsLoaded(Localizer.DefaultLocale) || Inner.IsLoaded("en") || Inner.IsLoaded("fr");
        return Loaded;
    }
}