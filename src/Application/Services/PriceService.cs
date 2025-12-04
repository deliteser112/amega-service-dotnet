using Core.Entities;
using Core.Interfaces.Infrastructure;
using Core.Interfaces.Services;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Application.Services;

/// <summary>
/// Service for managing price data and streaming.
/// 
/// PERFORMANCE OPTIMIZATION FOR 1000+ SUBSCRIBERS:
/// ===============================================
/// This is the CRITICAL component that enables handling 1000+ subscribers efficiently:
/// 
/// 1. SINGLE CONNECTION PER INSTRUMENT:
///    - Maintains ONE WebSocket connection to external provider per instrument
///    - Example: 1000 clients subscribe to BTCUSD = 1 Binance connection
///    - Example: 500 clients subscribe to EURUSD = 1 additional connection
///    - This prevents connection exhaustion and API rate limiting
/// 
/// 2. SUBSCRIPTION COUNTING:
///    - Tracks how many internal subscriptions exist per symbol
///    - Only connects to external provider on first subscription (count = 1)
///    - Only disconnects when last subscription is removed (count = 0)
///    - All operations are O(1) and thread-safe
/// 
/// 3. PRICE CACHING:
///    - Caches latest price per symbol for fast REST API responses
///    - O(1) cache lookup regardless of subscriber count
///    - Thread-safe updates from WebSocket stream
/// 
/// 4. EVENT-BASED BROADCASTING:
///    - Single price update from external provider triggers one event
///    - PriceBroadcastService handles distribution to all 1000+ subscribers
///    - No polling or active checking required
/// </summary>
public class PriceService : IPriceService, IDisposable
{
    private readonly IExternalPriceProvider _externalPriceProvider;
    private readonly ILogger<PriceService> _logger;
    
    // PERFORMANCE: O(1) price lookup for REST API endpoints
    // Thread-safe cache - updated by WebSocket stream, read by REST API
    private readonly ConcurrentDictionary<string, Price> _priceCache = new();
    
    // PERFORMANCE: O(1) subscription count tracking
    // Critical for maintaining single connection per instrument
    // Key: symbol (e.g., "BTCUSD"), Value: number of active subscriptions
    private readonly ConcurrentDictionary<string, int> _subscriptionCounts = new();
    private bool _disposed = false;

    public event EventHandler<Price>? PriceUpdated;

    public PriceService(
        IExternalPriceProvider externalPriceProvider,
        ILogger<PriceService> logger)
    {
        _externalPriceProvider = externalPriceProvider;
        _logger = logger;
        
        // Subscribe to external provider price updates
        _externalPriceProvider.PriceReceived += OnExternalPriceReceived;
    }

    public async Task<Price?> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting current price for symbol: {Symbol}", symbol);
        
        // Check cache first - O(1) lookup
        if (_priceCache.TryGetValue(symbol, out var cachedPrice))
        {
            _logger.LogDebug("Price found in cache for {Symbol}: {Value}", symbol, cachedPrice.Value);
            return cachedPrice;
        }

        _logger.LogInformation("Price not in cache for {Symbol}, starting subscription and waiting for price update", symbol);
        
        // If not in cache, ensure subscription is started and wait for first price
        var wasSubscribed = _subscriptionCounts.ContainsKey(symbol);
        
        if (!wasSubscribed)
        {
            await StartPriceStreamAsync(symbol, cancellationToken);
            
            // Give the connection a moment to establish and start receiving prices
            // Binance WebSocket typically sends prices very frequently (multiple per second)
            await Task.Delay(500, cancellationToken);
            
            // Check cache again after brief delay - price might have arrived
            if (_priceCache.TryGetValue(symbol, out cachedPrice))
            {
                _logger.LogDebug("Price received shortly after subscription for {Symbol}: {Value}", symbol, cachedPrice.Value);
                return cachedPrice;
            }
        }
        
        // Wait for price update with timeout (max 10 seconds to allow for connection establishment)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var tcs = new TaskCompletionSource<Price?>();
        EventHandler<Price>? handler = null;
        
