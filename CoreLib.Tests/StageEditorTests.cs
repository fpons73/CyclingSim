using ProCycling.Core.Data;
using ProCycling.Core.Models;

namespace ProCycling.Core.Tests;

public class StageEditorTests
{
    [Fact]
    public void CreaEtapaValida_SinAvisos()
    {
        var stage = new StageEditor("e1", "Plana de prueba", StageType.Flat, 175)
            .Season(2026).Date("2026-07-01")
            .Section(45, 0.4, Terrain.Flat, Terrain.Rolling)
            .Sprint(45, 20, 17, 15, 13, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1)
            .Section(80, 0.8, Terrain.Rolling)
            .Section(20, 5.0, Terrain.Climb)
            .AddClimb("c1", "Puerto falso", 125, 145, 4, 5.0)
            .Section(30, -3.0, Terrain.Descent, Terrain.Flat)
            .Finish()
            .Build();

        Assert.Equal(4, stage.Sections.Count);
        Assert.True(stage.Sections[^1].Finish);
        Assert.Equal(175, stage.DistanceKm);
        Assert.Equal(1, stage.Climbs.Count);

        var issues = StageValidator.Validate(stage);
        Assert.True(issues.Count == 0, string.Join("\n", issues.Select(i => i.Message)));
    }

    [Fact]
    public void Section_EsContiguaALaAnterior()
    {
        var stage = new StageEditor("e2", "Contigua", StageType.Flat, 10)
            .Section(3, 0)
            .Section(4, 0)
            .Section(3, 0)
            .Finish()
            .Build();

        Assert.Equal(0.0, stage.Sections[0].KmFrom);
        Assert.Equal(3.0, stage.Sections[1].KmFrom);
        Assert.Equal(7.0, stage.Sections[2].KmFrom);
        Assert.Equal(10.0, stage.Sections[^1].KmTo);
    }

    [Fact]
    public void PeticionDeLongitudNegativa_EsRechazada()
    {
        var editor = new StageEditor("e3", "Invalida", StageType.Flat, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Section(-5, 0));
    }

    [Fact]
    public void SprintFueraDeSecciones_EsRechazada()
    {
        var editor = new StageEditor("e4", "Sin sprint", StageType.Flat, 10)
            .Section(10, 0);
        Assert.Throws<ArgumentException>(() => editor.Sprint(25, 20, 15));
    }

    [Fact]
    public void PuertaSinSeccionAncla_EsRechazada()
    {
        var editor = new StageEditor("e5", "Sin puerto", StageType.Flat, 100)
            .Section(100, 0);
        Assert.Throws<ArgumentException>(() => editor.AddClimb("c9", "Raro", 5, 9, 4, 6));
    }

    [Fact]
    public void FinSinSeccionesPrevias_EsRechazada()
    {
        var editor = new StageEditor("e6", "Vacia", StageType.Flat, 0);
        Assert.Throws<InvalidOperationException>(() => editor.Finish());
    }

    [Fact]
    public void BuildLanza_ConErroresDeDistancia()
    {
        var editor = new StageEditor("e7", "Corta", StageType.Flat, 175)
            .Section(100, 0, Terrain.Flat); // solo 100 km, faltan 75
        // Es un aviso, no un error → Build debería permitirlo
        var ex = Record.Exception(() => editor.Build());
        Assert.Null(ex);
    }
}

public class StageValidatorTests
{
    private static Stage FlatStage()
    {
        return new StageEditor("v1", "Validacion", StageType.Flat, 100)
            .Section(50, 0, Terrain.Flat)
            .Section(50, 0, Terrain.Flat)
            .Finish()
            .Build();
    }

    [Fact]
    public void EtapaInvalida_SinSecciones()
    {
        var stage = new Stage { Id = "x", DistanceKm = 100 };
        var issues = StageValidator.Validate(stage);
        Assert.Contains(issues, i => i.Level == StageValidator.Severity.Error);
    }

    [Fact]
    public void EtapaValida_SinErrores()
    {
        Assert.True(StageValidator.IsValid(FlatStage()));
    }

    [Fact]
    public void SeccionNoContigua_EsError()
    {
        var stage = FlatStage();
        stage.Sections[1].KmFrom = 55;
        var issues = StageValidator.Validate(stage);
        Assert.Contains(issues,
            i => i.Level == StageValidator.Severity.Error && i.Message.Contains("contigua"));
    }

    [Fact]
    public void SprintFueraDelRecorrido_EsError()
    {
        var stage = FlatStage();
        stage.Sections[0].IntermediateSprint = new SprintInfo { Km = 120, Points = new[] { 20, 15 } };
        Assert.False(StageValidator.IsValid(stage));
    }

    [Fact]
    public void ClimbSinSeccionAncla_EsAviso()
    {
        var stage = FlatStage();
        stage.Climbs.Add(new Climb
        {
            Id = "fantasma", Name = "Fantasma", KmFrom = 10, KmTo = 15,
            Category = 2, LengthKm = 5, SummitKm = 15
        });
        var issues = StageValidator.Validate(stage);
        Assert.Contains(issues,
            i => i.Level == StageValidator.Severity.Warning && i.Message.Contains("ninguna sección"));
    }

    [Fact]
    public void SeccionConReferenciaAClimbFantasma_EsError()
    {
        var stage = FlatStage();
        stage.Sections[0].ClimbId = "noexiste";
        Assert.False(StageValidator.IsValid(stage));
    }

    [Fact]
    public void DistanciaMuyCorta_OuMuyLargaDaAviso()
    {
        var stage = FlatStage();
        stage.DistanceKm = 90; // 100 km de secciones
        var issues = StageValidator.Validate(stage);
        Assert.Contains(issues, i => i.Message.Contains("no coincide"));
    }

    [Fact]
    public void PendienteExtrema_EsAviso()
    {
        var stage = FlatStage();
        stage.Sections[0].GradientPct = 25;
        var issues = StageValidator.Validate(stage);
        Assert.Contains(issues, i => i.Level == StageValidator.Severity.Warning);
    }

    [Fact]
    public void EtapaSinMeta_SoloGeneraAviso()
    {
        var stage = new StageEditor("v2", "Sin meta", StageType.Flat, 50)
            .Section(50, 0, Terrain.Flat)
            .Build();
        var issues = StageValidator.Validate(stage);
        Assert.DoesNotContain(issues, i => i.Level == StageValidator.Severity.Error);
    }
}