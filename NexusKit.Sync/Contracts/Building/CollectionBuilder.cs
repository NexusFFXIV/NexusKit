using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace NexusKit.Sync.Contracts.Building;

/// <summary>
/// Builds one collection from a POCO, inferring fields by reflection and letting the author
/// refine what the inference could not know.
/// <para><b>Field names are snake_case</b>, produced by
/// <see cref="JsonNamingPolicy.SnakeCaseLower"/> — <c>VenueId</c> becomes <c>venue_id</c>.
/// The payload serialiser has to apply the same policy, and it does so by using the very same
/// <see cref="JsonNamingPolicy"/> instance rather than a reimplementation, because two
/// independent snake_case implementations agreeing on <c>IOPort</c> is not something to
/// leave to chance.</para>
/// </summary>
/// <typeparam name="T">The record or class whose public properties become fields.</typeparam>
public sealed class CollectionBuilder<T>
{
    private readonly string mName;
    private readonly SyncDirection mDirection;
    private readonly List<FieldBuilder> mFields = [];
    private readonly List<string> mIndexed = [];
    private string? mKey;
    private RateLimitPolicy? mRateLimit;
    private TimeSpan? mRetention;
    private bool mLive;

    internal CollectionBuilder(string name, SyncDirection direction)
    {
        mName = name;
        mDirection = direction;
        InferFields();
    }

    /// <summary>
    /// Names the field that identifies a record. The chosen field is forced to required —
    /// a key that may be absent cannot address anything, and making the author discover that
    /// through a validation error would be pedantry rather than help.
    /// </summary>
    public CollectionBuilder<T> Key<TValue>(Expression<Func<T, TValue>> selector)
    {
        var field = Find(selector);
        field.ForceRequired();
        mKey = field.Name;
        return this;
    }

    /// <summary>Refines an inferred field.</summary>
    public CollectionBuilder<T> Field<TValue>(Expression<Func<T, TValue>> selector, Action<FieldBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(Find(selector));
        return this;
    }

    /// <summary>Drops an inferred field from the contract.</summary>
    public CollectionBuilder<T> Ignore<TValue>(Expression<Func<T, TValue>> selector)
    {
        var field = Find(selector);
        mFields.Remove(field);
        mIndexed.Remove(field.Name);
        if (string.Equals(mKey, field.Name, StringComparison.Ordinal)) mKey = null;
        return this;
    }

    /// <summary>Asks the server to index this field for querying.</summary>
    public CollectionBuilder<T> Indexed<TValue>(Expression<Func<T, TValue>> selector)
    {
        var field = Find(selector);
        if (!mIndexed.Contains(field.Name)) mIndexed.Add(field.Name);
        return this;
    }

    /// <summary>Sets the per-key write budget for this collection.</summary>
    public CollectionBuilder<T> RateLimit(int perMinute)
    {
        mRateLimit = new RateLimitPolicy(perMinute);
        return this;
    }

    /// <summary>Sets how long records live before the server prunes them.</summary>
    public CollectionBuilder<T> Retention(TimeSpan retention)
    {
        mRetention = retention;
        return this;
    }

    /// <summary>Marks the collection as a candidate for the live push channel.</summary>
    public CollectionBuilder<T> Live(bool live = true)
    {
        mLive = live;
        return this;
    }

    internal CollectionDefinition Build()
    {
        var key = mKey ?? InferKey();

        return new CollectionDefinition
        {
            Name = mName,
            Direction = mDirection,
            Key = key,
            Fields = mFields.Select(f => f.Build()).ToArray(),
            Indexed = mIndexed.ToArray(),
            RateLimit = mRateLimit,
            Retention = mRetention,
            Live = mLive,
        };
    }

    private string InferKey()
    {
        // A property literally called Id is the overwhelmingly common case, so accepting it
        // without ceremony is worth the small magic. Anything else has to be explicit —
        // guessing at "the first string field" would silently pick a key that changes.
        foreach (var field in mFields)
        {
            if (string.Equals(field.Name, "id", StringComparison.Ordinal))
            {
                field.ForceRequired();
                return field.Name;
            }
        }

        throw new ContractDefinitionException(
            $"Collection '{mName}' has no key. {typeof(T).Name} declares no 'Id' property to infer one from, "
            + "so name it explicitly with .Key(x => x.Something).");
    }

    private void InferFields()
    {
        var nullability = new NullabilityInfoContext();

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetMethod is null || !property.GetMethod.IsPublic) continue;
            if (property.GetIndexParameters().Length > 0) continue;
            if (property.IsDefined(typeof(SyncIgnoreAttribute), inherit: false)) continue;

            if (!FieldTypeMap.TryMap(property.PropertyType, out var fieldType, out var nullableValueType))
            {
                throw new ContractDefinitionException(
                    $"Property {typeof(T).Name}.{property.Name} is of type {property.PropertyType.Name}, which a "
                    + $"contract field cannot express. Supported: {FieldTypeMap.SupportedTypes}. "
                    + "Mark it [SyncIgnore] if it is not meant to travel.");
            }

            mFields.Add(new FieldBuilder(
                JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name),
                fieldType,
                required: IsRequired(property, nullability, nullableValueType)));
        }

        if (mFields.Count == 0)
        {
            throw new ContractDefinitionException(
                $"Collection '{mName}' would have no fields: {typeof(T).Name} exposes no public readable "
                + "properties that a contract can carry.");
        }
    }

    private static bool IsRequired(PropertyInfo property, NullabilityInfoContext nullability, bool nullableValueType)
    {
        if (nullableValueType) return false;
        if (property.PropertyType.IsValueType) return true;

        // Reference types: trust the author's nullable annotations. A `string` is required,
        // a `string?` is not — which means turning on NRTs and meaning it is what produces a
        // sensible contract, rather than a separate set of attributes to keep in sync.
        return nullability.Create(property).WriteState != NullabilityState.Nullable;
    }

    private FieldBuilder Find<TValue>(Expression<Func<T, TValue>> selector)
    {
        var property = ExtractProperty(selector);
        var name = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);

        foreach (var field in mFields)
        {
            if (string.Equals(field.Name, name, StringComparison.Ordinal)) return field;
        }

        throw new ContractDefinitionException(
            $"Collection '{mName}' has no field for {typeof(T).Name}.{property.Name}. "
            + "It was either ignored earlier in this builder or carries [SyncIgnore].");
    }

    private static PropertyInfo ExtractProperty<TValue>(Expression<Func<T, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        // Unwrap the boxing conversion the compiler inserts for value-typed selectors
        // (x => x.Rating typed as Expression<Func<T, object>>).
        var body = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert
            ? convert.Operand
            : selector.Body;

        if (body is MemberExpression { Member: PropertyInfo property }) return property;

        throw new ArgumentException(
            $"Expected a simple property selector such as x => x.VenueId, but got '{selector}'.",
            nameof(selector));
    }
}
