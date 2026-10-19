using System;
using MelonLoader;

namespace RareParts.Logging
{
    public sealed class LoggerFactory : ILoggerFactory
    {
        private readonly LogLevel _minimumLevel;

        public LoggerFactory(LogLevel minimumLevel = LogLevel.Trace)
        {
            _minimumLevel = minimumLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new MelonLoggerAdapter(categoryName, _minimumLevel);
        }

        public ILogger<T> CreateLogger<T>()
        {
            return new MelonLoggerAdapter<T>(_minimumLevel);
        }
    }

    public sealed class MelonLoggerAdapter<T> : MelonLoggerAdapter, ILogger<T>
    {
        public MelonLoggerAdapter(LogLevel minimumLevel = LogLevel.Trace)
            : base(typeof(T).FullName, minimumLevel)
        {
        }
    }

    public class MelonLoggerAdapter : ILogger
    {
        private readonly string _categoryName;
        private readonly LogLevel _minimumLevel;

        [ThreadStatic]
        private static string _scope;

        public MelonLoggerAdapter(string categoryName, LogLevel minimumLevel = LogLevel.Trace)
        {
            _categoryName = string.IsNullOrEmpty(categoryName) ? "RareParts" : categoryName;
            _minimumLevel = minimumLevel;
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _minimumLevel;

        public IDisposable BeginScope(string scope)
        {
            var previous = _scope;
            _scope = scope;
            return new ScopeDisposable(previous);
        }

        public void Log(LogLevel logLevel, string message, Exception exception = null)
        {
            if (!IsEnabled(logLevel)) return;

            var prefix = string.IsNullOrEmpty(_scope)
                ? $"[{_categoryName}]"
                : $"[{_categoryName}][{_scope}]";

            var full = exception == null
                ? $"{prefix} {message}"
                : $"{prefix} {message}\n{exception}";

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Information:
                    MelonLogger.Msg(full);
                    break;
                case LogLevel.Warning:
                    MelonLogger.Warning(full);
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    MelonLogger.Error(full);
                    break;
                case LogLevel.None:
                default:
                    break;
            }
        }

        private sealed class ScopeDisposable : IDisposable
        {
            private readonly string _previous;
            private bool _disposed;

            public ScopeDisposable(string previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _scope = _previous;
                _disposed = true;
            }
        }
    }
}
