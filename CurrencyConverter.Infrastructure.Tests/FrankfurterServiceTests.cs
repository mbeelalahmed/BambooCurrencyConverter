using CurrencyConverter.Application.Interfaces;
using CurrencyConverter.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Moq.Protected;
using System.Net;

namespace CurrencyConverter.Infrastructure.Tests
{
    public class FrankfurterServiceTests
        {
            private readonly Mock<ILoggingService> _loggerMock;
            private readonly IMemoryCache _memoryCache;

            public FrankfurterServiceTests()
            {
                _loggerMock = new Mock<ILoggingService>();
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
                    BaseAddress = new Uri("https://api.frankfurter.app/")
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

                var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object);

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
                var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object);

                var first = await service.GetLatestRatesAsync("USD");
                var second = await service.GetLatestRatesAsync("USD"); // should hit cache

                _loggerMock.Verify(l => l.LogInfo(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
                second.Should().BeEquivalentTo(first);
            }

            [Fact]
            public async Task ConvertCurrencyAsync_Throws_WhenCurrencyExcluded()
            {
                var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
                var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object);

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
                var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object);

                var result = await service.ConvertCurrencyAsync("USD", "EUR", 100);

                result.Should().Be(0.85m);
            }

            [Fact]
            public async Task GetHistoricalRatesAsync_ReturnsPagedResults()
            {
                var date1 = DateTime.Today.AddDays(-1);
                var date2 = DateTime.Today;

                var jsonResponse1 = """
                {
                    "base": "USD",
                    "date": "%s",
                    "rates": { "EUR": 0.9 }
                }
            """.Replace("%s", date1.ToString("yyyy-MM-dd"));

                var jsonResponse2 = """
                {
                    "base": "USD",
                    "date": "%s",
                    "rates": { "EUR": 0.91 }
                }
            """.Replace("%s", date2.ToString("yyyy-MM-dd"));

                var responses = new Queue<HttpResponseMessage>();
                responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jsonResponse1) });
                responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jsonResponse2) });

                var handlerMock = new Mock<HttpMessageHandler>();

                handlerMock
                   .Protected()
                   .SetupSequence<Task<HttpResponseMessage>>("SendAsync",
                       ItExpr.IsAny<HttpRequestMessage>(),
                       ItExpr.IsAny<CancellationToken>())
                   .ReturnsAsync(responses.Dequeue())
                   .ReturnsAsync(responses.Dequeue());

                var httpClient = new HttpClient(handlerMock.Object)
                {
                    BaseAddress = new Uri("https://api.frankfurter.app/")
                };

                var service = new FrankfurterService(httpClient, _memoryCache, _loggerMock.Object);

                var result = await service.GetHistoricalRatesAsync("USD", date1, date2, 1, 10);

                result.Should().HaveCount(2);
                result.Should().ContainSingle(r => r.Date == date1);
                result.Should().ContainSingle(r => r.Date == date2);
            }
        }
    
}