using System.Numerics;
using Dalamud.Bindings.ImGui;
using NexusKit.Core.Context;
using NexusKit.Core.Localization;
using NexusKit.Core.Modules;
using NexusKit.Persistence.Settings;
using NexusKit.Persistence.Settings.Schema;
using NexusKit.Ui.Abstractions;

namespace NexusKit.Ui.AutoSettings;

public sealed class AutoSettingsWindow : SettingsWindow
{
    private const float SidebarWidth = 170f;

    private readonly ISettingsSchemaProvider mSchemas;
    private readonly ISettingsStore mStore;
    private readonly ILocalizer mLocalizer;
    private readonly IReadOnlyList<IAutoSettingsSection> mSections;
    private readonly Dictionary<Type, object> mValues = new();
    private bool mInitialized;
    private string? mCurrentNavId;

    public AutoSettingsWindow(IPluginContext ctx, ISettingsSchemaProvider schemas, ISettingsStore store,
                              ILocalizer localizer, IEnumerable<IAutoSettingsSection> sections)
        : base($"{ctx.PluginName} Settings###{ctx.PluginName}_AutoSettings", store, restoreOpenState: false)
    {
        mSchemas = schemas;
        mStore = store;
        mLocalizer = localizer;
        // Materialize once at ctor — DI hands us a fresh enumerable; we want a
        // stable, ordered list for nav rendering. Order is the section's own
        // self-declared sort key; ties break by NavTitleKey for determinism.
        mSections = sections.OrderBy(s => s.Order).ThenBy(s => s.NavTitleKey).ToList();
        Size = new Vector2(720, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
        // Min size keeps the inline criterion-editor rows in the player-
        // filter section from collapsing onto themselves — that section is
        // the widest content the window hosts (field combo + operator combo
        // + value widget + per-row delete button). Anything narrower starts
        // wrapping in ways that look broken rather than responsive.
        SizeConstraints = new Dalamud.Interface.Windowing.WindowSizeConstraints
        {
            MinimumSize = new Vector2(680, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        if (!mInitialized)
        {
            LoadAll();
            mInitialized = true;
        }

        var navItems = BuildNavigation();
        if (navItems.Count == 0)
        {
            ImGui.TextDisabled(mLocalizer.Get("nexuskit.ui.no_settings_registered"));
            return;
        }

        mCurrentNavId ??= navItems[0].Id;
        if (navItems.All(n => n.Id != mCurrentNavId))
            mCurrentNavId = navItems[0].Id;

        var totalHeight = ImGui.GetContentRegionAvail().Y;

        if (ImGui.BeginChild("##nexuskit_nav", new Vector2(SidebarWidth, totalHeight), true))
        {
            foreach (var item in navItems)
            {
                if (ImGui.Selectable($"{item.Label}##nav_{item.Id}", item.Id == mCurrentNavId))
                    mCurrentNavId = item.Id;
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("##nexuskit_content", new Vector2(0, totalHeight), false))
        {
            var active = navItems.First(n => n.Id == mCurrentNavId);
            switch (active.Kind)
            {
                case NavKind.PluginGroup:
                    RenderPluginGroup(active.Schemas);
                    break;

                case NavKind.Modules:
                    RenderModulesSection(active.Schemas);
                    break;

                case NavKind.Notifications:
                    active.Section!.Render(mStore);
                    break;
            }
        }
        ImGui.EndChild();
    }

    private List<NavItem> BuildNavigation()
    {
        var navItems = new List<NavItem>();
        var fallback = mLocalizer.Get("nexuskit.ui.group.general");

        var pluginGroups = mSchemas.All
            .Where(s => !IsModuleSchema(s))
            .GroupBy(s => (Identity: s.Group.Literal ?? s.Group.Key ?? string.Empty, s.GroupOrder))
            .OrderBy(g => g.Key.GroupOrder)
            .ThenBy(g => g.Key.Identity);

        foreach (var group in pluginGroups)
        {
            var label = group.First().Group.Resolve(mLocalizer, fallback);
            navItems.Add(new NavItem(
                Id: $"plugin:{group.Key.Identity}",
                Kind: NavKind.PluginGroup,
                Label: label,
                Schemas: group.ToList()));
        }

        var moduleSchemas = mSchemas.All.Where(IsModuleSchema).ToList();
        var externalDataSchemas = moduleSchemas.Where(s => GetKind(s) == ModuleKind.ExternalDataSource).ToList();
        var otherModuleSchemas = moduleSchemas.Except(externalDataSchemas).ToList();

        if (externalDataSchemas.Count > 0)
        {
            navItems.Add(new NavItem(
                Id: "modules:external_data_sources",
                Kind: NavKind.Modules,
                Label: mLocalizer.Get("nexuskit.module.external_data_sources.group"),
                Schemas: externalDataSchemas));
        }

        if (otherModuleSchemas.Count > 0)
        {
            navItems.Add(new NavItem(
                Id: "modules",
                Kind: NavKind.Modules,
                Label: mLocalizer.Get("nexuskit.module.group"),
                Schemas: otherModuleSchemas));
        }

        // Extension hooks — sections own their rendering and aren't tied to
        // the declarative schema model. Sorted by their self-declared Order
        // (computed in the ctor) so deterministic placement is up to the
        // section itself.
        foreach (var section in mSections)
        {
            navItems.Add(new NavItem(
                Id: $"section:{section.NavTitleKey}",
                Kind: NavKind.Notifications,
                Label: mLocalizer.Get(section.NavTitleKey),
                Schemas: Array.Empty<IRegisteredSettingsSchema>(),
                Section: section));
        }

        return navItems;
    }

    private ModuleKind? GetKind(IRegisteredSettingsSchema schema)
        => mValues.TryGetValue(schema.SettingsType, out var instance) ? (instance as IModuleSettings)?.Kind : null;

    private void RenderPluginGroup(IReadOnlyList<IRegisteredSettingsSchema> schemasInGroup)
    {
        var multiple = schemasInGroup.Count > 1;
        foreach (var schema in schemasInGroup)
        {
            if (multiple && !schema.Title.IsEmpty)
            {
                ImGui.Spacing();
                ImGui.TextDisabled(schema.Title.Resolve(mLocalizer));
                ImGui.Separator();
            }
            RenderSchemaProperties(schema);
        }
    }

    private void RenderModulesSection(IReadOnlyList<IRegisteredSettingsSchema> moduleSchemas)
    {
        if (!ImGui.BeginTabBar("##nexuskit_modules_tabs"))
            return;

        var generalLabel = mLocalizer.Get("nexuskit.ui.group.general");
        if (ImGui.BeginTabItem($"{generalLabel}##modules_general"))
        {
            RenderModuleToggles(moduleSchemas);
            ImGui.EndTabItem();
        }

        foreach (var schema in moduleSchemas.OrderBy(m => m.GroupOrder).ThenBy(m => Title(m)))
        {
            if (!IsEnabled(schema)) continue;
            if (schema.Properties.Count <= 1) continue;

            var title = Title(schema);
            if (!ImGui.BeginTabItem($"{title}##module_{schema.StoreKey}"))
                continue;

            RenderSchemaProperties(schema);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void RenderModuleToggles(IReadOnlyList<IRegisteredSettingsSchema> modules)
    {
        foreach (var schema in modules.OrderBy(m => m.GroupOrder).ThenBy(m => Title(m)))
        {
            if (!mValues.TryGetValue(schema.SettingsType, out var instance))
                continue;

            var enabledProp = schema.Properties.FirstOrDefault(p => p.Name == nameof(IModuleSettings.ModuleEnabled));
            if (enabledProp is null) continue;

            var title = Title(schema);
            var enabled = (bool)(enabledProp.GetValue(instance) ?? false);
            if (ImGui.Checkbox($"{title}##overview_{schema.StoreKey}", ref enabled))
            {
                enabledProp.SetValue(instance, enabled);
                _ = schema.SaveAsync(mStore, instance);
            }
        }
    }

    private void RenderSchemaProperties(IRegisteredSettingsSchema schema)
    {
        if (!mValues.TryGetValue(schema.SettingsType, out var instance))
            return;

        foreach (var prop in schema.Properties.OrderBy(p => p.Order))
        {
            if (prop.ResolveKind() == ControlKind.Hidden) continue;
            if (SettingsControlRenderer.Render(prop, instance, mLocalizer))
            {
                _ = schema.SaveAsync(mStore, instance);
            }
        }
    }

    private bool IsEnabled(IRegisteredSettingsSchema schema)
    {
        if (!mValues.TryGetValue(schema.SettingsType, out var instance)) return false;
        var enabledProp = schema.Properties.FirstOrDefault(p => p.Name == nameof(IModuleSettings.ModuleEnabled));
        if (enabledProp is null) return false;
        return (bool)(enabledProp.GetValue(instance) ?? false);
    }

    private string Title(IRegisteredSettingsSchema schema)
        => schema.Title.IsEmpty
            ? schema.SettingsType.Name
            : schema.Title.Resolve(mLocalizer);

    private static bool IsModuleSchema(IRegisteredSettingsSchema schema)
        => typeof(IModuleSettings).IsAssignableFrom(schema.SettingsType);

    private void LoadAll()
    {
        foreach (var schema in mSchemas.All)
        {
            mValues[schema.SettingsType] = schema.LoadAsync(mStore).GetAwaiter().GetResult();
        }
    }

    private enum NavKind
    { PluginGroup, Modules, Notifications }

    private sealed record NavItem(
        string Id,
        NavKind Kind,
        string Label,
        IReadOnlyList<IRegisteredSettingsSchema> Schemas,
        IAutoSettingsSection? Section = null);
}