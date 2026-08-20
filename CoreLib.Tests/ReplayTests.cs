using ProCycling.Core.Models;
using ProCycling.Core.Replay;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class ReplayTests
{
    private static Stage FlatStage()
    {
        return new Data.StageEditor("r1", "Replay Flat", StageType.Flat, 120)
            .Section(20, 0, Terrain.Flat)
            .Section(25, 0, Terrain.Flat)
            .Section(25, 1, Terrain.Rolling)
            .Section(25, 0, Terrain.Flat)
            .Section(25, 0, Terrain.Flat)
            .Finish()
            .Build();
    }

    private static (RaceState state, List<Rider> riders) Setup(Stage stage)
    {
        var teams = new[]
        {
            new Team { Id = 1, Name = "Alpha" },
            new Team { Id = 2, Name = "Bravo" },
            new Team { Id = 3, Name = "Charlie" },
            new Team { Id = 4, Name = "Delta" },
            new Team { Id = 5, Name = "Echo" },
            new Team { Id = 6, Name = "Foxtrot" }
        };
        var riders = new List<Rider>();
        int id = 1;
        foreach (var t in teams)
        {
            riders.Add(new Rider { Id = id, Name = $"S{t.Id}", TeamId = t.Id, Attributes = Attr(spr: 84) });
            riders.Add(new Rider { Id = id + 1, Name = $"G{t.Id}", TeamId = t.Id, Attributes = Attr(fla: 72) });
            riders.Add(new Rider { Id = id + 2, Name = $"R{t.Id}", TeamId = t.Id, Attributes = Attr() });
            id += 3;
        }
        var state = RaceSetup.Create(stage, teams, riders, seed: 7);
        state.Riders = riders.ToDictionary(r => r.Id);
        return (state, riders);
    }

    private static Attributes Attr(int fla = 60, int spr = 60) => new()
    { Flat = fla, Mountain = 60, MediumMountain = 60, Hill = 60, TimeTrial = 60, Prologue = 60,
      Cobbles = 60, Sprint = spr, Acceleration = 60, Descent = 60, Attack = 60, Endurance = 70,
      Resistance = 70, Recovery = 60 };

    [Fact]
    public void CapturaUnSnapshotPorSeccion()
    {
        var stage = FlatStage();
        var (state, _) = Setup(stage);
        var timeline = new RaceTimeline();
        new FlatStageSimulator(RulesConfig.Default(), 7).Run(state, timeline);

        Assert.Equal(stage.Sections.Count, timeline.Snapshots.Count);
        Assert.True(Math.Abs(stage.DistanceKm - timeline.Snapshots[^1].KmCovered) <= 0.01,
            $"Km final {timeline.Snapshots[^1].KmCovered} != distancia {stage.DistanceKm}");
        // La primera sección cubre 20 km.
        Assert.True(Math.Abs(20.0 - timeline.Snapshots[0].KmCovered) <= 0.01,
            $"Km inicial {timeline.Snapshots[0].KmCovered}");
    }

    [Fact]
    public void GrupoYLeaderSonConsistentesConElLog()
    {
        var stage = FlatStage();
        var (state, _) = Setup(stage);
        var timeline = new RaceTimeline();
        new FlatStageSimulator(RulesConfig.Default(), 7).Run(state, timeline);

        var last = timeline.Snapshots[^1];
        // El grupo del pelotón siempre está presente con miembros.
        Assert.Contains(last.Groups, g => g.Kind == GroupKind.Peloton && g.MemberCount > 0);
        // El líder tiene nombre conocido (alguno de los riders sembrados).
        Assert.Contains(last.LeaderLabel, new[] { "S1", "G1", "R1", "S2", "G2", "R2", "S3", "G3", "R3", "S4", "G4", "R4", "S5", "G5", "R5", "S6", "G6", "R6" });

        // Las acciones de cada sección están en el log global en orden.
        Assert.True(last.SectionActions.Count > 0);
        Assert.Contains(last.SectionActions, a => a.StartsWith("[PCRM] km"));
    }

    [Fact]
    public void LaDistancianCubiertaEsMonotona()
    {
        var stage = FlatStage();
        var (state, _) = Setup(stage);
        var timeline = new RaceTimeline();
        new FlatStageSimulator(RulesConfig.Default(), 7).Run(state, timeline);

        for (int i = 1; i < timeline.Snapshots.Count; i++)
            Assert.True(timeline.Snapshots[i].KmCovered >= timeline.Snapshots[i - 1].KmCovered,
                $"KmCovered bajó en sección {i}");
    }

    [Fact]
    public void ElReplayEsDeterminista_MismoSeedMismoTimeline()
    {
        var stage = FlatStage();
        RaceTimeline Run()
        {
            var (state, _) = Setup(stage);
            var t = new RaceTimeline();
            new FlatStageSimulator(RulesConfig.Default(), 7).Run(state, t);
            return t;
        }

        var a = Run();
        var b = Run();
        Assert.Equal(a.Snapshots.Count, b.Snapshots.Count);
        for (int i = 0; i < a.Snapshots.Count; i++)
        {
            Assert.Equal(a.Snapshots[i].LeaderRiderId, b.Snapshots[i].LeaderRiderId);
            Assert.Equal(a.Snapshots[i].Groups.Count, b.Snapshots[i].Groups.Count);
            Assert.Equal(a.Snapshots[i].SectionActions.Count, b.Snapshots[i].SectionActions.Count);
        }
    }

    [Fact]
    public void RevisarDecisionesIA_ExtraeLasAccionesDeLaIA()
    {
        var stage = FlatStage();
        var (state, _) = Setup(stage);
        var timeline = new RaceTimeline();
        new FlatStageSimulator(RulesConfig.Default(), 7).Run(state, timeline);

        var decisions = timeline.Decisions();
        // Al menos una decisión de IA en alguna sección (control de ritmo, fuga, etc.).
        Assert.True(decisions.Count > 0, "El director debía emitir decisiones IA durante la etapa.");
        Assert.All(decisions, d => Assert.Contains("[IA]", d.Action));
    }

    [Fact]
    public void MountainStage_TambienRegistraTimeline()
    {
        var stage = new Data.StageEditor("r2", "Replay Mtn", StageType.Mountain, 100)
            .Section(20, 0, Terrain.Flat)
            .Section(20, 6.0, Terrain.Climb)
            .AddClimb("c1", "Alto", 20, 40, 1, 6.0)
            .Section(20, -5.0, Terrain.Descent)
            .Section(40, 0, Terrain.Flat)
            .Finish()
            .Build();
        var (state, _) = Setup(stage);
        var timeline = new RaceTimeline();
        new MountainStageSimulator(RulesConfig.Default(), 7).Run(state, timeline);

        Assert.Equal(stage.Sections.Count, timeline.Snapshots.Count);
        Assert.Contains(timeline.Decisions(), d => d.Item2.Contains("[IA]"));
    }
}

