using CurrencyConverter.API; 
using CurrencyConverter.Application.Interfaces;
using CurrencyConverter.Application.Services;
using CurrencyConverter.Domain.Entities;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace CurrencyConverter.IntegrationTests
{
    public class FakeAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public FakeAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "testuser"),
                new Claim("client_id", "abc123"),
                new Claim(ClaimTypes.Role, "admin")
            };
            var identity = new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, JwtBearerDefaults.AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class StubProvider : IExchangeRateService
    {
        public const decimal FixedRate = 0.5m;

        public Task<ExchangeRate> GetLatestRatesAsync(string baseCurrency)
        {
            var result = new ExchangeRate
            {
                Date = DateTime.UtcNow.Date,
                Rates = new Dictionary<string, decimal>
                {
                    { "EUR", FixedRate }
                }
            };

            return Task.FromResult(result);
        }

        public Task<decimal> ConvertCurrencyAsync(string fromCurrency, string toCurrency, decimal amount)
        {
            var convertedAmount = amount * FixedRate;
            return Task.FromResult(convertedAmount);
        }

        public Task<IEnumerable<ExchangeRate>> GetHistoricalRatesAsync(string baseCurrency, DateTime start, DateTime end, int page, int size)
        {
            var list = new List<ExchangeRate>();
            for (int i = 0; i < size; i++)
            {
                list.Add(new ExchangeRate
                {
                    Date = start.AddDays(i),
                    Rates = new Dictionary<string, decimal>
                    {
                        { "EUR", FixedRate }
                    }
                });
            }

            return Task.FromResult<IEnumerable<ExchangeRate>>(list);
        }

        public bool CanHandle(string providerName)
        {
            return providerName == "stub";
        }
    }

    public class ExchangeControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public ExchangeControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Test");

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ExchangeRateServiceProviderFactory>();
                    services.AddSingleton(new ExchangeRateServiceProviderFactory(new List<IExchangeRateService> { new StubProvider() }));

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
                });
            }).CreateClient();
        }
       

        [Fact]
        public async Task GetLatest_ReturnsOk_WithRates()
        {
            var response = await _client.GetAsync("/api/v1.0/exchange/latest?baseCurrency=USD");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var rates = await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>();
            Assert.NotNull(rates);
            Assert.Contains("EUR", rates.Keys);
            Assert.Equal(StubProvider.FixedRate, rates["EUR"]);
        }

        [Fact]
        public async Task Convert_ReturnsOk_WithResult()
        {
            decimal amount = 100m;
            var response = await _client.GetAsync($"/api/v1.0/exchange/convert?from=USD&to=EUR&amount={amount}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var wrapper = await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>();
            Assert.NotNull(wrapper);
            Assert.True(wrapper.TryGetValue("result", out var converted));
            Assert.Equal(StubProvider.FixedRate * amount, converted);
        }

        [Fact]
        public async Task GetHistorical_ReturnsOk_WithPagedResults()
        {
            var start = DateTime.UtcNow.Date.AddDays(-5).ToString("o");
            var end = DateTime.UtcNow.Date.ToString("o");
            var response = await _client.GetAsync($"/api/v1.0/exchange/historical?baseCurrency=USD&start={start}&end={end}&page=1&size=2");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<ExchangeRate>>();
            Assert.NotNull(list);
            Assert.Equal(2, list.Count);
        }
    }
}