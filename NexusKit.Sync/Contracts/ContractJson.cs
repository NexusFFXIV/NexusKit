using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NexusKit.Sync.Contracts;

/// <summary>
/// The canonical wire form of a contract, and the hash computed over it.
/// <para><b>Why canonical matters.</b> Client and server each hold their own copy of the
/// contract and each compute a hash. If the two hashes disagree for any reason other than a
/// genuine difference in content, peers report a mismatch over a formatting accident. So the
/// serialisation removes every degree of freedom:</para>
/// <list type="bullet">
///   <item><description>no whitespace, no indentation</description></item>
///   <item><description>a fixed property order, written explicitly rather than by reflection</description></item>
///   <item><description>collections ordered by name, field keys and index lists ordered ordinally —
///     these are sets, so declaration order carries no meaning and must not carry a hash</description></item>
///   <item><description>defaults omitted, so "absent" and "explicitly the default" cannot differ</description></item>
///   <item><description>decimals normalised, so <c>1</c> and <c>1.0</c> are the same number rather than
///     two spellings of it</description></item>
///   <item><description>invariant culture throughout — a German build must not emit <c>1,5</c></description></item>
/// </list>
/// <para>The result is reproducible across processes, machines and operating systems, which
/// the CI matrix builds on both Linux and Windows specifically to prove.</para>
/// </summary>
public static class ContractJson
{
    // Root
    private const string ContractIdKey = "contractId";
    private const string VersionKey = "version";
    private const string CollectionsKey = "collections";

    // Collection
    private const string NameKey = "name";
    private const string DirectionKey = "direction";
    private const string KeyKey = "key";
    private const string FieldsKey = "fields";
    private const string IndexedKey = "indexed";
    private const string LiveKey = "live";
    private const string RateLimitKey = "rateLimit";
    private const string RetentionKey = "retention";

    // Field
    private const string TypeKey = "type";
    private const string RequiredKey = "required";
    private const string MinKey = "min";
    private const string MaxKey = "max";
    private const string MaxLengthKey = "maxLength";

    // Rate limit
    private const string PerMinuteKey = "perMinute";

