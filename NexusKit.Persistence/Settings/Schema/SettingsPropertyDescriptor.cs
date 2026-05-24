using System.Reflection;
using NexusKit.Core.Localization;

namespace NexusKit.Persistence.Settings.Schema;

public sealed class SettingsPropertyDescriptor
{
    private readonly PropertyInfo mProperty;

    public string Name => mProperty.Name;
    public Type PropertyType => mProperty.PropertyType;
    public LocalizedText Label { get; }
    public LocalizedText Description { get; }
    public LocalizedText Placeholder { get; }
    public int Order { get; }
    public ControlKind Kind { get; }
    public double? Min { get; }
    public double? Max { get; }
    public IReadOnlyList<object>? Choices { get; }

    public SettingsPropertyDescriptor(
        PropertyInfo property,
        LocalizedText label,
        LocalizedText description,
        LocalizedText placeholder,
        int order,
        ControlKind kind,
        double? min,
        double? max,
        IReadOnlyList<object>? choices)
    {
        mProperty = property;
        Label = label;
        Description = description;
        Placeholder = placeholder;
        Order = order;
        Kind = kind;
        Min = min;
        Max = max;
        Choices = choices;
    }

    public object? GetValue(object instance) => mProperty.GetValue(instance);

    public void SetValue(object instance, object? value) => mProperty.SetValue(instance, value);

    public ControlKind ResolveKind()
    {
        if (Kind != ControlKind.Auto) return Kind;

        if (PropertyType == typeof(bool)) return ControlKind.Checkbox;
        if (PropertyType == typeof(string)) return ControlKind.TextBox;
        if (PropertyType == typeof(int) || PropertyType == typeof(float) || PropertyType == typeof(double))
            return ControlKind.NumericInput;
        if (PropertyType.IsEnum) return ControlKind.Combo;

        return ControlKind.TextBox;
    }
}
