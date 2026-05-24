using System.Numerics;
using Dalamud.Bindings.ImGui;
using NexusKit.Core.Localization;
using NexusKit.Persistence.Settings;
using NexusKit.Ui.AutoSettings;
using NexusKit.Ui.Widgets;

namespace NexusKit.ChatNotifications.Internal;

/// <summary>
/// Auto-settings section that lists every registered notification kind and
/// lets the user toggle / re-channel / re-color it. The section reads the
/// registry's cached <see cref="ChatNotificationSettings"/> directly,
/// mutates in place, and asks the registry to persist on change — publishers
/// see the new values on the very next <c>Publish</c>.
/// </summary>
internal sealed class ChatNotificationsSettingsSection : IAutoSettingsSection
{
    /// <summary>Default sort order — placed after the existing plugin /
    /// module groups (which sort at 0). Bumps positive so configuration
    /// for notifications appears at the bottom of the sidebar.</summary>
    public int Order => 100;

    public string NavTitleKey => "nexuskit.chatnotifications.nav";

    /// <summary>Column shape for one kind's row. Three equal-weight stretch
    /// columns (Width = 0 → WidthStretch with weight 1). Per-cell combo width
    /// is measured at draw time via <see cref="ImGui.GetContentRegionAvail"/>
    /// — using <c>SetNextItemWidth(-N)</c> inside a table cell fed the
    /// combo's own width back into the layout pass and produced runaway
    /// shrink/grow behaviour across frames.</summary>
    private static readonly NexusTableColumn[] NotificationKindColumns =
    {
        new("##label"),
        new("##channel"),
        new("##color"),
    };

    private readonly ChatNotificationRegistry mRegistry;
    private readonly ILocalizer mLoc;

    public ChatNotificationsSettingsSection(ChatNotificationRegistry registry, ILocalizer localizer)
    {
        mRegistry = registry;
        mLoc = localizer;
    }

    public void Render(ISettingsStore store)
    {
        var kinds = mRegistry.Registered;
        if (kinds.Count == 0)
        {
            ImGui.TextDisabled(mLoc.Get("nexuskit.chatnotifications.empty"));
            return;
        }

        // Group by GroupKey preserving first-seen order so producer
        // registration order still dictates layout. Kinds without a group key
        // land in a "default" bucket rendered last under a generic heading.
        const string defaultGroupKey = "nexuskit.chatnotifications.group.default";
        var orderedGroupKeys = new List<string>();
        var byGroup = new Dictionary<string, List<NotificationKindDefinition>>(StringComparer.Ordinal);
        foreach (var k in kinds)
        {
            var gk = k.GroupKey ?? defaultGroupKey;
            if (!byGroup.TryGetValue(gk, out var bucket))
            {
                bucket = new List<NotificationKindDefinition>();
                byGroup[gk] = bucket;
                orderedGroupKeys.Add(gk);
            }
            bucket.Add(k);
        }

        // Materialise the group list for NexusGroupedTable. Each row carries
        // one delegate per column — the widget owns row + column
        // advancement and just invokes the delegates. One underlying
        // ImGui table means column widths stay aligned across groups
        // (separate per-group tables would drift and cause combo overflow).
        // Precompute the currently-enabled kinds so each row can evaluate its
        // SuppressedBy relationship without re-resolving. Effective enabled
        // state = override.Enabled when present, else Kind.DefaultEnabled.
        var enabledIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in kinds)
        {
            var effEnabled = mRegistry.Settings.Overrides.TryGetValue(k.Id, out var o)
                ? o.Enabled
                : k.DefaultEnabled;
            if (effEnabled) enabledIds.Add(k.Id);
        }

