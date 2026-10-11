namespace NexusKit.Ui.AutoSettings;

/// <summary>Receives live changes made through the generic settings window.</summary>
public interface ISettingsChangeObserver
{
    void OnChanged(object settings, string propertyName);
}
