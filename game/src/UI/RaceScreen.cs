using Godot;
using ProCycling.Core.Models;

namespace ProCycling.Game.UI;

/// <summary>Pantalla de carrera: muestra el log de la simulación y el estado de grupos.</summary>
public partial class RaceScreen : Control
{
    private RichTextLabel? _log;
    private Label? _groups;
    private Godot.Timer? _displayTimer;
    private float _elapsed;

    public override void _Ready()
    {
        if (GameManager.State is null)
        {
            GetTree().ChangeSceneToFile("res://src/UI/PreStageScreen.tscn");
            return;
        }
        Build();

        // La simulación se ejecuta en "tiempo real"; al terminar, pasamos a PostStage.
        if (GameManager.Results is null)
        {
            GameManager.RunRace();
            _displayTimer?.Start();
        }
        else
        {
            ShowResults();
        }
    }

    private void Build()
    {
        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(root);

        var header = new Label
        {
            Text = $"EN CARRERA — {GameManager.Stage?.Name}",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(header);

        _groups = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        root.AddChild(_groups);

        _log = new RichTextLabel();
        _log.CustomMinimumSize = new Vector2(0, 460);
        _log.BbcodeEnabled = false;
        root.AddChild(_log);

        _displayTimer = new Godot.Timer { WaitTime = 0.05 };
        _displayTimer.Timeout += OnTick;
        AddChild(_displayTimer);
    }

    private void OnTick()
    {
        _elapsed += 0.05f;
        ShowResults();
        if (GameManager.Results is not null)
        {
            _displayTimer!.Stop();
            CallDeferred(nameof(GoPost));
        }
    }

    private void ShowResults()
    {
        var state = GameManager.State!;
        _groups!.Text = string.Join("\n", state.Groups.Select(g =>
            $"{g.Kind} #{g.Id} · {g.MemberRiderIds.Count} corredores · gap {g.GapSeconds:0} s"));

        var log = string.Join("\n", state.ActionLog.Skip(Math.Max(0, state.ActionLog.Count - 30)));
        _log!.Text = log;
    }

    private void GoPost()
    {
        GetTree().ChangeSceneToFile("res://src/UI/PostStageScreen.tscn");
    }
}