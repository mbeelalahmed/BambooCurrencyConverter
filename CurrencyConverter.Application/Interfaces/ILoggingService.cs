namespace CurrencyConverter.Application.Interfaces
{
    public interface ILoggingService
    {
        void LogInfo(string message, params object[] args);
        void LogError(Exception ex, string message, params object[] args);
    }
}
