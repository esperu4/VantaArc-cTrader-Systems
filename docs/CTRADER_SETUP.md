# cTrader Setup and Verification Guide

## Current scope

The repository contains a platform-independent C# core that can be built and tested locally, plus a cTrader Automate entry-point source file. The cTrader entry point is not compiled by the sandbox because the proprietary `cAlgo.API` assembly is supplied by the cTrader environment rather than this repository.

## Local build

Install the .NET 8 SDK, then run from the repository root:

```bash
dotnet restore VantaArc.cTrader.sln
dotnet build VantaArc.cTrader.sln --configuration Release
dotnet test VantaArc.cTrader.sln --configuration Release
```

The local solution builds:

```text
VantaArc.Core
VantaArc.Analytics
VantaArc.cTrader adapter contracts
VantaArc.Core.Tests
VantaArc.Integration.Tests
```

The cTrader API-dependent source is located at:

```text
src/VantaArc.cTrader/cBot/VantaArcNas100VwapConfluenceBot.cs
```

## Target cTrader setup

1. Install the current cTrader Windows or Mac application with Algo support.
2. Select a broker that officially supports cTrader and provides a suitable US Tech 100/NAS100 instrument.
3. Record the exact symbol name, timeframe, trading hours, volume units, spread, commission, tick size, tick value, minimum volume, volume step, stop distance, freeze/protection rule, and margin behavior.
4. Create a cBot project named `VantaArcNas100VwapConfluenceBot`.
5. Add the platform-independent `src/VantaArc.Core` source files or assembly according to the cTrader project model in use.
6. Add `src/VantaArc.cTrader/cBot/VantaArcNas100VwapConfluenceBot.cs`.
7. Compile inside cTrader. Record the cTrader version and compiler result in [`TEST_REPORT.md`](TEST_REPORT.md).
8. Attach the cBot to the exact US Tech 100/NAS100 chart.
9. Start in `Shadow` mode with `Enable order execution=false`.
10. Verify the Journal, chart state, and structured diagnostic output before enabling demo execution.

## Initial parameters

The first run should use:

| Parameter | Starting value | Reason |
| --- | ---: | --- |
| Operating mode | Shadow | No order submission |
| Enable order execution | false | Explicit safety lock |
| Live readiness acknowledged | false | Prevents accidental live start |
| Minimum confluences | 2 | Matches the product baseline |
| Confirmation window | 3 bars | Gives later confirmation without indefinite waiting |
| Minimum body/range | 0.50 | Rejects weak wick-dominant confirmation candles |
| Risk percent | 0.25% | Research starting point only |
| Daily loss limit | 2.0% | Research starting point only |
| Maximum spread | Broker-calibrated nonzero value | Zero must remain a visible diagnostic lock |
| One position per symbol | true | Prevents duplicate exposure |

## Shadow verification checklist

On a visual backtest or demo chart, confirm that:

- The cBot identifies the correct symbol and timeframe.
- Completed bars, not the forming bar, drive signal decisions.
- Daily, weekly, and session VWAP values are visible or logged.
- The current regime is visible.
- A confluence interaction changes the state to `ConfluenceArmed`.
- Confirmation is checked on a later candle.
- A wick-heavy confirmation is rejected.
- A zero or invalid spread cap blocks execution with a visible reason.
- A valid confirmation is logged as ready but no order is sent in Shadow mode.
- The same bar is not processed twice.

## Demo verification checklist

Only after Shadow mode is understood:

- Use a demo account.
- Use the smallest practical volume.
- Confirm the initial stop price independently.
- Confirm the position label identifies only this strategy.
- Confirm the broker accepts the volume and stop distance.
- Confirm order-result codes are logged.
- Confirm a restart does not create a duplicate position.
- Confirm breakeven, profit lock, trailing, stall, and session-close behavior.
- Confirm the daily loss lock blocks new entries when reached.

Live mode is not part of the first installation procedure.
