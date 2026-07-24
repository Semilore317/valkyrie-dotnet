# Valkyrie-dotnet

A limit order book and matching engine i'm building C# on **.NET 10**. 
It uses FIFO and pro-rata matching, price-time priority, an async logging pipeline, and a REST API over ASP.NET Core.
There are no exchange libraries: the book, the matching loop, and the allocation math are all custom.

---

## What is it?

An in-memory trading engine that maintains a live bid/ask book per instrument and matches incoming orders against it. I built it to work through the fundamentals of electronic trading infrastructure: order-book data structures, matching algorithms, and the low-latency-minded design choices that come with them.

Two matching algorithms are implemented, selectable from config:

- **FIFO (price-time priority)**: the oldest resting order at a price level fills first.
- **Pro-rata**: incoming volume is split across resting orders relative to their size.

---

## How Matching Works

**Pro-rata** allocates an incoming order across resting orders by *size*, not arrival time, the same way most futures markets fill:

```
Resting asks @ $1.00 :  #2 = 100    #3 = 200    #4 = 300      ==> 600 total
Incoming buy         :  300 @ $1.00

Fills                :  #2 → 50     #3 → 100    #4 → 150
                        each resting order gets 50% of its size; leftover lots are
                        handed out by largest fractional remainder, tie-broken by
                        time priority (FIFO)
```

Flipping `MatchingEngineConfiguration.Algorithm` to `Fifo` makes that same incoming 300 fill the oldest resting order to completion first, then the next, without any proportional split.

---

## Design decisions

The choices that shaped the engine, and why:

- **Prices as `long` cents, not `decimal`/`double`.** No floating-point drift anywhere in the matching path... comparisons and fills are exact integer math.
- **`OrderbookEntry : Order` *is* the linked-list node.** The order carries its own `Next`/`Previous` pointers, so cancels are O(1) pointer splices with zero wrapper allocations.
- **`SortedSet<Limit>` + `Dictionary<orderId, entry>`.** O(log n) to reach the best bid/ask price level, O(1) to find or cancel any order by id.
- **Pro-rata uses largest-remainder allocation.** Fractional lots are distributed deterministically instead of silently dropped, and ties fall back to time priority.
- **Async logging off the hot path.** Log calls just enqueue onto a thread-safe `BufferBlock`; a background writer drains it to disk and flushes cleanly on shutdown, keeping file I/O out of the execution threads.

---

## Architecture

The solution is split into focused projects so instrument data, order state, book mechanics, and matching never bleed into each other:

```
Valkyrie/ (solution root)
├── Instruments/          Security reference data (id + ticker) e.g (1, AAPL)
├── Orders/               Order / limit domain models, comparers, linked-list node
├── OrderBook/            Per-instrument bid/ask book + tiered interfaces
├── MatchingEngine/       Multi-book orchestrator + FIFO / pro-rata algorithms
├── Logging/              Async logger (only text is supported for now in .log files)
├── UnitTests/            xUnit + FluentAssertions (+ WebApplicationFactory API tests)
└── Valkyrie/             DI wiring + REST API host (Api/ = endpoints, gateway, DTOs, Simulations)
```

**Order book.** Price levels live in a `SortedSet<Limit>` ordered by `BidLimitComparer` (descending) and `AskLimitComparer` (ascending), so the best bid/ask is always `Min`. Every order is additionally indexed in a `Dictionary<long, OrderbookEntry>` for O(1) lookup and cancel.

**Interface tiers.** `IReadonlyOrderBook → IOrderEntryOrderBook → IRetrievalOrderBook → IMatchingOrderBook` widen access one step at a time. A caller takes the narrowest interface it needs: read-only consumers can't mutate, and the fully-mutable book stays internal to the matching path.

**Matching engine.** Holds a dictionary of order books keyed by `SecurityId`, so a single engine serves the whole venue. Algorithms implement `IMatchingAlgorithm` as stateless singletons and are resolved from config via DI: swapping matching algorithms is a one-line settings change in `appsettings.json`

**REST gateway.** The `Valkyrie` host runs on ASP.NET Core (Kestrel) and maps the endpoints below onto the engine. Endpoints don't call the engine directly; they go through `OrderGateway`, which holds one lock around every engine call and assigns order ids. That lock is what keeps concurrent requests from stepping on the book.

---

## Running it

Requires the **.NET 10 SDK**.

```bash
dotnet build
dotnet test          
dotnet run --project Valkyrie/Valkyrie.csproj
```

Pick the matching algorithm in `Valkyrie/appsettings.json`:

```json
"MatchingEngineConfiguration": {
  "Algorithm": "Fifo"      // or "ProRata"
}
```

