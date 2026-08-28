using VantaArc.Core;

namespace VantaArc.cTrader;

/// <summary>
/// The only path from a strategy decision to an execution adapter. This keeps
/// strategy, safety, and broker actions observable as separate outcomes.
/// </summary>
public sealed class DecisionCoordinator
{
    private readonly VwapConfluenceStateMachine _machine;
    private readonly DiagnosticLedger _ledger;
    private readonly IExecutionAdapter _execution;
    private readonly StrategyParameters _strategy;
    private readonly RiskPolicy _risk;
    private readonly string _runId;

    public DecisionCoordinator(VwapConfluenceStateMachine machine, DiagnosticLedger ledger, IExecutionAdapter execution, StrategyParameters strategy, RiskPolicy risk, string? runId = null)
    {
        _machine = machine;
        _ledger = ledger;
        _execution = execution;
        _strategy = strategy;
        _risk = risk;
        _runId = runId ?? Guid.NewGuid().ToString("N");
    }

    public async Task<DecisionOutcome> OnCompletedBarAsync(IMarketDataAdapter market, CancellationToken cancellationToken = default)
    {
        var bars = market.CompletedBars.OrderBy(b => b.TimeUtc).ToArray();
        if (bars.Length == 0) throw new InvalidOperationException("NO_COMPLETED_BARS");
        var strategyDecision = _machine.Process(bars, _strategy, bars[^1].TimeUtc);
        if (!strategyDecision.MayProceedToRisk || strategyDecision.Direction == TradeDirection.None)
        {
            Record(market, strategyDecision, false, false, false);
            return new DecisionOutcome(strategyDecision, null, null);
        }

        var previous = bars.Length >= 2 ? bars[^2] : bars[^1];
        var entry = strategyDecision.Direction == TradeDirection.Long ? market.Snapshot.Ask : market.Snapshot.Bid;
        var stop = strategyDecision.Direction == TradeDirection.Long
            ? previous.Low - strategyDecision.Atr * _strategy.StopAtrBuffer
            : previous.High + strategyDecision.Atr * _strategy.StopAtrBuffer;
        var candidate = new TradeCandidate(strategyDecision.Direction, entry, stop, strategyDecision.Atr, "VantaArc-NAS100-VWAP");
        var risk = RiskCalculator.Evaluate(candidate, market.Snapshot, _risk, _execution.HasManagedPosition);
        if (!risk.Passed)
        {
            Record(market, strategyDecision, false, true, false);
            return new DecisionOutcome(strategyDecision, candidate, new ExecutionResult(false, risk.Reason));
        }

        var executionDecision = new ExecutionDecision(true, "READY_FOR_EXECUTION", risk, candidate);
        var result = await _execution.SubmitAsync(executionDecision, cancellationToken);
        var filled = result.Success && result.Reason is not "SHADOW_ORDER_NOT_SENT";
        Record(market, strategyDecision, true, true, filled);
        return new DecisionOutcome(strategyDecision, candidate, result);
    }

    private void Record(IMarketDataAdapter market, StrategyDecision decision, bool riskPassed, bool executionAttempted, bool filled)
    {
        _ledger.Record(new DecisionEvent(
            _runId, Guid.NewGuid().ToString("N"), decision.BarTimeUtc, market.SymbolName, market.Timeframe,
            _risk.EnableOrderExecution ? OperatingMode.Demo : OperatingMode.Shadow, decision.Regime, decision.Setup.State,
            decision.Reason, decision.SignalAccepted, riskPassed, executionAttempted, filled,
            decision.LongConfluence.Count, decision.ShortConfluence.Count, market.Snapshot.SpreadPips, "runtime-config"));
    }
}

public sealed record DecisionOutcome(StrategyDecision Strategy, TradeCandidate? Candidate, ExecutionResult? Execution);
