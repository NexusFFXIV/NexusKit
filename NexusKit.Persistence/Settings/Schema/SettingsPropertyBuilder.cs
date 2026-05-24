using System.Reflection;
using NexusKit.Core.Localization;

namespace NexusKit.Persistence.Settings.Schema;

public sealed class SettingsPropertyBuilder<T, TValue>
{
    private readonly PropertyInfo mProperty;
    private LocalizedText mLabel;
    private LocalizedText mDescription;
    private LocalizedText mPlaceholder;
    private int mOrder;
    private ControlKind mKind = ControlKind.Auto;
    private double? mMin;
    private double? mMax;
    private IReadOnlyList<object>? mChoices;

    internal SettingsPropertyBuilder(PropertyInfo property)
    {
        mProperty = property;
    }

    public SettingsPropertyBuilder<T, TValue> Label(string text)
    {
        mLabel = LocalizedText.FromLiteral(text);
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> LabelKey(string key)
    {
        mLabel = LocalizedText.FromKey(key);
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> Description(string text)
    {
        mDescription = LocalizedText.FromLiteral(text);
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> DescriptionKey(string key)
    {
        mDescription = LocalizedText.FromKey(key);
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> Placeholder(string text)
    {
        mPlaceholder = LocalizedText.FromLiteral(text);
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> PlaceholderKey(string key)
    {
        mPlaceholder = LocalizedText.FromKey(key);
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> Order(int value) { mOrder = value; return this; }

    public SettingsPropertyBuilder<T, TValue> Checkbox()
    {
        mKind = ControlKind.Checkbox;
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> TextBox()
    {
        mKind = ControlKind.TextBox;
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> NumericInput()
    {
        mKind = ControlKind.NumericInput;
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> Slider(double min, double max)
    {
        mKind = ControlKind.Slider;
        mMin = min;
        mMax = max;
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> Choices(params TValue[] options)
    {
        mKind = ControlKind.Combo;
        mChoices = options.Cast<object>().ToArray();
        return this;
    }

    public SettingsPropertyBuilder<T, TValue> Hidden()
    {
        mKind = ControlKind.Hidden;
        return this;
    }

    internal SettingsPropertyDescriptor Build() => new(
        property: mProperty,
        label: mLabel,
        description: mDescription,
        placeholder: mPlaceholder,
        order: mOrder,
        kind: mKind,
        min: mMin,
        max: mMax,
        choices: mChoices);
}
