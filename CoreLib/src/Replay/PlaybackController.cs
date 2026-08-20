namespace ProCycling.Core.Replay;

/// <summary>Estados de reproducción del modo espectador (PRD §23).</summary>
public enum PlaybackState { Paused, Playing }

/// <summary>
/// Control de reproducción de la <see cref="RaceTimeline"/> (PRD §23):
/// pausar, reproducir, acelerar/ralentizar y avanzar por secciones.
/// Máquina de estados pura y determinista, testeable sin UI.
/// </summary>
public sealed class PlaybackController
{
    private readonly RaceTimeline _timeline;
    private int _index;
    private double _speed = 1.0;

    public PlaybackController(RaceTimeline timeline)
    {
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        if (timeline.Snapshots.Count == 0)
            throw new ArgumentException("El timeline está vacío.", nameof(timeline));
    }

    public PlaybackState State { get; private set; } = PlaybackState.Paused;

    /// <summary>Índice de snapshot actual (0..Snapshots.Count-1).</summary>
    public int CurrentIndex => _index;

    /// <summary>Velocidad ×1..×4 (o ×0.25..×4 con pasos).</summary>
    public double Speed
    {
        get => _speed;
        set => _speed = Math.Clamp(value, 0.25, 4.0);
    }

    public bool IsFinished => _index >= _timeline.Snapshots.Count - 1;
    public RaceSnapshot Current => _timeline.Snapshots[_index];
    public IReadOnlyList<RaceSnapshot> Snapshots => _timeline.Snapshots;
    public double KmCovered => Current.KmCovered;
    public int SectionIndex => Current.SectionIndex;

    public void Play() { State = PlaybackState.Playing; }
    public void Pause() { State = PlaybackState.Paused; }
    public void Toggle() { State = State == PlaybackState.Playing ? PlaybackState.Paused : PlaybackState.Playing; }

    public void SetSpeed(double speed) => Speed = speed;
    public void SpeedUp() => Speed = _speed + 0.25;
    public void SlowDown() => Speed = _speed - 0.25;

    /// <summary>Avanza una sección (si está en marca, pasa a la siguiente).</summary>
    public bool Advance()
    {
        if (IsFinished) return false;
        _index++;
        return true;
    }

    /// <summary>Retrocede una sección.</summary>
    public bool Previous()
    {
        if (_index == 0) return false;
        _index--;
        return true;
    }

    /// <summary>Salta directamente a una sección (0..N-1).</summary>
    public bool JumpTo(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _timeline.Snapshots.Count) return false;
        _index = sectionIndex;
        return true;
    }

    /// <summary>Avanza a la última sección.</summary>
    public void End() => _index = _timeline.Snapshots.Count - 1;

    /// <summary>Reproduce un "tick" a la velocidad actual: si está en Playing avanza
    /// a la velocidad, si no permanece. Devuelve true si se avanzó de sección.</summary>
    public bool Tick(double deltaSeconds)
    {
        if (State != PlaybackState.Playing || IsFinished) return false;
        _accum += deltaSeconds * _speed;
        if (_accum >= SectionDurationSeconds)
        {
            _accum -= SectionDurationSeconds;
            return Advance();
        }
        return false;
    }

    private double _accum;
    private const double SectionDurationSeconds = 1.0;
}