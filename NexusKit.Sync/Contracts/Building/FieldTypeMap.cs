using System.Diagnostics.CodeAnalysis;

namespace NexusKit.Sync.Contracts.Building;

/// <summary>
/// Maps CLR types onto <see cref="FieldType"/> for the builder's field inference.
/// </summary>
internal static class FieldTypeMap
{
    /// <summary>
    /// Resolves the contract type for a property type, unwrapping <see cref="Nullable{T}"/>.
    /// Returns false for anything the contract cannot express.
    /// </summary>
    public static bool TryMap(Type clrType, out FieldType fieldType, out bool wasNullableValueType)
    {
        var underlying = Nullable.GetUnderlyingType(clrType);
        wasNullableValueType = underlying is not null;

        var effective = underlying ?? clrType;

        if (effective.IsEnum)
        {
            // Enums map to their name, not their numeric value: renumbering an enum is a
            // routine refactor in C# and must not silently reinterpret stored data.
            fieldType = FieldType.String;
            return true;
        }

        fieldType = default;

        if (effective == typeof(string)) fieldType = FieldType.String;
        else if (effective == typeof(bool)) fieldType = FieldType.Boolean;
        else if (effective == typeof(byte)
                 || effective == typeof(sbyte)
                 || effective == typeof(short)
                 || effective == typeof(ushort)
                 || effective == typeof(int)
                 || effective == typeof(uint)
                 || effective == typeof(long)) fieldType = FieldType.Integer;
        else if (effective == typeof(ulong))
        {
            // ulong above long.MaxValue has no faithful JSON-number representation that every
            // implementation will read back identically, so it travels as a string. FFXIV
            // ContentIds live in exactly this range, which is precisely why it matters.
            fieldType = FieldType.String;
        }
        else if (effective == typeof(float)
                 || effective == typeof(double)
                 || effective == typeof(decimal)) fieldType = FieldType.Number;
        else if (effective == typeof(DateTimeOffset)) fieldType = FieldType.Timestamp;
        else if (effective == typeof(DateTime))
        {
            // Deliberately unsupported: DateTime carries a Kind that survives neither JSON nor
            // a round trip through most databases, so "the same instant" stops being the same
            // instant somewhere in the middle. Authors use DateTimeOffset instead.
            return false;
        }
        else if (effective == typeof(Guid)) fieldType = FieldType.Guid;
        else return false;

        return true;
    }

    /// <summary>Human-readable list of supported types, for error messages.</summary>
    public static string SupportedTypes =>
        "string, bool, byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, "
        + "DateTimeOffset, Guid, any enum, and the nullable form of each";

    /// <summary>True when the numeric bounds Min/Max apply to this type.</summary>
    public static bool IsNumeric(FieldType type) =>
        type is FieldType.Integer or FieldType.Number;
}
