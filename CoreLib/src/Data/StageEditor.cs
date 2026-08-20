using ProCycling.Core.Models;

namespace ProCycling.Core.Data;

/// <summary>
/// Editor de etapas (PRD §28, Fase 4 — "Editor de etapas", Roadmap §42).
/// Permite construir una etapa programáticamente (secciones contiguas,
/// sprints, puertos, viento) y validarla/exportarla a JSON.
/// El objetivo es poder crear o modificar recorridos sin recompilar el motor.
/// </summary>
public sealed class StageEditor
{
    private readonly Stage _stage = new();

    public StageEditor(string id, string name, StageType type, double distanceKm)
    {
        _stage.Id = id;
        _stage.Name = name;
        _stage.DistanceKm = distanceKm;
        _stage.TypeRaw = type.ToString().ToLowerInvariant();
    }

    public StageEditor Season(int seasonId) { _stage.SeasonId = seasonId; return this; }
    public StageEditor Date(string date) { _stage.Date = date; return this; }
    public StageEditor TimeFactor(double t) { _stage.TimeFactor = t; return this; }
    public StageEditor TempoModifier(double t) { _stage.TempoModifier = t; return this; }

    /// <summary>Añade una sección contigua a la anterior.</summary>
    public StageEditor Section(double lengthKm, double gradientPct, params Terrain[] terrains)
    {
        if (lengthKm < 0) throw new ArgumentOutOfRangeException(nameof(lengthKm), "La sección no puede tener longitud negativa.");
        var from = _stage.Sections.Count == 0 ? 0 : _stage.Sections[^1].KmTo;
        var terrainsRaw = terrains.Length == 0
            ? new List<string> { "flat" }
            : terrains.Select(t => t.ToString().ToLowerInvariant()).ToList();
        _stage.Sections.Add(new StageSection
        {
            KmFrom = from,
            KmTo = from + lengthKm,
            GradientPct = gradientPct,
            TerrainsRaw = terrainsRaw
        });
        return this;
    }

    public StageEditor SectionF(double lengthKm, double gradientPct, IEnumerable<string> terrainsRaw)
    {
        if (lengthKm < 0) throw new ArgumentOutOfRangeException(nameof(lengthKm), "La sección no puede tener longitud negativa.");
        var from = _stage.Sections.Count == 0 ? 0 : _stage.Sections[^1].KmTo;
        _stage.Sections.Add(new StageSection
        {
            KmFrom = from,
            KmTo = from + lengthKm,
            GradientPct = gradientPct,
            TerrainsRaw = terrainsRaw.ToList()
        });
        return this;
    }

    /// <summary>Marca la sección actual como meta (finish=true).</summary>
    public StageEditor Finish()
    {
        if (_stage.Sections.Count == 0)
            throw new InvalidOperationException("No hay secciones para marcar como meta.");
        _stage.Sections[^1].Finish = true;
        return this;
    }

    /// <summary>Añade un sprint intermedio en la sección que contiene `km`.</summary>
    public StageEditor Sprint(double km, params int[] points)
    {
        var section = _stage.Sections.FirstOrDefault(s => km >= s.KmFrom && km <= s.KmTo)
            ?? throw new ArgumentException($"El sprint en km {km} no cae en ninguna sección.");
        section.IntermediateSprint = new SprintInfo { Km = km, Points = points };
        return this;
    }

    /// <summary>Puntos KoM por categoría usados por defecto cuando la etapa no los declara.</summary>
    public static int[] DefaultKoMPoints(int category) => category switch
    {
        4 => new[] { 2, 1 },
        3 => new[] { 5, 3, 2, 1 },
        2 => new[] { 7, 5, 3, 2, 1 },
        1 => new[] { 10, 8, 6, 4, 2, 1 },
        _ => new[] { 20, 15, 12, 10, 8, 6, 4, 2 }
    };

    /// <summary>Añade un puerto KoM anclado a secciones existentes.</summary>
    /// <param name="points">Tabla de puntos KoM; si es null se usa la tabla por defecto de su categoría.</param>
    public StageEditor AddClimb(string id, string name, double kmFrom, double kmTo, int category, double avgGradient, double? summitKm = null, int[]? points = null)
    {
        var climbed = _stage.Sections.FirstOrDefault(s => Math.Abs(s.KmFrom - kmFrom) < 0.001 && Math.Abs(s.KmTo - kmTo) < 0.001);
        if (climbed is null && _stage.Sections.Any(s => s.KmFrom >= kmFrom && s.KmTo <= kmTo))
        {
            climbed = _stage.Sections.First(s => s.KmFrom >= kmFrom && s.KmTo <= kmTo);
        }
        if (climbed is null)
            throw new ArgumentException($"No hay sección {kmFrom}–{kmTo} para anclar el puerto '{name}'.");

        climbed.ClimbId = id;
        climbed.TerrainsRaw = new List<string> { "climb" };
        climbed.GradientPct = avgGradient;
        _stage.Climbs.Add(new Climb
        {
            Id = id,
            Name = name,
            KmFrom = kmFrom,
            KmTo = kmTo,
            Category = category,
            LengthKm = kmTo - kmFrom,
            AvgGradient = avgGradient,
            SummitKm = summitKm ?? kmTo,
            KoM_Points = points ?? DefaultKoMPoints(category)
        });
        return this;
    }

    public StageEditor Wind(double kmFrom, double kmTo, string direction, int strength)
    {
        var section = _stage.Sections.FirstOrDefault(s => s.KmFrom >= kmFrom && s.KmTo <= kmTo)
            ?? throw new ArgumentException($"No hay sección para el viento {kmFrom}–{kmTo}.");
        section.Wind = new WindInfo { DirectionRaw = direction, Strength = strength };
        return this;
    }

    /// <summary>Devuelve la etapa construida (tras validar que es estructuralmente válida).</summary>
    public Stage Build()
    {
        var issues = StageValidator.Validate(_stage);
        var errors = issues.Where(i => i.Level == StageValidator.Severity.Error).ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException(
                "La etapa no es válida:\n" + string.Join("\n", errors.Select(e => $"  - {e.Message}")));
        return _stage;
    }
}