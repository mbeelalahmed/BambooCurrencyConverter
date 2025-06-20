using CurrencyConverter.Application.Interfaces;

namespace CurrencyConverter.Application.Services
{
    public class ExchangeRateServiceProviderFactory
    {
        private readonly IEnumerable<IExchangeRateService> _providers;

        public ExchangeRateServiceProviderFactory(IEnumerable<IExchangeRateService> providers)
        {
            _providers = providers;
        }

        public IExchangeRateService GetProvider(string providerName)
        {
            var provider = _providers.FirstOrDefault(p => p.CanHandle(providerName));
            if (provider == null)
                throw new InvalidOperationException($"No provider found for: {providerName}");
            return provider;
        }
    }
}
