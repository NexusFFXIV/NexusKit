namespace NexusKit.Core.Logging;

public interface IPluginLogSink
{
    void Verbose(string message, Exception? exception = null);
    void Debug(string message, Exception? exception = null);
    void Information(string message, Exception? exception = null);
    void Warning(string message, Exception? exception = null);
    void Error(string message, Exception? exception = null);
    void Fatal(string message, Exception? exception = null);
}
