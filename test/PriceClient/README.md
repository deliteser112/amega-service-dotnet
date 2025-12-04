# Price Client - SignalR WebSocket Test Client

A simple console application to test the WebSocket price streaming functionality.

## Usage

### Basic Usage (Default: BTCUSD)
```bash
dotnet run --project test/PriceClient/PriceClient.csproj
```

### Specify Symbol
```bash
dotnet run --project test/PriceClient/PriceClient.csproj -- BTCUSD
```

### Specify Symbol and Server URL
```bash
dotnet run --project test/PriceClient/PriceClient.csproj -- BTCUSD http://localhost:5120
```

## Features

- Connects to the SignalR PriceHub
- Subscribes to live price updates for a specified instrument
- Displays real-time price updates with timestamps
- Automatic reconnection on connection loss
- Resubscribes after reconnection

## Example Output

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

## Requirements

- The Web server must be running on the specified URL (default: http://localhost:5120)
- The server must have the PriceHub configured at `/priceHub`

