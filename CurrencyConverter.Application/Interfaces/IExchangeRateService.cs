using CurrencyConverter.Domain.Entities;
namespace CurrencyConverter.Application.Interfaces
{
    public interface IExchangeRateService
    {
        Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency);
        Task<decimal> ConvertCurrencyAsync(string fromCurrency, string toCurrency, decimal amount);
        Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime start, DateTime end, int page, int size);
        bool CanHandle(string providerName);
    }
}
