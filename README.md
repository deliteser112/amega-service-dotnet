# Financial Instrument Price Service

A high-performance REST API and WebSocket service for streaming live financial instrument prices, built with Clean Architecture principles. The service efficiently handles 1000+ concurrent WebSocket subscribers with a single connection to the external data provider per instrument.

## Overview

This service provides:
- **REST API** endpoints to query available financial instruments and current prices
- **WebSocket (SignalR)** endpoints for real-time price streaming
- **Efficient subscription management** that maintains a single external connection per instrument, regardless of subscriber count
- **Support for multiple instruments**: EURUSD, USDJPY, BTCUSD (extensible to more)

## Architecture

The solution follows **Clean Architecture** principles with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                        Web Layer                            │
│  (Hosting, Configuration, Dependency Injection)             │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                      API Layer                              │
│  (Controllers, SignalR Hubs, Background Services)           │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                  Application Layer                          │
│  (Use Cases, DTOs, Services, Repositories, Infrastructure)  │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                      Core Layer                             │
│  (Domain Entities, Interfaces)                              │
└─────────────────────────────────────────────────────────────┘
```

### Project Structure

```
amega.service.test/
├── src/
│   ├── Core/                          # Domain Layer
│   │   ├── Entities/                  # Domain entities (FinancialInstrument, Price)
│   │   └── Interfaces/                # Repository and service interfaces
│   │       ├── Repositories/          # IInstrumentRepository
│   │       ├── Services/              # IPriceService, IPriceSubscriptionManager
│   │       └── Infrastructure/        # IExternalPriceProvider
│   │
│   ├── Application/                   # Application Layer
│   │   ├── DTOs/                      # Data Transfer Objects
│   │   ├── UseCases/                  # Business logic (GetInstrumentsUseCase, GetCurrentPriceUseCase)
│   │   ├── Services/                  # Application services
│   │   │   ├── PriceService.cs        # Manages price caching and streaming
│   │   │   └── PriceSubscriptionManager.cs  # Manages WebSocket subscriptions
│   │   ├── Repositories/              # Repository implementations
│   │   └── Infrastructure/            # External service integrations
│   │       └── BinancePriceProvider.cs  # Binance WebSocket client
│   │
│   ├── Api/                           # Presentation Layer
│   │   ├── Controllers/               # REST API controllers
│   │   │   ├── InstrumentsController.cs
│   │   │   └── PricesController.cs
│   │   ├── Hubs/                      # SignalR hubs
│   │   │   └── PriceHub.cs            # WebSocket hub for price streaming
│   │   └── ApiServiceCollection.cs    # Dependency injection configuration
│   │
│   └── Web/                           # Hosting Layer
│       ├── Program.cs                 # Application entry point
│       ├── Startup.cs                 # Service configuration
│       └── appsettings.json           # Configuration
│
└── test/
    └── PriceClient/                   # SignalR Test Client
        ├── Program.cs                 # Console client application
        └── README.md                  # Client usage instructions
```

## Key Features

### Performance Optimizations for 1000+ Subscribers

The service is designed to efficiently handle 1000+ concurrent WebSocket subscribers:

1. **Single Connection Per Instrument**
   - Maintains ONE WebSocket connection to external provider per instrument
   - 1000 subscribers to BTCUSD = 1 Binance connection (not 1000!)
   - Prevents connection exhaustion and API rate limiting

2. **Efficient Subscription Management**
   - Uses `ConcurrentDictionary` for O(1) subscription lookups
   - Thread-safe operations without locking
   - Dual-index structure for fast connection→symbol and symbol→connection lookups

3. **Event-Driven Broadcasting**
   - Single price update triggers one broadcast operation
   - SignalR efficiently distributes to all subscribers
   - No polling or active checking overhead

4. **Scalability**
   - Can handle 10,000+ subscribers with proper infrastructure
   - Supports horizontal scaling with SignalR backplane (Redis, Azure SignalR)

## Prerequisites

- .NET 9.0 SDK or later
- Internet connection (for Binance WebSocket API)

## Getting Started

### 1. Clone and Build

```bash
# Clone the repository (if applicable)
# cd to the project directory

