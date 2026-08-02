# Valkyrie

## What is it?
It's a limit order book + matching engine... essentially the core of an exchange.

![Valkyrie dashboard](docs/assets/valkyrie-demo.gif)

*Valkyrie Dashboard*

---

## Launch Profiles
Since I don't have actual access to a real live market data feed, I configured the project two work in two ways

| Mode | What it demonstrates | Launch profile |
|---|---|---|
| Synthetic market | Executable liquidity flowing through the local matching engine | `simulated-market` |
| Historical replay | Observational LOBSTER books and market prints streamed at configurable speed | `historical-replay` |

Run either mode with `dotnet run --project Valkyrie --launch-profile <profile>`, then start the Angular client from `Frontend` with `npm start`. Historical replay also requires the [sample-data setup](#obtaining-lobster-data).

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

## Getting Started 

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

### 3. Start with historical market data

Historical replay requires compatible LOBSTER files that are not included in this repository. Follow the data-placement instructions in [Historical market-data replay](#historical-market-data-replay), then run:

```powershell
dotnet run --project .\Valkyrie --launch-profile historical-replay
```

The checked-in profile enables the simulator and selects `LobsterReplay` as its market-data source.

The default configuration replays the 2012-06-21 session at 1x speed, publishes at most five book snapshots per second per instrument, and stops after one pass. Restart the backend to replay it again.

### 4. Start the Angular dashboard

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
All configurations for the project are in the `appsettings.json` file.

Supported market-data sources are:

- `Synthetic`
- `LobsterReplay`  

Lobster is a site i was able to get historical exchange sample data from.

`MarketSimulatorConfiguration.Enabled` must be `true` for either source to run. The checked-in launch profiles set these values automatically.

Prices are expressed in integer cents:

```text
41800 = $418.00
```

---

## Historical market-data replay

 Historical books and prints are observational market data, you can't trade against old data (for now at least... looking at adding functionality for backtesting...perhaps in some participant-side project)


### Obtaining LOBSTER data

The default configuration uses LOBSTER's official depth-10 sample files for MSFT, AAPL, AMZN, GOOG, and INTC. Each download link below points directly to the corresponding ZIP archive hosted by LOBSTER.

LOBSTER data is not mirrored or redistributed by this repository. Access and use remain subject to the data provider's applicable terms.

ZIP streaming is the checked-in default and requires no extraction. Create a directory named `ReplayData` at the repository root, then save each archive at the path shown:

| Ticker | Official depth-10 sample | Required local path |
|---|---|---|
| MSFT | [Download ZIP](https://php.lobsterdata.com/info/sample/LOBSTER_SampleFile_MSFT_2012-06-21_10.zip) | `ReplayData/LOBSTER_SampleFile_MSFT_2012-06-21_10.zip` |
| AAPL | [Download ZIP](https://php.lobsterdata.com/info/sample/LOBSTER_SampleFile_AAPL_2012-06-21_10.zip) | `ReplayData/LOBSTER_SampleFile_AAPL_2012-06-21_10.zip` |
| AMZN | [Download ZIP](https://php.lobsterdata.com/info/sample/LOBSTER_SampleFile_AMZN_2012-06-21_10.zip) | `ReplayData/LOBSTER_SampleFile_AMZN_2012-06-21_10.zip` |
| GOOG | [Download ZIP](https://php.lobsterdata.com/info/sample/LOBSTER_SampleFile_GOOG_2012-06-21_10.zip) | `ReplayData/LOBSTER_SampleFile_GOOG_2012-06-21_10.zip` |
| INTC | [Download ZIP](https://php.lobsterdata.com/info/sample/LOBSTER_SampleFile_INTC_2012-06-21_10.zip) | `ReplayData/LOBSTER_SampleFile_INTC_2012-06-21_10.zip` |

The application rejects:

- Missing or duplicate message/order-book files
- Message and order-book files from different datasets
- Ticker or session-date mismatches
- Replay security IDs or tickers that disagree with the canonical instrument catalogue
- Invalid or missing configured paths

#### Using extracted CSV files
You could also use the extracted CSVs instead of the ZIP archives....

For example:

```text
ReplayData/
`-- LOBSTER_SampleFile_MSFT_2012-06-21_10/
    |-- MSFT_2012-06-21_34200000_57600000_message_10.csv
    `-- MSFT_2012-06-21_34200000_57600000_orderbook_10.csv
```

Then change that instrument's `DataFormat` and `DataPath`:

```json
{
  "SecurityId": 1,
  "Ticker": "MSFT",
  "DataFormat": "CsvDirectory",
  "DataPath": "../ReplayData/LOBSTER_SampleFile_MSFT_2012-06-21_10",
  "SessionMidnight": "2012-06-21T00:00:00-04:00",
  "BookDepth": 10
}
```

## Current limitations

- Application state is held in memory (there are plans to add persistence)
- There's no auth, usernames are gotten from the client.
- Market-data updates use full snapshots rather than deltas.
