namespace NexusKit.Core.Localization;

public interface ILocalizer
{
    bool TryGet(string key, out string text);
}
