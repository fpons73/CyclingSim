namespace ProCycling.Core.Models;

/// <summary>Los 14 atributos permanentes, escala 50–99.</summary>
public sealed class Attributes
{
    public int Flat { get; set; }             // FLA
    public int Mountain { get; set; }         // MNT
    public int MediumMountain { get; set; }   // MM
    public int Hill { get; set; }             // HIL
    public int TimeTrial { get; set; }        // TTR
    public int Prologue { get; set; }         // PRL
    public int Cobbles { get; set; }          // COB
    public int Sprint { get; set; }           // SPR
    public int Acceleration { get; set; }     // ACC
    public int Descent { get; set; }          // DHI
    public int Attack { get; set; }           // ATT
    public int Endurance { get; set; }        // STA (Aguante)
    public int Resistance { get; set; }       // RES
    public int Recovery { get; set; }         // REC

    public const int Min = 50;
    public const int Max = 99;

    public int Value(Terrain terrain) => terrain switch
    {
        Terrain.Flat => Flat,
        Terrain.Rolling => MediumMountain,
        Terrain.Hill => Hill,
        Terrain.Climb => Mountain,
        Terrain.Cobbles => Cobbles,
        Terrain.Descent => Descent,
        Terrain.TimeTrial => TimeTrial,
        _ => Flat
    };

    public static double Normalized(double value) =>
        Math.Clamp((value - Min) / (Max - Min), 0.0, 1.0);

    public double NormalizedFlat => Normalized(Flat);
    public double NormalizedMountain => Normalized(Mountain);
    public double NormalizedMediumMountain => Normalized(MediumMountain);
    public double NormalizedHill => Normalized(Hill);
    public double NormalizedTimeTrial => Normalized(TimeTrial);
    public double NormalizedPrologue => Normalized(Prologue);
    public double NormalizedCobbles => Normalized(Cobbles);
    public double NormalizedSprint => Normalized(Sprint);
    public double NormalizedAcceleration => Normalized(Acceleration);
    public double NormalizedDescent => Normalized(Descent);
    public double NormalizedAttack => Normalized(Attack);
    public double NormalizedEndurance => Normalized(Endurance);
    public double NormalizedResistance => Normalized(Resistance);
    public double NormalizedRecovery => Normalized(Recovery);

    public double Get(string attributeKey) => attributeKey switch
    {
        "flat" => Flat,
        "mountain" => Mountain,
        "mm" => MediumMountain,
        "hill" => Hill,
        "ttr" => TimeTrial,
        "prl" => Prologue,
        "cobbles" => Cobbles,
        "sprint" => Sprint,
        "acceleration" => Acceleration,
        "descent" => Descent,
        "attack" => Attack,
        "endurance" => Endurance,
        "resistance" => Resistance,
        "recovery" => Recovery,
        _ => 50
    };

    public IEnumerable<int> All =>
        new[] { Flat, Mountain, MediumMountain, Hill, TimeTrial, Prologue, Cobbles,
                Sprint, Acceleration, Descent, Attack, Endurance, Resistance, Recovery };
}