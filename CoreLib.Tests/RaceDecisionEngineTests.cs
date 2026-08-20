using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class RaceDecisionEngineTests
{
    private static RaceState Build(double kmToFinish, bool strongSprinterInPeloton, string type = "flat")
    {
        var riders = new List<Rider>();
        var teams = new List<Team>();
        teams.Add(new Team { Id = 1, Name = "Sprinters", Abbr = "SPR" });
        teams.Add(new Team { Id = 2, Name = "GC", Abbr = "GC" });
        teams.Add(new Team { Id = 3, Name = "GC2", Abbr = "GC2" });
        teams.Add(new Team { Id = 4, Name = "Noob", Abbr = "NOB" });
        teams.Add(new Team { Id = 5, Name = "Hunters", Abbr = "HUN" });

        // Esprinter élite (pelotón) y fugado roulleur.
        riders.Add(new Rider
        {
            Id = 1, Name = "SprinterTop", TeamId = 1,
            Attributes = new Attributes { Sprint = strongSprinterInPeloton ? 85 : 55, Flat = 78, Acceleration = 80 }
        });
        riders.Add(new Rider
        {
            Id = 2, Name = "Fugado", TeamId = 2,
            Attributes = new Attributes { Sprint = 50, Flat = 72, Attack = 72, Acceleration = 60 }
        });
        // Escalador de GC fuerte del equipo 2.
        riders.Add(new Rider
        {
            Id = 3, Name = "LiderGC", TeamId = 2,
            Attributes = new Attributes { Sprint = 50, Mountain = 88, Attack = 82, Flat = 60, Acceleration = 75, Endurance = 80 }
        });
        // Escalador de GC medio del equipo 3 (reto de la general).
        riders.Add(new Rider
        {
            Id = 4, Name = "Escalador2", TeamId = 3,
            Attributes = new Attributes { Sprint = 55, Mountain = 80, Attack = 80, Flat = 62, Acceleration = 72, Endurance = 78 }
        });
        // Equipo sin carta (Noob): nadie por encima del nivel base → ahorra energía / mantiene ritmo.
        riders.Add(new Rider
        {
            Id = 5, Name = "Gregario", TeamId = 4,
            Attributes = new Attributes { Sprint = 55, Flat = 58, Attack = 55, Acceleration = 55, Mountain = 54, Endurance = 60 }
        });
        // Cazador de montaña del equipo 5 (cazador entre 58 y 70 de blend).
        riders.Add(new Rider
        {
            Id = 6, Name = "Cazador", TeamId = 5,
            Attributes = new Attributes { Sprint = 55, Mountain = 68, Attack = 66, Flat = 62, Acceleration = 60, Endurance = 70 }
        });

        var stage = new Stage
        {
            Id = "t", Name = "T", TypeRaw = type, DistanceKm = 180,
            Sections = new List<StageSection>
            {
                new() { KmFrom = 0, KmTo = 180, TerrainsRaw = new() { type == "mountain" ? "climb" : "flat" } }
            }
        };
        if (type == "mountain")
            stage.Climbs.Add(new Climb { Id = "c1", Name = "C", KmFrom = 0, KmTo = 180, Category = 1 });

        var state = RaceSetup.Create(stage, teams, riders, 1);

        state.Groups.Clear();
        state.Groups.Add(new RiderGroup { Id = 1, Kind = GroupKind.Breakaway, MemberRiderIds = { 2 }, GapSeconds = 240 });
        state.Groups.Add(new RiderGroup { Id = 2, Kind = GroupKind.Peloton, MemberRiderIds = { 1, 3, 4, 5, 6 }, GapSeconds = 0 });
        state.RiderStates.First(s => s.RiderId == 2).GroupId = 1;
        return state;
    }

    [Fact]
    public void ConSprinterFuerte_PersigueDuro_EnLosUltimos50Km()
    {
        var state = Build(kmToFinish: 20, strongSprinterInPeloton: true);
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);
        var breakaway = state.Groups.First(g => g.Kind == GroupKind.Breakaway);

        var intensity = ia.DecideChase(breakaway, 160, state.Stage!.DistanceKm);
        Assert.Equal(ChaseIntensity.Strong, intensity);
    }

    [Fact]
    public void SinSprinterFuerte_NoPersigue_AlInicio()
    {
        var state = Build(kmToFinish: 150, strongSprinterInPeloton: false);
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);
        var breakaway = state.Groups.First(g => g.Kind == GroupKind.Breakaway);

        var intensity = ia.DecideChase(breakaway, 30, state.Stage!.DistanceKm);
        Assert.Equal(ChaseIntensity.None, intensity);
    }

    [Fact]
    public void DirectorMode_RegistraDecisionesYEjecuta()
    {
        var state = Build(kmToFinish: 20, strongSprinterInPeloton: true);
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state, DirectorMode.Directed);
        var breakaway = state.Groups.First(g => g.Kind == GroupKind.Breakaway);

        double adjust = ia.ApplyChase(state, breakaway, 160, state.Stage!.DistanceKm);
        Assert.True(adjust > 0, $"Persecución esperada >0, fue {adjust}");
        Assert.Contains(state.ActionLog, l => l.Contains("[IA]"));
    }

    [Fact]
    public void PlayerMode_NoEjecuta_PeroRegistra()
    {
        var state = Build(kmToFinish: 20, strongSprinterInPeloton: true);
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state, DirectorMode.Player);
        var breakaway = state.Groups.First(g => g.Kind == GroupKind.Breakaway);

        double adjust = ia.ApplyChase(state, breakaway, 160, state.Stage!.DistanceKm);
        Assert.Equal(0, adjust);
        Assert.Contains(state.ActionLog, l => l.Contains("[JUGADOR]"));
    }

    private static List<TacticalDecisionKind> KindsOf(RaceState state)
    {
        state.CurrentSectionIndex = 0;
        state.KmCovered = 30;
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);
        return ia.Evaluate(state).Select(d => d.Kind).Distinct().ToList();
    }

    [Fact]
    public void EquipoDeSprint_DecideControlDelPeloton_YLanzarSprint()
    {
        var state = Build(kmToFinish: 10, strongSprinterInPeloton: true);
        state.KmCovered = 170;
        state.CurrentSectionIndex = 0;
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);

        var kinds = ia.Evaluate(state).Select(d => d.Kind).ToList();
        Assert.Contains(TacticalDecisionKind.ControlPack, kinds);
        Assert.Contains(TacticalDecisionKind.LaunchSprint, kinds);
    }

    [Fact]
    public void EquipoDeGC_Lider_DecideProtegerLider_EnSubida()
    {
        var state = Build(kmToFinish: 100, strongSprinterInPeloton: false, type: "mountain");
        state.KmCovered = 80;
        state.CurrentSectionIndex = 0;
        // Los dos equipos de GC ocupan la zona alta de la general (nadie ataca sin ventaja).
        state.Classifications.RegisterStage(new[]
        {
            new StageResultRider(3, 2, 1000, 50, 10, false),   // LiderGC → líder
            new StageResultRider(4, 3, 1005, 30, 15, false),   // Escalador2 → 2º
          new StageResultRider(6, 5, 1010, 10, 20, false)
        });
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);

        var kinds = ia.Evaluate(state).ToList();
        Assert.Contains(kinds, d => d.Kind == TacticalDecisionKind.ProtectLeader);
        Assert.Contains(kinds, d => d.Kind == TacticalDecisionKind.ContestKoM);
    }

    [Fact]
    public void EquipoDeGC_NoLider_DecideAtacar_EnSubidaLejana()
    {
        var state = Build(kmToFinish: 120, strongSprinterInPeloton: false, type: "mountain");
        state.KmCovered = 60;
        state.CurrentSectionIndex = 0;
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);
        // El equipo GC (2) no lidera la general (sin clasificaciones) → ataca.
        var decisions = ia.Evaluate(state);

        var attack = decisions.FirstOrDefault(d => d.Kind == TacticalDecisionKind.Attack);
        Assert.NotNull(attack);
        Assert.Equal(3, attack!.RiderId);
    }

    [Fact]
    public void EquipoDeGCRival_DecideSeguirElAtaque()
    {
        var state = Build(kmToFinish: 100, strongSprinterInPeloton: false, type: "mountain");
        state.KmCovered = 80;
        state.CurrentSectionIndex = 0;
        // El equipo 2 (GC, LiderGC #3) es el más fuerte → ataca; no hay clasificación de GC.
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);

        var kinds = ia.Evaluate(state).ToList();
        Assert.Contains(kinds, d => d.Kind == TacticalDecisionKind.Attack);
        // El resto de equipos GC (Hunters #3, cazador de 75) sigue el ataque.
        Assert.Contains(kinds, d => d.Kind == TacticalDecisionKind.FollowAttack);
    }

    [Fact]
    public void EnDescenso_EquipoDeGCNoLider_AsumeRiesgo()
    {
        var state = Build(kmToFinish: 90, strongSprinterInPeloton: false, type: "mountain");
        // Sección de descenso.
        state.Stage!.Sections[0] = new StageSection { KmFrom = 90, KmTo = 100, TerrainsRaw = new() { "descent" } };
        state.KmCovered = 95;
        state.CurrentSectionIndex = 0;
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);

        var kinds = ia.Evaluate(state).ToList();
        Assert.Contains(kinds, d => d.Kind == TacticalDecisionKind.RiskDescent);
    }

    [Fact]
    public void EquipoDeSprint_DecidePerseguir_EnLosUltimos50Km()
    {
        var state = Build(kmToFinish: 30, strongSprinterInPeloton: true);
        state.KmCovered = 150;
        state.CurrentSectionIndex = 0;
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);

        var kinds = ia.Evaluate(state).ToList();
        Assert.Contains(kinds, d => d.Kind == TacticalDecisionKind.Chase);
    }

    [Fact]
    public void EquipoSinObjetivo_DecideAhorrarEnergia()
    {
        var state = Build(kmToFinish: 100, strongSprinterInPeloton: true);
        state.CurrentSectionIndex = 0;
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state);

        var kinds = ia.Evaluate(state).ToList();
        // El equipo "Noob" (4) no tiene carta → ahorra energía.
        var noob = ia.TacticFor(4);
        Assert.NotNull(noob);
        Assert.False(noob!.HasAGoal);
        Assert.Contains(kinds, d => d.Kind == TacticalDecisionKind.SaveEnergy);
    }

    [Fact]
    public void Las12DecisionesDelPRD_EstanCubiertas_EnAlgunSitio()
    {
        var expected = new[]
        {
            TacticalDecisionKind.Attack, TacticalDecisionKind.FollowAttack,
            TacticalDecisionKind.Chase, TacticalDecisionKind.MaintainPace,
            TacticalDecisionKind.JoinBreakaway, TacticalDecisionKind.ProtectLeader,
            TacticalDecisionKind.SaveEnergy, TacticalDecisionKind.LaunchSprint,
            TacticalDecisionKind.ContestKoM, TacticalDecisionKind.ControlPack,
            TacticalDecisionKind.Counterattack, TacticalDecisionKind.RiskDescent
        };
        var joined = new List<TacticalDecisionKind>();

        // Llana con fuga lejana y llegada cercana.
        var flat = Build(kmToFinish: 90, strongSprinterInPeloton: true);
        flat.CurrentSectionIndex = 0;
        var iaFlat = new RaceDecisionEngine(RulesConfig.Default(), flat);
        foreach (var km in new[] { 5.0, 170.0 })
        {
            flat.KmCovered = km;
            joined.AddRange(iaFlat.Evaluate(flat).Select(d => d.Kind));
        }

        // Montaña sin ventaja de GC (ataques y seguimientos).
        var mountain = Build(kmToFinish: 90, strongSprinterInPeloton: false, type: "mountain");
        mountain.CurrentSectionIndex = 0;
        var iaMountain = new RaceDecisionEngine(RulesConfig.Default(), mountain);
        foreach (var km in new[] { 60.0, 95.0 })
        {
            mountain.KmCovered = km;
            joined.AddRange(iaMountain.Evaluate(mountain).Select(d => d.Kind));
        }

        // Montaña con líder de GC y retador (contraataque: el líder responde al ataque del retador).
        var gcRace = Build(kmToFinish: 90, strongSprinterInPeloton: false, type: "mountain");
        gcRace.KmCovered = 70;
        gcRace.CurrentSectionIndex = 0;
        // Líder claro (#3) en top-3; el retador (#4) queda fuera del podio → ataca y el líder contraataca.
        gcRace.Classifications.RegisterStage(new[]
        {
            new StageResultRider(3, 2, 1000, 50, 10, false),   // líder
            new StageResultRider(1, 1, 1010, 20, 0, false),    // 2º (sprinter, no GC)
            new StageResultRider(5, 4, 1015, 15, 0, false)     // 3º (no GC)
        });
        var iaGc = new RaceDecisionEngine(RulesConfig.Default(), gcRace);
        joined.AddRange(iaGc.Evaluate(gcRace).Select(d => d.Kind));

        // Mt. sin amenaza (final próximo ≤40 km): sin ataque, todos protegen a su líder.
        var protRace = Build(kmToFinish: 30, strongSprinterInPeloton: false, type: "mountain");
        protRace.KmCovered = 150;
        protRace.CurrentSectionIndex = 0;
        protRace.Classifications.RegisterStage(new[]
        {
            new StageResultRider(3, 2, 1000, 50, 10, false),
            new StageResultRider(4, 3, 1005, 30, 15, false)
        });
        var iaProt = new RaceDecisionEngine(RulesConfig.Default(), protRace);
        joined.AddRange(iaProt.Evaluate(protRace).Select(d => d.Kind));

        // Descenso en montaña (riesgo en la bajada).
        var descent = Build(kmToFinish: 60, strongSprinterInPeloton: false, type: "mountain");
        descent.Stage!.Sections[0] = new StageSection { KmFrom = 120, KmTo = 140, TerrainsRaw = new() { "descent" } };
        descent.KmCovered = 130;
        descent.CurrentSectionIndex = 0;
        var iaDescent = new RaceDecisionEngine(RulesConfig.Default(), descent);
        joined.AddRange(iaDescent.Evaluate(descent).Select(d => d.Kind));

        // TTT/CRI: equipos neutros mantienen el ritmo (MaintainPace).
        var tt = Build(kmToFinish: 90, strongSprinterInPeloton: false, type: "itt");
        tt.CurrentSectionIndex = 0;
        var iaTt = new RaceDecisionEngine(RulesConfig.Default(), tt);
        joined.AddRange(iaTt.Evaluate(tt).Select(d => d.Kind));

        foreach (var kind in expected)
            Assert.Contains(kind, joined);
    }

    [Fact]
    public void DirectorMode_EmiteDecisionesYLasRegistraEnElLog()
    {
        var state = Build(kmToFinish: 90, strongSprinterInPeloton: true);
        state.CurrentSectionIndex = 0;
        state.KmCovered = 60;
        var ia = new RaceDecisionEngine(RulesConfig.Default(), state, DirectorMode.Directed);

        var decisions = ia.Evaluate(state);
        Assert.NotEmpty(decisions);
        Assert.All(decisions, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Reason));
            Assert.Contains(state.ActionLog, l => l.Contains("[IA]") && l.Contains("decide"));
        });
    }
}