using Microsoft.Extensions.Logging;
using NexusKit.Core.Logging;

namespace NexusKit.Hosting.Logging;

internal sealed class PluginLoggerProvider : ILoggerProvider
{
    private readonly IPluginLogSink mSink;

    public PluginLoggerProvider(IPluginLogSink sink)
    {
        mSink = sink;
    }

    public ILogger CreateLogger(string categoryName) => new PluginLogger(mSink, categoryName);

    public void Dispose() { }

    private sealed class PluginLogger : ILogger
    {
        private readonly IPluginLogSink mSink;
        private readonly string mCategory;

        public PluginLogger(IPluginLogSink sink, string category)
        {
            mSink = sink;
            mCategory = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None) return false;
#if !DEBUG
            // Release-Builds: Trace/Debug/Information aus /xllog raushalten.
            if (logLevel < LogLevel.Warning) return false;
#endif
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var line = $"[{mCategory}] {message}";

            switch (logLevel)
            {
                case LogLevel.Trace:       mSink.Verbose(line, exception); break;
                case LogLevel.Debug:       mSink.Debug(line, exception); break;
                case LogLevel.Information: mSink.Information(line, exception); break;
                case LogLevel.Warning:     mSink.Warning(line, exception); break;
                case LogLevel.Error:       mSink.Error(line, exception); break;
                case LogLevel.Critical:    mSink.Fatal(line, exception); break;
            }
        }
    }
}
