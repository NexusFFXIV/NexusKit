using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusKit.Sync.Protocol;

/// <summary>
/// The serialiser settings for protocol envelopes, shared by client and server.
/// <para>Shared rather than configured on each side, because two independently-configured
/// serialisers agree right up until someone changes one of them, and the symptom of that is a
/// field silently arriving as <c>null</c>.</para>
/// <para><b>This governs the envelope, not the records inside it.</b> Record payloads travel
/// as <see cref="JsonElement"/> and are never re-serialised here — their field naming is the
/// contract's business, and the plugin-side integration matches it by using the same
/// <see cref="JsonNamingPolicy.SnakeCaseLower"/> the contract builder used to derive the
/// names.</para>
/// </summary>
public static class SyncJson
{
    /// <summary>Options for reading and writing protocol envelopes.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        // Web defaults: camelCase properties, case-insensitive reads. Case-insensitive matters
        // for interoperability — a third-party implementation writing PascalCase should be
        // understood rather than rejected over capitalisation.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            // Optional fields are simply absent rather than present-and-null. Keeps envelopes
            // small and makes "not set" unambiguous.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        options.Converters.Add(new ContractVersionJsonConverter());

        // Enums as names, not ordinals. An ordinal silently changes meaning the day someone
        // inserts a member in the middle of the enum.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        // Frozen so a caller cannot mutate the shared instance and change how every other
        // caller serialises. populateMissingResolver installs the reflection-based resolver;
        // the plain overload refuses to freeze options that have none.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
