using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace PriceClient;

class Program
{
    private static HubConnection? _connection;
    private static string _symbol = "BTCUSD";
    private static string _serverUrl = "http://localhost:5120";

    static async Task Main(string[] args)
    {
        // Parse command line arguments
        if (args.Length > 0)
        {
            _symbol = args[0].ToUpper();
        }

        if (args.Length > 1)
        {
            _serverUrl = args[1];
        }

        Console.WriteLine("=== Financial Instrument Price Client ===");
        Console.WriteLine($"Connecting to server: {_serverUrl}");
        Console.WriteLine($"Subscribing to symbol: {_symbol}");
        Console.WriteLine("Press Ctrl+C to exit\n");

        // Build SignalR connection
        _connection = new HubConnectionBuilder()
            .WithUrl($"{_serverUrl}/priceHub")
            .WithAutomaticReconnect()
            .Build();

        // Setup event handlers
        _connection.On<string>("Subscribed", (symbol) =>
        {
            Console.WriteLine($"[✓] Successfully subscribed to {symbol}");
        });

        _connection.On<string>("Unsubscribed", (symbol) =>
        {
            Console.WriteLine($"[✓] Unsubscribed from {symbol}");
        });

        _connection.On<string>("Error", (error) =>
        {
            Console.WriteLine($"[✗] Error: {error}");
        });

        _connection.On<PriceUpdate>("PriceUpdate", (price) =>
        {
            Console.WriteLine($"[{price.Timestamp:HH:mm:ss.fff}] {price.Symbol}: ${price.Value:N2}");
        });

        _connection.On<IEnumerable<string>>("Subscriptions", (symbols) =>
        {
            Console.WriteLine($"[Info] Current subscriptions: {string.Join(", ", symbols)}");
        });

        // Connection event handlers
        _connection.Reconnecting += (error) =>
        {
            Console.WriteLine($"[!] Connection lost. Reconnecting... {error?.Message}");
            return Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            Console.WriteLine($"[✓] Reconnected. Connection ID: {connectionId}");
            // Resubscribe after reconnection
            if (_connection.State == HubConnectionState.Connected)
            {
                _ = _connection.InvokeAsync("Subscribe", _symbol);
            }
            return Task.CompletedTask;
        };

        _connection.Closed += (error) =>
        {
            Console.WriteLine($"[✗] Connection closed. {error?.Message}");
            return Task.CompletedTask;
        };

        try
        {
            // Connect to server
            await _connection.StartAsync();
            Console.WriteLine($"[✓] Connected to server. Connection ID: {_connection.ConnectionId}\n");

            // Subscribe to the symbol
            await _connection.InvokeAsync("Subscribe", _symbol);

            // Keep the application running
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[✗] Connection error: {ex.Message}");
            Console.WriteLine($"Make sure the server is running at {_serverUrl}");
        }
        finally
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }
}

// DTO for price updates from SignalR
public class PriceUpdate
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime Timestamp { get; set; }
}

