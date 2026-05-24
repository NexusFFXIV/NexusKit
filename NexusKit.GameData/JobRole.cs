namespace NexusKit.GameData;

/// <summary>Role classification derived from <c>ClassJob.Role</c>. Crafter / Gatherer
/// fall outside FFXIV's Tank/Healer/Dps role byte and are inferred from JobIndex.</summary>
public enum JobRole
{
    Unknown    = 0,
    Tank       = 1,
    Healer     = 2,
    MeleeDps   = 3,
    RangedDps  = 4,
    MagicalDps = 5,
    Crafter    = 6,
    Gatherer   = 7,
}
