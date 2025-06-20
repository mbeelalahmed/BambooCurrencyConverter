namespace CurrencyConverter.Domain.Entities
{
    public class ExchangeRate
    {
        public string BaseCurrency { get; set; }
        public Dictionary<string, decimal> Rates { get; set; }
        public DateTime Date { get; set; }
    }
}
