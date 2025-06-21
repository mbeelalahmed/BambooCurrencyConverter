using CurrencyConverter.Application.Interfaces;
using CurrencyConverter.Domain.Entities;
using CurrencyConverter.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Moq.Protected;
using System.Net;

namespace CurrencyConverter.Infrastructure.Tests.Services
{
    public class FrankfurterServiceTests
    {
        private readonly Mock<ILoggingService> _loggerMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly IMemoryCache _memoryCache;

        delegate void TryGetValueCallback(object key, out object value);
        public FrankfurterServiceTests()
        {
            _loggerMock = new Mock<ILoggingService>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        private HttpClient CreateHttpClient(HttpResponseMessage responseMessage)
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(responseMessage)
               .Verifiable();

            return new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://api.frankfurter.dev/v1/")
            };
        }

        [Fact]
        public async Task GetLatestRatesAsync_ReturnsExchangeRate_FromHttp()
        {
            var jsonResponse = """
                {
                    "base": "USD",
                    "date": "2025-06-20",
                    "rates": { "EUR": 0.9 }
                }
            """;

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };

            var httpClient = CreateHttpClient(httpResponse);

            var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object, _httpContextAccessorMock.Object);

            var result = await service.GetLatestRatesAsync("USD");

            result.BaseCurrency.Should().Be("USD");
            result.Rates.Should().ContainKey("EUR");
            result.Rates["EUR"].Should().Be(0.9m);
        }

        [Fact]
        public async Task GetLatestRatesAsync_UsesCache_OnSecondCall()
        {
            var jsonResponse = """
                {
                    "base": "USD",
                    "date": "2025-06-20",
                    "rates": { "EUR": 0.9 }
                }
            """;

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };

            var httpClient = CreateHttpClient(httpResponse);
            var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object, _httpContextAccessorMock.Object);

            var first = await service.GetLatestRatesAsync("USD");
            var second = await service.GetLatestRatesAsync("USD"); // should hit cache

            _loggerMock.Verify(l => l.LogInfo(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
            second.Should().BeEquivalentTo(first);
        }

        [Fact]
        public async Task ConvertCurrencyAsync_Throws_WhenCurrencyExcluded()
        {
            var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
            var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object, _httpContextAccessorMock.Object);

            Func<Task> act = () => service.ConvertCurrencyAsync("TRY", "USD", 100);

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Conversion using TRY, PLN, THB, or MXN is not supported.");
        }

        [Fact]
        public async Task ConvertCurrencyAsync_ReturnsRate_FromHttp()
        {
            var jsonResponse = """
                {
                    "rates": {
                        "EUR": 0.85
                    }
                }
            """;

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };

            var httpClient = CreateHttpClient(httpResponse);
            var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object, _httpContextAccessorMock.Object);

            var result = await service.ConvertCurrencyAsync("USD", "EUR", 100);

            result.Should().Be(0.85m);
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_ReturnsPagedResults()
        {
            var date1 = new DateTime(2025, 06, 06);
            var date2 = new DateTime(2025, 06, 07);
            var baseCurrency = "USD";

            var jsonResponse = """
            {
                "amount": 1,
                "base": "USD",
                "start_date": "2025-06-06",
                "end_date": "2025-06-07",
                "rates": {
                    "2025-06-06": {
                        "EUR": 0.9
                    },
                    "2025-06-07": {
                        "EUR": 0.91
                    }
                }
            }
        """;

            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.Is<HttpRequestMessage>(req =>
                       req.RequestUri.ToString().Contains("2025-06-06..2025-06-07") &&
                       req.RequestUri.ToString().Contains("base=USD")),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
               {
                   Content = new StringContent(jsonResponse)
               });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://api.frankfurter.dev/v1/")
            };

            var loggerMock = new Mock<ILoggingService>();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());

            var service = new FrankfurterService(httpClient, memoryCache, loggerMock.Object, _httpContextAccessorMock.Object);

            var result = await service.GetHistoricalRatesAsync(baseCurrency, date1, date2, 1, 10);

            result.Should().HaveCount(2);
            result.Should().ContainSingle(r => r.Date == date1 && r.Rates["EUR"] == 0.9m);
            result.Should().ContainSingle(r => r.Date == date2 && r.Rates["EUR"] == 0.91m);
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_Throws_WhenDeserializationReturnsNull()
        {
            var baseCurrency = "USD";
            var start = new DateTime(2025, 01, 01);
            var end = new DateTime(2025, 01, 01);
            var page = 1;
            var size = 1;

            var cacheMock = new Mock<IMemoryCache>();
            object outVal;
            cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out outVal))
                     .Returns(false);

            var loggerMock = new Mock<ILoggingService>();

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = System.Net.HttpStatusCode.OK,
                   Content = new StringContent(""),
               });

            var httpClient = new HttpClient(handlerMock.Object);

            var service = new FrankfurterService(httpClient, cacheMock.Object, loggerMock.Object, _httpContextAccessorMock.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetHistoricalRatesAsync(baseCurrency, start, end, page, size));
        }


        [Fact]
        public async Task GetHistoricalRatesAsync_ReturnsCachedValue_WhenCacheHit()
        {
           
                var baseCurrency = "USD";
                var start = new DateTime(2025, 01, 01);
                var end = new DateTime(2025, 01, 01);
                var page = 1;
                var size = 1;

                var cacheMock = new Mock<IMemoryCache>();
                var cachedExchangeRate = new List<ExchangeRate> { 
                    new ExchangeRate
                    {
                        BaseCurrency = "USD",
                        Date = new DateTime(2025, 01, 01),
                        Rates = new Dictionary<string, decimal>
                        {
                            { "EUR", 0.91m }
                        }
                    }
                };
                cacheMock.Setup(c => c.TryGetValue(It.IsAny<object>(), out It.Ref<object>.IsAny))
                    .Callback(new TryGetValueCallback((object key, out object value) =>
                    {
                        value = cachedExchangeRate;
                    }))
                    .Returns(true);

                var loggerMock = new Mock<ILoggingService>();

                var handlerMock = new Mock<HttpMessageHandler>();

                var httpClient = new HttpClient(handlerMock.Object)
                {
                    BaseAddress = new Uri("https://api.frankfurter.dev/v1/")
                };
                var service = new FrankfurterService(httpClient, cacheMock.Object, loggerMock.Object, _httpContextAccessorMock.Object);

                var result = await service.GetHistoricalRatesAsync(baseCurrency, start, end, page, size);

                Assert.Single(result);
                result.Should().Contain(cachedExchangeRate);

                handlerMock.Protected().Verify(
                    "SendAsync",
                    Times.Never(),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());

        }

        [Theory]
        [InlineData("frankfurter", true)]
        [InlineData("Frankfurter", true)]
        [InlineData("FRANKFURTER", true)]
        [InlineData("FrAnKfUrTeR", true)]
        [InlineData("other", false)]
        [InlineData("", false)]
        public void CanHandle_ReturnsExpectedResult(string providerName, bool expected)
        {
            var service = new FrankfurterService(new HttpClient(), Mock.Of<IMemoryCache>(), Mock.Of<ILoggingService>(), _httpContextAccessorMock.Object);
            var result = service.CanHandle(providerName);
            Assert.Equal(expected, result);
        }

    }
}