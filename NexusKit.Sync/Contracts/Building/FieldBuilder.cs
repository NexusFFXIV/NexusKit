namespace NexusKit.Sync.Contracts.Building;

/// <summary>
/// Refines one inferred field. Everything here is optional — a field the author never touches
/// keeps whatever <see cref="SyncContractBuilder"/> inferred from the CLR property.
/// </summary>
public sealed class FieldBuilder
{
    private readonly string mName;
    private FieldType mType;
    private bool mRequired;
    private decimal? mMin;
    private decimal? mMax;
    private int? mMaxLength;

    internal FieldBuilder(string name, FieldType type, bool required)
    {
        mName = name;
        mType = type;
        mRequired = required;
    }

    /// <summary>Overrides the inferred contract type.</summary>
    public FieldBuilder As(FieldType type)
    {
        mType = type;
        return this;
    }

    /// <summary>Marks the field required — a payload omitting it is rejected.</summary>
    public FieldBuilder Required(bool required = true)
    {
        mRequired = required;
        return this;
    }

    /// <summary>Marks the field optional.</summary>
    public FieldBuilder Optional() => Required(false);

    /// <summary>Sets an inclusive numeric range.</summary>
    public FieldBuilder Range(decimal min, decimal max)
    {
        mMin = min;
        mMax = max;
        return this;
    }

    /// <summary>Sets an inclusive lower bound.</summary>
    public FieldBuilder Min(decimal min)
    {
        mMin = min;
        return this;
    }

    /// <summary>Sets an inclusive upper bound.</summary>
    public FieldBuilder Max(decimal max)
    {
        mMax = max;
        return this;
    }

    /// <summary>Caps string length in UTF-16 code units.</summary>
    public FieldBuilder MaxLength(int maxLength)
    {
        mMaxLength = maxLength;
        return this;
    }

    internal FieldDefinition Build() => new()
    {
        Name = mName,
        Type = mType,
        Required = mRequired,
        Min = mMin,
        Max = mMax,
        MaxLength = mMaxLength,
    };

    internal void ForceRequired() => mRequired = true;

    internal string Name => mName;
}
