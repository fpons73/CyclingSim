using ProCycling.Core.Models;
using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class RaceDecisionEngineTests
{
    private static RaceState Build(double kmToFinish, bool strongSprinterInPeloton)
    {
        var riders = new List<Rider>();
        var teams = new List<Team>();
        teams.Add(new Team { Id = 1, Name = "Sprinters", Abbr = "SPR" });
        teams.Add(new Team { Id = 2, Name = "GC", Abbr = "GC" });

        // Esprinter élite (pelotón) y fugado roulleur.
        riders.Add(new Rider
        {
            Id = 1, Name = "SprinterTop", TeamId = 1,
            Attributes = new Attributes { Sprint = strongSprinterInPeloton ? 85 : 55, Flat = 78 }
        });
        riders.Add(new Rider
        {
            Id = 2, Name = "Fugado", TeamId = 2,
            Attributes = new Attributes { Sprint = 50, Flat = 72, Attack = 72 }
        });

        var state = RaceSetup.Create(new Stage
        {
            Id = "t", Name = "T", TypeRaw = "flat", DistanceKm = 180,
            Sections = new List<StageSection> { new() { KmFrom = 0, KmTo = 180, TerrainsRaw = new() { "flat" } } }
        }, teams, riders, 1);

        state.Groups.Clear();
        state.Groups.Add(new RiderGroup { Id = 1, Kind = GroupKind.Breakaway, MemberRiderIds = { 2 }, GapSeconds = 240 });
        state.Groups.Add(new RiderGroup { Id = 2, Kind = GroupKind.Peloton, MemberRiderIds = { 1 }, GapSeconds = 0 });
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
}