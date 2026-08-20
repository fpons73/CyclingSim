using Godot;
using ProCycling.Core.Models;

namespace ProCycling.Game.UI;

/// <summary>
/// Ficha de corredor: identidad, equipo, 14 atributos (50–99) y fatiga (PRD §29).
/// Componente reutilizable (prestage, fuga, clasificación...).
/// </summary>
public partial class RiderCard : PanelContainer
{
    private VBoxContainer? _root;
    private readonly (string Key, string Label)[] _attrs =
    {
        ("flat", "FLA Llanura"), ("mountain", "MNT Montaña"), ("mm", "MM Media montaña"),
        ("hill", "HIL Colina"), ("ttr", "TTR Contrareloj"), ("prl", "PRL Prólogo"),
        ("cobbles", "COB Pavés"), ("sprint", "SPR Sprint"), ("acceleration", "ACC Aceleración"),
        ("descent", "DHI Descenso"), ("attack", "ATT Ataque"), ("endurance", "STA Aguante"),
        ("resistance", "RES Resistencia"), ("recovery", "REC Recuperación")
    };

    private Rider? _rider;
    private RiderState? _state;

    public void Setup(Rider rider, RiderState? state = null)
    {
        _rider = rider;
        _state = state;
        Build();
    }

    public override void _Ready()
    {
        if (_rider is not null) Build();
    }

    private void Build()
    {
        if (_rider is null) return;
        ClearChildren();

        _root = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        AddChild(_root);

        var nameLabel = new Label
        {
            Text = $"{_rider.Name}  (#{_rider.Number})",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 18);
        _root.AddChild(nameLabel);

        var team = _rider.TeamId > 0 ? $"Equipo {_rider.TeamId}" : "Sin equipo";
        _root.AddChild(new Label
        {
            Text = $"{team} · {_rider.Nationality ?? "?"} · {RoleText()}",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var fatigue = _state?.Fatigue ?? 0;
        _root.AddChild(new Label
        {
            Text = $"Fatiga: {fatigue:0.0} / 100",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = fatigue > 60 ? new Color(0.9f, 0.4f, 0.3f)
                : fatigue > 35 ? new Color(0.95f, 0.7f, 0.2f) : new Color(0.4f, 0.85f, 0.4f)
        });

        var grid = new GridContainer { Columns = 2 };
        _root.AddChild(grid);

        foreach (var (key, label) in _attrs)
        {
            int value = (int)_rider.Attributes.Get(key);
            grid.AddChild(new Label { Text = label });
            var valueLabel = new Label
            {
                Text = $"{value}",
                HorizontalAlignment = HorizontalAlignment.Right,
                Modulate = BarColor(value)
            };
            grid.AddChild(valueLabel);
        }

        if (_state is not null)
        {
            _root.AddChild(new Label
            {
                Text = $"Tiempo etapa: {FormatTime(_state.StageTimeSeconds)} · Pos {_state.RacePosition}",
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
    }

    private string RoleText()
    {
        if (_rider!.Roles.Count == 0) return "Rol sin determinar";
        return string.Join(", ", _rider.Roles);
    }

    private static Color BarColor(int value) => value switch
    {
        >= 80 => new Color(0.4f, 0.9f, 0.4f),
        >= 70 => new Color(0.7f, 0.9f, 0.4f),
        >= 60 => new Color(0.9f, 0.85f, 0.4f),
        >= 50 => new Color(0.95f, 0.7f, 0.45f),
        _ => new Color(0.95f, 0.5f, 0.4f)
    };

    public static string FormatTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{t.Hours:D1}h {t.Minutes:D2}m {t.Seconds:D2}s" : $"{t.Minutes:D2}m {t.Seconds:D2}s";
    }

    private void ClearChildren()
    {
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
    }
}