        var changed = false;
        var groups = new List<NexusGroupedTableGroup>(orderedGroupKeys.Count);
        foreach (var groupKey in orderedGroupKeys)
        {
            var bucket = byGroup[groupKey];
            var rows = new List<NexusTableRow>(bucket.Count);
            foreach (var kind in bucket)
            {
                var k = kind; // capture for closure
                var (suppressed, suppressorLabel) = ResolveSuppression(k, enabledIds, kinds);
                var s = suppressed;
                var supLabel = suppressorLabel;
                rows.Add(new NexusTableRow(new Action[]
                {
                    () => { if (RenderEnabledCell(k, s, supLabel)) changed = true; },
                    () => { if (RenderChannelCell(k, s)) changed = true; },
                    () => { if (RenderColorCell(k, s))   changed = true; },
                }));
            }
            groups.Add(new NexusGroupedTableGroup(
                Id: groupKey,
                Label: mLoc.Get(groupKey),
                Rows: rows));
        }

        NexusGroupedTable.Draw(
            id: "##nx_notif_section",
            columns: NotificationKindColumns,
            groups: groups,
            defaultOpen: true,
            showHeader: false);

        if (changed) _ = mRegistry.PersistAsync();
    }

    /// <summary>Column 0 — enable checkbox + label. Label is the checkbox's
    /// own label so they share an interaction surface; a hover anywhere on
    /// the checkbox or label area shows the kind's description tooltip
    /// (with a suppression-explanation appended when the row is greyed out
    /// because another enabled kind suppresses it).</summary>
    private bool RenderEnabledCell(
        NotificationKindDefinition kind,
        bool suppressed,
        string? suppressorLabel)
    {
        var setting = ResolveOrCreate(kind);
        // Disable the checkbox itself when this kind is suppressed by an
        // enabled coarser kind — the user still sees the row + its label,
        // but they can't toggle it (and the tooltip explains why).
        ImGui.BeginDisabled(suppressed);
        var enabled = setting.Enabled;
        var labelText = mLoc.Get(kind.LabelKey);
        var changed = false;
        if (ImGui.Checkbox($"{labelText}##nx_notif_enabled_{kind.Id}", ref enabled))
        {
            setting.Enabled = enabled;
            changed = true;
        }
        ImGui.EndDisabled();

        // IsItemHovered must run on the checkbox AFTER EndDisabled — ImGui
        // suppresses some hover detection inside BeginDisabled blocks, but
        // the last-drawn item is still queryable from the outer scope.
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var tooltip = mLoc.Get(kind.DescriptionKey);
            if (suppressed && suppressorLabel is not null)
                tooltip += "\n\n" + string.Format(
                    mLoc.Get("nexuskit.chatnotifications.suppressed_by"), suppressorLabel);
            ImGui.SetTooltip(tooltip);
        }
        return changed;
    }

    /// <summary>Column 1 — channel combo. Disabled when the kind is off or
    /// when an enabled suppressor blocks this row. Combo width is the
    /// cell's currently-available content width, captured explicitly —
    /// <c>SetNextItemWidth(-N)</c> inside a table cell feeds the combo's
    /// own width back into the layout pass and produced runaway shrink/grow
    /// across frames.</summary>
    private bool RenderChannelCell(NotificationKindDefinition kind, bool suppressed)
    {
        var setting = ResolveOrCreate(kind);
        var changed = false;
        ImGui.BeginDisabled(suppressed || !setting.Enabled);
        try
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var values = Enum.GetValues<NotificationChannel>();
            var labels = values.Select(LabelFor).ToArray();
            var idx = Array.IndexOf(values, setting.Channel);
            if (idx < 0) idx = 0;
            if (ImGui.Combo($"##nx_notif_channel_{kind.Id}", ref idx, labels, labels.Length))
            {
                setting.Channel = values[idx];
                changed = true;
            }
        }
        finally { ImGui.EndDisabled(); }
        return changed;
    }

    /// <summary>Column 2 — color combo + inline preview swatch. Combo width
    /// is the cell's available space minus the swatch + inner spacing, all
    /// captured before any item is drawn so the math doesn't depend on this
    /// frame's own content. Disabled on suppression or when the kind is off.</summary>
    private bool RenderColorCell(NotificationKindDefinition kind, bool suppressed)
    {
        var setting = ResolveOrCreate(kind);
        var changed = false;
        ImGui.BeginDisabled(suppressed || !setting.Enabled);
        try
        {
            var swatchSize = ImGui.GetTextLineHeight();
            var spacing = ImGui.GetStyle().ItemInnerSpacing.X;
            var comboWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X - swatchSize - spacing);
            ImGui.SetNextItemWidth(comboWidth);

            var values = Enum.GetValues<NotificationColor>();
            var labels = values.Select(LabelFor).ToArray();
            var idx = Array.IndexOf(values, setting.Color);
            if (idx < 0) idx = 0;
            if (ImGui.Combo($"##nx_notif_color_{kind.Id}", ref idx, labels, labels.Length))
            {
                setting.Color = values[idx];
                changed = true;
            }
            var (r, g, b, a) = ChatColorMap.ToPreviewRgba(setting.Color);
            ImGui.SameLine(0, spacing);
            var drawList = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            drawList.AddRectFilled(p, p + new Vector2(swatchSize, swatchSize),
                ImGui.GetColorU32(new Vector4(r, g, b, a)));
            ImGui.Dummy(new Vector2(swatchSize, swatchSize));
        }
        finally { ImGui.EndDisabled(); }
        return changed;
    }

    /// <summary>Returns the in-place override entry for the kind, creating one
    /// from the kind's defaults if it didn't exist yet. The reference is held
    /// by the registry's cached <see cref="ChatNotificationSettings"/>, so
    /// mutating it propagates to publishers immediately.</summary>
    private ChatNotificationKindSetting ResolveOrCreate(NotificationKindDefinition kind)
    {
        if (mRegistry.Settings.Overrides.TryGetValue(kind.Id, out var existing)) return existing;
        var created = new ChatNotificationKindSetting
        {
            Enabled = kind.DefaultEnabled,
            Channel = kind.DefaultChannel,
            Color = kind.DefaultColor,
        };
        mRegistry.Settings.Overrides[kind.Id] = created;
        return created;
    }

    private string LabelFor(NotificationChannel ch) => mLoc.Get($"nexuskit.chatnotifications.channel.{ch.ToString().ToLowerInvariant()}");
    private string LabelFor(NotificationColor color) => mLoc.Get($"nexuskit.chatnotifications.color.{color.ToString().ToLowerInvariant()}");

    /// <summary>Decides whether <paramref name="kind"/>'s row should be
    /// greyed out, in either direction:
    /// <list type="bullet">
    /// <item><b>Forward</b>: this kind names a currently-enabled suppressor
    /// in its <see cref="NotificationKindDefinition.SuppressedBy"/> list —
    /// the suppressor "covers" this kind so we don't let the user enable
    /// the redundant variant.</item>
    /// <item><b>Reverse</b>: some other currently-enabled kind names
    /// <paramref name="kind"/> in <i>its</i> <c>SuppressedBy</c> list —
    /// meaning enabling <paramref name="kind"/> right now would supersede
    /// that other kind. We block the toggle so the user has to disable
    /// the granular variant first; otherwise the two-way relationship
    /// could silently invalidate a setting the user just enabled.</item>
    /// </list>
    /// Returns (true, label) with the other kind's localized label so the
    /// row's hover tooltip can name what's blocking it.</summary>
    private (bool Suppressed, string? Label) ResolveSuppression(
        NotificationKindDefinition kind,
        HashSet<string> enabledIds,
        IReadOnlyList<NotificationKindDefinition> allKinds)
    {
        // Forward direction.
        if (kind.SuppressedBy is { Count: > 0 } suppressors)
        {
            foreach (var supId in suppressors)
            {
                if (!enabledIds.Contains(supId)) continue;
                string? label = null;
                foreach (var k in allKinds)
                {
                    if (k.Id != supId) continue;
                    label = mLoc.Get(k.LabelKey);
                    break;
                }
                return (true, label);
            }
        }

        // Reverse direction — any enabled kind that lists `kind` as a
        // suppressor blocks `kind` from being toggled.
        foreach (var other in allKinds)
        {
            if (other.Id == kind.Id) continue;
            if (other.SuppressedBy is not { Count: > 0 } osup) continue;
            if (!enabledIds.Contains(other.Id)) continue;
            foreach (var supId in osup)
            {
                if (supId != kind.Id) continue;
                return (true, mLoc.Get(other.LabelKey));
            }
        }

        return (false, null);
    }
}
