using Core.Entities;

namespace Core.Interfaces.Infrastructure;

/// <summary>
/// Interface for external price data providers (e.g., Binance, Tiingo).
/// Handles the connection to external WebSocket streams and converts their format to our domain model.
/// </summary>
public interface IExternalPriceProvider
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SubscribeToSymbolAsync(string symbol, CancellationToken cancellationToken = default);
    Task UnsubscribeFromSymbolAsync(string symbol, CancellationToken cancellationToken = default);
    event EventHandler<Price>? PriceReceived;
    bool IsConnected { get; }
}

