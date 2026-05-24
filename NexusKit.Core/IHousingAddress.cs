namespace NexusKit.Core;

/// <summary>
/// Minimal address surface needed to navigate to a housing plot. Any record
/// or class carrying these fields (e.g. <c>NexusKit.Modules.ExternalData.Models.EstateAddress</c>)
/// can implement this and be passed directly to foreign-plugin adapters like
/// <c>ILifestreamAdapter</c> without dragging the full module into the
/// adapter's dependency tree.
/// <para>All values are nullable because Lodestone scrapes can be partial
/// (e.g. a profile-known FC with no housing block). Consumers treat any
/// missing field as "address not navigable".</para>
/// </summary>
public interface IHousingAddress
{
    /// <summary>FFXIV <c>World.RowId</c> the plot sits on. Adapters use it to
    /// decide whether a same-world or cross-world command form is needed.</summary>
    uint? WorldId { get; }

    /// <summary>FFXIV <c>TerritoryType.RowId</c> of the residential district.
    /// One of <c>NexusKit.GameData.ResidentialDistricts.*</c> in practice.</summary>
    uint? DistrictTerritoryId { get; }

    /// <summary>Ward number, 1-indexed as shown in-game and on Lodestone.</summary>
    int? Ward { get; }

    /// <summary>Plot number (houses) or apartment number (apartments),
    /// 1-indexed.</summary>
    int? PlotNumber { get; }

    /// <summary>True when the address is an apartment rather than a plot.
    /// FC estates are always houses, so this is <c>false</c> in the current
    /// use-cases; the field exists for forward compatibility with player-owned
    /// apartment rows.</summary>
    bool IsApartment { get; }

    /// <summary>True when the plot is in the subdivision area of its district.</summary>
    bool IsSubdivision { get; }
}
