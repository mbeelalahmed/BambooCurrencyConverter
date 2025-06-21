using CurrencyConverter.API.Helpers;
using CurrencyConverter.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace CurrencyConverter.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class ExchangeController : ControllerBase
    {
        private readonly ExchangeRateServiceProviderFactory _exchangeRateServiceProviderFactory;

        public ExchangeController(ExchangeRateServiceProviderFactory factory)
        {
            _exchangeRateServiceProviderFactory = factory;
        }

        [HttpGet("latest")]
        [Authorize(Roles = "admin,operator")]
        public async Task<IActionResult> GetLatest([FromQuery] string baseCurrency, [FromQuery] string provider = "frankfurter")
        {
            try
            {
                var service = _exchangeRateServiceProviderFactory.GetProvider(provider);
                var result = await service.GetLatestRatesAsync(baseCurrency);
                return Ok(result);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, AppConstants.INTERNAL_SERVER_ERROR);
            }
        }

        [HttpGet("convert")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Convert(
            [FromQuery] string from, 
            [FromQuery] string to, 
            [FromQuery] decimal amount, 
            [FromQuery] string provider = "frankfurter")
        {
            try
            {
                var service = _exchangeRateServiceProviderFactory.GetProvider(provider);
                var result = await service.ConvertCurrencyAsync(from, to, amount);
                return Ok(new { result });
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, AppConstants.INTERNAL_SERVER_ERROR);
            }
        }

        [HttpGet("historical")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetHistorical([FromQuery] string baseCurrency,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string provider = "frankfurter")
        {
            try
            {
                var service = _exchangeRateServiceProviderFactory.GetProvider(provider);
                var result = await service.GetHistoricalRatesAsync(baseCurrency, start, end, page, size);
                return Ok(result);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, AppConstants.INTERNAL_SERVER_ERROR);
            }
        }
    }
}