    /// <summary>Renders the contract in its canonical form.</summary>
    public static string Write(SyncContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
               {
                   Indented = false,
                   // Contract documents carry identifiers and numbers, never user text, so the
                   // strict default encoder never has anything to escape here. Keeping it strict
                   // rather than relaxing it means the byte sequence stays predictable if a future
                   // field ever does admit wider characters.
                   SkipValidation = false,
               }))
        {
            writer.WriteStartObject();
            writer.WriteString(ContractIdKey, contract.ContractId);
            writer.WriteString(VersionKey, contract.Version.ToString());

            writer.WriteStartArray(CollectionsKey);
            foreach (var collection in contract.Collections) WriteCollection(writer, collection);
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Lowercase hex SHA-256 over the UTF-8 bytes of a canonical document.</summary>
    public static string ComputeHash(string canonicalJson)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
    }

    /// <summary>
    /// Parses a contract document. Property order in the input does not matter — only the
    /// output of <see cref="Write"/> is canonical.
    /// <para><b>Unknown properties are rejected.</b> Tolerating them would mean an older
    /// server silently ignoring a constraint a newer author declared, and enforcing less than
    /// the contract says is exactly the direction a validation layer must never fail in.</para>
    /// </summary>
    /// <exception cref="ContractDefinitionException">The document is malformed or invalid.</exception>
    public static SyncContract Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ContractDefinitionException($"The contract document is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ContractDefinitionException("The contract document must be a JSON object.");

            RejectUnknown(root, "the contract document", ContractIdKey, VersionKey, CollectionsKey);

            var contractId = RequireString(root, ContractIdKey, "the contract document");
            var versionText = RequireString(root, VersionKey, "the contract document");

            if (!ContractVersion.TryParse(versionText, out var version))
                throw new ContractDefinitionException($"'{versionText}' is not a contract version. Expected 'major.minor', e.g. '1.0'.");

            if (!root.TryGetProperty(CollectionsKey, out var collectionsElement)
                || collectionsElement.ValueKind != JsonValueKind.Array)
            {
                throw new ContractDefinitionException("The contract document must carry a 'collections' array.");
            }

            var collections = new List<CollectionDefinition>();
            foreach (var element in collectionsElement.EnumerateArray()) collections.Add(ReadCollection(element));

            // Create runs the structural rules and throws with every problem at once.
            return SyncContract.Create(contractId, version, collections);
        }
    }

    private static void WriteCollection(Utf8JsonWriter writer, CollectionDefinition collection)
    {
        writer.WriteStartObject();
        writer.WriteString(NameKey, collection.Name);
        writer.WriteString(DirectionKey, DirectionText(collection.Direction));
        writer.WriteString(KeyKey, collection.Key);

        writer.WriteStartObject(FieldsKey);
        foreach (var field in collection.Fields.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            writer.WritePropertyName(field.Name);
            WriteField(writer, field);
        }

        writer.WriteEndObject();

        if (collection.Indexed.Count > 0)
        {
            writer.WriteStartArray(IndexedKey);
            foreach (var indexed in collection.Indexed.OrderBy(i => i, StringComparer.Ordinal))
                writer.WriteStringValue(indexed);
            writer.WriteEndArray();
        }

        if (collection.Live) writer.WriteBoolean(LiveKey, true);

        if (collection.RateLimit is { } rateLimit)
        {
            writer.WriteStartObject(RateLimitKey);
            writer.WriteNumber(PerMinuteKey, rateLimit.PerMinute);
            writer.WriteEndObject();
        }

        if (collection.Retention is { } retention)
            writer.WriteString(RetentionKey, DurationText.Format(retention));

        writer.WriteEndObject();
    }

    private static void WriteField(Utf8JsonWriter writer, FieldDefinition field)
    {
        writer.WriteStartObject();
        writer.WriteString(TypeKey, FieldTypeText(field.Type));

        if (field.Required) writer.WriteBoolean(RequiredKey, true);

        if (field.Min is { } min)
        {
            writer.WritePropertyName(MinKey);
            writer.WriteRawValue(NormalizeDecimal(min));
        }

        if (field.Max is { } max)
        {
            writer.WritePropertyName(MaxKey);
            writer.WriteRawValue(NormalizeDecimal(max));
        }

        if (field.MaxLength is { } maxLength) writer.WriteNumber(MaxLengthKey, maxLength);

        writer.WriteEndObject();
    }

    private static CollectionDefinition ReadCollection(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ContractDefinitionException("Every entry in 'collections' must be a JSON object.");

        RejectUnknown(element, "a collection", NameKey, DirectionKey, KeyKey, FieldsKey, IndexedKey, LiveKey, RateLimitKey, RetentionKey);

        var name = RequireString(element, NameKey, "a collection");
        var where = $"collection '{name}'";

        var direction = ParseDirection(RequireString(element, DirectionKey, where), where);
        var key = RequireString(element, KeyKey, where);

        if (!element.TryGetProperty(FieldsKey, out var fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Object)
            throw new ContractDefinitionException($"The {where} must carry a 'fields' object.");

        var fields = new List<FieldDefinition>();
        foreach (var property in fieldsElement.EnumerateObject())
            fields.Add(ReadField(property.Name, property.Value, where));

        var indexed = Array.Empty<string>();
        if (element.TryGetProperty(IndexedKey, out var indexedElement))
        {
            if (indexedElement.ValueKind != JsonValueKind.Array)
                throw new ContractDefinitionException($"'indexed' on {where} must be an array of field names.");

            indexed = indexedElement.EnumerateArray()
                .Select(i => i.GetString() ?? throw new ContractDefinitionException($"'indexed' on {where} contains a non-string entry."))
                .ToArray();
        }

        var live = element.TryGetProperty(LiveKey, out var liveElement) && liveElement.GetBoolean();

        RateLimitPolicy? rateLimit = null;
        if (element.TryGetProperty(RateLimitKey, out var rateLimitElement))
        {
            if (rateLimitElement.ValueKind != JsonValueKind.Object)
                throw new ContractDefinitionException($"'rateLimit' on {where} must be an object.");

            RejectUnknown(rateLimitElement, $"'rateLimit' on {where}", PerMinuteKey);

            if (!rateLimitElement.TryGetProperty(PerMinuteKey, out var perMinute) || !perMinute.TryGetInt32(out var value))
                throw new ContractDefinitionException($"'rateLimit' on {where} must carry an integer 'perMinute'.");

            rateLimit = new RateLimitPolicy(value);
        }

        TimeSpan? retention = null;
        if (element.TryGetProperty(RetentionKey, out var retentionElement))
        {
            var text = retentionElement.GetString();
            if (!DurationText.TryParse(text, out var parsed))
                throw new ContractDefinitionException($"'retention' on {where} is '{text}', which is not a duration like '180d'.");

            retention = parsed;
        }

        return new CollectionDefinition
        {
            Name = name,
            Direction = direction,
            Key = key,
            Fields = fields,
            Indexed = indexed,
            Live = live,
            RateLimit = rateLimit,
            Retention = retention,
        };
    }

    private static FieldDefinition ReadField(string name, JsonElement element, string collectionWhere)
    {
        var where = $"field '{name}' on {collectionWhere}";

        if (element.ValueKind != JsonValueKind.Object)
            throw new ContractDefinitionException($"The {where} must be a JSON object.");

        RejectUnknown(element, $"the {where}", TypeKey, RequiredKey, MinKey, MaxKey, MaxLengthKey);

        var type = ParseFieldType(RequireString(element, TypeKey, where), where);

        return new FieldDefinition
        {
            Name = name,
            Type = type,
            Required = element.TryGetProperty(RequiredKey, out var required) && required.GetBoolean(),
            Min = element.TryGetProperty(MinKey, out var min) ? min.GetDecimal() : null,
            Max = element.TryGetProperty(MaxKey, out var max) ? max.GetDecimal() : null,
            MaxLength = element.TryGetProperty(MaxLengthKey, out var maxLength) ? maxLength.GetInt32() : null,
        };
    }

    private static void RejectUnknown(JsonElement element, string where, params string[] known)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (Array.IndexOf(known, property.Name) < 0)
            {
                throw new ContractDefinitionException(
                    $"Unknown property '{property.Name}' on {where}. Known properties: {string.Join(", ", known)}. "
                    + "Unknown properties are rejected rather than ignored, so a constraint this build does not "
                    + "understand can never be silently dropped.");
            }
        }
    }

    private static string RequireString(JsonElement element, string property, string where)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            throw new ContractDefinitionException($"'{property}' is missing or not a string on {where}.");

        return value.GetString()!;
    }

    private static string DirectionText(SyncDirection direction) => direction switch
    {
        SyncDirection.Uplink => "uplink",
        SyncDirection.Downlink => "downlink",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unhandled sync direction."),
    };

    private static SyncDirection ParseDirection(string text, string where) => text switch
    {
        "uplink" => SyncDirection.Uplink,
        "downlink" => SyncDirection.Downlink,
        _ => throw new ContractDefinitionException($"'{text}' on {where} is not a direction. Expected 'uplink' or 'downlink'."),
    };

    private static string FieldTypeText(FieldType type) => type switch
    {
        FieldType.String => "string",
        FieldType.Integer => "integer",
        FieldType.Number => "number",
        FieldType.Boolean => "boolean",
        FieldType.Timestamp => "timestamp",
        FieldType.Guid => "guid",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unhandled field type."),
    };

    private static FieldType ParseFieldType(string text, string where) => text switch
    {
        "string" => FieldType.String,
        "integer" => FieldType.Integer,
        "number" => FieldType.Number,
        "boolean" => FieldType.Boolean,
        "timestamp" => FieldType.Timestamp,
        "guid" => FieldType.Guid,
        _ => throw new ContractDefinitionException(
            $"'{text}' on {where} is not a field type. Expected one of: string, integer, number, boolean, timestamp, guid."),
    };

    /// <summary>
    /// Renders a decimal without trailing zeros.
    /// <para><c>decimal</c> keeps its scale, so <c>1m</c> and <c>1.0m</c> compare equal but
    /// stringify as <c>"1"</c> and <c>"1.0"</c>. Writing either straight into the document
    /// would let two identical contracts hash differently depending on how the author happened
    /// to type a bound. G29 collapses both to the same digits.</para>
    /// </summary>
    private static string NormalizeDecimal(decimal value) =>
        value.ToString("G29", CultureInfo.InvariantCulture);
}
