using CurrencyConverter.API.Controllers;
using CurrencyConverter.Application.Interfaces;
using CurrencyConverter.Application.Services;
using CurrencyConverter.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CurrencyConverter.API.Tests.Controllers
{
    public class ExchangeControllerTests
    {
        private readonly Mock<IExchangeRateService> _mockService;
        private readonly ExchangeRateServiceProviderFactory _factory;
        private readonly ExchangeController _controller;

        public ExchangeControllerTests()
        {
            _mockService = new Mock<IExchangeRateService>();
            _mockService.Setup(s => s.CanHandle(It.IsAny<string>())).Returns(true);

            _factory = new ExchangeRateServiceProviderFactory(new[] { _mockService.Object });
            _controller = new ExchangeController(_factory);
        }

        [Fact]
        public async Task GetLatest_ReturnsOk_WhenValid()
        {
            var expected = new ExchangeRate
            {
                BaseCurrency = "USD",
                Date = DateTime.Today,
                Rates = new Dictionary<string, decimal> { { "EUR", 0.9m } }
            };

            _mockService.Setup(s => s.GetLatestRatesAsync("USD")).ReturnsAsync(expected);

            var result = await _controller.GetLatest("USD", "mock");

            result.Should().BeOfType<OkObjectResult>()
                  .Which.Value.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task Convert_ReturnsOk_WhenValid()
        {
            _mockService.Setup(s => s.ConvertCurrencyAsync("USD", "EUR", 100)).ReturnsAsync(90m);

            var result = await _controller.Convert("USD", "EUR", 100, "mock");

            result.Should().BeOfType<OkObjectResult>()
                  .Which.Value.Should().BeEquivalentTo(new { result = 90m });
        }

        [Fact]
        public async Task Convert_ReturnsBadRequest_WhenInvalid()
        {
            _mockService.Setup(s => s.ConvertCurrencyAsync("USD", "XYZ", 100))
                        .ThrowsAsync(new ArgumentException("Invalid currency"));

            var result = await _controller.Convert("USD", "XYZ", 100, "mock");

            result.Should().BeOfType<BadRequestObjectResult>()
                  .Which.Value.Should().Be("Invalid currency");
        }

        [Fact]
        public async Task GetHistorical_ReturnsOk_WhenValid()
        {
            var history = new List<ExchangeRate>
            {
                new ExchangeRate
                {
                    BaseCurrency = "USD",
                    Date = DateTime.Today,
                    Rates = new Dictionary<string, decimal> { { "EUR", 0.91m } }
                }
            };

            _mockService.Setup(s => s.GetHistoricalRatesAsync("USD", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 1, 10))
                        .ReturnsAsync(history);

            var result = await _controller.GetHistorical("USD", DateTime.Today.AddDays(-1), DateTime.Today, 1, 10, "mock");

            result.Should().BeOfType<OkObjectResult>()
                  .Which.Value.Should().BeEquivalentTo(history);
        }

        [Fact]
        public async Task GetLatest_ReturnsInternalServerError_WhenUnhandled()
        {
            _mockService.Setup(s => s.GetLatestRatesAsync("USD")).ThrowsAsync(new Exception("unexpected"));

            var result = await _controller.GetLatest("USD", "mock");

            result.Should().BeOfType<ObjectResult>()
                  .Which.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task GetHistorical_ReturnsBadRequest_OnArgumentError()
        {
            _mockService.Setup(s => s.GetHistoricalRatesAsync("USD", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 1, 10))
                        .ThrowsAsync(new ArgumentException("Invalid date range"));

            var result = await _controller.GetHistorical("USD", DateTime.MinValue, DateTime.MaxValue, 1, 10, "mock");

            result.Should().BeOfType<BadRequestObjectResult>()
                  .Which.Value.Should().Be("Invalid date range");
        }

        [Fact]
        public async Task GetHistorical_ReturnsBadRequest_OnInvalidOperation()
        {
            _mockService.Setup(s => s.GetHistoricalRatesAsync("USD", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 1, 10))
                        .ThrowsAsync(new InvalidOperationException("Invalid pagination"));

            var result = await _controller.GetHistorical("USD", DateTime.Today.AddDays(-5), DateTime.Today, 1, 10, "mock");

            result.Should().BeOfType<BadRequestObjectResult>()
                  .Which.Value.Should().Be("Invalid pagination");
        }

        [Fact]
        public async Task GetLatest_ReturnsBadRequest_OnArgumentException()
        {
            _mockService.Setup(s => s.GetLatestRatesAsync("USD"))
                        .ThrowsAsync(new ArgumentException("Invalid base currency"));

            var result = await _controller.GetLatest("USD", "mock");

            result.Should().BeOfType<BadRequestObjectResult>()
                  .Which.Value.Should().Be("Invalid base currency");
        }

        [Fact]
        public async Task Convert_ReturnsBadRequest_OnInvalidOperationException()
        {
            _mockService.Setup(s => s.ConvertCurrencyAsync("USD", "EUR", 100))
                        .ThrowsAsync(new InvalidOperationException("Conversion not supported"));

            var result = await _controller.Convert("USD", "EUR", 100, "mock");

            result.Should().BeOfType<BadRequestObjectResult>()
                  .Which.Value.Should().Be("Conversion not supported");
        }

        [Fact]
        public async Task Convert_ReturnsInternalServerError_OnUnhandledException()
        {
            _mockService.Setup(s => s.ConvertCurrencyAsync("USD", "EUR", 100))
                        .ThrowsAsync(new Exception("unexpected"));

            var result = await _controller.Convert("USD", "EUR", 100, "mock");

            result.Should().BeOfType<ObjectResult>()
                  .Which.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task GetHistorical_ReturnsInternalServerError_OnUnhandledException()
        {
            _mockService.Setup(s => s.GetHistoricalRatesAsync("USD", It.IsAny<DateTime>(), It.IsAny<DateTime>(), 1, 10))
                        .ThrowsAsync(new Exception("unexpected"));

            var result = await _controller.GetHistorical("USD", DateTime.Today.AddDays(-1), DateTime.Today, 1, 10, "mock");

            result.Should().BeOfType<ObjectResult>()
                  .Which.StatusCode.Should().Be(500);
        }
    }
}
