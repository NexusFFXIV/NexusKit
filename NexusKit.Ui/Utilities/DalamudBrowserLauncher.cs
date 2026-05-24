using Dalamud.Utility;
using NexusKit.Core.Utilities;

namespace NexusKit.Ui.Utilities;

internal sealed class DalamudBrowserLauncher : IBrowserLauncher
{
    public void OpenUrl(string url) => Util.OpenLink(url);
}
