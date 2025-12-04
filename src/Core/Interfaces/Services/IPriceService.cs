using Core.Entities;

namespace Core.Interfaces.Services;

public interface IPriceService
{
    Task<Price?> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken = default);
    Task StartPriceStreamAsync(string symbol, CancellationToken cancellationToken = default);
    Task StopPriceStreamAsync(string symbol, CancellationToken cancellationToken = default);
    event EventHandler<Price>? PriceUpdated;
}

