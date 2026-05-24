namespace NexusKit.Core.Modules;

/// <summary>
/// Marker for module settings POCOs. Every NexusKit module ships a settings class
/// implementing this contract so the framework can render a uniform "Enabled" toggle
/// and treat modules consistently in the auto-settings UI.
/// </summary>
public interface IModuleSettings
{
    bool ModuleEnabled { get; set; }

    /// <summary>
    /// Optional classification used by aggregator modules to discover siblings of a given role
    /// (e.g. all <see cref="ModuleKind.ExternalDataSource"/> modules). Defaults to <c>null</c> —
    /// modules without a clear role leave it unset.
    /// </summary>
    ModuleKind? Kind => null;
}
