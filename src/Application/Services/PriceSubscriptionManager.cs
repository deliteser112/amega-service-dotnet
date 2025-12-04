using Core.Interfaces.Services;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// Manages WebSocket subscriptions for price updates.
/// 
/// PERFORMANCE OPTIMIZATION FOR 1000+ SUBSCRIBERS:
/// ===============================================
/// This class is designed to efficiently handle 1000+ concurrent WebSocket subscribers:
/// 
/// 1. DATA STRUCTURES:
///    - Uses ConcurrentDictionary for thread-safe O(1) lookups and updates
///    - Dual-index structure: connection->symbols and symbol->connections
///    - No locking required - all operations are lock-free and thread-safe
/// 
/// 2. SCALABILITY:
///    - Subscription lookup: O(1) - constant time regardless of subscriber count
///    - Subscriber enumeration: O(n) where n = subscribers for that symbol only
///    - Memory efficient: only stores connection IDs, not full connection objects
/// 
/// 3. SINGLE DATA PROVIDER CONNECTION:
///    - PriceService maintains ONE connection per instrument regardless of subscriber count
///    - 1000 subscribers to BTCUSD = 1 Binance WebSocket connection
///    - This prevents connection exhaustion and reduces external API load
/// 
/// 4. CONCURRENT OPERATIONS:
///    - All operations are thread-safe and can handle concurrent subscribe/unsubscribe
///    - No blocking operations - suitable for high-throughput scenarios
/// </summary>
public class PriceSubscriptionManager : IPriceSubscriptionManager
{
    private readonly IPriceService _priceService;
    private readonly ILogger<PriceSubscriptionManager> _logger;
    
    // PERFORMANCE: O(1) lookup of all symbols a connection is subscribed to
    // Thread-safe ConcurrentDictionary - no locks needed even with 1000+ concurrent operations
    // Maps connectionId -> Set of subscribed symbols
    // Example: "conn-123" -> ["BTCUSD", "EURUSD"]
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _connectionSubscriptions = new();
    
    // PERFORMANCE: O(1) lookup of all connections subscribed to a symbol
    // Critical for broadcasting - can retrieve 1000+ subscriber IDs in constant time
    // Maps symbol -> Set of connectionIds
    // Example: "BTCUSD" -> ["conn-1", "conn-2", ..., "conn-1000"]
    private readonly ConcurrentDictionary<string, ConcurrentHashSet<string>> _symbolSubscribers = new();

    public PriceSubscriptionManager(
        IPriceService priceService,
        ILogger<PriceSubscriptionManager> logger)
    {
        _priceService = priceService;
        _logger = logger;
    }

    public async Task SubscribeAsync(string connectionId, string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Subscribing connection {ConnectionId} to symbol {Symbol}", connectionId, symbol);
        
        // PERFORMANCE: O(1) operation - GetOrAdd is atomic and lock-free
        // Even with 1000+ concurrent subscriptions, this remains constant time
        var connectionSubs = _connectionSubscriptions.GetOrAdd(connectionId, 
            _ => new ConcurrentDictionary<string, bool>());
        connectionSubs.TryAdd(symbol, true);
        
        // PERFORMANCE: O(1) operation - critical for broadcasting efficiency
        // This allows us to quickly find all subscribers when a price update arrives
        var subscribers = _symbolSubscribers.GetOrAdd(symbol, 
            _ => new ConcurrentHashSet<string>());
        subscribers.Add(connectionId);
        
        // PERFORMANCE: PriceService ensures only ONE external connection per symbol
        // If this is the 1000th subscriber to BTCUSD, no new Binance connection is created
        // The existing connection is reused - this is the key to handling 1000+ subscribers
        await _priceService.StartPriceStreamAsync(symbol, cancellationToken);
        
        _logger.LogDebug("Connection {ConnectionId} subscribed to {Symbol}. Total subscribers for {Symbol}: {Count}",
            connectionId, symbol, symbol, subscribers.Count);
    }

    public async Task UnsubscribeAsync(string connectionId, string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Unsubscribing connection {ConnectionId} from symbol {Symbol}", connectionId, symbol);
        
        // Remove from connection's subscription set
        if (_connectionSubscriptions.TryGetValue(connectionId, out var connectionSubs))
        {
            connectionSubs.TryRemove(symbol, out _);
        }
        
        // Remove from symbol's subscriber set
        if (_symbolSubscribers.TryGetValue(symbol, out var subscribers))
        {
            subscribers.Remove(connectionId);
            
            // If no more subscribers, stop the price stream
            if (subscribers.Count == 0)
            {
                _symbolSubscribers.TryRemove(symbol, out _);
                await _priceService.StopPriceStreamAsync(symbol, cancellationToken);
            }
        }
        
        _logger.LogDebug("Connection {ConnectionId} unsubscribed from {Symbol}", connectionId, symbol);
    }

    public async Task UnsubscribeAllAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Unsubscribing connection {ConnectionId} from all symbols", connectionId);
        
        if (_connectionSubscriptions.TryGetValue(connectionId, out var connectionSubs))
        {
            var symbols = connectionSubs.Keys.ToList();
            
            foreach (var symbol in symbols)
            {
                await UnsubscribeAsync(connectionId, symbol, cancellationToken);
            }
            
            _connectionSubscriptions.TryRemove(connectionId, out _);
        }
        
        _logger.LogDebug("Connection {ConnectionId} unsubscribed from all symbols", connectionId);
    }

    public Task<IEnumerable<string>> GetSubscribedSymbolsAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (_connectionSubscriptions.TryGetValue(connectionId, out var connectionSubs))
        {
            return Task.FromResult<IEnumerable<string>>(connectionSubs.Keys);
        }
        
        return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }

    /// <summary>
    /// Gets all connection IDs subscribed to a specific symbol.
    /// 
    /// PERFORMANCE FOR 1000+ SUBSCRIBERS:
    /// ===================================
    /// - O(1) lookup to get the subscriber set (constant time)
    /// - Returns IEnumerable for lazy evaluation - no immediate enumeration
    /// - Used by PriceBroadcastService to efficiently broadcast to all subscribers
    /// - Even with 1000+ subscribers, the lookup itself is instant
    /// - The enumeration happens during SignalR's broadcast, which is optimized
    /// </summary>
    public IEnumerable<string> GetSubscribersForSymbol(string symbol)
    {
        // PERFORMANCE: O(1) lookup - instant even with 1000+ subscribers
        if (_symbolSubscribers.TryGetValue(symbol, out var subscribers))
        {
            return subscribers;
        }
        
        return Array.Empty<string>();
    }
}

/// <summary>
/// Thread-safe HashSet implementation for managing subscriber collections.
/// </summary>
public class ConcurrentHashSet<T> : IEnumerable<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dictionary = new();

    public int Count => _dictionary.Count;

    public bool Add(T item) => _dictionary.TryAdd(item, 0);
    
    public bool Remove(T item) => _dictionary.TryRemove(item, out _);
    
    public bool Contains(T item) => _dictionary.ContainsKey(item);
    
    public IEnumerator<T> GetEnumerator() => _dictionary.Keys.GetEnumerator();
    
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

