using System.Reflection;
using System.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace NexusKit.Core.Localization;

public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Add a localization source to the layered chain. Later-added sources take
    /// precedence over earlier ones, so plugin sources override framework defaults.
    /// </summary>
    public static IServiceCollection AddLocalizer<T>(this IServiceCollection services)
        where T : class, ILocalizationSource
    {
        services.AddSingleton<ILocalizationSource, T>();
        return services;
    }

    public static IServiceCollection AddLocalizer(this IServiceCollection services, ILocalizationSource source)
    {
        services.AddSingleton(source);
        return services;
    }

    /// <summary>
    /// Wrap a <see cref="ResourceManager"/> as an <see cref="ILocalizationSource"/>
    /// and register it. Typical use: pass a designer-generated resource class's
    /// <c>ResourceManager</c> property.
    /// </summary>
    public static IServiceCollection AddResourceLocalizer(this IServiceCollection services, ResourceManager resources)
    {
        services.AddSingleton<ILocalizationSource>(new ResourceLocalizer(resources));
        return services;
    }

    /// <summary>
    /// Register a designer-generated resource class (e.g. <c>Language</c> from
    /// <c>Language.resx</c>) as a localization source. Auto-discovers its static
    /// <c>ResourceManager</c> property via reflection.
    /// </summary>
    public static IServiceCollection AddResourceLocalizer<TResources>(this IServiceCollection services)
    {
        var type = typeof(TResources);
        var prop = type.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException(
                $"Type '{type.FullName}' has no static 'ResourceManager' property. " +
                $"Make sure it is a designer-generated .resx class.");

        if (prop.GetValue(null) is not ResourceManager rm)
            throw new InvalidOperationException(
                $"'{type.FullName}.ResourceManager' returned null or a non-ResourceManager value.");

        return services.AddResourceLocalizer(rm);
    }
}
