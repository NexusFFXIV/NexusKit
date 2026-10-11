using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using NexusKit.Core.Context;
using NexusKit.Core.Localization;
using NexusKit.Core.Utilities;
using NexusKit.Persistence.Settings;

namespace NexusKit.Ui.AutoSettings.About;

/// <summary>Reusable About/Changelog surface for plugin settings. The read
/// marker is scoped by plugin name and version, so each release is shown as
/// unread once without requiring plugin-specific persistence code.</summary>
public sealed class AboutSettingsSection : IAutoSettingsSection
{
    private readonly IPluginContext mContext;
    private readonly ILocalizer mLocalizer;
    private readonly IBrowserLauncher mBrowser;
    private readonly AboutPageOptions mOptions;
    private bool? mRead;
    private Task<bool>? mReadTask;

    public AboutSettingsSection(IPluginContext context, ILocalizer localizer,
                                IBrowserLauncher browser, AboutPageOptions options)
    {
        mContext = context;
        mLocalizer = localizer;
        mBrowser = browser;
        mOptions = options;
    }

    public int Order => mOptions.Order;

    public string NavTitleKey => mOptions.NavTitleKey;

    public void Render(ISettingsStore store)
    {
        mReadTask ??= LoadReadStateAsync(store);
        if (mRead is null && mReadTask.IsCompletedSuccessfully)
            mRead = mReadTask.Result;

        ImGui.TextUnformatted(mLocalizer.Get(mOptions.TitleKey));
        ImGui.TextDisabled(string.Format(CultureInfo.CurrentCulture,
            mLocalizer.Get(mOptions.VersionKey), mContext.PluginVersion));
        ImGui.Spacing();
        ImGui.TextWrapped(mLocalizer.Get(mOptions.DescriptionKey));
        ImGui.Spacing();

        ImGui.TextColored(mRead is true ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow,
            mLocalizer.Get(mRead is true ? mOptions.StatusReadKey : mOptions.StatusUnreadKey));
        ImGui.SameLine();
        ImGui.BeginDisabled(mRead is not false);
        if (ImGui.Button(mLocalizer.Get(mOptions.MarkReadKey)))
        {
            mRead = true;
            _ = store.SetAsync(ReadKey, true);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button(mLocalizer.Get(mOptions.RepositoryKey)))
            mBrowser.OpenUrl(mOptions.RepositoryUrl);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextDisabled(mLocalizer.Get(mOptions.ReleaseNotesKey));
        foreach (var key in mOptions.NoteKeys)
            ImGui.BulletText(mLocalizer.Get(key));
    }

    private string ReadKey
        => $"ui.about.read.{mContext.PluginName}.{mContext.PluginVersion.ToString(4)}";

    private async Task<bool> LoadReadStateAsync(ISettingsStore store)
        => await store.GetAsync<bool>(ReadKey).ConfigureAwait(false);
}
