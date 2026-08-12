using System.Globalization;
using System.Text.Json;

namespace NexusKit.Sync.Contracts;

/// <summary>
/// Whether one stored value survives a change of field declaration.
/// <para>The companion to <see cref="FieldTypeConversion"/>, one level down: that says which
/// conversions exist, this says whether <i>this</i> value makes it through one. Together they
/// answer the question a narrowing raises — <c>integer → boolean</c> is fine exactly when the
/// column holds nothing but 0 and 1, and only the rows can say.</para>
/// <para><b>Two steps, and the split is the point.</b> First the value is re-expressed in the
/// target's JSON shape; then <see cref="PayloadValidator"/> judges the result, unchanged. Bounds,
/// lengths and the timestamp-offset rule therefore live in exactly one place and cannot drift from
/// what the write path enforces. Only the re-expression is new, and it is pure representation: no
/// rule about which values are acceptable is restated here.</para>
/// <para>Asking the validator directly instead would answer a different question — whether the
/// stored value is <i>already</i> valid under the new declaration. For a narrowing it never is,
/// because the JSON shape is exactly what changed: a stored <c>0</c> is a number, a boolean field
/// wants <c>false</c>, and every row would be reported as blocking including the ones that convert
/// cleanly.</para>
/// </summary>
public static class FieldValueConversion
{
    /// <summary>
    /// Whether <paramref name="value"/>, declared as <paramref name="from"/>, can be stored under
    /// <paramref name="to"/>.
    /// </summary>
    /// <param name="from">The declaration the value was written under.</param>
    /// <param name="to">The declaration it must satisfy.</param>
    /// <param name="value">The stored value. JSON <c>null</c> and absence are the caller's business.</param>
    /// <returns>A clean result when it converts, otherwise the reason it does not.</returns>
    /// <exception cref="ArgumentNullException">Either declaration is null.</exception>
    public static ValidationResult Check(FieldDefinition from, FieldDefinition to, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (!TryRepresent(from.Type, to.Type, value, out var converted))
        {
            return ValidationResult.From(
            [
                new ValidationProblem(
                    to.Name,
                    $"Stored {Describe(value)} cannot be read as {to.Type.ToString().ToLowerInvariant()}."),
            ]);
        }

        return PayloadValidator.ValidateField(to, converted);
    }

    /// <summary>
    /// Re-expresses a value in the target type's JSON shape. False when no value of the source
    /// could take that shape, or when this particular one cannot.
    /// <para>Representation only. Whether the result is <i>acceptable</i> — in range, short enough,
    /// carrying an offset — is not decided here.</para>
    /// </summary>
    private static bool TryRepresent(FieldType from, FieldType to, JsonElement value, out JsonElement converted)
    {
        converted = value;

        // Already the right shape, so the validator can have it untouched:
        //   same type              — nothing changed
        //   integer → number       — both are JSON numbers
        //   number  → integer      — both are JSON numbers; ValidateInteger rejects a fraction
        //   string  → guid         — a guid travels as a JSON string; TryGetGuid decides
        //   string  → timestamp    — likewise, and the offset rule stays with the validator
        if (from == to) return true;

        switch (from, to)
        {
            case (FieldType.Integer, FieldType.Number):
            case (FieldType.Number, FieldType.Integer):
            case (FieldType.String, FieldType.Guid):
            case (FieldType.String, FieldType.Timestamp):
                return true;

            // Anything to text. The rendering has to match what the writer would send, because
            // MaxLength is measured on it.
            case (_, FieldType.String):
                return TryAsString(from, value, out converted);

            case (FieldType.Integer, FieldType.Boolean):
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var flag)) return false;
                if (flag is not (0 or 1)) return false;

                converted = JsonSerializer.SerializeToElement(flag == 1);
                return true;

            case (FieldType.String, FieldType.Integer):
                if (value.ValueKind != JsonValueKind.String) return false;
                if (!long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
                    return false;

                converted = JsonSerializer.SerializeToElement(whole);
                return true;

            case (FieldType.String, FieldType.Number):
                if (value.ValueKind != JsonValueKind.String) return false;
                if (!decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var some))
                    return false;

                converted = JsonSerializer.SerializeToElement(some);
                return true;

            case (FieldType.String, FieldType.Boolean):
                if (value.ValueKind != JsonValueKind.String) return false;

                // Exactly the two spellings JSON uses. Accepting "True" or "yes" here would be a
                // rule about acceptable values, invented in the one place that must not invent any.
                var text = value.GetString();
                if (text is not ("true" or "false")) return false;

                converted = JsonSerializer.SerializeToElement(text == "true");
                return true;

            default:
                return false;
        }
    }

    /// <summary>Renders a value as the JSON string a writer would send for it.</summary>
    private static bool TryAsString(FieldType from, JsonElement value, out JsonElement converted)
    {
        converted = value;

        string text;

        switch (from)
        {
            case FieldType.Integer when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var whole):
                text = whole.ToString(CultureInfo.InvariantCulture);
                break;

            case FieldType.Number when value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var some):
                // G29 to match how the canonical form renders decimals, so a length check here
                // measures the same text the wire would carry.
                text = some.ToString("G29", CultureInfo.InvariantCulture);
                break;

            case FieldType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                text = value.ValueKind == JsonValueKind.True ? "true" : "false";
                break;

            // Both already travel as JSON strings, so their text is whatever was stored.
            case FieldType.Guid when value.ValueKind == JsonValueKind.String:
            case FieldType.Timestamp when value.ValueKind == JsonValueKind.String:
                return true;

            default:
                return false;
        }

        converted = JsonSerializer.SerializeToElement(text);
        return true;
    }

    /// <summary>Names what is actually there, for a message somebody has to act on.</summary>
    private static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => $"'{value.GetString()}'",
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Array => "an array",
        JsonValueKind.Object => "an object",
        _ => "null",
    };
}
