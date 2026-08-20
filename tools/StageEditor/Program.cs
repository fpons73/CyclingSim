using System.Text.Json;
using ProCycling.Core.Data;
using ProCycling.Core.Models;

namespace ProCycling.Tools.StageEditorTool;

/// <summary>
/// Editor de etapas por línea de comandos (PRD §28, fase 4 — "Editor de etapas").
///
/// Uso:
///   StageEditor validate data/stages/flat1.json
///   StageEditor profile  data/stages/flat1.json          (perfil de altimetría)
///   StageEditor demo out.json                           (etapa de ejemplo)
///
/// Permite crear y validar etapas sin recompilar el motor del juego.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0) { PrintUsage(); return 0; }

        return args[0].ToLowerInvariant() switch
        {
            "validate" when args.Length >= 2 => Validate(args[1]),
            "profile" when args.Length >= 2 => Profile(args[1]),
            "demo" when args.Length >= 2 => Demo(args[1]),
            _ => PrintUsage()
        };
    }

    private static int PrintUsage()
    {
        Console.WriteLine("Etapas JSON (Sin recompilar)");
        Console.WriteLine("  validate <etapa.json>   Valida una etapa y muestra avisos/errores.");
        Console.WriteLine("  profile  <etapa.json>   Muestra el perfil de altimetría en ASCII.");
        Console.WriteLine("  demo     <salida.json>  Crea una etapa de ejemplo y la guarda.");
        return 0;
    }

    private static int Validate(string path)
    {
        if (!File.Exists(path)) { Console.Error.WriteLine($"No existe el archivo: {path}"); return 2; }
        Stage stage;
        try { stage = StageJsonLoader.LoadFile(path); }
        catch (Exception e) { Console.Error.WriteLine($"Error de JSON: {e.Message}"); return 2; }

        var issues = StageValidator.Validate(stage);
        Console.WriteLine($"Etapa: {stage.Id} — {stage.Name} ({stage.DistanceKm} km)");
        if (issues.Count == 0) { Console.WriteLine("Válida (sin avisos)."); return 0; }

        foreach (var issue in issues)
            Console.WriteLine($"  [{(issue.Level == StageValidator.Severity.Error ? "ERROR" : "aviso")}] {issue.Message}");
        return issues.Any(i => i.Level == StageValidator.Severity.Error) ? 1 : 0;
    }

    private static int Profile(string path)
    {
        if (!File.Exists(path)) { Console.Error.WriteLine($"No existe el archivo: {path}"); return 2; }
        Stage stage;
        try { stage = StageJsonLoader.LoadFile(path); }
        catch (Exception e) { Console.Error.WriteLine($"Error de JSON: {e.Message}"); return 2; }

        const int width = 60;
        double totalH = stage.Sections.Count == 0 ? 0 :
            stage.Sections.Max(s => Math.Abs(s.GradientPct)) * 2;
        if (totalH <= 0) totalH = 5;

        Console.WriteLine($"Perfil de {stage.Name} ({stage.DistanceKm} km)");
        for (int row = 0; row < width; row++)
        {
            var sb = new char[width];
            Array.Fill(sb, ' ');
            for (int i = 0; i < stage.Sections.Count; i++)
            {
                var sec = stage.Sections[i];
                int x0 = (int)(width * sec.KmFrom / stage.DistanceKm);
                int x1 = (int)(width * sec.KmTo / stage.DistanceKm);
                double g = Math.Clamp(sec.GradientPct, -totalH / 2, totalH / 2);
                int y = (int)((g + totalH / 2) / totalH * (width - 1));
                if (row == y)
                    for (int x = x0; x < Math.Min(x1, width); x++) sb[x] = sec.Terrains.Count > 0 && sec.Terrains.Contains(Terrain.Climb) ? '^' : '-';
            }
            Console.WriteLine(new string(sb));
        }
        return 0;
    }

    private static int Demo(string outPath)
    {
        var stage = new StageEditor("demo_vuelta", "Etapa de demostración", StageType.Flat, 175.0)
            .Season(2026)
            .Date("2026-07-01")
            .Section(45.0, 0.5, Terrain.Flat, Terrain.Flat, Terrain.Rolling)
            .Sprint(45.0, 20, 17, 15, 13, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1)
            .Section(60.0, 1.0, Terrain.Rolling, Terrain.Rolling, Terrain.Hill)
            .Section(18.0, 6.0, Terrain.Climb)
            .AddClimb("c1", "Alto de la Fuente", 105.0, 123.0, 3, 6.0, summitKm: 123.0)
            .Section(22.0, -4.0, Terrain.Descent, Terrain.Rolling)
            .Section(30.0, 0.3, Terrain.Flat, Terrain.Rolling, Terrain.Flat)
            .Finish()
            .Wind(123.0, 175.0, "head", 3)
            .TimeFactor(1.0)
            .TempoModifier(0.0)
            .Build();

        var json = JsonSerializer.Serialize(stage, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outPath, json);
        Console.WriteLine($"Etapa de demostración creada: {outPath}");
        var issues = StageValidator.Validate(stage);
        foreach (var issue in issues)
            Console.WriteLine($"  [aviso] {issue.Message}");
        return 0;
    }
}