namespace RareParts.Logging
{
    public interface ILoggerFactory
    {
        ILogger CreateLogger(string categoryName);
        ILogger<T> CreateLogger<T>();
    }
}
