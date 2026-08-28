# VantaArc cTrader Systems — Product Requirements Document

**Document status:** Approved product requirements; implementation in progress
**Version:** 1.0
**Date:** 2026-08-28
**Owner:** VantaArc Systems
**Author:** Manus AI

The first implementation increment now includes the platform-independent C# core, the delayed-confirmation state machine, broker-aware risk contracts, diagnostic ledger/export, shadow execution boundary, and a cTrader API-dependent cBot entry point. See [`TEST_REPORT.md`](TEST_REPORT.md) and [`IMPROVEMENTS.md`](IMPROVEMENTS.md) for current evidence and open platform-validation gates.

## Table of Contents

- [1. Executive summary](#1-executive-summary)
- [2. Product vision](#2-product-vision)
- [3. Problem statement](#3-problem-statement)
- [4. Product principles](#4-product-principles)
- [5. Scope](#5-scope)
- [6. Users and operating modes](#6-users-and-operating-modes)
- [7. Strategy requirements](#7-strategy-requirements)
  - [7.1 Initial strategy target](#71-initial-strategy-target)
  - [7.2 VWAP context model](#72-vwap-context-model)
  - [7.3 Regime model](#73-regime-model)
  - [7.4 Confluence and setup lifecycle](#74-confluence-and-setup-lifecycle)
  - [7.5 Confirmation model](#75-confirmation-model)
  - [7.6 Future strategy modules](#76-future-strategy-modules)
- [8. Risk and guardrail requirements](#8-risk-and-guardrail-requirements)
- [9. cTrader/C# system architecture](#9-ctraderc-system-architecture)
- [10. Data, time, and broker requirements](#10-data-time-and-broker-requirements)
- [11. Diagnostics and observability](#11-diagnostics-and-observability)
- [12. Backtesting and research requirements](#12-backtesting-and-research-requirements)
- [13. Testing strategy](#13-testing-strategy)
- [14. User experience and documentation](#14-user-experience-and-documentation)
- [15. Delivery plan](#15-delivery-plan)
- [16. Acceptance criteria](#16-acceptance-criteria)
- [17. Risks and mitigations](#17-risks-and-mitigations)
- [18. Open decisions](#18-open-decisions)
- [19. References](#19-references)

## 1. Executive summary

VantaArc cTrader Systems will be a professional C# trading-system project for cTrader Automate. Its purpose is to translate the trader's strategy ideas into explicit, testable rules without silently removing context merely to make the bot trade more often.

The first production target is a **NAS100/US Tech 100 VWAP Confluence cBot**. It will identify the current VWAP regime, find a meaningful confluence zone, arm a setup when price interacts with that zone, wait for a later closed-bar confirmation, and only then pass the candidate through risk and broker checks. The system will make every decision visible through chart status, structured logs, and a diagnostic summary.

The project is being separated from the existing MetaTrader repository because cTrader uses a different runtime, API, source layout, debugging workflow, and deployment model. The MT5 repository remains the historical MT5 implementation; this repository is the cTrader implementation and research system.

This document is a product and engineering contract. No trading code should be considered complete until it satisfies the requirements and acceptance tests defined here.

## 2. Product vision

The system should feel like a precise assistant that follows the trader's written plan rather than a black box that occasionally places an order. A non-technical user should be able to answer four questions after every setup:

1. What market context did the bot see?
2. What setup did it identify?
3. Which condition did it wait for or reject?
4. If the order was valid, why was it placed or blocked?

The cBot must preserve context. A low trade count is acceptable when the rules genuinely produce few opportunities; an unexplained low trade count is not acceptable.

## 3. Problem statement

The existing MT5 work showed that a strategy can become unintentionally over-filtered when multiple requirements are combined: VWAP levels, regimes, confluence, delayed confirmation, candle quality, spread, margin, stop distance, session timing, and risk controls. Some implementation issues also made it difficult to distinguish a missing signal from a blocked order.

The new system must therefore separate three concerns:

| Concern | Meaning | Required behavior |
| --- | --- | --- |
| Strategy context | What the market is doing | Calculate and display it independently |
| Signal decision | Whether the trader's setup exists | Evaluate explicit rules without broker-side shortcuts |
| Execution safety | Whether it is safe and possible to trade | Block unsafe orders and record the exact reason |

The platform change is intended to improve code clarity, testing, debugging, and visual verification. It is not a claim that cTrader data or execution is automatically better than MT5 data or execution.

## 4. Product principles

### 4.1 Context is never silently discarded

If a strategy requires a daily VWAP, session VWAP, regime, and confirmation, the implementation must calculate each one and expose its result. If a later decision rejects a candidate, the rejection must name the condition.

### 4.2 Closed bars for strategy decisions

Entry signals must be evaluated from completed candles. cTrader provides an `OnBarClosed()` lifecycle hook for the candle that has just closed, which maps directly to this requirement.[1] Tick-level updates may be used for spread, position protection, and order management, but not to manufacture a completed-candle signal.

### 4.3 Safety cannot change the strategy silently

A spread, margin, stop-distance, or volume failure may block execution, but it must not be reported as “no signal.” The system will record separate strategy and execution outcomes.

### 4.4 Fail closed

Missing data, invalid symbol properties, unknown time conversion, invalid risk configuration, or unavailable indicator values must result in no trade and an explicit diagnostic message.

### 4.5 One source of truth

The strategy rules will be implemented once in a testable domain layer. Chart presentation, logging, and execution adapters must consume the same decision object rather than reimplementing the rules.

### 4.6 Research before optimization

The first objective is behavioral correctness and opportunity measurement. Parameter optimization begins only after the diagnostic funnel proves that the cBot is seeing and classifying the intended opportunities.

## 5. Scope

### 5.1 In scope for the foundation

The foundation includes a C# cBot solution with separate components for market data, time/session handling, VWAP calculations, regime classification, confluence detection, signal state, risk controls, execution, position management, diagnostics, and testing.

The foundation must support:

- Exact broker symbol configuration rather than a universal hardcoded NAS100 name.
- Configurable timeframes and session timezone.
- Closed-bar decisions through cTrader bar events.
- Multiple timeframes and indicator contexts.
- Chart overlays showing current state and rejection reason.
- Structured CSV or JSON-compatible decision logs.
- Backtest, demo-forward, and live-readiness modes.
- Restart reconciliation for positions opened by the cBot.
- Strategy-specific labels and magic-equivalent identifiers.

### 5.2 First strategy release

The first strategy release is the NAS100 VWAP Confluence cBot described in Section 7. It is a reversal/fade-style system with delayed confirmation, not a generic indicator-cross bot.

### 5.3 Out of scope for the first release

The following are explicitly deferred:

| Deferred item | Reason |
| --- | --- |
| Automatic machine learning or predictive modeling | The first release must remain explainable and deterministic |
| News API dependency | It adds external availability and timezone complexity before the core behavior is proven |
| Portfolio-wide multi-broker execution | The first release is symbol- and account-scoped |
| Automatic live parameter optimization | Optimization must not rewrite production settings without review |
| Partial profit-taking | The current design keeps the full position open and follows price with protection |
| Copy trading or account mirroring | Separate product and compliance requirements |
| Unsupervised self-modifying logic | Not compatible with auditability or controlled research |

## 6. Users and operating modes

The primary user is a trader who can describe a discretionary strategy in market terms but does not want to maintain complex C# infrastructure. A secondary user is a technical reviewer who needs to inspect calculations, logs, tests, and execution behavior.

The cBot must support four explicit operating modes:

| Mode | Can calculate context? | Can draw charts? | Can place orders? | Purpose |
| --- | ---: | ---: | ---: | --- |
| Shadow | Yes | Yes | No | Verify signals and contexts without financial activity |
| Backtest | Yes | Optional visual mode | Simulated only | Historical research |
| Demo execution | Yes | Yes | Yes, demo account | Validate broker behavior |
| Live execution | Yes | Yes | Yes | Only after acceptance criteria are met |

The mode must be visible in the chart status and log. Shadow mode is the default for a newly created instance.

## 7. Strategy requirements

### 7.1 Initial strategy target

The initial cBot will preserve this sequence:

```text
1. Identify the current VWAP regime.
2. Find a meaningful VWAP confluence zone.
3. Arm the setup when price reaches or interacts with that zone.
4. Wait for confirmation on a later completed candle.
5. For a short: green candle followed by a red candle whose body engulfs it.
6. For a long: red candle followed by a green candle whose body engulfs it.
7. Reject a weak confirmation candle whose body is too small relative to its range.
8. Enter only after the confirmation candle closes and all safety gates pass.
9. Keep the full position open; do not take partial profits.
10. Move the stop progressively as price develops.
```

The exact numeric defaults are configuration values, not permanent truths. Every default must be visible in the cBot parameters and stored in a reviewed `.cbotset` file.

### 7.2 VWAP context model

The domain layer must calculate the following from completed bars and clearly identify their anchor periods:

| Context | Required output |
| --- | --- |
| Daily VWAP | Value, population variance, standard deviation, ±1 standard-deviation bands |
| Weekly VWAP | Value and validity; optional weekly dispersion is a later controlled extension |
| Session VWAP | Value, population variance, standard deviation, ±1 standard-deviation bands |
| ATR | Current completed-bar ATR on the configured signal timeframe |

Each calculation must include:

- Anchor start and end timestamps.
- Number of bars included.
- Whether the value is valid.
- Price source used, initially typical price unless a reviewed decision changes it.
- Treatment of bars at the exact start and end boundary.
- Behavior when volume or dispersion is unavailable.

No signal may use a partially formed bar in an anchored calculation.

### 7.3 Regime model

The initial regime classifier is:

| Regime | Definition |
| --- | --- |
| Balanced | Latest closed price is inside daily VWAP ± `BalanceBandSigma × daily sigma` |
| Directional | Price is outside the balance band but inside the discovery band |
| Discovery | Price is at or beyond daily VWAP ± `DiscoveryBandSigma × daily sigma` |
| Unknown | Required daily VWAP or dispersion is unavailable or invalid |

The regime is a participation decision, not a replacement for the entry pattern. The cBot must expose independent `AllowBalanced`, `AllowDirectional`, and `AllowDiscovery` controls. An unknown regime always blocks a trade.

The regime decision object must state both the calculated regime and whether that regime is currently allowed. This prevents an unallowed regime from being mistaken for a missing calculation.

### 7.4 Confluence and setup lifecycle

The initial directional confluence map is:

| Direction | Level 1 | Level 2 | Level 3 |
| --- | --- | --- | --- |
| Short | Daily VWAP +1σ | Weekly VWAP | Session VWAP +1σ |
| Long | Daily VWAP −1σ | Weekly VWAP | Session VWAP −1σ |

A level is touched when the interaction candle range overlaps the level within a configured tolerance. The tolerance must be shown in both price and points/pips in diagnostics. The first implementation may use the larger of an ATR-based tolerance and a fixed point tolerance, but the chosen term must be logged.

The setup state machine is:

```text
WaitingForContext
    ↓
WaitingForConfluence
    ↓  valid regime + required confluence
ConfluenceArmed
    ↓
WaitingForConfirmation
    ├── confirmation accepted → CandidateValidated
    ├── window expires → SetupExpired
    ├── session ends → SetupCancelled
    └── context becomes invalid → SetupCancelled
CandidateValidated
    ├── safety gates pass → OrderSubmitted / PositionOpen
    └── safety gate fails → ExecutionBlocked
```

The interaction candle arms the setup; it does not also count as the later confirmation candle. This separation is a non-negotiable acceptance criterion.

The setup must store:

- Direction.
- Regime at arming.
- Interaction timestamp.
- Level names touched.
- Confluence count and score.
- Confirmation deadline.
- Parameter snapshot or configuration hash.

### 7.5 Confirmation model

For a short:

1. The immediately previous completed candle is bullish: `close > open`.
2. The confirmation candle is bearish: `close < open`.
3. The confirmation candle body contains the previous bullish candle body:
   `confirmation.open >= previous.close` and `confirmation.close <= previous.open`.
4. The confirmation candle passes the body-to-range threshold.
5. The confirmation candle closes before the setup deadline.

For a long, the colors and body relationships are mirrored.

The confirmation window is measured in completed signal-timeframe bars after arming. The initial default is three bars, but the cBot must make this configurable and log the age of every confirmation attempt.

The wick-quality filter applies to the confirmation candle only. A wick-heavy interaction candle is allowed because its wick may be the evidence that price reached the zone.

### 7.6 Future strategy modules

The architecture must allow additional strategies without copying the execution engine:

| Module | Planned role |
| --- | --- |
| NAS100 ORB | Opening range construction and first closed-bar breakout |
| NAS100 Engulfing | Bidirectional two-candle engulfing signal without VWAP context |
| Shared risk/execution | Common safety, sizing, order, and position controls |

These modules must be separate signal providers. A future release may run them independently or through a deliberate portfolio coordinator, but they must not accidentally share positions or labels.

## 8. Risk and guardrail requirements

The cBot must include the following controls before any demo execution:

| Guardrail | Requirement |
| --- | --- |
| Symbol validation | Require an explicit configured symbol or validated symbol alias; never assume all brokers use `NAS100` |
| Account mode | Refuse live mode unless the user explicitly enables it after the build is marked live-ready |
| Spread | Reject entries above a configured spread cap; zero means fail closed and must produce a visible warning |
| Daily loss | Stop new entries after the configured realized/unrealized loss threshold |
| Position count | Enforce one strategy position per symbol by default |
| Exposure | Prevent unreviewed simultaneous conflicting positions from the same strategy |
| Volume | Validate minimum, maximum, step, and normalized volume units |
| Risk sizing | Size from equity and initial stop distance; never round upward to exceed risk |
| Margin | Validate required margin against free margin before submission |
| Stop distance | Validate broker minimum stop and freeze/protection constraints before order submission |
| Quote freshness | Require valid bid/ask and a non-stale market state |
| Duplicate signal | Process each completed signal bar once |
| Session boundaries | Reject entries outside the configured session and close if the reviewed policy requires it |
| Restart | Reconcile existing labeled positions after cBot restart before taking new action |
| Order result | Treat a failed order result as a separate execution failure and record its code |
| Emergency stop | Provide a clear disable switch that blocks new orders while retaining monitoring |

Risk sizing must be independently unit-tested with edge cases for small accounts, large stop distances, minimum volume, unusual volume steps, zero tick value, and insufficient margin.

Position management must be one-way and protective:

```text
Initial stop
→ Breakeven
→ Profit lock
→ ATR/structure trailing
→ Stall or session exit when configured
```

The cBot must never widen a protective stop. Partial close logic is not part of the first release.

## 9. cTrader/C# system architecture

The solution should be organized so the strategy domain can be tested without launching cTrader.

```text
VantaArc.cTrader.sln
├── src/
│   ├── VantaArc.Core/
│   │   ├── Domain models and enums
│   │   ├── SessionClock
│   │   ├── VwapCalculator
│   │   ├── RegimeClassifier
│   │   ├── ConfluenceDetector
│   │   ├── SignalStateMachine
│   │   ├── RiskCalculator
│   │   └── Decision objects
│   ├── VantaArc.cTrader/
│   │   ├── cBot entry points
│   │   ├── cTrader market-data adapter
│   │   ├── order and position adapter
│   │   ├── chart overlay adapter
│   │   └── cTrader parameter bindings
│   └── VantaArc.Analytics/
│       ├── CSV/JSON export
│       ├── diagnostic summaries
│       └── research calculations
├── tests/
│   ├── VantaArc.Core.Tests/
│   ├── VantaArc.Analytics.Tests/
│   └── Fixtures/
├── docs/
│   ├── PRD.md
│   ├── DECISIONS.md
│   └── TESTING.md
└── README.md
```

The cTrader adapter must be thin. It translates cTrader `Bars`, `Symbol`, `Positions`, `PendingOrders`, and trade-result types into domain inputs and translates domain decisions into cTrader operations. The core layer must not call cTrader APIs directly.

The cBot lifecycle should use `OnStart()` for validation and subscriptions, `OnBarClosed()` for completed-bar strategy decisions, and tick or position events for execution and protective management. cTrader documents separate bar-opened and bar-closed handling, as well as position and order events.[1] [2]

Each submitted order must carry a stable strategy label containing the strategy name, symbol context, and instance identity. The label is the cTrader equivalent of a strategy-specific magic number and is required for safe restart reconciliation.

## 10. Data, time, and broker requirements

### 10.1 Symbol abstraction

The cBot must run on the chart symbol selected by the user and validate its configuration. The strategy must not assume that a broker's US Tech 100 instrument is named `NAS100`. Exness currently identifies its US Tech 100 CFD as `USTEC`, while its current official platform list identifies MetaTrader and Exness proprietary platforms rather than cTrader.[3] [4] This means an Exness account should not be assumed to be directly deployable to a cTrader cBot.

The project will remain broker-agnostic at the core level. Broker-specific symbol names, trading hours, volume units, spreads, commissions, and stop rules belong in a broker profile.

### 10.2 Time policy

All timestamps in the domain model must be UTC. User-facing session inputs may be expressed as a named timezone or an explicit UTC offset, but the conversion must be performed once by `SessionClock` and logged.

The system must define:

- Session start and end.
- Opening-range or VWAP anchor boundaries.
- Daylight-saving behavior.
- Weekend and holiday behavior.
- Broker server time versus UTC.
- Exact half-open interval semantics: `[start, end)`.

A session test must include bars exactly at the start and end boundaries.

### 10.3 Market-data quality

Backtests must record the selected data source, timeframe, spread model, commission assumption, and date range. cTrader documents server tick data, server M1 data, CSV M1 data, fixed spread, random spread, and visual/non-real-time backtesting modes.[5] The project must use the highest-quality broker-relevant data available for final evaluation and must not compare results from different feeds as if they were identical.

## 11. Diagnostics and observability

The system must be observable before it is executable. Every completed bar should produce a decision record in shadow/backtest diagnostic mode, with a configurable reduction in production if performance requires it.

Required decision fields:

| Field | Purpose |
| --- | --- |
| Run ID | Groups one backtest or forward-test run |
| Decision ID | Uniquely identifies one bar decision |
| UTC timestamp | Makes chronology unambiguous |
| Symbol and timeframe | Identifies the market context |
| Mode | Shadow, backtest, demo, or live |
| Session state | Before, inside, or after the trading window |
| Regime | Balanced, directional, discovery, or unknown |
| VWAP values | Shows the actual calculated context |
| Level-touch results | Shows each daily/weekly/session level independently |
| Setup state | Shows the state-machine phase |
| Confirmation age | Shows how long the setup has been waiting |
| Signal result | Accepted, rejected, expired, or not applicable |
| Risk result | Passed or exact blocking reason |
| Execution result | Submitted, filled, rejected, or not attempted |
| Position state | Current stage and protective stop |
| Configuration hash | Proves which settings produced the decision |

The chart overlay must show, at minimum:

```text
Mode
Symbol/timeframe
Current session state
Current regime
Daily/weekly/session VWAP
Confluence count by direction
Setup state and confirmation bars remaining
Last decision reason
Spread and spread cap
Position and management stage
```

At the end of a run, the cBot must produce a diagnostic summary containing the opportunity funnel:

```text
context bars
→ valid regime bars
→ level touches
→ confluence arms
→ confirmation attempts
→ confirmations accepted
→ safety checks passed
→ order submissions
→ fills
```

This summary is a required research artifact, not an optional convenience.

## 12. Backtesting and research requirements

The research workflow must separate correctness testing from performance testing.

### 12.1 Baseline run

A baseline run uses a fixed date range, fixed symbol, fixed timeframe, fixed spread/commission assumptions, and one reviewed parameter set. The first baseline must use a nonzero, broker-calibrated spread cap; a zero spread cap is a deliberate diagnostic lock, not a valid trading test.

### 12.2 Controlled variants

Only one strategy variable may change per comparison until the opportunity funnel is understood. Initial comparisons include:

| Variable | Suggested variants |
| --- | --- |
| Minimum confluences | 1 versus 2 |
| Confirmation window | 3 versus 5 versus 8 completed bars |
| ATR tolerance | 0.15 versus 0.30 versus 0.50 |
| Fixed tolerance | Broker-calibrated values |
| Regime participation | All regimes versus controlled exclusions |

Each run must report trade count, long/short count, win rate, expectancy, profit factor, maximum drawdown, average R, spread conditions, and results by regime and confluence combination.

### 12.3 Optimization policy

cTrader provides built-in grid and genetic optimization and allows custom fitness criteria.[6] The project will use optimization only after a baseline is behaviorally verified. The first custom fitness function should penalize drawdown, very low trade count, and unstable long/short distribution rather than maximizing net profit alone.

Optimization results must be saved with:

- Source commit identifier.
- Data source and broker.
- Date range.
- Spread and commission model.
- Parameters optimized.
- Fitness definition.
- Out-of-sample validation result.

## 13. Testing strategy

### 13.1 Unit tests

The core library must have tests for:

- VWAP and variance calculations.
- Daily, weekly, and session anchors.
- Timezone and session boundaries.
- Balanced, directional, discovery, and unknown regimes.
- Each VWAP level touch independently.
- 1-of-3, 2-of-3, and 3-of-3 confluence rules.
- Setup arming and expiration.
- Confirmation windows.
- Bullish and bearish engulfing geometry.
- Body-to-range wick filtering.
- ORB range construction and first-breakout logic.
- Risk sizing and volume-step rounding.
- Stop improvement invariants.
- Daily loss and position-count locks.

### 13.2 Integration tests

Integration tests must use a fake cTrader adapter to verify:

- Correct order direction and volume.
- Initial stop placement.
- Refusal of invalid broker properties.
- Handling of failed order results.
- Restart reconciliation.
- Position modification only when protection improves.
- No duplicate processing for the same bar.

### 13.3 Visual tests

A visual backtest must demonstrate:

- VWAP lines and bands match the logged values.
- Regime label changes at the documented threshold.
- Confluence touch markers appear on the correct candle.
- The setup remains armed while waiting for confirmation.
- Confirmation is evaluated only on a later closed candle.
- Blocked orders display the blocking reason.

### 13.4 Acceptance data sets

The project must maintain small deterministic fixtures for:

- A valid short setup.
- A valid long setup.
- A confluence interaction followed by a delayed confirmation.
- An expired setup.
- A wick-dominant confirmation rejection.
- A spread-blocked candidate.
- A minimum-volume rejection.
- A daily-loss lock.
- A session-boundary case.
- A missing-data/unknown-regime case.

## 14. User experience and documentation

The README must be written for a non-technical user first. It must explain:

- What the cBot is trying to do in ordinary language.
- What a valid setup looks like.
- What it will never do.
- How to run shadow mode.
- How to backtest.
- How to interpret no-trade results.
- How to find logs and chart diagnostics.
- How to move to demo mode.
- Why live trading is not enabled by default.

Every parameter must have a plain-language description, unit, safe starting value, and warning about common mistakes.

## 15. Delivery plan

### Phase 0 — Product lock

Complete this PRD, record decisions, choose the first cTrader-supported broker/data source, and freeze the plain-language strategy rules.

### Phase 1 — Core calculation library

Implement and test time, VWAP, ATR, regime, confluence, candle, and risk calculations outside cTrader.

### Phase 2 — Shadow cBot

Connect the core library to cTrader bars, render the chart overlay, and produce decision logs without placing orders.

### Phase 3 — Backtest harness

Run deterministic backtests, export reports, validate the opportunity funnel, and compare the cBot's visual state with its logs.

### Phase 4 — Demo execution

Enable order submission on a demo account, validate symbol properties, spread, margin, stop placement, restarts, and position management.

### Phase 5 — Research cycles

Run controlled variants and regime/confluence analysis. No live deployment is considered during this phase.

### Phase 6 — Live-readiness review

Require source review, clean build, test results, demo-forward evidence, broker-condition review, risk plan, and explicit user decision before enabling live mode.

## 16. Acceptance criteria

The first VWAP Confluence release is accepted only when all of the following are true:

| ID | Acceptance criterion |
| --- | --- |
| AC-01 | The project builds in the selected cTrader/C# environment with no compiler errors or warnings treated as errors |
| AC-02 | The core calculation library runs independently of cTrader and passes its unit tests |
| AC-03 | The cBot uses completed-bar events for strategy decisions |
| AC-04 | The cBot displays the current regime, VWAP values, setup state, and last decision reason |
| AC-05 | The cBot records every strategy rejection separately from every execution rejection |
| AC-06 | A confluence interaction can arm a setup without requiring same-candle confirmation |
| AC-07 | A setup expires exactly according to the configured completed-bar window |
| AC-08 | Wick filtering applies to the confirmation candle according to the written rule |
| AC-09 | Spread, daily loss, volume, margin, stop-distance, symbol, and duplicate-position guards are tested |
| AC-10 | A zero spread cap visibly warns and blocks orders rather than appearing inactive |
| AC-11 | Protective stops never widen and no partial close is performed in the first release |
| AC-12 | A restart reconciles existing cBot positions and does not create a duplicate entry |
| AC-13 | A baseline backtest produces a diagnostic funnel and exportable report |
| AC-14 | A demo-forward run confirms order and position behavior on the selected broker |
| AC-15 | The README allows a non-technical user to install, shadow-test, backtest, and diagnose the cBot |

## 17. Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Exness does not provide cTrader for the current account | cTrader build cannot trade the existing account | Select a cTrader-supported broker for cTrader execution or retain MT5 for Exness live execution |
| Different broker feeds produce different signals | Backtest conclusions do not transfer | Test with the intended broker's data and record feed assumptions |
| Too many guardrails reduce trade count | Strategy appears inactive | Log the full opportunity funnel and separate signal from execution rejection |
| Parameter optimization overfits | Attractive but fragile results | Use fixed baseline, one-variable comparisons, out-of-sample and demo-forward tests |
| Session timezone is wrong | VWAP and ORB context is wrong | Centralize UTC conversion, show it on chart, and test exact boundaries |
| Broker volume units differ | Orders are rejected or mis-sized | Validate symbol metadata and use adapter-level normalization |
| Visual overlays slow optimization | Research becomes inefficient | Disable overlays during optimization while retaining summary logs |
| Restart loses internal state | Duplicate or unmanaged positions | Reconcile labeled positions and persist/reconstruct setup state where needed |

## 18. Open decisions

These decisions must be made before Phase 1 implementation:

| Decision | Current recommendation |
| --- | --- |
| cTrader broker | Choose a broker that officially provides cTrader and a US Tech 100/NAS100 instrument; do not assume Exness compatibility |
| First strategy | NAS100 VWAP Confluence |
| First timeframe | M5, subject to broker data availability |
| Session timezone | UTC-based with an explicit user-configured offset or named timezone |
| Default mode | Shadow |
| Default risk | 0.25% per trade for demo research, subject to broker minimum-volume feasibility |
| Partial profits | Disabled |
| Regime participation | All valid regimes visible and configurable; no hidden exclusions |
| Weekly level | Begin with weekly VWAP centerline; measure contribution before changing it |
| Confirmation window | Begin at three completed bars; test longer windows only after baseline diagnostics |
| Live deployment | Disabled until all acceptance criteria pass |

## 19. References

[1]: https://help.ctrader.com/ctrader-algo/how-tos/cbots/handle-bar-events/ "cTrader Algo: How to handle bar events"
[2]: https://help.ctrader.com/ctrader-algo/how-tos/cbots/cbot-trading-operations/ "cTrader Algo: cBot trading operations"
[3]: https://www.exness.com/trading-platforms/ "Exness trading platforms overview"
[4]: https://www.exness.com/indices/us-tech-100/ "Exness US Tech 100 index trading USTEC"
[5]: https://help.ctrader.com/ctrader-algo/how-tos/cbots/backtest-a-cbot/ "cTrader Algo: Backtest a cBot"
[6]: https://help.ctrader.com/ctrader-algo/how-tos/cbots/optimise-a-cbot/ "cTrader Algo: Optimise a cBot"
