using System.Linq.Expressions;
using System.Reflection;
using NexusKit.Core.Localization;

namespace NexusKit.Persistence.Settings.Schema;

public sealed class SettingsSchemaBuilder<T> where T : class, new()
{
    private string mStoreKey = typeof(T).FullName ?? typeof(T).Name;
    private LocalizedText mGroup = LocalizedText.FromLiteral("General");
    private int mGroupOrder;
    private LocalizedText mTitle = LocalizedText.Empty;
    private readonly List<SettingsPropertyDescriptor> mProperties = new();

    public SettingsSchemaBuilder<T> StoredAs(string key)
    {
        mStoreKey = key;
        return this;
    }

    public SettingsSchemaBuilder<T> Group(string name, int order = 0)
    {
        mGroup = LocalizedText.FromLiteral(name);
        mGroupOrder = order;
        return this;
    }

    public SettingsSchemaBuilder<T> GroupKey(string key, int order = 0)
    {
        mGroup = LocalizedText.FromKey(key);
        mGroupOrder = order;
        return this;
    }

    /// <summary>
    /// Sets a per-schema title used as a section header when multiple schemas share
    /// the same group (e.g. several modules under a "Modules" group). When unset and
    /// the schema is alone in its group, no separator is rendered.
    /// </summary>
    public SettingsSchemaBuilder<T> Title(string text)
    {
        mTitle = LocalizedText.FromLiteral(text);
        return this;
    }

    public SettingsSchemaBuilder<T> TitleKey(string key)
    {
        mTitle = LocalizedText.FromKey(key);
        return this;
    }

    public SettingsSchemaBuilder<T> Property<TValue>(
        Expression<Func<T, TValue>> selector,
        Action<SettingsPropertyBuilder<T, TValue>> configure)
    {
        var propInfo = ExtractProperty(selector);
        var propBuilder = new SettingsPropertyBuilder<T, TValue>(propInfo);
        configure(propBuilder);
        mProperties.Add(propBuilder.Build());
        return this;
    }

    internal IRegisteredSettingsSchema Build()
    {
        return new RegisteredSettingsSchema<T>(mStoreKey, mGroup, mGroupOrder, mTitle, mProperties);
    }

    private static PropertyInfo ExtractProperty<TValue>(Expression<Func<T, TValue>> selector)
    {
        if (selector.Body is MemberExpression member && member.Member is PropertyInfo pi)
            return pi;

        if (selector.Body is UnaryExpression unary &&
            unary.Operand is MemberExpression unaryMember &&
            unaryMember.Member is PropertyInfo upi)
            return upi;

        throw new ArgumentException($"Expression must be a property access: {selector}", nameof(selector));
    }
}
