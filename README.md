# Valkyrie

Valkyrie is an in-memory limit-order book and matching engine built with C# and .NET 10.

It includes custom FIFO and pro-rata matching algorithms, multi-instrument order books, an ASP.NET Core API, live WebSocket market data, a synthetic market simulator, and an Angular trading dashboard.

No exchange or order-book libraries are used. The book structures, matching logic, allocation mathematics, and market-data pipeline are implemented from scratch.

---

## Features

### Matching engine

- Multi-instrument limit-order books
- Matching algorithms: FIFO, pro-rata
- Partial and multi-level fills
- Order placement, modification, and cancellation
- Aggregated bid/ask snapshots
- Thread-safe API access through `OrderGateway`

### Market data

- Live WebSocket market-data feed (currently all seeded from the server, not ACTUAL market data for those instruments)
- Full order-book snapshots
- Trade events containing bid and ask order IDs
- Per-instrument subscriptions
- Synthetic multi-instrument market simulator

### Angular dashboard

- Live depth ladder
- Best bid, best ask, spread, and total depth
- Rolling mid-price traces
- Live trade tape
- Buy and sell limit-order entry
- Working-order and partial-fill tracking
- Order cancellation
- Trade history
- Buy and sell VWAP(Volume Weighted Average Price)
- Net executed quantity
- Maker participation percentage
- Persistent light and dark theme preference

---

## Matching algorithms

The active algorithm is selected in `Valkyrie/appsettings.json`:

```json
{
  "MatchingEngineConfiguration": {
    "Algorithm": "Fifo"
  }
}
```

Supported values (for now) are:

- `Fifo`
- `ProRata`

### FIFO

FIFO uses price-time priority. The best available price is matched first, and orders at the same price are filled in arrival order.

### Pro-rata

Pro-rata distributes incoming quantity across resting orders at the best price in proportion to their sizes.

```text
Resting asks at $1.00:

Order #2: 100 shares
Order #3: 200 shares
Order #4: 300 shares
Total:    600 shares

Incoming buy: 300 shares at $1.00

Fills:

Order #2:  50 shares
Order #3: 100 shares
Order #4: 150 shares
```

Fractional allocation remainders are distributed using largest-remainder allocation, with time priority used to break ties.

---

## Design decisions

### Integer prices

Prices are represented as `long` integer cents rather than floating-point values. This keeps price comparisons and executions deterministic and avoids floating-point drift in the matching path.

### Order-book structure

Each instrument uses:

- `SortedSet<Limit>` for ordered bid and ask price levels
- `Dictionary<long, OrderbookEntry>` for direct order lookup
- Doubly linked entries within each price level

This provides O(log n) price-level insertion and lookup, O(1) order lookup, and O(1) linked-list cancellation after lookup.

### Resting-price execution

Crossing orders execute at the resting order's price. For example, a buy order priced at `$101.00` crossing a resting sell at `$100.00` executes at `$100.00`.

### Full-book snapshots

Market-data book messages contain the complete aggregated book rather than incremental deltas.

Full snapshots consume more bandwidth, but they are stateless and self-healing: newly connected or temporarily disconnected clients can become correct from a single message.

### Execution journal

Trader executions are written to an append-only, in-memory execution journal.

The journal supports session-scoped execution history and analytics while keeping the matching engine independent of dashboard concerns. Execution history survives browser refreshes but is cleared when the backend process restarts.

---

## Architecture

