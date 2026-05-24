using NexusKit.Core.Modules;

namespace NexusKit.Persistence.Settings.Schema;

public static class SettingsSchemaBuilderExtensions
{
    /// <summary>
    /// Add the standard module Enabled toggle to a schema. Uses framework localization
    /// keys (<c>nexuskit.module.enabled.label</c> / <c>.description</c>) so the toggle
    /// reads consistently across every module. Place this first in the schema to render
    /// the toggle at the top.
    /// </summary>
    public static SettingsSchemaBuilder<T> RegisterModuleEnabledFlag<T>(this SettingsSchemaBuilder<T> builder, int order = 0)
        where T : class, IModuleSettings, new()
    {
        return builder.Property(x => x.ModuleEnabled, p => p
            .LabelKey("nexuskit.module.enabled.label")
            .DescriptionKey("nexuskit.module.enabled.description")
            .Hidden()
            .Order(order));
    }
}