using NexusKit.Persistence.Settings;

namespace NexusKit.Ui.AutoSettings;

/// <summary>
/// Extension hook for the <see cref="AutoSettingsWindow"/>: anything implementing
/// this contributes one extra nav entry to the sidebar and is fully responsible
/// for rendering its own body. The window enumerates implementations from DI;
/// adding a new section is "register one singleton" with no changes to the
/// window itself.
/// <para>Use this for content that doesn't fit the declarative
/// <c>AddSettings&lt;T&gt;</c> model — dynamic lists, multi-table layouts,
/// per-row controls a registry contributes at runtime, etc. The chat-notification
/// framework's <c>Notifications</c> tab is the first consumer.</para>
/// </summary>
public interface IAutoSettingsSection
{
    /// <summary>Localization key for the sidebar label.</summary>
    string NavTitleKey { get; }

    /// <summary>Sort key relative to other nav items (plugin groups and module
    /// sections). Lower values appear earlier; the existing groups use the
    /// default of 0 so sections with negative order sort to the top, positive
    /// to the bottom.</summary>
    int Order { get; }

    /// <summary>Render the entire content area. Called within a child window
    /// already sized to the available content region; standard ImGui calls
    /// only. The implementor owns persistence via the provided store.</summary>
    void Render(ISettingsStore store);
}
