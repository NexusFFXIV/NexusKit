using System.Numerics;

namespace NexusKit.GameData;

/// <summary>UI-side helpers for <see cref="JobRole"/>.</summary>
public static class JobRoleExtensions
{
    /// <summary>
    /// FFXIV's in-game role color convention as an ImGui-ready RGBA Vector4:
    /// tanks blue, healers green, melee DPS red, ranged physical orange,
    /// magical DPS magenta, crafters yellow, gatherers light green. Returns
    /// <c>null</c> for <see cref="JobRole.Unknown"/> so the caller can fall
    /// back to the default text color instead of a misleading tint.
    /// </summary>
    public static Vector4? ToRoleColor(this JobRole role) => role switch
    {
        JobRole.Tank        => new Vector4(0.30f, 0.55f, 1.00f, 1f),
        JobRole.Healer      => new Vector4(0.35f, 0.85f, 0.40f, 1f),
        JobRole.MeleeDps    => new Vector4(0.95f, 0.45f, 0.45f, 1f),
        JobRole.RangedDps   => new Vector4(0.95f, 0.65f, 0.40f, 1f),
        JobRole.MagicalDps  => new Vector4(0.85f, 0.55f, 0.95f, 1f),
        JobRole.Crafter     => new Vector4(0.95f, 0.85f, 0.30f, 1f),
        JobRole.Gatherer    => new Vector4(0.65f, 0.85f, 0.45f, 1f),
        _                   => null,
    };

    /// <summary>
    /// Sort key for the canonical role-grouped ordering used by the in-game
    /// class-job UI: tank → healer → melee → ranged → magical DPS → crafter →
    /// gatherer → unknown. The raw <see cref="JobRole"/> enum has
    /// <see cref="JobRole.Unknown"/> at value 0 which would put it at the
    /// top — this helper bumps Unknown to the end so any unclassified job
    /// lands below the rest. Use as the leading <c>OrderBy</c> key when
    /// rendering job lists so the same grouping shows up across tabs
    /// (ClassJobs, Encounters, Summary, …).
    /// </summary>
    public static int ToSortOrder(this JobRole role) => role switch
    {
        JobRole.Tank        => 0,
        JobRole.Healer      => 1,
        JobRole.MeleeDps    => 2,
        JobRole.RangedDps   => 3,
        JobRole.MagicalDps  => 4,
        JobRole.Crafter     => 5,
        JobRole.Gatherer    => 6,
        _                   => 99,
    };
}
