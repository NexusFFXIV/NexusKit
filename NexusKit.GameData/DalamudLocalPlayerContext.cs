using Dalamud.Plugin.Services;
using NexusKit.Core;
using NexusKit.GameData.ObjectTables;

namespace NexusKit.GameData;

/// <summary>
/// Dalamud-side <see cref="ILocalPlayerContext"/> implementation: reads the
/// local player at <c>objectTable[0]</c> via
/// <see cref="ObjectTableExtensions.GetSelf"/> and projects the
/// fields views actually need into a Dalamud-free <see cref="PlayerLocation"/>.
/// </summary>
internal sealed class DalamudLocalPlayerContext : ILocalPlayerContext
{
    private readonly IObjectTable mObjectTable;

    public DalamudLocalPlayerContext(IObjectTable objectTable)
    {
        mObjectTable = objectTable;
    }

    public PlayerLocation? GetLocation()
    {
        var self = mObjectTable.GetSelf();
        if (self is null) return null;
        return new PlayerLocation(self.CurrentWorldId);
    }
}