public class PlaybackControllerTests
{
    private static PlaybackController Build(out RaceTimeline timeline)
    {
        var stage = new Data.StageEditor("p1", "Playback", StageType.Flat, 100)
            .Section(25, 0, Terrain.Flat)
            .Section(25, 0, Terrain.Flat)
            .Section(25, 0, Terrain.Flat)
            .Section(25, 0, Terrain.Flat)
            .Finish()
            .Build();
        var teams = new[] { new Team { Id = 1, Name = "A" } };
        var riders = new List<Rider>
        {
            new Rider { Id = 1, Name = "A1", TeamId = 1, Attributes = SpAttrs() },
            new Rider { Id = 2, Name = "A2", TeamId = 1, Attributes = SpAttrs() },
            new Rider { Id = 3, Name = "A3", TeamId = 1, Attributes = SpAttrs() }
        };
        var state = RaceSetup.Create(stage, teams, riders, 9);
        state.Riders = riders.ToDictionary(r => r.Id);
        timeline = new RaceTimeline();
        new FlatStageSimulator(RulesConfig.Default(), 9).Run(state, timeline);
        return new PlaybackController(timeline);
    }

    private static Attributes SpAttrs() => new()
    { Flat = 70, Mountain = 60, MediumMountain = 60, Hill = 60, TimeTrial = 60, Prologue = 60,
      Cobbles = 60, Sprint = 65, Acceleration = 60, Descent = 60, Attack = 60, Endurance = 70,
      Resistance = 70, Recovery = 60 };

    [Fact]
    public void EmpiezaPausadoEnLaPrimeraSeccion()
    {
        var pc = Build(out _);
        Assert.Equal(PlaybackState.Paused, pc.State);
        Assert.Equal(0, pc.CurrentIndex);
    }

    [Fact]
    public void AvanceYRetrocesoPorSecciones()
    {
        var pc = Build(out var timeline);
        Assert.True(pc.Advance());
        Assert.Equal(1, pc.CurrentIndex);
        Assert.True(pc.Previous());
        Assert.Equal(0, pc.CurrentIndex);
        Assert.False(pc.Previous()); // no baja de la primera

        while (pc.Advance()) { }
        Assert.True(pc.IsFinished);
        Assert.False(pc.Advance());
    }

    [Fact]
    public void VelocidadQuedaAcotada()
    {
        var pc = Build(out _);
        pc.SetSpeed(10);
        Assert.Equal(4, pc.Speed);
        pc.SetSpeed(-1);
        Assert.Equal(0.25, pc.Speed);
        pc.SpeedUp();
        Assert.True(pc.Speed > 0.25);
        pc.SlowDown();
        Assert.Equal(0.25, pc.Speed);
    }

    [Fact]
    public void JumpToFueraDeRangoEsRechazado()
    {
        var pc = Build(out var timeline);
        Assert.False(pc.JumpTo(-1));
        Assert.False(pc.JumpTo(timeline.Snapshots.Count));
        Assert.True(pc.JumpTo(timeline.Snapshots.Count - 1));
        Assert.Equal(timeline.Snapshots.Count - 1, pc.CurrentIndex);
    }

    [Fact]
    public void TickSoloAvanzaEstandoEnJuego()
    {
        var pc = Build(out _);
        Assert.False(pc.Tick(1.0)); // pausado: no avanza
        pc.Play();
        _ = pc.Tick(15.0); // >= 1.0 a ×1 → avanza al menos una sección
        Assert.True(pc.CurrentIndex > 0);
        pc.Toggle(); // Pause
        int frozen = pc.CurrentIndex;
        _ = pc.Tick(10.0);
        Assert.Equal(frozen, pc.CurrentIndex);
    }

    [Fact]
    public void SnapshotActualYSeccionActualSonCoherentes()
    {
        var pc = Build(out _);
        pc.JumpTo(2);
        Assert.Equal(2, pc.SectionIndex);
        Assert.Equal(2, pc.Current.SectionIndex);
        Assert.True(pc.KmCovered > 0);
    }
}