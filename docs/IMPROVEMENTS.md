# VantaArc cTrader Systems — Improvement and Gap Register

**Date:** 2026-08-28
**Purpose:** Record what is implemented, what is incomplete, and what should improve before demo or live execution.

## Summary

The first increment deliberately prioritizes a testable domain core and a transparent shadow boundary. It is not yet a production-ready live-trading product. The most important remaining work is not adding more indicators; it is completing the platform adapter, proving broker behavior, and ensuring that the chart, logs, state machine, and order layer all describe the same decision.

## Priority register

| Priority | Area | Current state | Required improvement | Why it matters |
| --- | --- | --- | --- | --- |
| P0 | cTrader compilation | Adapter source is not compiled locally because `cAlgo.API` is proprietary and unavailable | Compile in the selected cTrader build and record API version | A local core build cannot prove platform compatibility |
| P0 | Broker selection | No cTrader broker or symbol is selected | Select a cTrader-supported broker and record the exact US Tech 100 symbol contract | Feed, spread, margin, and symbol behavior are broker-specific |
| P0 | Execution integration | Shadow boundary is tested; target-platform order adapter remains to be verified | Test market order, stop placement, result codes, volume units, and margin on demo | A valid strategy signal is not sufficient for a valid order |
| P0 | Restart reconciliation | Domain contracts exist, but persistent setup/position state is not yet implemented | Reconstruct labeled positions and initial risk after restart; test duplicate prevention | Restart errors can create duplicate or unmanaged exposure |
| P0 | Position management | Core monotonic stop logic exists; cTrader adapter currently needs full staged wiring | Wire breakeven, profit lock, ATR/structure trail, stall exit, and session exit through one manager | The written product promise includes full-position progressive protection |
| P1 | Structured cTrader logging | Core ledger and JSON export exist; target adapter currently prints basic Journal lines | Persist schema-versioned JSON/CSV through an approved cTrader storage path | Users need to know why a candidate or order was rejected |
| P1 | Chart overlay | Required fields are defined; full overlay is not yet implemented | Draw regime, VWAPs, confluence, setup state, countdown, spread, and last reason | Visual confirmation is essential for strategy trust |
| P1 | Risk economics | Core uses explicit tick value, tick size, and margin-per-unit fields | Verify each field against the selected broker and cTrader's actual economic semantics | Incorrect tick economics can mis-size risk materially |
| P1 | Timezone | Core domain timestamps are UTC; cBot defaults to UTC | Add named timezone or explicit offset policy with DST tests and chart display | Session and VWAP anchors are highly time-sensitive |
| P1 | Daily loss definition | Core checks a broker-provided day P/L snapshot | Define whether the limit includes realized, floating, commission, and swap; add account-session baseline | Ambiguous loss accounting can disable too late or too early |
| P1 | Configuration validation | Some core calculations fail closed; cross-field configuration validation is incomplete | Add a single validator for thresholds, windows, risk, session, and broker inputs | Invalid combinations should fail before a backtest starts |
| P1 | Parameter snapshot | Decision contract has a configuration hash field, but runtime hashing is placeholder | Hash a canonical parameter document and include it in every event | Research results must be reproducible |
| P2 | Higher-timeframe data | Core accepts completed-bar input; cTrader adapter currently builds one bar series | Add explicit higher-timeframe series or a tested resampling policy for daily/weekly VWAP | Context can be wrong if only signal-timeframe history is used |
| P2 | Volume source | Core uses a generic volume field | Decide tick volume versus real volume per broker and record the choice | VWAP weighting differs by data source |
| P2 | Spread model | Risk policy uses pips | Validate spread conversion, commission, and slippage assumptions against the symbol | A pip/point mismatch can block or admit the wrong trades |
| P2 | Backtest export | Analytics can create JSON and Markdown summaries from the ledger | Add a cTrader backtest export/import path and a reproducible report command | Historical runs need comparable evidence |
| P2 | Data-quality metrics | No missing-bar or stale-data counters in the first core | Add gaps, duplicate timestamps, zero-volume, and stale-quote diagnostics | Bad history can look like a strategy failure |
| P2 | Performance | Current VWAP implementation is intentionally clear rather than optimized | Profile long backtests, cache anchors, and avoid repeated full-history scans | Research must remain fast without changing results |
| P3 | Optimization | No custom fitness implementation is wired yet | Add a drawdown- and trade-count-aware fitness function after baseline validation | Raw profit optimization is prone to overfitting |
| P3 | Notifications | No alert channel is included | Add optional local/platform alerts for order blocks and emergency locks | Useful for demo monitoring, but not core correctness |
| P3 | Portfolio scope | Single symbol/strategy is the initial scope | Add deliberate multi-instance coordination only after single-instance stability | Portfolio complexity can hide basic defects |

## Important logic review findings

### The daily/weekly/session VWAP anchor must be verified in cTrader

The core API accepts a completed-bar history list and calculates anchors from timestamps. That is testable and deterministic, but the platform adapter must supply the correct history. The final cBot should not assume that a single M5 series contains the intended daily and weekly context without verifying the broker's history and session boundaries.

### The product must retain an opportunity funnel

The cBot should report the difference between:

```text
No confluence
→ regime not allowed
→ setup armed
→ confirmation absent
→ confirmation rejected
→ risk blocked
→ broker rejected
→ filled
```

Without this funnel, a user may incorrectly conclude that the strategy is over-fitted when the real issue is a zero spread cap, minimum volume, wrong session time, or symbol mismatch.

### The risk model needs broker evidence

The core risk calculator now uses tick size and tick value per unit. This is safer than treating price distance as money, but it still requires a broker-specific test. The selected broker's `TickValue`, volume unit, contract size, and estimated margin must be recorded in a fixture and checked against a known manual calculation.

### The state machine needs a clear context-change policy

The current machine cancels an armed setup if the context becomes invalid before confirmation and expires it after the configured number of completed bars. Before demo execution, we should decide and document whether a regime change from allowed to another allowed regime preserves the setup or cancels it. The conservative recommendation is to cancel when the original regime becomes invalid, but measure the opportunity impact.

### The first release should not add more filters

The first implementation should be improved by better evidence, not by adding additional indicators. The next research decision should come from the diagnostic funnel and controlled tests. A larger filter set could reduce trade count while making the system harder to understand.

## Definition of “better” for the next increment

The next increment is better only if it achieves all of the following:

- The exact cTrader target build compiles the adapter with no errors or warnings treated as errors.
- A shadow run shows every relevant VWAP and state on the chart.
- Every setup has a reason-coded outcome.
- A demo order's volume and stop match an independently calculated expectation.
- A restart neither duplicates nor loses a managed position.
- The final summary distinguishes signal scarcity from execution blocking.
- A fixed baseline can be reproduced from the source commit, parameter snapshot, broker, symbol, data range, spread model, and commission model.

## Non-negotiable stop conditions

Do not move from Shadow to Demo if any of these remain unresolved:

```text
Unknown broker symbol contract
Unverified cTrader compile
Unexplained zero-trade result
Unverified volume economics
Unverified stop-distance behavior
Missing order-result logging
Missing restart behavior
Unbounded daily-loss behavior
```
