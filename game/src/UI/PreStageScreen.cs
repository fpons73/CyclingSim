using Godot;
using ProCycling.Core.Models;

namespace ProCycling.Game.UI;

/// <summary>Pantalla previa a la etapa: selección de etapa, tamaño de pelotón, seed, fichas y arranque.</summary>
public partial class PreStageScreen : Control
{
    private OptionButton? _stagePicker;
    private SpinBox? _teamCount;
    private SpinBox? _seedBox;
    private VBoxContainer? _ridersList;
    private Label? _status;

    public override void _Ready()
    {
        if (!GameManager.DataLoaded)
        {
            if (!GameManager.LoadData())
            {
                GD.PushError("[PreStage] No se pudieron cargar los datos (res://data/pcrm.sqlite).");
                return;
            }
        }
        Build();
    }

    private void Build()
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(1280, 0) };
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(root);

        root.AddChild(new Label
        {
            Text = "PRO CYCLING REPLAY MANAGER — Etapa individual",
            HorizontalAlignment = HorizontalAlignment.Center
        });
        var title = root.GetChild<Label>(0);
        title.AddThemeFontSizeOverride("font_size", 28);

        var hbox = new HBoxContainer();
        root.AddChild(hbox);

        hbox.AddChild(new Label { Text = "Etapa:" });
        _stagePicker = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var stage in GameData.Stages ?? new List<Stage>())
            _stagePicker.AddItem($"{stage.Name}  ({stage.DistanceKm:0} km)", stage.Id.GetHashCode());
        hbox.AddChild(_stagePicker);

        hbox.AddChild(new Label { Text = "Equipos:" });
        _teamCount = new SpinBox { MinValue = 4, MaxValue = 30, Value = 10, Step = 1 };
        hbox.AddChild(_teamCount);

        hbox.AddChild(new Label { Text = "Seed:" });
        _seedBox = new SpinBox { MinValue = 1, MaxValue = 99999, Value = GameManager.Seed, Step = 1 };
        hbox.AddChild(_seedBox);

        var startButton = new Button { Text = "▶ Iniciar etapa" };
        startButton.Pressed += OnStart;
        hbox.AddChild(startButton);

        _status = new Label { Text = "" };
        root.AddChild(_status);

        _ridersList = new VBoxContainer();
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0, 540);
        scroll.AddChild(_ridersList);
        root.AddChild(scroll);

        RefreshRiders();
        _teamCount.ValueChanged += _ => RefreshRiders();
    }

    private void RefreshRiders()
    {
        if (_ridersList is null) return;
        foreach (Node child in _ridersList.GetChildren())
        {
            _ridersList.RemoveChild(child);
            child.QueueFree();
        }

        int teams = (int)(_teamCount?.Value ?? 10);
        var (count, _, riders) = GameData.BuildStartList(teams);
        _status!.Text = $"{count} corredores en start list (max 8/equipo).";

        foreach (var rider in riders.Take(12))
        {
            var card = new RiderCard();
            card.Setup(rider);
            _ridersList.AddChild(card);
        }
        if (riders.Count > 12)
            _ridersList.AddChild(new Label { Text = $"... y {riders.Count - 12} corredores más" });
    }

    private void OnStart()
    {
        if (_stagePicker is null || _seedBox is null) return;
        int idx = _stagePicker.Selected;
        string stageId = GameData.Stages![Math.Max(0, idx)].Id;
        ulong seed = (ulong)(long)_seedBox.Value;
        int teams = (int)_teamCount!.Value;

        if (!GameManager.PrepareRace(stageId, teams, seed))
        {
            _status!.Text = "Error preparando la etapa.";
            return;
        }
        GetTree().ChangeSceneToFile("res://src/UI/RaceScreen.tscn");
    }
}
