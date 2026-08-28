# VantaArc cTrader Systems — Implementation Test Report

**Date:** 2026-08-28
**Scope:** First implementation increment for the NAS100 VWAP Confluence system

## Executive result

The platform-independent core and the dependency-injected shadow/integration boundary are implemented and tested locally. The current local solution builds with **zero warnings and zero errors**, and the automated suite passes **24 tests**.

The cTrader API-dependent source is present as a deployable adapter artifact but is intentionally excluded from the sandbox .NET build because the proprietary `cAlgo.API` assembly is not available in this environment. The target cTrader compile and visual-backtest gates therefore remain open.

## Evidence

| Check | Result |
| --- | --- |
| .NET solution build, Release | Passed; 0 warnings, 0 errors |
| Core unit tests | Passed; 13 tests |
| Adapter/integration tests | Passed; 11 tests |
| Total automated tests | Passed; 24 tests |
| Core calculation coverage | VWAP, population sigma, ATR, session, regime, confluence, candle geometry |
| State-machine coverage | Arming, delayed confirmation, expiry, duplicate bars, invalid context |
| Safety coverage | Spread, daily loss, live acknowledgement, tick economics, margin reserve, volume rounding, position limit |
| Shadow boundary coverage | Decisions are recorded without a real order being sent |
| MetaEditor/cTrader platform compilation | Not run in sandbox; required on target cTrader installation |
| cTrader visual backtest | Not run; requires target cTrader data and platform |
| Broker demo execution | Not run; cTrader-supported broker and symbol are not selected |

## Covered behavior

The tests prove the following deterministic contracts:

- VWAP is volume-weighted using typical price.
- VWAP dispersion uses population variance.
- Anchor windows use the documented half-open interval.
- Invalid or zero dispersion produces an unknown regime.
- Balanced, directional, and discovery regimes are classified independently.
- A disabled regime remains visible but is not allowed to trade.
- Bullish and bearish body engulfing are mirrored correctly.
- A wick-dominant confirmation is rejected.
- Confluence is counted level by level.
- A setup arms before confirmation is checked.
- The confirmation must occur on a later completed candle.
- The setup expires after the configured completed-bar window.
- The same completed bar cannot be processed twice.
- Invalid quote and broker economics fail closed.
- Live execution requires explicit acknowledgement.
- Daily loss and free-margin reserves can block entry.
- Volume is rounded down and never up.
- A protective stop can improve but cannot widen.
- The shadow execution boundary records the decision without sending an order.

## Findings from the first test cycle

The first test run intentionally exposed implementation defects rather than hiding them. The following failures were corrected:

| Finding | Correction |
| --- | --- |
| Test project lacked the global xUnit import | Added explicit global test imports |
| cTrader source was being compiled without `cAlgo.API` | Excluded the platform-bound cBot source from the local adapter build and documented the target-platform gate |
| Fake broker fixture had an incorrect constructor shape | Added explicit tick, margin, and risk-economics fields |
| Risk sizing initially treated price distance as money | Changed sizing to use tick size and tick value per unit |
| Diagnostics counted a deliberately rejected event as accepted | Corrected the event fixture and retained separate signal/risk/execution fields |
| Session fixture fell outside the configured start minute | Corrected the test session boundaries |
| Short trailing fixture accidentally proposed an improvement | Corrected the fixture to test a genuine widening attempt |

This is the intended development behavior: a failing test is evidence that a contract is unclear or a fixture is wrong; it is not suppressed to produce a green build.

## Open validation gates

The following items cannot be proven by the local solution alone:

1. Compilation against the exact cTrader `cAlgo.API` version used by the selected broker.
2. Correct cTrader API overloads for market orders, position modification, volume normalization, and estimated margin.
3. Whether the chosen broker exposes the required US Tech 100/NAS100 symbol and history.
4. Visual overlay alignment with the cBot's logged VWAP values.
5. Real spread, commission, slippage, trading hours, stop distance, volume units, and margin behavior.
6. Restart reconciliation and persistence behavior inside the target cTrader terminal.
7. Realistic tick backtest results and out-of-sample stability.
8. Demo-forward order execution and position-management behavior.

These are not reasons to bypass testing. They are explicit acceptance gates for the next phase.

## Recommended next test run

After selecting a cTrader-supported broker:

1. Copy the core source into a cTrader Algo project.
2. Add `src/VantaArc.cTrader/cBot/VantaArcNas100VwapConfluenceBot.cs`.
3. Compile inside cTrader and record the exact API/build version.
4. Run Shadow mode on the exact US Tech 100 symbol.
5. Compare the chart overlay, Journal output, and decision log.
6. Run a visual backtest on a fixed date range.
7. Run a demo execution test with the smallest practical volume.
8. Update this report with platform-specific evidence.
