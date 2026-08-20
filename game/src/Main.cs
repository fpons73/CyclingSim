using Godot;
using ProCycling.Core.Simulation;

namespace ProCycling.Game;

public partial class Main : Node2D
{
    public override void _Ready()
    {
        var rng = new SeededRandom(42);
        GD.Print($"[PCRM] Pro Cycling Replay Manager listo. 1d6={rng.RollDie(6)} 2d6={rng.Roll2D6Sum()}");
    }
}