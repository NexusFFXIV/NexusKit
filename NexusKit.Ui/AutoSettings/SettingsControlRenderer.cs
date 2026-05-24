using Dalamud.Bindings.ImGui;
using NexusKit.Core.Localization;
using NexusKit.Persistence.Settings.Schema;

namespace NexusKit.Ui.AutoSettings;

internal static class SettingsControlRenderer
{
    public static bool Render(SettingsPropertyDescriptor prop, object instance, ILocalizer localizer)
    {
        var kind = prop.ResolveKind();
        var label = prop.Label.Resolve(localizer, fallback: prop.Name);

        var changed = kind switch
        {
            ControlKind.Checkbox     => RenderCheckbox(prop, instance, label),
            ControlKind.TextBox      => RenderTextBox(prop, instance, label, prop.Placeholder.ResolveOrNull(localizer)),
            ControlKind.NumericInput => RenderNumeric(prop, instance, label),
            ControlKind.Slider       => RenderSlider(prop, instance, label),
            ControlKind.Combo        => RenderCombo(prop, instance, label),
            _ => false,
        };

        RenderDescriptionTooltip(prop, localizer);
        return changed;
    }

    private static bool RenderCheckbox(SettingsPropertyDescriptor prop, object instance, string label)
    {
        var value = (bool)(prop.GetValue(instance) ?? false);
        if (ImGui.Checkbox(label, ref value))
        {
            prop.SetValue(instance, value);
            return true;
        }
        return false;
    }

    private static bool RenderTextBox(SettingsPropertyDescriptor prop, object instance, string label, string? placeholder)
    {
        var value = (string?)prop.GetValue(instance) ?? string.Empty;
        bool changed;

        if (!string.IsNullOrEmpty(placeholder))
            changed = ImGui.InputTextWithHint(label, placeholder, ref value, 1024);
        else
            changed = ImGui.InputText(label, ref value, 1024);

        if (changed)
        {
            prop.SetValue(instance, value);
            return true;
        }
        return false;
    }

    private static bool RenderNumeric(SettingsPropertyDescriptor prop, object instance, string label)
    {
        if (prop.PropertyType == typeof(int))
        {
            var value = (int)(prop.GetValue(instance) ?? 0);
            if (ImGui.InputInt(label, ref value))
            {
                prop.SetValue(instance, value);
                return true;
            }
        }
        else if (prop.PropertyType == typeof(float))
        {
            var value = (float)(prop.GetValue(instance) ?? 0f);
            if (ImGui.InputFloat(label, ref value))
            {
                prop.SetValue(instance, value);
                return true;
            }
        }
        else if (prop.PropertyType == typeof(double))
        {
            var value = (double)(prop.GetValue(instance) ?? 0d);
            if (ImGui.InputDouble(label, ref value))
            {
                prop.SetValue(instance, value);
                return true;
            }
        }
        return false;
    }

    private static bool RenderSlider(SettingsPropertyDescriptor prop, object instance, string label)
    {
        var min = prop.Min ?? 0;
        var max = prop.Max ?? 100;

        if (prop.PropertyType == typeof(int))
        {
            var value = (int)(prop.GetValue(instance) ?? 0);
            if (ImGui.SliderInt(label, ref value, (int)min, (int)max))
            {
                prop.SetValue(instance, value);
                return true;
            }
        }
        else if (prop.PropertyType == typeof(float))
        {
            var value = (float)(prop.GetValue(instance) ?? 0f);
            if (ImGui.SliderFloat(label, ref value, (float)min, (float)max))
            {
                prop.SetValue(instance, value);
                return true;
            }
        }
        else if (prop.PropertyType == typeof(double))
        {
            var value = (float)Convert.ToDouble(prop.GetValue(instance) ?? 0d);
            if (ImGui.SliderFloat(label, ref value, (float)min, (float)max))
            {
                prop.SetValue(instance, (double)value);
                return true;
            }
        }
        return false;
    }

    private static bool RenderCombo(SettingsPropertyDescriptor prop, object instance, string label)
    {
        if (prop.Choices is null || prop.Choices.Count == 0) return false;

        var current = prop.GetValue(instance);
        var currentIndex = 0;
        for (var i = 0; i < prop.Choices.Count; i++)
        {
            if (Equals(prop.Choices[i], current))
            {
                currentIndex = i;
                break;
            }
        }

        var labels = prop.Choices.Select(c => c?.ToString() ?? string.Empty).ToArray();
        var idx = currentIndex;
        if (ImGui.Combo(label, ref idx, labels, labels.Length))
        {
            prop.SetValue(instance, prop.Choices[idx]);
            return true;
        }
        return false;
    }

    private static void RenderDescriptionTooltip(SettingsPropertyDescriptor prop, ILocalizer localizer)
    {
        var description = prop.Description.ResolveOrNull(localizer);
        if (string.IsNullOrEmpty(description)) return;
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(description);
        }
    }
}
