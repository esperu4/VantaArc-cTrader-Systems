# Verified platform API notes

**Research date:** 2026-08-28

## cTrader

The official cTrader Robot reference documents `OnStart()`, `OnStop()`, `OnTick()`, `OnBar()`, and `OnBarClosed()`. `OnBarClosed()` is called when a new bar opens and gives the previous bar as the closed bar, which supports the completed-candle entry requirement.

The official Symbol reference documents:

- `NormalizeVolumeInUnits(double, RoundingMode)`.
- `GetEstimatedMargin(TradeType, double)`.
- `VolumeForFixedRisk(double, double)`.
- `VolumeForProportionalRisk(...)`.
- `TickSize`, `TickValue`, `PipSize`, `LotSize`.
- `VolumeInUnitsMin`, `VolumeInUnitsMax`, `VolumeInUnitsStep`.
- `MinDistanceType` and `MinStopLossDistance`.
- `IsTradingEnabled`.

The official IAccount reference documents `Equity`, `FreeMargin`, `IsLive`, `Balance`, and `UnrealizedNetProfit`. These are the properties needed to build broker-aware risk and account-mode guards.

The official chart documentation documents `Chart.DrawStaticText` and chart drawing APIs needed for the state overlay.

The official optimization documentation confirms grid and genetic optimization, standard criteria, and custom `GetFitness()` criteria. The official backtesting documentation confirms server tick, server M1, CSV M1, fixed/random spread, visual mode, and result reporting.

Sources:

- https://help.ctrader.com/ctrader-algo/references/General/Robot/
- https://help.ctrader.com/ctrader-algo/references/MarketData/Symbols/Symbol/
- https://help.ctrader.com/ctrader-algo/references/Account/IAccount/
- https://help.ctrader.com/ctrader-algo/references/Chart/Drawings/ChartStaticText/
- https://help.ctrader.com/ctrader-algo/how-tos/cbots/backtest-a-cbot/
- https://help.ctrader.com/ctrader-algo/how-tos/cbots/optimise-a-cbot/

## Exness

The official Exness platform overview currently lists MetaTrader 4, MetaTrader 5, Exness Terminal, and Exness Trade. It does not list cTrader. The official Exness US Tech 100 page identifies the instrument as `USTEC` and describes it as a CFD.

Sources:

- https://www.exness.com/trading-platforms/
- https://www.exness.com/indices/us-tech-100/

## Implementation consequence

The cTrader cBot must be compiled inside the selected cTrader environment against the installed `cAlgo.API` version. The local .NET build proves the platform-independent core and adapter contracts, not the proprietary platform binding. The broker must be selected before demo execution, and the exact symbol, volume, tick-value, margin, trading-hours, and minimum-distance behavior must be captured in a broker profile and test fixture.
