using CurrencyConverter.Application.Interfaces;
using CurrencyConverter.Domain.Entities;
using CurrencyConverter.Infrastructure.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CurrencyConverter.Infrastructure.Services
{
    public class FrankfurterService : IExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILoggingService _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreaker;

        private static readonly string[] ExcludedCurrencies = { "TRY", "PLN", "THB", "MXN" };
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public FrankfurterService(HttpClient httpClient, IMemoryCache cache, ILoggingService logger)
        {
            _httpClient = httpClient;
            _cache = cache;

            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            _circuitBreaker = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));

            _policy = Policy.WrapAsync(_retryPolicy, _circuitBreaker);
            _logger = logger;
        }

        public async Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency)
        {
             if (_cache.TryGetValue($"latest_{baseCurrency}", out ExchangeRate cached))
                 return cached;

            _logger.LogInfo("Calling Frankfurter API method: latest");

            var response = await _policy.ExecuteAsync(() =>
                _httpClient.GetAsync($"latest?base={baseCurrency}")
            );

            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<FrankfurterLatestResponseDto>(
                await response.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result is null)
            {
                throw new InvalidOperationException("Failed to deserialize exchange rate data.");
            }

            _logger.LogInfo("Frankfurter API Response: {StatusCode}", response.StatusCode);

            _cache.Set($"latest_{baseCurrency}", result, TimeSpan.FromMinutes(30));
            return result.ToDomainModel();
        }

        public async Task<decimal> ConvertCurrencyAsync(string from, string to, decimal amount)
        {
            if (ExcludedCurrencies.Contains(from) || ExcludedCurrencies.Contains(to))
                throw new ArgumentException("Conversion using TRY, PLN, THB, or MXN is not supported.");

            var cacheKey = $"convert_{from}_{to}";
            if (!_cache.TryGetValue(cacheKey, out decimal rate))
            {
                _logger.LogInfo("Calling Frankfurter API method: convert");

                var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync($"latest?from={from}&to={to}&amount={amount}"));

                _logger.LogInfo("Frankfurter API Response: {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode();

                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var value = json.RootElement.GetProperty("rates").EnumerateObject().First().Value.GetDecimal();
                _cache.Set(cacheKey, value, TimeSpan.FromHours(1));
                return value;
            }
            return rate;
        }

        public async Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime start, DateTime end, int page, int size)
        {
            var days = (end - start).Days + 1;
            var pagedDates = Enumerable.Range(0, days)
                                        .Select(i => start.AddDays(i))
                                        .Skip((page - 1) * size)
                                        .Take(size);

            var results = new List<ExchangeRate>();

            foreach (var date in pagedDates)
            {
                var cacheKey = $"historical_{baseCurrency}_{date:yyyyMMdd}";
                if (!_cache.TryGetValue(cacheKey, out ExchangeRate rate))
                {
                    _logger.LogInfo("Calling Frankfurter API method: historical");

                    var response = await _retryPolicy.ExecuteAsync(() => _httpClient.GetAsync($"{date:yyyy-MM-dd}?base={baseCurrency}"));
                    
                    _logger.LogInfo("Frankfurter API Response: {StatusCode}", response.StatusCode);
                    response.EnsureSuccessStatusCode();
                    var result = JsonSerializer.Deserialize<FrankfurterLatestResponseDto>(
                        await response.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (result is null)
                    {
                        throw new InvalidOperationException("Failed to deserialize exchange rate data.");
                    }
                    _cache.Set(cacheKey, result.ToDomainModel(), TimeSpan.FromHours(1));
                    results.Add(result.ToDomainModel());
                }
                else
                {
                    results.Add(rate);
                }
            }

            return results;
        }

        public bool CanHandle(string providerName)
        {
            return providerName.Equals("frankfurter", StringComparison.OrdinalIgnoreCase);
        }
    }
}