```text
TradingEngineServer/
|-- Instruments/       Instrument reference data
|-- Orders/            Orders, limits, comparers, and linked entries
|-- OrderBook/         Bid/ask book implementation
|-- MatchingEngine/    FIFO and pro-rata matching
|-- Logging/           Asynchronous text logging
|-- Valkyrie/          ASP.NET Core API and simulator
|-- Frontend/          Angular trading dashboard
`-- UnitTests/         Domain, matching-engine, and API tests
```


The matching engine remains responsible only for matching. API transport, market-data publication, simulator flow, and session execution tracking are handled outside the core algorithm.

---

## Requirements

Install:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22](https://nodejs.org/)
- npm

---

## Running locally

The server and client run as separate processes.

### 1. Start the server 

From the repository root:

```powershell
dotnet restore Valkyrie.slnx
dotnet run --project .\Valkyrie
```

It runs at:

```text
http://localhost:5000
```

### 2. Start with synthetic market data

The simulator is disabled by default. Enable it with the checked-in launch profile:

```powershell
dotnet run --project .\Valkyrie --launch-profile simulated-market
```

In Rider, add the following environment variable to the Valkyrie run configuration:

```text
MarketSimulatorConfiguration__Enabled=true
```

### 3. Start the Angular dashboard

Open a second terminal:

```powershell
cd .\Frontend
npm ci
npm start
```

Open:

```text
http://localhost:4200
```

The Angular development server proxies REST and WebSocket requests to the backend on port `5000`. Both processes must be running for the complete dashboard to work.

---

## Configuration

Instrument and simulator configuration lives in `Valkyrie/appsettings.json`.

```json
{
  "MatchingEngineConfiguration": {
    "Algorithm": "Fifo"
  },
  "Instruments": [
    {
      "SecurityId": 1,
      "Symbol": "MSFT"
    },
    {
      "SecurityId": 2,
      "Symbol": "AAPL"
    },
    {
      "SecurityId": 3,
      "Symbol": "SPCX"
    }
  ],
  "MarketSimulatorConfiguration": {
    "Enabled": false,
    "Username": "mm",
    "Instruments": [
      {
        "SecurityId": 1,
        "SeedPrice": 41800,
        "TickSize": 1,
        "OrdersPerSecond": 4.0,
        "BookDepth": 6
      }
    ]
  }
}
```

Prices are expressed in integer cents:

```text
41800 = $418.00
```

---

## REST API

The API is available at `http://localhost:5000`.

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/orders` | Submit an order |
| `PUT` | `/instruments/{securityId}/orders/{orderId}` | Modify an order |
| `DELETE` | `/instruments/{securityId}/orders/{orderId}?username={username}` | Cancel an order |
| `GET` | `/book/{securityId}` | Read an aggregated book snapshot |
| `POST` | `/sessions` | Create a browser trading session |
| `GET` | `/sessions/{sessionId}/executions` | Read session trades |
| `GET` | `/sessions/{sessionId}/executions?securityId={id}` | Filter session trades by instrument |

Prices are integer cents and order sides are `"Buy"` or `"Sell"`.

### Submit an order

```powershell
$body = @{
    securityId = 1
    username   = "sam"
    side       = "Sell"
    price      = 41800
    quantity   = 100
} | ConvertTo-Json

Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:5000/orders" `
    -ContentType "application/json" `
    -Body $body
```

Example response:

```json
{
  "orderId": 1,
  "matched": false,
  "fills": []
}
```

### Read the order book

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/book/1"
```

### Create a trading session

```powershell
$session = Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:5000/sessions" `
    -ContentType "application/json" `
    -Body "{}"

$session
```

---

## WebSocket market data

The market-data WebSocket is available at:

```text
ws://localhost:5000/ws/marketdata
```

Clients subscribe by instrument:

```json
{
  "action": "subscribe",
  "securityId": 1
}
```

The server publishes two message types:

- `book`: an aggregated bid/ask snapshot
- `trade`: a completed match

---

## Session trades

The dashboard creates a browser-scoped trading session and attaches its ID to submitted orders.

The Session Trades panel shows:

- Individual fills
- Order side
- Execution price and quantity
- Order ID
- Maker or taker role
- Buy and sell VWAP
- Net executed quantity
- Maker participation percentage

The browser retains the session ID through page refreshes using `sessionStorage`.

The execution journal is currently in memory, so restarting the backend clears its history(for now...).

---

## Market simulator

The synthetic market source runs one order-flow loop per configured instrument.

It generates:

- Resting liquidity
- Cancellations
- Crossing orders
- Partial fills
- Price movement around a simulated fair value
- Poisson-style arrival timing

Simulator orders travel through the same `OrderGateway` and matching engine as API orders.

The simulator is implemented behind `IMarketDataSource`, allowing other sources, such as historical market-data replay, to be added without changing the host or matching engine.

---

## Logs

Text logs are written under `logs/` in date-stamped directories.

Tail the newest log in PowerShell:

```powershell
$latestLog = Get-ChildItem "logs/*/*.log" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

Get-Content $latestLog.FullName -Wait
```

---

## Building and testing

### Complete .NET solution

```powershell
dotnet restore Valkyrie.slnx
dotnet build Valkyrie.slnx --no-restore
dotnet test Valkyrie.slnx --no-build
```

Stop any running Valkyrie backend before building or testing on Windows. A running process can lock compiled DLLs.

### Angular frontend

```powershell
cd .\Frontend
npm ci
npm run build
npm test -- --watch=false
```

---
## Technology

| Layer | Technology |
|---|---|
| Runtime | .NET 10 |
| Backend | ASP.NET Core Minimal APIs |
| Language | C# |
| Matching | Custom FIFO and pro-rata algorithms |
| Frontend | Angular 21 |
| Frontend language | TypeScript 5.9 |
| Reactive state | Angular signals and RxJS |
| Testing | xUnit, FluentAssertions, and Vitest |
| Transport | HTTP and WebSocket |
| Hosting | Kestrel |

---

## Current limitations

- Application state is held in memory
- Executions are cleared when the backend restarts
- Username is caller-supplied; authentication is not implemented
- Market-data updates use full snapshots rather than deltas
- Order IDs restart with the backend process
- Session execution updates currently use HTTP refreshes rather than a private execution stream
- The simulator uses synthetic rather than historical market data
