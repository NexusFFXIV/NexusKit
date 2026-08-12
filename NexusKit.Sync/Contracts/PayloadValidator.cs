using System.Globalization;
using System.Text.Json;

namespace NexusKit.Sync.Contracts;

/// <summary>
/// Checks one record against the collection that is supposed to describe it.
/// <para>This runs <b>server-side on every write</b>. A client may run it too for a faster,
/// friendlier error, but it is never the authority: the contract exists precisely so that a
/// forged, buggy or outdated client cannot put something in the database that the contract
/// forbids. Any change here that makes validation more permissive is a security change, not
/// a convenience change.</para>
/// </summary>
public static class PayloadValidator
{
    /// <summary>Validates a record against a collection definition.</summary>
    /// <param name="collection">The collection the record claims to belong to.</param>
    /// <param name="payload">The record, as a JSON object.</param>
    public static ValidationResult Validate(CollectionDefinition collection, JsonElement payload)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (payload.ValueKind != JsonValueKind.Object)
            return ValidationResult.From([new ValidationProblem(null, "A record must be a JSON object.")]);

        var problems = new List<ValidationProblem>();

        // Unknown properties are rejected rather than dropped. Silently discarding data a
        // caller believed it had stored is the kind of bug that surfaces weeks later as
        // "the server lost my field", and version negotiation already exists to keep peers
        // from sending fields the other side has not heard of.
        foreach (var property in payload.EnumerateObject())
        {
            if (collection.FindField(property.Name) is null)
            {
                problems.Add(new ValidationProblem(
                    property.Name,
                    $"Collection '{collection.Name}' declares no such field."));
            }
        }

        foreach (var field in collection.Fields)
        {
            if (!payload.TryGetProperty(field.Name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                if (field.Required)
                    problems.Add(new ValidationProblem(field.Name, "Required field is missing or null."));

                continue;
            }

            ValidateValue(field, value, problems);
        }

        return ValidationResult.From(problems);
    }

    /// <summary>
    /// Validates a single value against a single field declaration.
    /// <para>Exists for callers holding one field and one value rather than a whole record — the
    /// caller that asks "would this stored value survive a narrowing conversion?". Those cannot use
    /// <see cref="Validate"/>, because it rejects properties the collection does not declare, so a
    /// stand-in collection holding only the field under test would report every <i>other</i>
    /// property of the record as an error.</para>
    /// <para>This adds no rule and relaxes none: it is the same per-type check
    /// <see cref="Validate"/> performs, reached directly. A JSON <c>null</c> is a problem here
    /// rather than an absence, because a caller passing one value has already decided the value is
    /// present — <see cref="Validate"/> keeps owning the missing-versus-null distinction, which
    /// only a whole record can answer.</para>
    /// </summary>
    /// <param name="field">The declaration the value must satisfy.</param>
    /// <param name="value">The value, as it appears in the payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> is null.</exception>
    public static ValidationResult ValidateField(FieldDefinition field, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(field);

        var problems = new List<ValidationProblem>();
        ValidateValue(field, value, problems);

        return ValidationResult.From(problems);
    }

    private static void ValidateValue(FieldDefinition field, JsonElement value, List<ValidationProblem> problems)
    {
        switch (field.Type)
        {
            case FieldType.String:
                ValidateString(field, value, problems);
                break;

            case FieldType.Integer:
                ValidateInteger(field, value, problems);
                break;

            case FieldType.Number:
                ValidateNumber(field, value, problems);
                break;

            case FieldType.Boolean:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    problems.Add(Expected(field, "a boolean", value));
                break;

            case FieldType.Timestamp:
                ValidateTimestamp(field, value, problems);
                break;

            case FieldType.Guid:
                if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out _))
                    problems.Add(Expected(field, "a GUID string", value));
                break;

            default:
                problems.Add(new ValidationProblem(field.Name, $"Unhandled field type {field.Type}."));
                break;
        }
    }

    private static void ValidateString(FieldDefinition field, JsonElement value, List<ValidationProblem> problems)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            problems.Add(Expected(field, "a string", value));
            return;
        }

        if (field.MaxLength is not { } maxLength) return;

        var text = value.GetString()!;
        if (text.Length > maxLength)
        {
            problems.Add(new ValidationProblem(
                field.Name,
                $"Value is {text.Length} characters; the field allows at most {maxLength}."));
        }
    }

    private static void ValidateInteger(FieldDefinition field, JsonElement value, List<ValidationProblem> problems)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
        {
            // TryGetInt64 rejects both non-numbers and numbers with a fractional part, which
            // is the behaviour we want: 3.5 is not an integer, and neither is 1e400.
            problems.Add(Expected(field, "an integer", value));
            return;
        }

        CheckRange(field, number, problems);
    }

    private static void ValidateNumber(FieldDefinition field, JsonElement value, List<ValidationProblem> problems)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
        {
            problems.Add(Expected(field, "a number", value));
            return;
        }

        CheckRange(field, number, problems);
    }

    private static void ValidateTimestamp(FieldDefinition field, JsonElement value, List<ValidationProblem> problems)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            problems.Add(Expected(field, "an ISO-8601 timestamp string", value));
            return;
        }

        var text = value.GetString()!;

        if (!HasExplicitOffset(text))
        {
            // Refusing to guess is the whole point. A client in Berlin and a client in Tokyo
            // both sending "2026-08-04T12:00:00" mean instants nine hours apart, and whichever
            // default the server picks is wrong for one of them.
            problems.Add(new ValidationProblem(
                field.Name,
                "Timestamp must carry an explicit UTC offset (a trailing 'Z' or ±hh:mm)."));
            return;
        }

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
            problems.Add(Expected(field, "an ISO-8601 timestamp string", value));
    }

    private static void CheckRange(FieldDefinition field, decimal number, List<ValidationProblem> problems)
    {
        if (field.Min is { } min && number < min)
        {
            problems.Add(new ValidationProblem(
                field.Name,
                $"Value {Format(number)} is below the minimum {Format(min)}."));
        }

        if (field.Max is { } max && number > max)
        {
            problems.Add(new ValidationProblem(
                field.Name,
                $"Value {Format(number)} is above the maximum {Format(max)}."));
        }
    }

    private static bool HasExplicitOffset(string text)
    {
        if (text.Length == 0) return false;
        if (text[^1] is 'Z' or 'z') return true;

        // Look for ±hh:mm at the very end. Scanning from the end avoids mistaking the date's
        // own hyphens (2026-08-04) for an offset sign.
        if (text.Length < 6) return false;

        var candidate = text.AsSpan(text.Length - 6);
        return candidate[0] is '+' or '-'
               && char.IsAsciiDigit(candidate[1])
               && char.IsAsciiDigit(candidate[2])
               && candidate[3] == ':'
               && char.IsAsciiDigit(candidate[4])
               && char.IsAsciiDigit(candidate[5]);
    }

    private static ValidationProblem Expected(FieldDefinition field, string expected, JsonElement actual) =>
        new(field.Name, $"Expected {expected}, got {Describe(actual)}.");

    private static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Array => "an array",
        JsonValueKind.Object => "an object",
        JsonValueKind.Null => "null",
        _ => "an unsupported value",
    };

    private static string Format(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