This starts the host on `http://localhost:5000`, seeds the instruments listed in `appsettings.json`, and exposes the REST API below.
A live WebSocket feed is exposed at `ws://localhost:500/ws/marketdata`; a dashboard is still on the roadmap

### Logs

Written to date-stamped folders under `logs/`. Tail the most recent one live in PowerShell:

```powershell
Get-Content -Path (Get-ChildItem "logs/*/*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1) -Wait
```

---

## REST API

With the host running, four endpoints are live on `http://localhost:5000`. Prices are **integer cents** and `side` is `"Buy"` or `"Sell"`. Instruments are seeded at startup from `appsettings.json` (`1 = MSFT`, `2 = AAPL`, `3 = SPCX`).

| Method   | Route                                                   | Purpose                                                     |
|----------|---------------------------------------------------------|-------------------------------------------------------------|
| `POST`   | `/orders`                                               | Place an order; returns the assigned id and any fills       |
| `PUT`    | `/instruments/{securityId}/orders/{id}`                 | Modify a resting order (cancel-and-replace)                 |
| `DELETE` | `/instruments/{securityId}/orders/{id}?username={user}` | Cancel a resting order                                       |
| `GET`    | `/book/{securityId}`                                     | Read the aggregated bid/ask book                            |

Cancel and modify live under `/instruments/{securityId}` so each order has a single URL. `POST` assigns the id; the client never sends one.

## WebSocket Feed
A live market-data feed runs alongside the REST API at `ws://localhost:5000/ws/marketdata`.
Clients subscribe per instrument, receive a book snapshot, then get a push on every change. One socket carries two logical
streams, tagged by a `type` to demux the socket client-side.

### Try it from the Browser Console
```js
const ws = new WebSocket('ws://localhost:5000/ws/marketdata');
ws.onopen    = () => { console.log('OPEN'); ws.send(JSON.stringify({ action: 'subscribe', securityId: 1 })); };
ws.onmessage = e => console.log('MSG', JSON.parse(e.data));
ws.onerror   = () => console.log('ERR');
```
Then place a crossing order over REST and watch the `book` and `trade` frames appear.

```bash
# rest a sell, then read the book
curl -X POST localhost:5000/orders -H "Content-Type: application/json" \
  -d '{"securityId":1,"username":"sam","side":"Sell","price":10000,"quantity":100}'
# output -> {"orderId":1,"matched":false,"fills":[]}

curl localhost:5000/book/1
# output -> {"securityId":1,"bid":null,"ask":10000,"spread":null,"bids":[],"asks":[{"price":10000,"quantity":100}]}
```

## Market Simulator
A background service seeds and drives synthetic order flow through the same `OrderGateway` the REST API uses
, so the book and trade tape stay live with no manual input. Each intrument gets its own loop; a random fair value plus
a Poisson arrival process for order timing, mixing resting liquidity, cancels and crossing orders.

The simulator is disabled by default so API tests remain deterministic. You can enable it locally with the checked-in launch profile:

```powershell
dotnet run --project Valkyrie --launch-profile simulated-market
```

The variable applies only to that PowerShell session. In Rider, add `MarketSimulatorConfiguration__Enabled=true` to the Valkyrie run configuration's environment variables, then run the host normally.

The flow source sits behind an interface:  `IMarketDataSource`, since I'm going to add a historical replay source later
without modifying the host, [LOBSTER](https://lobsterdata.com/home) looks appealing... 

---

As of Now, i placed a lock on the `OrderGateway` such that it guards every engine call.
Username is caller-supplied for now; proper auth and concurrency model are still TODO.

Aside that, I'm using full-book snapshots, not incremental deltas (for now). Every `book` message
carries the *entire* current depth, not just what changed.   
The trade-off? full snapshots are **stateless and self-healing**; a client that
connects late, drops a frame, or reconnects is instantly correct, without me needing to write code for correction.  
The cost is bandwidth: each update resends levels that didn't move.  
The alternative would be some sort of delta snapshot, sending only the changes in each snapshot. I'll work on implementing that later

---

## Tech stack

| Layer     | Technology                |
|-----------|---------------------------|
| Runtime   | .NET 10.0                 |
| Language  | C# 13                     |
| API       | ASP.NET Core Minimal APIs |
| Testing   | xUnit + FluentAssertions  |
| Hosting   | ASP.NET Core (Kestrel)    |

---

## Roadmap

- [x] Domain models (orders, limit levels, comparers)
- [x] Order-book structural layer (interfaces, sorted sets, linked lists)
- [x] Matching engine (FIFO + pro-rata)
- [x] Unit test suite
- [x] REST gateway (order entry, modify, cancel, book snapshot)
- [x] WebSocket layer (live book + trade broadcast)
- [x] Market simulator (synthetic order flow)
- [ ] Browser dashboard (order entry form + live book table)
- [ ] Deployment and future upgrades 

---


