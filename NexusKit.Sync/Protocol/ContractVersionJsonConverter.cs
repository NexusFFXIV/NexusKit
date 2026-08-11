using System.Text.Json;
using System.Text.Json.Serialization;
using NexusKit.Sync.Contracts;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// Carries a <see cref="ContractVersion"/> as the string <c>"1.0"</c> rather than as an object
/// with two numbers.
/// <para>The string form is what appears in the contract document, in URLs and in log lines,
/// so using it on the wire too means there is exactly one spelling of a version anywhere in
/// the system.</para>
/// </summary>
public sealed class ContractVersionJsonConverter : JsonConverter<ContractVersion>
{
    /// <inheritdoc />
    public override ContractVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a contract version string such as \"1.0\", got {reader.TokenType}.");

        var text = reader.GetString();

        return ContractVersion.TryParse(text, out var version)
            ? version
            : throw new JsonException($"'{text}' is not a contract version. Expected 'major.minor', e.g. '1.0'.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ContractVersion value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
