using Godot;
using ProCycling.Core.Models;
using ProCycling.Core.Replay;

namespace ProCycling.Game.UI;

/// <summary>
/// Pantalla de carrera en modo espectador (PRD §23): tras simular, permite
/// pausar, acelerar/ralentizar y avanzar por secciones sobre el timeline de la
/// carrera, y revisar las decisiones IA de cada sección.
/// </summary>
public partial class RaceScreen : Control
{
    private RichTextLabel? _log;
    private Label? _groups;
    private Label? _stateLabel;
    private PlaybackController? _pc;
    private Godot.Timer? _autoTimer;
    private float _elapsed;

    public override void _Ready()
    {
        if (GameManager.State is null)
        {
            GetTree().ChangeSceneToFile("res://src/UI/PreStageScreen.tscn");
            return;
        }
        if (GameManager.Results is null)
            GameManager.RunRace();

        if (GameManager.Timeline is null || GameManager.Timeline.Snapshots.Count == 0)
        {
            GetTree().ChangeSceneToFile("res://src/UI/PostStageScreen.tscn");
            return;
        }

        _pc = new PlaybackController(GameManager.Timeline);
        Build();
        ShowSnapshot();

        // Reproducción automática lenta; el usuario puede pausar/avanzar.
        _autoTimer = new Godot.Timer { WaitTime = 0.35 };
        _autoTimer.Timeout += OnAutoTick;
        AddChild(_autoTimer);
        _autoTimer.Start();
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

        _stateLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        root.AddChild(_stateLabel);

        _groups = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        root.AddChild(_groups);

        _log = new RichTextLabel();
        _log.CustomMinimumSize = new Vector2(0, 380);
        _log.BbcodeEnabled = false;
        root.AddChild(_log);

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 8);
        root.AddChild(controls);

        var playPause = new Button { Text = "▶ / ⏸" };
        playPause.Pressed += () => { _pc!.Toggle(); UpdateState(); };
        controls.AddChild(playPause);

        var prev = new Button { Text = "◀ Sección" };
        prev.Pressed += () => { _pc!.Previous(); RefreshSnapshot(); };
        controls.AddChild(prev);

        var next = new Button { Text = "Sección ▶" };
        next.Pressed += () => { _pc!.Advance(); RefreshSnapshot(); };
        controls.AddChild(next);

        var slower = new Button { Text = "×0.5" };
        slower.Pressed += () => { _pc!.SlowDown(); UpdateState(); };
        controls.AddChild(slower);

        var faster = new Button { Text = "×2" };
        faster.Pressed += () => { _pc!.SpeedUp(); UpdateState(); };
        controls.AddChild(faster);

        var toEnd = new Button { Text = "→ Fin" };
        toEnd.Pressed += () => { _pc!.End(); RefreshSnapshot(); };
        controls.AddChild(toEnd);

        var results = new Button { Text = "Resultados →" };
        results.Pressed += () => GoPost();
        controls.AddChild(results);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(1, 1) };
        root.AddChild(spacer);
    }

    private void OnAutoTick()
    {
        _elapsed += (float)(0.35 * _pc!.Speed);
        _pc.Speed = Math.Clamp(_pc.Speed, 0.25, 4);
        if (_pc.State == PlaybackState.Playing)
            _pc.Tick(0.35);
        RefreshSnapshot();
    }

    private void UpdateState()
    {
        if (_stateLabel is null) return;
        _stateLabel.Text =
            $"Modo espectador · {(_pc!.State == PlaybackState.Playing ? "▶" : "⏸")} ×{_pc.Speed:0.##} · " +
            $"sección {_pc.SectionIndex + 1}/{_pc.Snapshots.Count}";
    }

    private void RefreshSnapshot() => ShowSnapshot();

    private void ShowSnapshot()
    {
        var snap = _pc!.Current;
        UpdateState();

        _groups!.Text = string.Join("\n", snap.Groups.Select(g =>
            $"{TeamLabel(g)} #{g.GroupId} · {g.MemberCount} corredores · gap {g.GapSeconds:0} s" +
            (g.SpeedKmh > 0 ? $" · {g.SpeedKmh:0} km/h" : "")));

        var finish = _pc.IsFinished;
        _log!.Text =
            $"[km {snap.KmCovered:0} de {GameManager.Stage?.DistanceKm:0} · cabeza {snap.LeaderLabel}]" +
            (finish ? " · META" : "") + "\n" +
            string.Join("\n", snap.SectionActions) +
            "\n\n· Decisiones IA:\n" +
            string.Join("\n", snap.SectionActions.Where(a => a.Contains("[IA]")));
    }

    private static string TeamLabel(GroupSnapshot g) => g.Kind switch
    {
        GroupKind.Breakaway => "Fuga",
        GroupKind.Peloton => "Pelotón",
        _ => g.Kind.ToString()
    };

    private void GoPost()
    {
        _autoTimer?.Stop();
        GetTree().ChangeSceneToFile("res://src/UI/PostStageScreen.tscn");
    }
}