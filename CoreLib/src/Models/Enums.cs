namespace ProCycling.Core.Models;

public enum StageType
{
    Flat,
    FlatHilly,
    FlatCobbles,
    MediumMountain,
    Mountain,
    IndividualTimeTrial,
    TeamTimeTrial,
    Crosswind,
    Prologue,
    Rest
}

public enum Terrain
{
    Flat,
    Rolling,
    Hill,
    Climb,
    Descent,
    Cobbles,
    TimeTrial
}

public enum WindDirection
{
    Tail,
    Head,
    Cross
}

public enum GroupKind
{
    Breakaway,
    Chase,
    Peloton,
    Echelon,
    SmallGroup,
    LoneRider,
    TimeTrialGroup
}

public enum RiderStatus
{
    Active,
    Questionable,
    Dropped,
    Dns,
    Dnf,
    Dsq,
    Finished
}

public enum RiderTeamRole
{
    Leader,
    Protected,
    Domestique,
    Sprinter,
    None
}

/// <summary>Especializaciones derivadas de las estadísticas (no sustituyen los 14 atributos).</summary>
public enum RiderSpecialty
{
    Sprinter,
    Climber,
    Puncheur,
    Rouleur,
    TimeTrialist,
    PrologueSpecialist,
    Paveur,
    Allrounder
}