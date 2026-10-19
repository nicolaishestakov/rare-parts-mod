using System;

namespace RareParts.Logging
{
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