# Restore dependencies and build
dotnet restore
dotnet build
```

### 2. Run the Service

```bash
# Navigate to Web project
cd src/Web

# Run the service
dotnet run
```

The service will start on:
- **HTTP**: `http://localhost:5120` (or check `launchSettings.json` for the configured port)
- **Swagger UI**: `http://localhost:5120/swagger` (in Development mode)

### 3. Test REST API Endpoints

#### Get Available Instruments
```bash
curl http://localhost:5120/api/instruments
```

Response:
```json
[
  {
    "symbol": "EURUSD",
    "name": "Euro/US Dollar",
    "type": "Forex"
  },
  {
    "symbol": "USDJPY",
    "name": "US Dollar/Japanese Yen",
    "type": "Forex"
  },
  {
    "symbol": "BTCUSD",
    "name": "Bitcoin/US Dollar",
    "type": "Crypto"
  }
]
```

#### Get Current Price
```bash
curl http://localhost:5120/api/prices?symbol=BTCUSD
```

Response:
```json
{
  "symbol": "BTCUSD",
  "value": 43250.50,
  "timestamp": "2024-01-15T14:23:45.123Z"
}
```

### 4. Test WebSocket (SignalR) Client

The test client is a console application that connects to the SignalR hub and displays real-time price updates.

#### Run the Test Client

```bash
# From the project root
dotnet run --project test/PriceClient/PriceClient.csproj
```

#### Specify Symbol and Server URL

```bash
# Subscribe to BTCUSD (default)
dotnet run --project test/PriceClient/PriceClient.csproj -- BTCUSD

# Subscribe to BTCUSD with custom server URL
dotnet run --project test/PriceClient/PriceClient.csproj -- BTCUSD http://localhost:5120
```

#### Example Output

```
=== Financial Instrument Price Client ===
Connecting to server: http://localhost:5120
Subscribing to symbol: BTCUSD
Press Ctrl+C to exit

[✓] Connected to server. Connection ID: abc123
[✓] Successfully subscribed to BTCUSD
[14:23:45.123] BTCUSD: $43,250.50
[14:23:45.456] BTCUSD: $43,251.00
[14:23:45.789] BTCUSD: $43,250.75
```

## API Documentation

### REST API Endpoints

#### GET `/api/instruments`
Returns a list of all available financial instruments.

**Response:** `200 OK`
```json
[
  {
    "symbol": "EURUSD",
    "name": "Euro/US Dollar",
    "type": "Forex"
  }
]
```

#### GET `/api/prices?symbol={symbol}`
Returns the current price for a specific instrument.

**Parameters:**
- `symbol` (query string, required): The instrument symbol (e.g., BTCUSD, EURUSD)

**Response:** `200 OK`
```json
{
  "symbol": "BTCUSD",
  "value": 43250.50,
  "timestamp": "2024-01-15T14:23:45.123Z"
}
```

**Error Responses:**
- `400 Bad Request`: Symbol parameter is missing
- `404 Not Found`: Price not found for the symbol
- `500 Internal Server Error`: Server error

### WebSocket (SignalR) API

#### Hub Endpoint: `/priceHub`

**Connection:**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5120/priceHub")
    .build();
