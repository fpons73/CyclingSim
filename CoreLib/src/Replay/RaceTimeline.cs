using ProCycling.Core.Models;

namespace ProCycling.Core.Replay;

/// <summary>Captura el estado de un grupo en un instante de la carrera.</summary>
public sealed record GroupSnapshot(
    int GroupId, GroupKind Kind, int MemberCount, double GapSeconds,
    double SpeedKmh, double FrontKmPos);

/// <summary>Instante de la carrera al terminar una sección (Modo Espectador §23).</summary>
public sealed record RaceSnapshot(
    int SectionIndex, double KmCovered,
    IReadOnlyList<GroupSnapshot> Groups,
    int LeaderRiderId, string LeaderLabel, double LeaderTimeSeconds,
    IReadOnlyList<string> SectionActions);

/// <summary>
/// Red de captura de la carrera (PRD §23 Modo Espectador): pausa, avance por
/// sección, velocidad, consulta de decisiones y estadísticas. El simulador
/// notifica al recorder al terminar cada sección; luego un PlaybackController
/// reproduce la red para streaming y análisis.
/// </summary>
public interface IRaceRecorder
{
    /// <summary>El simulador llama a este método al terminar cada sección.</summary>
    void RecordSection(RaceState state);
}

/// <summary>
/// Construye una <see cref="RaceTimeline"/> de la etapa: un snapshot por sección
/// con grupos, gaps, líder y las acciones de esa sección (incluidas las decisiones IA).
/// </summary>
public sealed class RaceTimeline : IRaceRecorder
{
    private readonly List<RaceSnapshot> _snapshots = new();
    private int _lastLogCount;

    public IReadOnlyList<RaceSnapshot> Snapshots => _snapshots;

    public void RecordSection(RaceState state)
    {
        var sectionActions = state.ActionLog.Skip(_lastLogCount).ToList();
        _lastLogCount = state.ActionLog.Count;

        var groups = state.Groups
            .Where(g => g.MemberRiderIds.Count > 0)
            .Select(g => new GroupSnapshot(g.Id, g.Kind, g.MemberRiderIds.Count,
                g.GapSeconds, g.SpeedKmh, g.FrontKmPos))
            .OrderBy(g => g.Kind == GroupKind.Breakaway ? 0 : 1)
            .ToList();

        StageResultRider? leader = null;
        if (state.RiderStates.Count > 0)
        {
            leader = state.RiderStates
                .Where(s => s.Status == RiderStatus.Active)
                .OrderBy(s => s.StageTimeSeconds)
                .Select(s => new StageResultRider(s.RiderId, 0, s.StageTimeSeconds, 0, 0, false))
                .FirstOrDefault();
        }

        _snapshots.Add(new RaceSnapshot(
            state.CurrentSectionIndex, state.KmCovered, groups,
            leader?.RiderId ?? 0,
            leader is null ? "" : RiderName(state, leader.RiderId),
            leader?.StageSeconds ?? 0,
            sectionActions));
    }

    private static string RiderName(RaceState state, int id) =>
        state.Riders.TryGetValue(id, out var r) && !string.IsNullOrEmpty(r.Name) ? r.Name : $"#{id}";

    /// <summary>Todas las decisiones IA tomadas en la etapa, con su sección.</summary>
    public IReadOnlyList<(int Section, string Action)> Decisions() =>
        _snapshots
            .SelectMany(s => s.SectionActions.Select(a => (s.SectionIndex, a)))
            .Where(x => x.a.Contains("[IA]"))
            .ToList();

    /// <summary>Estado de "cabeza de carrera" (grupo con menor gap) en un snapshot.</summary>
    public static GroupSnapshot? HeadGroup(RaceSnapshot snapshot) =>
        snapshot.Groups.Count == 0
            ? null
            : snapshot.Groups.OrderBy(g => g.GapSeconds).First();
}