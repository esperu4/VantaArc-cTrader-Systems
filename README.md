# VantaArc cTrader Systems

**A carefully engineered cTrader/C# research project for transparent NAS100 automation.**

> **Project status:** Core implementation and shadow-adapter phase. The platform-independent strategy, risk, state, diagnostics, and test layers are implemented. The cTrader API-dependent entry point still requires compilation and verification inside the target cTrader installation before demo or live execution.

## Table of contents

- [What this project is](#what-this-project-is)
- [Why we are creating a new project](#why-we-are-creating-a-new-project)
- [The simple idea behind the system](#the-simple-idea-behind-the-system)
- [What the first bot will do](#what-the-first-bot-will-do)
- [What the first bot will not do](#what-the-first-bot-will-not-do)
- [How one possible trade would happen](#how-one-possible-trade-would-happen)
- [The three separate parts of the system](#the-three-separate-parts-of-the-system)
- [Safety guardrails](#safety-guardrails)
- [How we will test it](#how-we-will-test-it)
- [The four operating modes](#the-four-operating-modes)
- [What cTrader gives us](#what-ctrader-gives-us)
- [Important Exness limitation](#important-exness-limitation)
- [Planned project structure](#planned-project-structure)
- [How the project will be built](#how-the-project-will-be-built)
- [How a non-technical user will use it](#how-a-non-technical-user-will-use-it)
- [Current decisions](#current-decisions)
- [Current status](#current-status)
- [Documentation](#documentation)
- [Risk warning](#risk-warning)
- [References](#references)

## What this project is

This project is for building trading software that runs on **cTrader**. The software will be called a **cBot**, which is cTrader's name for an automated trading robot.

In simple terms, the cBot will watch the market, read the rules we give it, and decide whether the conditions for a trade are present. If they are present, it will first check whether the trade is safe and allowed. Only after both the strategy rules and safety checks pass will it be allowed to place an order.

The goal is not to create a robot that trades constantly. The goal is to create a robot that follows the intended trading idea faithfully and explains every decision clearly.

## Why we are creating a new project

The existing [VantaArc Systems MT5 repository](https://github.com/esperu4/VantaArc-Systems) contains the MetaTrader 5 versions of the ISS-IB, NAS100 VWAP Confluence, NAS100 Engulfing, and NAS100 ORB work. This new repository is separate because cTrader and MetaTrader 5 are different development environments.

| Existing MT5 project | New cTrader project |
| --- | --- |
| MQL5 source code | C# source code |
| MetaEditor compiler | cTrader Algo/C# compiler |
| MetaTrader Strategy Tester | cTrader Backtesting and Optimisation |
| `.mq5` and `.mqh` files | cTrader cBot project files |
| MT5-specific order and symbol APIs | cTrader-specific symbol, position, and order APIs |

Keeping them separate prevents the two platforms from being mixed together. It also makes it clear which version was tested, which broker data was used, and which platform is capable of executing the system.

The new repository is private and is intended to become the clean home for the cTrader implementation:

**[VantaArc-cTrader-Systems on GitHub](https://github.com/esperu4/VantaArc-cTrader-Systems)**

## The simple idea behind the system

The trader's idea has several layers. The bot must not skip those layers just because fewer filters would create more trades.

Think of the system as a series of questions:

| Question | Simple meaning |
| --- | --- |
| Where is price compared with VWAP? | What is the current market location? |
| What kind of market is this? | Is it balanced, directional, or in discovery? |
| Are important VWAP levels close together? | Is there a meaningful confluence area? |
| Has price reached that area? | Should the bot arm a possible setup? |
| Has a later candle confirmed the direction? | Is there evidence to enter? |
| Is the order safe and possible? | Do spread, risk, margin, volume, and broker rules allow it? |
| How should the position be protected? | How should the stop move after entry? |

A trade is not supposed to happen just because one indicator shows a condition. The bot must preserve the complete chain of reasoning.

## What the first bot will do

The first cBot will be a **NAS100 VWAP Confluence** system. It will be designed for a broker's US Tech 100/NAS100 instrument, but the exact symbol name will be configurable because brokers use different names.

The first version will:

1. Read completed market candles.
2. Calculate daily, weekly, and session VWAP information.
3. Calculate the relevant standard-deviation bands.
4. Identify whether the market is balanced, directional, in discovery, or unknown.
5. Look for a meaningful combination of VWAP levels.
6. Arm a setup when price reaches the confluence area.
7. Wait for a later completed candle instead of requiring everything on one candle.
8. Check the bullish or bearish confirmation pattern.
9. Check whether the confirmation candle is strong enough rather than mostly wick.
10. Check spread, risk, margin, volume, stop distance, symbol, and position limits.
11. Enter only when the strategy and safety checks both pass.
12. Keep the entire position open and move the protective stop progressively.
13. Record the reason for every important decision.

### The short example

A short setup will work like this:

```text
1. The bot identifies the current VWAP context.
2. It finds a short-side confluence area.
3. Price reaches that area.
4. The bot marks the setup as ARMED.
5. It waits for a later closed candle.
6. A bullish candle is followed by a bearish candle.
7. The bearish candle's body engulfs the bullish candle's body.
8. The bearish confirmation candle is not mostly wick.
9. All safety checks pass.
10. The bot opens a short position after the candle closes.
```

A long setup is the mirror image:

```text
1. Price reaches a long-side confluence area.
2. The setup becomes ARMED.
3. A bearish candle is followed by a bullish candle.
4. The bullish candle's body engulfs the bearish candle's body.
5. The confirmation candle is strong enough.
6. All safety checks pass.
7. The bot opens a long position after the candle closes.
```

The exact rules, boundaries, calculations, and defaults are written in [`docs/PRD.md`](docs/PRD.md). The code will not rely on vague descriptions such as “price is near VWAP” or “the candle looks strong.”

## What the first bot will not do

The first cBot will not:

- Enter because a candle is still forming.
- Hide a rejected signal.
- Treat a broker rejection as if no strategy signal existed.
- Change its own parameters.
- Take partial profits in the first release.
- Use machine learning or unexplained predictions.
- Trade an unknown or invalid regime.
- Assume every broker calls the instrument `NAS100`.
- Use a zero spread limit silently.
- Start in live mode by default.
- Claim that a profitable backtest guarantees future results.

A small number of trades may be the correct result of strict rules. The unacceptable result is a small number of trades with no explanation.

## How one possible trade would happen

The bot will use a clear state sequence:

```text
Waiting for data
    ↓
Reading the current market context
    ↓
Waiting for a valid VWAP confluence
    ↓
Setup armed
    ↓
Waiting for later candle confirmation
    ├── valid confirmation → safety checks
    ├── no confirmation in time → setup expires
    ├── session ends → setup is cancelled
    └── invalid context → setup is cancelled
    ↓
Safety checks
    ├── passed → order is submitted
    └── failed → order is blocked and the reason is recorded
    ↓
Position management
```

This structure matters because it lets the user distinguish between these situations:

```text
No VWAP confluence was found.
VWAP confluence was found but the regime was not allowed.
The setup was armed but no candle confirmed it.
The confirmation appeared but the spread was too wide.
The order passed the strategy but the broker rejected it.
The position was opened and is being managed.
```

## The three separate parts of the system

The cBot will be built as three connected but separate layers.

### 1. Strategy layer

This layer answers:

> “Does the trading setup exist?”

It calculates VWAP context, regimes, confluence, candle confirmation, and setup timing. It does not place orders.

### 2. Safety layer

This layer answers:

> “Even if the setup exists, is it safe and allowed to trade?”

It checks risk, spread, margin, volume, stop distance, daily loss, session timing, duplicate positions, and the broker's trading conditions.

### 3. cTrader layer

This layer answers:

> “How do we communicate the decision to cTrader?”

It receives market data, sends orders, changes stops, reads positions, draws information on the chart, and records cTrader responses.

Keeping these layers separate is one of the main design decisions of this project. It prevents a broker restriction from silently changing what the strategy means.

## Safety guardrails

The bot will be designed with safety controls active from the beginning, not added after the strategy is finished.

| Guardrail | What it protects against |
| --- | --- |
| Exact symbol configuration | Trading the wrong instrument |
| Explicit operating mode | Accidentally trading live while testing |
| Spread limit | Entering when transaction cost is unusually high |
| Daily loss limit | Continuing to open new trades after a defined loss limit |
| One-position rule | Duplicate or conflicting positions |
| One-setup/duplicate-signal control | Entering repeatedly from the same candle or setup |
| Risk-based position sizing | Using a volume that is too large for the planned risk |
| Minimum and maximum volume checks | Broker volume rejection or unintended sizing |
| Volume-step rounding down | Rounding a trade upward beyond the risk budget |
| Margin check | Submitting an order the account cannot support |
| Stop-distance check | Placing a stop too close for broker rules |
| Fresh quote check | Trading with missing or unusable prices |
| Session clock | Trading outside the intended research window |
| Restart reconciliation | Losing track of a position after a restart |
| Order-result logging | Hiding the broker's rejection reason |
| Emergency disable | Blocking new orders while keeping monitoring available |

The default mode will be **Shadow**, meaning the bot can calculate and display decisions but cannot place orders. Demo execution will be enabled only after the shadow behavior is reviewed.

## How we will test it

Testing will happen in stages rather than jumping directly to a live backtest or live account.

### Stage 1: Rule tests

Small, controlled examples will test the calculations and rules independently from cTrader. These tests will answer questions such as:

- Does the daily VWAP include the correct candles?
- Is the session boundary handled correctly?
- Is a balanced regime classified correctly?
- Does two-level confluence mean exactly what we wrote?
- Does the confirmation window count completed candles correctly?
- Is a bullish or bearish body genuinely engulfed?
- Is a wick-dominant candle rejected?
- Does the stop only move in the protective direction?

### Stage 2: Shadow mode

The cBot will observe the market and display its reasoning without sending orders. The chart will show the VWAP values, regime, confluence count, setup state, confirmation countdown, spread, and last decision reason.

### Stage 3: Backtesting

cTrader supports server tick data, server M1 data, local CSV M1 data, spread choices, visual mode, trade statistics, logs, and HTML reports.[1] The final research runs will record the exact data source, date range, spread, commission, symbol, timeframe, and parameter set.

### Stage 4: Demo execution

The bot will be allowed to submit orders only on a demo account. We will check that its order volume, initial stop, position label, stop changes, and closing behavior match the written rules.

### Stage 5: Controlled research

Only after the baseline is proven will we compare one variable at a time, such as:

```text
Minimum confluences: 1 versus 2
Confirmation window: 3 versus 5 versus 8 bars
ATR tolerance: 0.15 versus 0.30 versus 0.50
Allowed regimes: all versus selected regimes
```

The purpose is not to find the most attractive historical result. The purpose is to understand the trade-off between opportunity count, quality, drawdown, and robustness.

## The four operating modes

| Mode | What the bot can do | What it cannot do |
| --- | --- | --- |
| Shadow | Calculate, explain, log, and draw | Place orders |
| Backtest | Simulate decisions and orders on history | Affect real funds |
| Demo execution | Place and manage demo positions | Risk live capital |
| Live execution | Place and manage live positions | Nothing is automatically guaranteed |

A new instance will start in Shadow mode. Live execution will require a deliberate configuration change and a completed readiness review.

## What cTrader gives us

cTrader is attractive for this project mainly because it gives us a cleaner engineering environment for a context-heavy strategy.

### Clearer closed-bar behavior

cTrader provides an `OnBarClosed()` event for the candle that has just closed. That fits the strategy requirement: evaluate the signal after the candle closes, not while the candle is still changing.[2]

### C# and better development tools

cTrader supports C# development and external IDEs such as Visual Studio, Visual Studio Code, and Rider. It also supports Python algorithms and third-party libraries, although the first project will use C# for strong structure and testing.[3]

### Visual explanations

The cBot can draw chart information such as VWAP lines, bands, state labels, confluence markers, and rejection reasons. This should make it easier to see whether the code and the trader's visual interpretation agree.

### Backtest reports and optimization

cTrader provides visual and non-visual backtesting, structured result tabs, HTML reports, and built-in optimization. It supports grid and genetic optimization and custom fitness criteria.[1] [4]

These features improve research and code review, but they do not make a strategy profitable by themselves. Data quality, broker conditions, and correct rules still matter.

## Important Exness limitation

The current Exness platform information lists MetaTrader 4, MetaTrader 5, Exness Terminal, and Exness Trade. cTrader is not listed as an Exness trading platform.[5] Exness identifies its US Tech 100 instrument as `USTEC`.[6]

This creates an important distinction:

| Goal | Most practical platform |
| --- | --- |
| Continue trading the existing Exness `USTEC_x100m` account | MT5, unless Exness confirms cTrader access for the specific account |
| Build and operate the new C# cBot | A broker that officially supports cTrader and provides a suitable US Tech 100/NAS100 instrument |
| Compare platforms fairly | Use the same broker/feed where possible and record all data assumptions |

We must not compare an Exness MT5 backtest with a different broker's cTrader backtest and conclude that the platform alone caused the difference. The broker feed, spread, commission, symbol contract, session hours, and tick history may all be different.

Before implementation reaches demo execution, we must choose and document the cTrader-supported broker and its exact symbol properties.

## Planned project structure

The intended structure is:

```text
VantaArc-cTrader-Systems/
├── src/
│   ├── VantaArc.Core/
│   │   ├── market context and data models
│   │   ├── session clock
│   │   ├── VWAP calculator
│   │   ├── regime classifier
│   │   ├── confluence detector
│   │   ├── setup state machine
│   │   └── risk calculations
│   ├── VantaArc.cTrader/
│   │   ├── cBot entry point
│   │   ├── cTrader data adapter
│   │   ├── order adapter
│   │   ├── position manager
│   │   └── chart diagnostics
│   └── VantaArc.Analytics/
│       ├── log reader
│       ├── diagnostic summary
│       └── research exports
├── tests/
│   ├── VantaArc.Core.Tests/
│   ├── VantaArc.Analytics.Tests/
│   └── Fixtures/
├── docs/
│   └── PRD.md
└── README.md
```

The first implementation may use a simpler cTrader-compatible project layout if that is required by the cTrader environment, but the separation between strategy, safety, and platform adapter must remain.

## How the project will be built

The work will proceed in this order:

1. Confirm the broker and cTrader symbol.
2. Freeze the strategy rules in plain language.
3. Write tests for those rules.
4. Build the calculation library.
5. Build the shadow cBot.
6. Add chart explanations and decision logs.
7. Verify backtests and the diagnostic opportunity funnel.
8. Add demo execution.
9. Run controlled research comparisons.
10. Conduct a live-readiness review only after the evidence is complete.

This order is deliberate. We will not add more filters merely because the first backtest has few trades, and we will not remove context merely to make the trade counter larger.

## How a non-technical user will use it

When the first cBot is ready, the normal user journey will be:

1. Open cTrader on a demo account.
2. Open the exact NAS100/US Tech 100 symbol provided by the selected broker.
3. Add the cBot to the chart.
4. Leave it in Shadow mode first.
5. Confirm that the chart shows the expected VWAP lines and market state.
6. Run a backtest with the documented date range and data settings.
7. Read the simple “why” message for each setup.
8. Change only one reviewed setting at a time.
9. Save the results and compare them with the baseline.
10. Move to demo execution only after the shadow and backtest results make sense.

If the bot does not trade, the user should not have to guess. The chart and log should say something like:

```text
No trade: waiting for VWAP confluence.
No trade: discovery regime is disabled.
No trade: setup armed, confirmation window expired.
No trade: confirmation passed, spread too high.
No trade: order blocked because minimum volume exceeds risk budget.
Trade opened: long confirmation passed and all safety checks passed.
```

## Current decisions

| Decision | Current position |
| --- | --- |
| Repository | Separate private cTrader repository |
| First platform language | C# |
| First strategy | NAS100 VWAP Confluence |
| First mode | Shadow |
| First research timeframe | M5, subject to broker data availability |
| Signal decisions | Completed candles only |
| Partial profits | Disabled in first release |
| Risk starting point | 0.25% for demo research, subject to broker feasibility |
| Exness compatibility | Not assumed; verify before deployment |
| Live trading | Not enabled during development |

## Current status

The project is currently in the **core implementation and shadow-adapter phase**. The platform-independent strategy, risk, state, diagnostics, and test layers are implemented. The cTrader API-dependent entry point still requires compilation and verification inside the target cTrader installation.

Completed:

- Separate private GitHub repository created.
- Platform choice documented.
- First strategy scope documented.
- Safety architecture documented.
- Testing and diagnostic requirements documented.
- Non-technical user workflow documented.
- .NET solution and C# project architecture created.
- VWAP, ATR, session, regime, confluence, candle, risk, and position-management core implemented.
- Delayed-confirmation state machine implemented.
- Shadow execution boundary and decision coordinator implemented.
- JSON diagnostic export and opportunity-funnel summary implemented.
- 24 automated C# tests passing with zero build warnings and zero build errors.

Not yet completed:

- cTrader broker selection.
- cTrader symbol and contract-property verification.
- Compilation against the proprietary cTrader `cAlgo.API` assembly.
- Full chart overlay wiring.
- Full staged position-management wiring in the cBot adapter.
- cTrader visual backtest and demo validation.
- Restart reconciliation and persistent setup-state validation.

## Documentation

- [Product Requirements Document](docs/PRD.md) — the full product, strategy, architecture, safety, testing, and acceptance specification.
- [cTrader Setup Guide](docs/CTRADER_SETUP.md) — how to build locally and verify the cBot inside cTrader.
- [Implementation Test Report](docs/TEST_REPORT.md) — current automated evidence and open target-platform gates.
- [Improvement and Gap Register](docs/IMPROVEMENTS.md) — missing pieces, risks, and recommended next improvements.
- [Architecture Decisions](docs/DECISIONS.md) — the decisions that keep strategy, risk, and platform responsibilities separate.
- [Platform API Notes](docs/research/PLATFORM_API_NOTES.md) — verified cTrader API facts and the Exness platform caveat.

## Risk warning

Trading CFDs and leveraged financial instruments carries a high risk of loss. A backtest is not a promise of future performance. Historical data can be incomplete, spreads can change, slippage can occur, and broker execution conditions can differ from research assumptions.

This project is software and research documentation, not financial advice. Use Shadow mode and demo testing before considering any live deployment, and do not risk money that you cannot afford to lose.

## References

[1]: https://help.ctrader.com/ctrader-algo/how-tos/cbots/backtest-a-cbot/ "cTrader Algo: Backtest a cBot"
[2]: https://help.ctrader.com/ctrader-algo/how-tos/cbots/handle-bar-events/ "cTrader Algo: How to handle bar events"
[3]: https://help.ctrader.com/ctrader-algo/faq/ "cTrader Algo FAQ"
[4]: https://help.ctrader.com/ctrader-algo/how-tos/cbots/optimise-a-cbot/ "cTrader Algo: Optimise a cBot"
[5]: https://www.exness.com/trading-platforms/ "Exness trading platforms overview"
[6]: https://www.exness.com/indices/us-tech-100/ "Exness US Tech 100 index trading USTEC"