```

#### Methods

**Subscribe to Symbol**
```javascript
connection.invoke("Subscribe", "BTCUSD");
```

**Unsubscribe from Symbol**
```javascript
connection.invoke("Unsubscribe", "BTCUSD");
```

**Get Current Subscriptions**
```javascript
connection.invoke("GetSubscriptions");
```

#### Events

**PriceUpdate**
```javascript
connection.on("PriceUpdate", (price) => {
    console.log(`${price.symbol}: $${price.value} at ${price.timestamp}`);
});
```

**Subscribed**
```javascript
connection.on("Subscribed", (symbol) => {
    console.log(`Subscribed to ${symbol}`);
});
```

**Unsubscribed**
```javascript
connection.on("Unsubscribed", (symbol) => {
    console.log(`Unsubscribed from ${symbol}`);
});
```

**Error**
```javascript
connection.on("Error", (error) => {
    console.error(`Error: ${error}`);
});
```

## Configuration

### Application Settings

Edit `src/Web/appsettings.json` to configure:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Port Configuration

Edit `src/Web/Properties/launchSettings.json` to change the port:

```json
{
  "applicationUrl": "http://localhost:5120"
}
```

## Supported Instruments

Currently supported instruments:

- **BTCUSD** - Bitcoin/US Dollar (via Binance WebSocket)
- **EURUSD** - Euro/US Dollar (placeholder - add provider)
- **USDJPY** - US Dollar/Japanese Yen (placeholder - add provider)

### Adding New Instruments

1. Add the instrument to `Application/Repositories/InstrumentRepository.cs`
2. Add symbol mapping in `Application/Infrastructure/BinancePriceProvider.cs` (if using Binance)
3. Implement provider support if using a different data source

## Performance Characteristics

### Subscription Management
- **Lookup Time**: O(1) - Constant time regardless of subscriber count
- **Memory**: O(n) where n = number of connections
- **Thread Safety**: Lock-free operations using `ConcurrentDictionary`

### Broadcasting
- **Single Price Update**: One broadcast operation for all subscribers
- **SignalR Optimization**: Efficient bulk message distribution
- **Scalability**: Supports 10,000+ subscribers with proper infrastructure

### External Connections
- **Per Instrument**: One WebSocket connection per instrument
- **Connection Reuse**: All subscribers share the same connection
- **Resource Efficiency**: Prevents connection exhaustion

## Logging

The service logs to console (stdout) with the following levels:

- **Information**: Connection events, subscriptions, price updates
- **Warning**: Connection issues, missing prices
- **Error**: Exceptions and failures
- **Debug**: Detailed operation information

## Troubleshooting

### Price Not Found Error

If you get `"Price not found for symbol: BTCUSD"`:

1. **Check server logs** - Look for connection errors
2. **Verify network connectivity** - Ensure server can reach Binance WebSocket
3. **Wait a few seconds** - First price may take 5-10 seconds to arrive
4. **Check symbol support** - Ensure the symbol is supported (currently only BTCUSD via Binance)

### WebSocket Connection Issues

1. **Verify server is running** - Check the service is started
2. **Check CORS settings** - If connecting from browser, ensure CORS is configured
3. **Verify hub endpoint** - Ensure connecting to `/priceHub`
4. **Check firewall** - Ensure WebSocket connections are allowed

### No Price Updates

1. **Check subscription** - Verify you called `Subscribe` method
2. **Check server logs** - Look for subscription confirmations
3. **Verify external provider** - Check if Binance connection is established
4. **Check symbol** - Ensure symbol is supported and active

## Development

### Building the Solution

```bash
dotnet build
```

### Running Tests

```bash
# Run the test client
dotnet run --project test/PriceClient/PriceClient.csproj
```

### Project Dependencies

- **Core**: No dependencies (pure domain layer)
- **Application**: Depends on Core
- **Api**: Depends on Application
- **Web**: Depends on Api and Application

## Architecture Decisions

### Why Clean Architecture?

- **Separation of Concerns**: Clear boundaries between layers
- **Testability**: Easy to unit test each layer independently
- **Maintainability**: Changes in one layer don't affect others
- **Flexibility**: Easy to swap implementations (e.g., different price providers)

### Why Single Connection Per Instrument?

- **Resource Efficiency**: Prevents connection exhaustion
- **API Rate Limits**: Avoids hitting external API limits
- **Cost Optimization**: Reduces network and server resources
- **Scalability**: Can handle thousands of subscribers with minimal connections

### Why SignalR?

- **Built-in Scalability**: Designed for thousands of concurrent connections
- **Automatic Reconnection**: Handles connection failures gracefully
- **Multiple Transports**: Falls back to long polling if WebSocket unavailable
- **Efficient Broadcasting**: Optimized for bulk message distribution

## Future Enhancements

- [ ] Support for more instruments (EURUSD, USDJPY via different providers)
- [ ] Redis backplane for horizontal scaling
- [ ] Authentication and authorization
- [ ] Rate limiting
- [ ] Metrics and monitoring
- [ ] Database persistence for historical prices
- [ ] GraphQL API option

## License

[Specify your license here]

## Contributing

[Contributing guidelines if applicable]

