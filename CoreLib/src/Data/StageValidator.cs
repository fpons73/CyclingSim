using ProCycling.Core.Models;

namespace ProCycling.Core.Data;

/// <summary>
/// Validador de etapas (PRD §28, §9): comprueba la coherencia de una etapa
/// descrita en JSON/CSV para que pueda ser usada sin recompilar el motor.
/// Reglas: secciones contiguas y no solapadas, sprints intermedios dentro del
/// recorrido, puertos (KoM) con categoría y puntos válidos, y viento con dirección
/// conocida. No modifica la etapa; sólo la comprueba.
/// </summary>
public static class StageValidator
{
    public sealed record Issue(Severity Level, string Message);
    public enum Severity { Error, Warning }

    public static IReadOnlyList<Issue> Validate(Stage stage)
    {
        var issues = new List<Issue>();
        if (stage is null) { issues.Add(new(Severity.Error, "La etapa es null.")); return issues; }

        if (string.IsNullOrWhiteSpace(stage.Id))
            issues.Add(new(Severity.Error, "El id de la etapa es obligatorio."));
        if (stage.DistanceKm <= 0)
            issues.Add(new(Severity.Error, $"La distancia debe ser > 0 (recibida {stage.DistanceKm})."));

        if (stage.Sections.Count == 0)
        {
            issues.Add(new(Severity.Error, "La etapa no tiene secciones."));
            return issues;
        }

        // --- Secciones contiguas y no solapadas ---
        double cursor = 0;
        for (int i = 0; i < stage.Sections.Count; i++)
        {
            var sec = stage.Sections[i];
            if (sec.KmFrom < 0 || sec.KmTo < sec.KmFrom)
                issues.Add(new(Severity.Error, $"Sección {i}: km_inválidos ({sec.KmFrom} → {sec.KmTo})."));
            if (sec.TerrainsRaw.Count == 0)
                issues.Add(new(Severity.Error, $"Sección {i}: sin terrenos."));

            if (Math.Abs(sec.KmFrom - cursor) > 0.001)
                issues.Add(new(Severity.Error,
                    $"Sección {i}: empieza en {sec.KmFrom} pero la anterior terminó en {cursor:0.###} (debe ser contigua)."));
            cursor = Math.Max(cursor, sec.KmTo);

            if (Math.Abs(sec.GradientPct) > 20)
                issues.Add(new(Severity.Warning,
                    $"Sección {i}: pendiente {sec.GradientPct}% fuera de rango razonable (±20%)."));

            if (sec.LimitadoWind() is { } wind)
            {
                if (wind.Strength is < 0 or > 12)
                    issues.Add(new(Severity.Warning,
                        $"Sección {i}: fuerza de viento {wind.Strength} no válida (0–12)."));
            }
        }

        // La última sección debe cubrir la distancia declarada.
        if (Math.Abs(cursor - stage.DistanceKm) > 0.01)
            issues.Add(new(Severity.Warning,
                $"La distancia de la etapa ({stage.DistanceKm} km) no coincide con la suma de secciones ({cursor:0.###} km)."));

        if (!stage.Sections.Any(s => s.Finish) && stage.DistanceKm > 0)
            issues.Add(new(Severity.Warning, "Ninguna sección está marcada como meta (finish=true)."));

        // --- Sprint intermedio ---
        foreach (var sec in stage.Sections.Where(s => s.IntermediateSprint is not null))
        {
            var sprint = sec.IntermediateSprint!;
            if (sprint.Km <= 0 || sprint.Km > stage.DistanceKm)
                issues.Add(new(Severity.Error, $"Sprint intermedio en km {sprint.Km} fuera del recorrido."));
            else if (sprint.Km < sec.KmFrom || sprint.Km > sec.KmTo)
                issues.Add(new(Severity.Warning,
                    $"Sprint intermedio en km {sprint.Km} fuera de su sección ({sec.KmFrom}–{sec.KmTo})."));
            if (Math.Abs(sprint.Km - stage.DistanceKm) < 1.0)
                issues.Add(new(Severity.Warning,
                    "Sprint intermedio demasiado cerca de la meta (debe ser un sprint de llegada, no intermedio)."));
            if (sprint.Points.Length == 0)
                issues.Add(new(Severity.Warning, $"Sprint en km {sprint.Km} sin puntos."));
        }

        // --- Puertos (KoM) ---
        foreach (var climb in stage.Climbs)
        {
            if (climb.Category is < 0 or > 4)
                issues.Add(new(Severity.Warning,
                    $"Puerto '{climb.Name}' con categoría {climb.Category} (0–4)."));
            if (climb.LengthKm <= 0)
                issues.Add(new(Severity.Error, $"Puerto '{climb.Name}' con longitud inválida."));
            if (climb.KmFrom >= climb.KmTo)
                issues.Add(new(Severity.Error,
                    $"Puerto '{climb.Name}' con intervalo inválido ({climb.KmFrom}–{climb.KmTo})."));
            if (climb.SummitKm < climb.KmFrom || climb.SummitKm > climb.KmTo)
                issues.Add(new(Severity.Warning,
                    $"Puerto '{climb.Name}': la cumbre ({climb.SummitKm}) debe estar dentro del intervalo."));
            if (climb.KoM_Points.Length == 0)
                issues.Add(new(Severity.Warning,
                    $"Puerto '{climb.Name}' sin tabla de puntos KoM (se usará la tabla por categoría)."));

            // Cada puerto debe estar anclado a una sección con subida (climb_id).
            if (stage.Sections.All(s => s.ClimbId != climb.Id))
                issues.Add(new(Severity.Warning,
                    $"Puerto '{climb.Name}': ninguna sección declara climb_id=\"{climb.Id}\"."));
        }

        // --- Referencias a climbs_id sin puerto definido ---
        foreach (var sec in stage.Sections.Where(s => !string.IsNullOrEmpty(s.ClimbId)))
        {
            if (stage.Climbs.All(c => c.Id != sec.ClimbId))
                issues.Add(new(Severity.Error,
                    $"Sección {sec.KmFrom}–{sec.KmTo} referencia climb_id=\"{sec.ClimbId}\" sin puerto definido."));
        }

        return issues;
    }

    public static bool IsValid(Stage stage) =>
        Validate(stage).All(i => i.Level != Severity.Error);
}

/// <summary>Extensión para acceder al viento de una sección de forma segura.</summary>
public static class StageSectionWind
{
    public static WindInfo? LimitadoWind(this StageSection sec) => sec.Wind;
}