using CurrencyConverter.Application.Interfaces;
using CurrencyConverter.Domain.Entities;
using CurrencyConverter.Infrastructure.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Globalization;
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
        private readonly IHttpContextAccessor _httpContextAccessor;


        private static readonly string[] ExcludedCurrencies = { "TRY", "PLN", "THB", "MXN" };
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public FrankfurterService(HttpClient httpClient, IMemoryCache cache, ILoggingService logger, IHttpContextAccessor contextAccessor)
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
            _httpContextAccessor = contextAccessor;
        }

        public async Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency)
        {
            var cacheKey = $"latest_{baseCurrency}";
            if (_cache.TryGetValue(cacheKey, out ExchangeRate cached))
                 return cached;

            var result = await ProcessRequestAndPrepareExchangeRateResult($"latest?base={baseCurrency}");

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }

        public async Task<decimal> ConvertCurrencyAsync(string from, string to, decimal amount)
        {
            if (ExcludedCurrencies.Contains(from) || ExcludedCurrencies.Contains(to))
                throw new ArgumentException("Conversion using TRY, PLN, THB, or MXN is not supported.");

            var cacheKey = $"convert_{from}_{to}";
            if (!_cache.TryGetValue(cacheKey, out decimal rate))
            {
                var responseContent = JsonDocument.Parse(await ProcessRequest($"latest?from={from}&to={to}&amount={amount}"));

                var value = responseContent.RootElement.GetProperty("rates").EnumerateObject().First().Value.GetDecimal();

                _cache.Set(cacheKey, value, TimeSpan.FromMinutes(5));

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

            var startDate = pagedDates.First();
            var endDate = pagedDates.Last();

            var cacheKey = $"historical_{baseCurrency}_{startDate:yyyyMMdd}_{end:yyyyMMdd}";
            if (_cache.TryGetValue(cacheKey, out List<ExchangeRate> cached))
                return cached;

            var result = await ProcessRequestAndPrepareExchangeRateListResult($"{startDate:yyyy-MM-dd}..{endDate:yyyy-MM-dd}?base={baseCurrency}");
             _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

            return result;
        }

        public bool CanHandle(string providerName)
        {
            return providerName.Equals("frankfurter", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> ProcessRequest(string apiPath)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, apiPath);

            var correlationId = _httpContextAccessor.HttpContext?.Items["X-Correlation-ID"]?.ToString();
            if (!string.IsNullOrEmpty(correlationId))
                request.Headers.Add("X-Correlation-ID", correlationId);

            _logger.LogInfo("Calling Frankfurter API: {Path} | CorrelationId: {CorrelationId}", apiPath, correlationId);

            var response = await _policy.ExecuteAsync(() => _httpClient.GetAsync(apiPath));

            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInfo("Frankfurter API Response: {Content} | StatusCode: {StatusCode} | CorrelationId: {CorrelationId}",
                    responseContent, response.StatusCode, correlationId);

            return responseContent;
        }

        private async Task<ExchangeRate> ProcessRequestAndPrepareExchangeRateResult(string apiPath)
        { 
            var responseContent = await ProcessRequest(apiPath);

            var result = JsonSerializer.Deserialize<FrankfurterLatestResponseDto>(
                responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result?.ToDomainModel();
        }

        private async Task<List<ExchangeRate>> ProcessRequestAndPrepareExchangeRateListResult(string apiPath)
        {
            var responseContent = await ProcessRequest(apiPath);

            var result = JsonSerializer.Deserialize<FrankfurterHistoricalResponseDto>(
                responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var exchangeRateList = new List<ExchangeRate>();

            foreach (var item in result.Rates)
            {
                var exchangeRate = new ExchangeRate
                {
                    BaseCurrency = result.BaseCurrency,
                    Date = DateTime.ParseExact(item.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Rates = item.Value
                };
                exchangeRateList.Add(exchangeRate);
            }

            return exchangeRateList;
        }
    }
}
