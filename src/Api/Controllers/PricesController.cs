using Application.DTOs;
using Application.UseCases;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PricesController(
    GetCurrentPriceUseCase getCurrentPriceUseCase,
    ILogger<PricesController> logger) : ControllerBase
{
    /// <summary>
    /// Get the current price of a specific financial instrument
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PriceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceDto>> GetCurrentPrice([FromQuery] string symbol, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("GET /api/prices/{Symbol} - Request received", symbol);
            
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return BadRequest(new { error = "Symbol is required" });
            }
            
            var price = await getCurrentPriceUseCase.ExecuteAsync(symbol, cancellationToken);
            
            if (price == null)
            {
                logger.LogWarning("Price not found for symbol: {Symbol}", symbol);
                return NotFound(new { error = $"Price not found for symbol: {symbol}" });
            }
            
            return Ok(price);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving price for symbol: {Symbol}", symbol);
            return StatusCode(500, new { error = "An error occurred while retrieving the price" });
        }
    }
}

