using System;
using System.Runtime.Remoting.Messaging;
using MelonLoader;

namespace RareParts.Logging
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6
    }

    public interface ILogger
    {
        bool IsEnabled(LogLevel logLevel);
        void Log(LogLevel logLevel, string message, Exception exception = null);
        IDisposable BeginScope(string scope);
    }

    public interface ILogger<out T> : ILogger { }

    public interface ILoggerFactory
    {
        ILogger CreateLogger(string categoryName);
        ILogger<T> CreateLogger<T>();
    }

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

    public static class LoggerExtensions
    {
        public static void Trace(this ILogger logger, string message) => logger.Log(LogLevel.Trace, message);
        public static void Debug(this ILogger logger, string message) => logger.Log(LogLevel.Debug, message);
        public static void Information(this ILogger logger, string message) => logger.Log(LogLevel.Information, message);
        public static void Warning(this ILogger logger, string message) => logger.Log(LogLevel.Warning, message);
        public static void Error(this ILogger logger, string message, Exception ex = null) => logger.Log(LogLevel.Error, message, ex);
        public static void Critical(this ILogger logger, string message, Exception ex = null) => logger.Log(LogLevel.Critical, message, ex);

        public static void Log(this ILogger logger, LogLevel level, string format, params object[] args)
        {
            if (!logger.IsEnabled(level)) return;
            var msg = args == null || args.Length == 0 ? format : string.Format(format, args);
            logger.Log(level, msg);
        }
    }
}
