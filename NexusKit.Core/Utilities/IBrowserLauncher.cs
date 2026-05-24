namespace NexusKit.Core.Utilities;

/// <summary>
/// Open a URL in the user's browser. Dalamud-free abstraction — implementations
/// live with the host (e.g. <c>DalamudBrowserLauncher</c> in NexusKit.Ui).
/// </summary>
public interface IBrowserLauncher
{
    void OpenUrl(string url);
}
