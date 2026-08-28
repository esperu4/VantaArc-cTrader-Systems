# VantaArc cTrader Systems — Architecture Decisions

## ADR-001: Use a separate repository

**Decision:** Keep the cTrader/C# implementation in `VantaArc-cTrader-Systems` and keep the MT5/MQL5 implementation in `VantaArc-Systems`.

**Reason:** The platforms have different APIs, build systems, event models, broker integrations, and deployment workflows. Separate repositories make the tested target and broker assumptions explicit.

## ADR-002: Make the platform-independent core the source of truth

**Decision:** Strategy calculations, state transitions, risk mathematics, and diagnostic contracts belong in `VantaArc.Core` and must not call cTrader APIs.

**Reason:** The strategy can then be unit-tested independently, and the cTrader adapter cannot quietly change the meaning of a signal while translating it into an order.

## ADR-003: Use completed bars for entry decisions

**Decision:** The cTrader entry point uses `OnBarClosed()` for strategy decisions. Tick events are reserved for spread checks, protection, and position management.

**Reason:** This directly expresses the requirement that a confirmation candle must close before entry and prevents intrabar look-ahead behavior.

## ADR-004: Default to Shadow mode

**Decision:** The default operating mode is Shadow, with order execution disabled.

**Reason:** The most important early question is whether the bot sees and explains the intended opportunities. Shadow mode allows chart and log verification without financial exposure.

## ADR-005: Keep the confluence interaction separate from confirmation

**Decision:** The candle that touches the confluence zone arms the setup. A later completed candle must confirm the direction inside a finite window.

**Reason:** This matches the trading idea and avoids the earlier same-candle interpretation that could eliminate intended opportunities or create hindsight ambiguity.

## ADR-006: Use explicit broker economics

**Decision:** Risk sizing requires tick size, tick value per unit, volume limits, volume step, margin per unit, and minimum stop distance. Missing economics fail closed.

**Reason:** NAS100/US Tech 100 contract specifications differ between brokers. Price distance cannot be treated as account-currency risk without broker metadata.

## ADR-007: Never widen protective stops

**Decision:** Every position-management action must prove that the new stop is more protective before modifying a position.

**Reason:** A trailing algorithm must not convert a protective action into increased risk after entry.

## ADR-008: Do not optimize before the opportunity funnel is visible

**Decision:** Baseline shadow and backtest runs precede parameter optimization.

**Reason:** A profitable optimization result is not useful if the system's skipped-signal and broker-blocking behavior is unknown.

## ADR-009: Keep the first release single-symbol and single-strategy scoped

**Decision:** One strategy label and one symbol are the default operating boundary.

**Reason:** Portfolio coordination creates additional state and risk complexity. The first release must prove one strategy end to end before broadening scope.
