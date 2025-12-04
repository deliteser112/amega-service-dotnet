using Application.DTOs;
using Core.Entities;
using Core.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for real-time price updates via WebSocket.
/// 
/// PERFORMANCE OPTIMIZATION FOR 1000+ CONCURRENT CONNECTIONS:
/// ==========================================================
/// SignalR is designed to handle thousands of concurrent WebSocket connections efficiently:
/// 
/// 1. SIGNALR SCALABILITY:
///    - Built-in connection pooling and management
///    - Efficient message serialization and compression
///    - Automatic reconnection handling
///    - Supports horizontal scaling with backplane (Redis, Azure SignalR)
/// 
/// 2. CONNECTION MANAGEMENT:
///    - Each client gets a unique ConnectionId
///    - Automatic cleanup on disconnect
///    - Efficient message routing to specific connections
/// 
/// 3. INTEGRATION WITH OUR SYSTEM:
///    - PriceSubscriptionManager tracks subscriptions per ConnectionId
///    - PriceBroadcastService uses ConnectionIds for targeted broadcasting
///    - Single external connection per instrument serves all SignalR clients
/// 
/// 4. MESSAGE BROADCASTING:
///    - SignalR's Clients.Clients() efficiently sends to multiple connections
///    - Optimized for bulk operations (1000+ recipients)
///    - Uses WebSocket frames efficiently to minimize overhead
/// </summary>
public class PriceHub(
    IPriceSubscriptionManager subscriptionManager,
    ILogger<PriceHub> logger) : Hub
{
    // PERFORMANCE: SignalR handles connection lifecycle efficiently
    // Each connection is lightweight - 1000+ connections are manageable
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    // PERFORMANCE: Automatic cleanup on disconnect
    // Removes all subscriptions for this connection - prevents memory leaks
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        
        // PERFORMANCE: O(n) where n = number of symbols this connection subscribed to
        // Typically n is small (1-5 symbols), so this is very fast
        // Ensures proper cleanup and subscription count management
        await subscriptionManager.UnsubscribeAllAsync(Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to live price updates for a specific financial instrument
    /// </summary>
    public async Task Subscribe(string symbol)
    {
        try
        {
            logger.LogInformation("Subscribe request from {ConnectionId} for symbol: {Symbol}", 
                Context.ConnectionId, symbol);
            
            if (string.IsNullOrWhiteSpace(symbol))
            {
                await Clients.Caller.SendAsync("Error", "Symbol is required");
                return;
            }
            
            await subscriptionManager.SubscribeAsync(Context.ConnectionId, symbol);
            await Clients.Caller.SendAsync("Subscribed", symbol);
            
            logger.LogDebug("Client {ConnectionId} subscribed to {Symbol}", Context.ConnectionId, symbol);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error subscribing {ConnectionId} to {Symbol}", Context.ConnectionId, symbol);
            await Clients.Caller.SendAsync("Error", $"Failed to subscribe to {symbol}: {ex.Message}");
        }
    }

    /// <summary>
    /// Unsubscribe from price updates for a specific financial instrument
    /// </summary>
    public async Task Unsubscribe(string symbol)
    {
        try
        {
            logger.LogInformation("Unsubscribe request from {ConnectionId} for symbol: {Symbol}", 
                Context.ConnectionId, symbol);
            
            await subscriptionManager.UnsubscribeAsync(Context.ConnectionId, symbol);
            await Clients.Caller.SendAsync("Unsubscribed", symbol);
            
            logger.LogDebug("Client {ConnectionId} unsubscribed from {Symbol}", Context.ConnectionId, symbol);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unsubscribing {ConnectionId} from {Symbol}", Context.ConnectionId, symbol);
            await Clients.Caller.SendAsync("Error", $"Failed to unsubscribe from {symbol}: {ex.Message}");
        }
    }

    /// <summary>
    /// Get list of currently subscribed symbols for this connection
    /// </summary>
    public async Task GetSubscriptions()
    {
        try
        {
            var symbols = await subscriptionManager.GetSubscribedSymbolsAsync(Context.ConnectionId);
            await Clients.Caller.SendAsync("Subscriptions", symbols);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting subscriptions for {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", $"Failed to get subscriptions: {ex.Message}");
        }
    }
}

/// <summary>
/// Background service that broadcasts price updates to all subscribed WebSocket clients.
/// 
/// PERFORMANCE OPTIMIZATION FOR 1000+ SUBSCRIBERS:
/// ===============================================
/// This service is the distribution layer that efficiently broadcasts to all subscribers:
/// 
/// 1. EVENT-DRIVEN ARCHITECTURE:
///    - Listens to PriceService.PriceUpdated event
///    - Single price update from external provider triggers one broadcast
///    - No polling or active checking - zero overhead when no updates
/// 
/// 2. EFFICIENT SUBSCRIBER LOOKUP:
///    - O(1) lookup to get subscriber list for a symbol
///    - Uses ConcurrentDictionary - instant even with 1000+ subscribers
///    - Only enumerates subscribers for the specific symbol that updated
/// 
/// 3. SIGNALR BULK BROADCASTING:
///    - SignalR's Clients.Clients() is optimized for bulk operations
///    - Efficiently serializes and sends to 1000+ connections
///    - Uses WebSocket frames efficiently - minimal overhead per message
///    - Can handle thousands of concurrent sends
/// 
/// 4. SINGLE MESSAGE, MULTIPLE RECIPIENTS:
///    - One price update from Binance = one broadcast operation
///    - SignalR handles the distribution to all 1000+ subscribers
///    - No per-subscriber loops or individual sends
/// 
/// 5. SCALABILITY:
///    - Performance scales linearly with subscriber count
///    - Can easily handle 10,000+ subscribers with proper infrastructure
///    - For horizontal scaling, use SignalR backplane (Redis, Azure SignalR)
/// </summary>
public class PriceBroadcastService(
    IPriceService priceService,
    IPriceSubscriptionManager subscriptionManager,
    IHubContext<PriceHub> hubContext,
    ILogger<PriceBroadcastService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PERFORMANCE: Event subscription - zero overhead when no updates
        // Only triggered when external provider sends price update
        priceService.PriceUpdated += OnPriceUpdated;
        logger.LogInformation("PriceBroadcastService started");
        return Task.CompletedTask;
    }

    private async void OnPriceUpdated(object? sender, Price price)
    {
        try
        {
            // PERFORMANCE CRITICAL: O(1) subscriber lookup
            // ============================================
            // This is the key to handling 1000+ subscribers efficiently:
            // - Instant lookup of all connection IDs subscribed to this symbol
            // - Returns IEnumerable for lazy evaluation
            // - Even with 1000+ subscribers, lookup is constant time
            var subscribers = subscriptionManager.GetSubscribersForSymbol(price.Symbol);
            var subscriberList = subscribers.ToList();
            
            if (subscriberList.Count == 0)
            {
                logger.LogDebug("No subscribers for {Symbol}, skipping broadcast", price.Symbol);
                return;
            }
            
            logger.LogDebug("Broadcasting price update for {Symbol} to {Count} subscribers", 
                price.Symbol, subscriberList.Count);
            
            var priceDto = new PriceDto
            {
                Symbol = price.Symbol,
                Value = price.Value,
                Timestamp = price.Timestamp
            };
            
            // PERFORMANCE CRITICAL: SignalR bulk broadcast
            // ============================================
            // SignalR's Clients.Clients() is highly optimized for bulk operations:
            // - Efficiently serializes message once
            // - Distributes to all 1000+ connections in parallel
            // - Uses WebSocket frames efficiently
            // - Can handle 10,000+ concurrent sends with proper infrastructure
            // - For horizontal scaling, use SignalR backplane (Redis/Azure SignalR)
            await hubContext.Clients.Clients(subscriberList).SendAsync("PriceUpdate", priceDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting price update for {Symbol}", price.Symbol);
        }
    }

    public override void Dispose()
    {
        priceService.PriceUpdated -= OnPriceUpdated;
        base.Dispose();
    }
}

