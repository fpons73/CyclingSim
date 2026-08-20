using ProCycling.Core.Simulation;

namespace ProCycling.Core.Tests;

public class SeededRandomTests
{
    [Fact]
    public void MismaSeed_ProduceSecuenciaIdentica()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);
        var seqA = Enumerable.Range(0, 100).Select(_ => a.NextULong()).ToArray();
        var seqB = Enumerable.Range(0, 100).Select(_ => b.NextULong()).ToArray();
        Assert.Equal(seqA, seqB);
    }

    [Fact]
    public void DistintaSeed_ProduceSecuenciaDiferente()
    {
        var a = new SeededRandom(1);
        var b = new SeededRandom(2);
        var seqA = Enumerable.Range(0, 20).Select(_ => a.NextULong()).ToArray();
        var seqB = Enumerable.Range(0, 20).Select(_ => b.NextULong()).ToArray();
        Assert.NotEqual(seqA, seqB);
    }

    [Fact]
    public void Roll2D6_DentroDe2_12()
    {
        var rng = new SeededRandom(7);
        for (int i = 0; i < 500; i++)
        {
            var (r, w) = rng.Roll2D6();
            Assert.InRange(r, 1, 6);
            Assert.InRange(w, 1, 6);
            Assert.InRange(r + w, 2, 12);
        }
    }

    [Fact]
    public void Roll1D10_DentroDe1_10()
    {
        var rng = new SeededRandom(9);
        for (int i = 0; i < 500; i++)
            Assert.InRange(rng.Roll1D10(), 1, 10);
    }

    [Fact]
    public void NextDouble_DentroDe0_1()
    {
        var rng = new SeededRandom(11);
        for (int i = 0; i < 1000; i++)
        {
            double d = rng.NextDouble();
            Assert.InRange(d, 0.0, 1.0);
        }
    }

    [Fact]
    public void GuardarYRestaurarEstado_ContinuaSecuencia()
    {
        var a = new SeededRandom(123);
        for (int i = 0; i < 10; i++) a.NextULong();
        ulong[] snapshot = a.GetState();

        var expected = Enumerable.Range(0, 5).Select(_ => a.NextULong()).ToArray();

        var b = new SeededRandom(0); // seed distinta
        b.RestoreState(snapshot);
        var actual = Enumerable.Range(0, 5).Select(_ => b.NextULong()).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Distribucion2D6_PorEncimaDelPromedio()
    {
        var rng = new SeededRandom(5);
        int total = 0, samples = 2000;
        for (int i = 0; i < samples; i++) total += rng.Roll2D6Sum();
        double avg = total / (double)samples;
        Assert.InRange(avg, 5.5, 8.5); // esperado ~7
    }
}