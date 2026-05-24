using Microsoft.Extensions.DependencyInjection;
using NexusKit.ChatNotifications.Internal;
using NexusKit.ChatNotifications.Resources;
using NexusKit.Core.Localization;
using NexusKit.Ui.AutoSettings;

namespace NexusKit.ChatNotifications;

public static class ChatNotificationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the chat-notification framework: the <see cref="IChatNotificationRegistry"/>
    /// singleton, the Notifications settings tab (<c>IAutoSettingsSection</c>),
    /// and the module's own resource localizer (channel names, color names,
    /// settings labels — all <c>nexuskit.chatnotifications.*</c> keys).
    /// Producers register themselves separately as <c>INotificationProducer</c>
    /// in the host plugin's composition root.
    /// </summary>
    public static IServiceCollection AddNexusKitChatNotifications(this IServiceCollection services)
    {
        services.AddSingleton<ChatNotificationRegistry>();
        services.AddSingleton<IChatNotificationRegistry>(sp => sp.GetRequiredService<ChatNotificationRegistry>());
        services.AddSingleton<IAutoSettingsSection, ChatNotificationsSettingsSection>();
        services.AddResourceLocalizer<Strings>();
        return services;
    }
}
