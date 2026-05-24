using Microsoft.Extensions.DependencyInjection;
using NexusKit.Core.Localization;
using NexusKit.Core.Resources;

namespace NexusKit.Core;

/// <summary>
/// Public DI entry point for NexusKit.Core. Registers framework-level
/// localization keys (the <c>nexuskit.time.*</c> family used by
/// <see cref="LocalizerExtensions.FormatRelativeTime"/> /
/// <see cref="LocalizerExtensions.FormatRelativeTimeAgo"/> /
/// <see cref="LocalizerExtensions.FormatTimeSpan"/>) so callers don't have
/// to know about Core's internal <c>Strings</c> resource class. Plugins
/// typically reach Core transitively through <c>AddNexusKitUi</c>, which
/// calls this method.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddNexusKitCore(this IServiceCollection services)
    {
        services.AddResourceLocalizer<Strings>();
        return services;
    }
}
