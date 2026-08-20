using ProCycling.Core.Models;

namespace ProCycling.Core.Simulation;

/// <summary>
/// Tour Mode (PRD §13–§25): recorre el catálogo de etapas, ejecuta el simulador
/// adecuado por tipo, aplica recuperación entre etapas (PRD §5) y acumula las
/// cinco clasificaciones (GC/Puntos/KoM/Jóvenes/Equipos) con fatiga heredada.
/// </summary>
public sealed class TourSimulator
{
    private readonly RulesConfig _cfg;
    private readonly ulong _seed;

    public TourSimulator(RulesConfig cfg, ulong seed)
    {
        _cfg = cfg;
        _seed = seed;
    }

    /// <summary>Ejecuta todas las etapas del tour. Devuelve la clasificación acumulada final.</summary>
    public Classifications Run(IReadOnlyList<Stage> stages, List<Team> teams, List<Rider> riders)
    {
        if (stages.Count == 0) throw new InvalidOperationException("Tour sin etapas.");

        var rng = new SeededRandom(_seed);
        ulong stageSeed = _seed;

        // Un único RaceState recorre todo el tour: GC/Clasificaciones acumulan,
        // StageTimeSeconds se reinicia cada etapa, la fatiga persiste con recuperación.
        var state = RaceSetup.Create(stages[0], teams, riders, stageSeed);
        state.ActionLog.Clear();

        for (int i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            stageSeed = rng.NextULong();
            state.Stage = stage;
            state.StageId = stage.Id;
            state.Seed = stageSeed;
            state.CurrentSectionIndex = 0;
            state.KmCovered = 0;

            if (stage.Type == StageType.Rest)
            {
                foreach (var rs in state.RiderStates)
                {
                    var r = riders.First(x => x.Id == rs.RiderId);
                    rs.Fatigue = Math.Min(_cfg.FatMax,
                        RecoveryCalculator.ApplyBetweenStages(rs.Fatigue, r.Attributes.Recovery, _cfg) * 0.5);
                    rs.Status = RiderStatus.Active;
                }
                state.ActionLog.Add($"[PCRM] Día {i + 1}: descanso. Fatiga reducida.");
                continue;
            }

            // Restablecer tiempos de etapa (la fatiga y el RNG se conservan).
            foreach (var rs in state.RiderStates)
            {
                rs.StageTimeSeconds = 0;
                rs.StageElevationMeters = 0;
                rs.StageEffortScore = 0;
                rs.StageRiddenSeconds = 0;
                if (rs.Status is RiderStatus.Dnf or RiderStatus.Dropped or RiderStatus.Finished)
                    rs.Status = RiderStatus.Active;
            }

            var results = RunSingleStage(state, stage, stageSeed);

            // GC/Clasificaciones se acumulan de forma nativa en RegisterStage;
            // solo hay que guardar el tiempo absoluto acumulado por corredor.
            foreach (var r in results)
            {
                var rs = state.RiderStates.First(x => x.RiderId == r.RiderId);
                rs.GcSeconds = r.StageSeconds; // se sobrescribe en FinalizeTimes del simulador
            }

            // Recuperación entre etapas: fatiga_final → REC → fatiga_residual.
            foreach (var rs in state.RiderStates)
            {
                var r = riders.First(x => x.Id == rs.RiderId);
                rs.Fatigue = RecoveryCalculator.ApplyBetweenStages(rs.Fatigue, r.Attributes.Recovery, _cfg);
            }
        }

        return state.Classifications;
    }

    private List<StageResultRider> RunSingleStage(RaceState state, Stage stage, ulong seed)
    {
        return stage.Type switch
        {
            StageType.Mountain or StageType.MediumMountain or StageType.FlatHilly =>
                new MountainStageSimulator(_cfg, seed).Run(state),
            StageType.IndividualTimeTrial or StageType.Prologue or StageType.TeamTimeTrial =>
                new TimeTrialStageSimulator(_cfg, seed).Run(state),
            _ => new FlatStageSimulator(_cfg, seed).Run(state),
        };
    }
}