using Application.DTOs;
using Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Application.UseCases;

public class GetCurrentPriceUseCase
{
    private readonly IPriceService _priceService;
    private readonly ILogger<GetCurrentPriceUseCase> _logger;

    public GetCurrentPriceUseCase(
        IPriceService priceService,
        ILogger<GetCurrentPriceUseCase> logger)
    {
        _priceService = priceService;
        _logger = logger;
    }

    public async Task<PriceDto?> ExecuteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching current price for symbol: {Symbol}", symbol);
        
        var price = await _priceService.GetCurrentPriceAsync(symbol, cancellationToken);
        
        if (price == null)
        {
            _logger.LogWarning("Price not found for symbol: {Symbol}", symbol);
            return null;
        }

        _logger.LogInformation("Retrieved price for {Symbol}: {Value} at {Timestamp}", 
            symbol, price.Value, price.Timestamp);

        return new PriceDto
        {
            Symbol = price.Symbol,
            Value = price.Value,
            Timestamp = price.Timestamp
        };
    }
}

