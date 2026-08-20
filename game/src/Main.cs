using Godot;
using ProCycling.Core.Models;
using ProCycling.Core.Replay;
using ProCycling.Core.Simulation;
using ProCycling.Game.UI;

namespace ProCycling.Game;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        // Self-test headless (verificación CI): carga datos + simula etapa llana + log.
        // Los argumentos de usuario van tras "--" y se leen con GetCmdlineUserArgs().
        var userArgs = OS.GetCmdlineUserArgs();
        if (OS.GetCmdlineArgs().Contains("--selftest") || userArgs.Contains("--selftest"))
        {
            CallDeferred(nameof(RunSelfTest));
            return;
        }
        if (OS.GetCmdlineArgs().Contains("--selftest-tour") || userArgs.Contains("--selftest-tour"))
        {
            CallDeferred(nameof(RunSelfTestTour));
            return;
        }
        if (OS.GetCmdlineArgs().Contains("--selftest-replay") || userArgs.Contains("--selftest-replay"))
        {
            CallDeferred(nameof(RunSelfTestReplay));
            return;
        }

        if (Game.UI.GameManager.LoadData())
            CallDeferred(nameof(GoPreStage));
        else
            GD.PushError("[PCRM] No se pudieron cargar los datos. Revisa game/data/.");
    }

    private void GoPreStage()
    {
        GetTree().ChangeSceneToFile("res://src/UI/PreStageScreen.tscn");
    }

    private void RunSelfTestTour()
    {
        GD.Print("[PCRM] SELFTEST-TOUR: cargando datos...");
        if (!GameManager.LoadData() || !GameData.LoadTour("res://data"))
        {
            GD.PushError("[PCRM] SELFTEST-TOUR falló: sin datos o sin tour.");
            GetTree().Quit(1);
            return;
        }

        var (_, teams, riders) = GameData.BuildStartList(12);
        var results = GameData.RunTour(GameData.TourStages!, teams, riders, 88);
        if (results is null || results.Count == 0)
        {
            GD.PushError("[PCRM] SELFTEST-TOUR falló: sin resultados.");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[PCRM] SELFTEST-TOUR: {GameData.TourName} · {results.Count} corredores en GC final.");
        foreach (var line in GameData.TourStages!.Select((s, i) => $"  {i + 1}. {s.Type} — {s.Name} ({s.DistanceKm:0} km)"))
            GD.Print(line);
        GD.Print("[PCRM] SELFTEST-TOUR GC final top 5:");
        foreach (var r in results.Take(5))
            GD.Print($"  {GameManager.RiderName(r.RiderId)} — {RiderCard.FormatTime(r.StageSeconds)} · pts {r.PointsEarned} · KoM {r.KoMPointsEarned}");

        var leader = results.First();
        var leaderRider = GameData.RidersById[leader.RiderId];
        GD.Print($"[PCRM] SELFTEST-TOUR OK: líder GC {GameManager.RiderName(leader.RiderId)} " +
                 $"({leaderRider.Attributes.Mountain} de montaña).");
        GetTree().Quit(0);
    }

    private void RunSelfTestReplay()
    {
        // Modo espectador (PRD §23): timeline por sección + control de reproducción.
        GD.Print("[PCRM] SELFTEST-REPLAY: cargando datos...");
        if (!Game.UI.GameManager.LoadData() || Game.UI.GameData.Stages is null)
        {
            GD.PushError("[PCRM] SELFTEST-REPLAY falló: sin datos.");
            GetTree().Quit(1);
            return;
        }

        var stage = Game.UI.GameData.Stages!.FirstOrDefault(s => s.Type == StageType.Flat);
        if (stage is null || !Game.UI.GameManager.PrepareRace(stage.Id, 8, 2026))
        {
            GD.PushError("[PCRM] SELFTEST-REPLAY falló: sin etapa llana.");
            GetTree().Quit(1);
            return;
        }

        var timeline = new RaceTimeline();
        new FlatStageSimulator(RulesConfig.Default(), Game.UI.GameManager.Seed)
            .Run(Game.UI.GameManager.State!, timeline);

        var pc = new PlaybackController(timeline);
        GD.Print($"[PCRM] SELFTEST-REPLAY: {timeline.Snapshots.Count} secciones · " +
                 $"{timeline.Decisions().Count} decisiones IA.");
        pc.Play();
        int steps = 0;
        while (pc.Tick(0.33) && steps++ < 40) { } // reproducción rápida
        pc.JumpTo(timeline.Snapshots.Count - 1);
        var fin = pc.Current;
        GD.Print($"[PCRM] SELFTEST-REPLAY final: km {fin.KmCovered:0} · cabeza {fin.LeaderLabel} " +
                 $"(gap {fin.LeaderTimeSeconds:0} s) · grupos {fin.Groups.Count}");
        foreach (var d in timeline.Decisions().Take(4))
            GD.Print($"  s{d.Section}: {d.Action}");

        GD.Print($"[PCRM] SELFTEST-REPLAY OK: {timeline.Snapshots.Count} secciones, " +
                 $"estado {pc.State}, fin = sección {pc.SectionIndex}.");
        GetTree().Quit(0);
    }

    private void RunSelfTest()
    {
        GD.Print("[PCRM] SELFTEST: cargando datos...");
        if (!Game.UI.GameManager.LoadData())
        {
            GD.PushError("[PCRM] SELFTEST falló: datos no disponibles.");
            GetTree().Quit(1);
            return;
        }

        var stage = Game.UI.GameData.Stages?.FirstOrDefault(s => s.Type == StageType.Flat);
        if (stage is null)
        {
            GD.PushError("[PCRM] SELFTEST falló: sin etapa llana.");
            GetTree().Quit(1);
            return;
        }

        ulong seed = 2026;
        if (!Game.UI.GameManager.PrepareRace(stage.Id, 12, seed))
        {
            GD.PushError("[PCRM] SELFTEST falló: no se pudo preparar la etapa.");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[PCRM] SELFTEST: etapa '{stage.Name}' ({stage.DistanceKm:0} km), " +
                 $"{Game.UI.GameManager.SelectedRiders.Count} corredores, seed {seed}");
        Game.UI.GameManager.RunRace();

        foreach (var line in Game.UI.GameManager.State!.ActionLog.Take(8))
            GD.Print(line);
        GD.Print("...");
        foreach (var line in Game.UI.GameManager.State.ActionLog.TakeLast(8))
            GD.Print(line);

        var top = Game.UI.GameManager.Results!.OrderBy(r => r.StageSeconds).Take(5);
        foreach (var r in top)
            GD.Print($"[PCRM] SELFTEST top: {Game.UI.GameManager.RiderName(r.RiderId)} " +
                     $"{RiderCard.FormatTime(r.StageSeconds)} pts {r.PointsEarned}");

        var winner = top.First();
        GD.Print($"[PCRM] SELFTEST OK: ganador {Game.UI.GameManager.RiderName(winner.RiderId)} " +
                 $"con {Game.UI.GameManager.State.Riders[winner.RiderId].Attributes.Sprint} de sprint.");
        GetTree().Quit(0);
    }
}