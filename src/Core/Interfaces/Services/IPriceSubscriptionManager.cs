namespace Core.Interfaces.Services;

/// <summary>
/// Manages WebSocket subscriptions for price updates.
/// Efficiently handles 1000+ subscribers by maintaining a single connection to the data provider
/// per instrument and broadcasting updates to all subscribed clients.
/// </summary>
public interface IPriceSubscriptionManager
{
    Task SubscribeAsync(string connectionId, string symbol, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(string connectionId, string symbol, CancellationToken cancellationToken = default);
    Task UnsubscribeAllAsync(string connectionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetSubscribedSymbolsAsync(string connectionId, CancellationToken cancellationToken = default);
    IEnumerable<string> GetSubscribersForSymbol(string symbol);
}

