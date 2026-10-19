using System;

namespace RareParts.Logging
{
    public interface ILogger
    {
        bool IsEnabled(LogLevel logLevel);
        void Log(LogLevel logLevel, string message, Exception exception = null);
        IDisposable BeginScope(string scope);
    }

    public interface ILogger<out T> : ILogger { }
}
