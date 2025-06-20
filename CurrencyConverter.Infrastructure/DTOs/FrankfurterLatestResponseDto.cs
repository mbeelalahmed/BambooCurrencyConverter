using CurrencyConverter.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CurrencyConverter.Infrastructure.DTOs
{
    public class FrankfurterLatestResponseDto
    {
        [JsonPropertyName("base")]
        public string BaseCurrency { get; set; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal> Rates { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        public ExchangeRate ToDomainModel() => new ExchangeRate
        {
            BaseCurrency = this.BaseCurrency,
            Rates = this.Rates,
            Date = this.Date
        };
    }
}