        handler = (sender, price) =>
        {
            if (price.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            {
                tcs.TrySetResult(price);
                PriceUpdated -= handler;
            }
        };
        
        PriceUpdated += handler;
        
        try
        {
            // Check cache again in case price arrived between checks
            if (_priceCache.TryGetValue(symbol, out cachedPrice))
            {
                PriceUpdated -= handler;
                return cachedPrice;
            }
            
            _logger.LogDebug("Waiting for price update for {Symbol}...", symbol);
            var price = await tcs.Task.WaitAsync(linkedCts.Token);
            
            _logger.LogInformation("Price received for {Symbol}: {Value}", symbol, price?.Value);
            
            // DON'T clean up subscription - keep it active for future requests
            // The subscription will remain active and serve other clients
            // It will only be cleaned up when no WebSocket subscribers remain
            
            return price;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout waiting for price update for {Symbol} after 10 seconds. Connection may not be established or Binance may not be sending prices.", symbol);
            PriceUpdated -= handler;
            
            // Check cache one more time - price might have arrived just as we timed out
            if (_priceCache.TryGetValue(symbol, out cachedPrice))
            {
                _logger.LogInformation("Price found in cache after timeout for {Symbol}: {Value}", symbol, cachedPrice.Value);
                return cachedPrice;
            }
            
            // DON'T stop subscription - it might be serving WebSocket clients
            // Only stop if we're sure no one else needs it (handled by subscription count)
            
            return null;
        }
    }

    public async Task StartPriceStreamAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting price stream for symbol: {Symbol}", symbol);
        
        // PERFORMANCE: O(1) atomic increment - thread-safe even with 1000+ concurrent calls
        // Returns the new count after increment
        var count = _subscriptionCounts.AddOrUpdate(symbol, 1, (key, value) => value + 1);
        
        // PERFORMANCE CRITICAL: Only ONE external connection per symbol
        // ==============================================================
        // This is the KEY optimization that enables 1000+ subscribers:
        // - 1st subscriber (count = 1): Creates Binance WebSocket connection
        // - 2nd-1000th subscriber (count > 1): Reuses existing connection
        // - Result: 1000 subscribers = 1 external connection (not 1000!)
        // 
        // Without this, 1000 subscribers would require 1000 Binance connections,
        // which would hit rate limits, exhaust resources, and be inefficient.
        if (count == 1)
        {
            _logger.LogInformation("First subscription for {Symbol}, connecting to external provider", symbol);
            await _externalPriceProvider.SubscribeToSymbolAsync(symbol, cancellationToken);
        }
        else
        {
            // PERFORMANCE: Additional subscribers don't create new connections
            // They simply increment the counter - the existing connection serves all
            _logger.LogDebug("Additional subscription for {Symbol}, total subscriptions: {Count} (reusing existing connection)", symbol, count);
        }
    }

    public async Task StopPriceStreamAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping price stream for symbol: {Symbol}", symbol);
        
        // PERFORMANCE: O(1) atomic decrement - thread-safe
        // Decrements count and returns new value
        var count = _subscriptionCounts.AddOrUpdate(symbol, 0, (key, value) => Math.Max(0, value - 1));
        
        // PERFORMANCE CRITICAL: Only disconnect when NO subscribers remain
        // ================================================================
        // - If 1000 subscribers exist and 1 unsubscribes: count = 999, keep connection
        // - If 1 subscriber exists and unsubscribes: count = 0, disconnect
        // - This ensures connection is maintained as long as ANY subscriber needs it
        if (count == 0)
        {
            _logger.LogInformation("Last subscription removed for {Symbol}, disconnecting from external provider", symbol);
            await _externalPriceProvider.UnsubscribeFromSymbolAsync(symbol, cancellationToken);
            _subscriptionCounts.TryRemove(symbol, out _);
        }
        else
        {
            // PERFORMANCE: Connection remains active for remaining subscribers
            // No action needed - connection continues serving other subscribers
            _logger.LogDebug("Subscription removed for {Symbol}, remaining subscriptions: {Count} (connection remains active)", symbol, count);
        }
    }

    private void OnExternalPriceReceived(object? sender, Price price)
    {
        _logger.LogDebug("Price update received for {Symbol}: {Value}", price.Symbol, price.Value);
        
        // Update cache
        _priceCache.AddOrUpdate(price.Symbol, price, (key, oldValue) => price);
        
        // Notify subscribers
        PriceUpdated?.Invoke(this, price);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _externalPriceProvider.PriceReceived -= OnExternalPriceReceived;
            _disposed = true;
        }
    }
}

