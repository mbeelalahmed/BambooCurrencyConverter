using CurrencyConverter.Application.Interfaces;
using CurrencyConverter.Application.Services;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyConverter.Application.Tests.Services
{
    public class ExchangeRateServiceProviderFactoryTests
    {
        [Fact]
        public void GetProvider_ReturnsMatchingProvider_WhenProviderExists()
        {
            var mockProvider = new Mock<IExchangeRateService>();
            mockProvider.Setup(p => p.CanHandle("frankfurter")).Returns(true);

            var factory = new ExchangeRateServiceProviderFactory(new[] { mockProvider.Object });

            var provider = factory.GetProvider("frankfurter");

            provider.Should().NotBeNull();
            provider.Should().BeSameAs(mockProvider.Object);
        }

        [Fact]
        public void GetProvider_ThrowsInvalidOperationException_WhenNoProviderMatches()
        {
            var mockProvider = new Mock<IExchangeRateService>();
            mockProvider.Setup(p => p.CanHandle(It.IsAny<string>())).Returns(false);

            var factory = new ExchangeRateServiceProviderFactory(new[] { mockProvider.Object });

            Action act = () => factory.GetProvider("unknown");

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("No provider found for: unknown");
        }

        [Fact]
        public void GetProvider_PrefersFirstMatch_WhenMultipleMatches()
        {
            var matchingProvider1 = new Mock<IExchangeRateService>();
            var matchingProvider2 = new Mock<IExchangeRateService>();

            matchingProvider1.Setup(p => p.CanHandle("mock")).Returns(true);
            matchingProvider2.Setup(p => p.CanHandle("mock")).Returns(true);

            var factory = new ExchangeRateServiceProviderFactory(new[] { matchingProvider1.Object, matchingProvider2.Object });

            var provider = factory.GetProvider("mock");

            provider.Should().BeSameAs(matchingProvider1.Object);
        }
    }
}
