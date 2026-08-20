using System.Text.Json.Serialization;

namespace ProCycling.Core.Simulation;

/// <summary>
/// RNG determinista xoshiro256** con semilla. El estado es serializable (4×ulong)
/// para permitir reproducción exacta de una carrera (PRD §8, §32).
/// </summary>
public sealed class SeededRandom
{
    private ulong _s0, _s1, _s2, _s3;

    public SeededRandom(ulong seed)
    {
        SetSeed(seed);
    }

    public void SetSeed(ulong seed)
    {
        ulong t = seed;
        _s0 = SplitMix64(ref t);
        _s1 = SplitMix64(ref t);
        _s2 = SplitMix64(ref t);
        _s3 = SplitMix64(ref t);
    }

    public static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong NextULong()
    {
        ulong result = Rotl(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);
        return result;
    }

    /// <summary>Double uniforme en [0, 1) con 53 bits de precisión.</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Entero en [min, maxExclusive).</summary>
    public int Next(int min, int maxExclusive)
    {
        if (maxExclusive <= min) return min;
        return min + (int)(NextULong() % (ulong)(maxExclusive - min));
    }

    public int RollDie(int sides) => Next(1, sides + 1);

    /// <summary>2d6 (rojo/blanco) → suma en [2, 12].</summary>
    public (int Red, int White) Roll2D6()
    {
        int red = RollDie(6);
        int white = RollDie(6);
        return (red, white);
    }

    public int Roll2D6Sum()
    {
        var (r, w) = Roll2D6();
        return r + w;
    }

    /// <summary>1d10 (azul) → [1, 10].</summary>
    public int Roll1D10() => RollDie(10);

    public ulong[] GetState() => new[] { _s0, _s1, _s2, _s3 };

    public void RestoreState(ulong[] state)
    {
        if (state is { Length: >= 4 })
        {
            _s0 = state[0]; _s1 = state[1]; _s2 = state[2]; _s3 = state[3];
        }
    }
}