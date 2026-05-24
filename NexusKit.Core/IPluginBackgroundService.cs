namespace NexusKit.Core;

/// <summary>
/// Marker for a long-lived singleton whose construction is itself the
/// activation: the ctor subscribes to events, starts a background loop,
/// or otherwise needs to run before anyone explicitly resolves the
/// service. The host eager-resolves every registered instance during
/// <c>BuildAsync</c> so modules don't have to push their background
/// services into the plugin's <c>LoadAsync</c> by hand.
/// <para>Mirrors the <c>IIpcProvider</c> resolution-as-side-effect
/// pattern. Implementations stay free to expose any other interface
/// they like — register the concrete singleton once, then forward both
/// <see cref="IPluginBackgroundService"/> and the public service type
/// to it via factory registrations so the host sees the same instance.</para>
/// </summary>
public interface IPluginBackgroundService
{
}
