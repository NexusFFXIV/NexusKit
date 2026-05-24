namespace NexusKit.Core;

/// <summary>
/// Dalamud-free view of the local player's live state. UI/services consume
/// this instead of <c>IObjectTable</c> directly so the Dalamud dependency
/// stays behind a single implementation (registered by
/// <c>AddNexusKitGameData</c>).
/// <para>All getters reflect the live game state at call time. Callers MUST
/// invoke from the framework thread (any ImGui callback, <c>IFramework.Update</c>
/// handler, or <c>IFramework.RunOnFrameworkThread</c> body satisfies that).</para>
/// </summary>
public interface ILocalPlayerContext
{
    /// <summary>Snapshot of where the local player is right now (current world).
    /// <c>null</c> when not in-game / character not yet loaded.</summary>
    PlayerLocation? GetLocation();
}
