using Godot;
using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Game.UI;

/// <summary>Pantalla post-etapa: resultados, clasificaciones, guardado y exportación CSV/HTML.</summary>
public partial class PostStageScreen : Control
{
    private RichTextLabel? _results;

    public override void _Ready()
    {
        if (GameManager.Results is null)
        {
            GetTree().ChangeSceneToFile("res://src/UI/PreStageScreen.tscn");
            return;
        }
        Build();
        ShowResults();
    }

    private void Build()
    {
        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(root);

        var header = new Label
        {
            Text = $"RESULTADOS — {GameManager.Stage?.Name}",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(header);

        _results = new RichTextLabel { BbcodeEnabled = false, CustomMinimumSize = new Vector2(0, 520) };
        root.AddChild(_results);

        var bar = new HBoxContainer();
        root.AddChild(bar);

        var save = new Button { Text = "💾 Guardar" };
        save.Pressed += SaveGame;
        bar.AddChild(save);

        var csv = new Button { Text = "CSV" };
        csv.Pressed += ExportCsv;
        bar.AddChild(csv);

        var html = new Button { Text = "HTML" };
        html.Pressed += ExportHtml;
        bar.AddChild(html);

        var again = new Button { Text = "↺ Nueva etapa" };
        again.Pressed += () => GetTree().ChangeSceneToFile("res://src/UI/PreStageScreen.tscn");
        bar.AddChild(again);

        var quit = new Button { Text = "Salir" };
        quit.Pressed += () => GetTree().Quit();
        bar.AddChild(quit);
    }

    private void ShowResults()
    {
        var standings = GameManager.Classifications();
        _results!.Text = string.Join("\n", standings);
    }

    private static void SaveGame()
    {
        string path = ProjectSettings.GlobalizePath("user://saves");
        System.IO.Directory.CreateDirectory(path);
        string file = System.IO.Path.Combine(path, $"race_{GameManager.Seed}.json");
        var snapshot = new
        {
            GameManager.Seed,
            stage = GameManager.Stage?.Id,
            results = GameManager.Results
        };
        System.IO.File.WriteAllText(file,
            System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"[PCRM] Guardado en {file}");
    }

    private static void ExportCsv()
    {
        string path = ProjectSettings.GlobalizePath("user://exports");
        System.IO.Directory.CreateDirectory(path);
        string file = System.IO.Path.Combine(path, $"stage_{GameManager.Seed}.csv");
        var rows = new System.Text.StringBuilder();
        rows.AppendLine("pos,rider,team,stage_s,points,gc_s");
        var order = GameManager.Results!
            .OrderBy(r => r.StageSeconds)
            .ToList();
        for (int i = 0; i < order.Count; i++)
        {
            var r = order[i];
            string rider = GameManager.RiderName(r.RiderId);
            string team = GameManager.TeamName(r.TeamId);
            rows.AppendLine($"{i + 1},{rider},{team},{r.StageSeconds:F2},{r.PointsEarned},{r.StageSeconds:F2}");
        }
        System.IO.File.WriteAllText(file, rows.ToString());
        GD.Print($"[PCRM] CSV exportado: {file}");
    }

    private static void ExportHtml()
    {
        string path = ProjectSettings.GlobalizePath("user://exports");
        System.IO.Directory.CreateDirectory(path);
        string file = System.IO.Path.Combine(path, $"stage_{GameManager.Seed}.html");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Resultados</title></head><body>");
        sb.AppendLine($"<h1>Pro Cycling Replay Manager — {GameManager.Stage?.Name}</h1>");
        sb.AppendLine("<table border='1' cellpadding='4'><tr><th>Pos</th><th>Corredor</th><th>Equipo</th><th>Tiempo</th><th>Puntos</th></tr>");
        foreach (var (r, i) in GameManager.Results!.OrderBy(r => r.StageSeconds).Select((r, i) => (r, i)))
        {
            sb.AppendLine($"<tr><td>{i + 1}</td><td>{GameManager.RiderName(r.RiderId)}</td>" +
                          $"<td>{GameManager.TeamName(r.TeamId)}</td>" +
                          $"<td>{RiderCard.FormatTime(r.StageSeconds)}</td><td>{r.PointsEarned}</td></tr>");
        }
        sb.AppendLine("</table></body></html>");
        System.IO.File.WriteAllText(file, sb.ToString());
        GD.Print($"[PCRM] HTML exportado: {file}");
    }
}