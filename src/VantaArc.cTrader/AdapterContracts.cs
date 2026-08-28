using VantaArc.Core;

namespace VantaArc.cTrader;

/// <summary>Minimal platform boundary. The core never depends on cTrader API types.</summary>
public interface IMarketDataAdapter
{
    string SymbolName { get; }
    string Timeframe { get; }
    IReadOnlyList<Candle> CompletedBars { get; }
    BrokerSnapshot Snapshot { get; }
}

public interface IExecutionAdapter
{
    bool HasManagedPosition { get; }
    Task<ExecutionResult> SubmitAsync(ExecutionDecision decision, CancellationToken cancellationToken = default);
    Task<ExecutionResult> ModifyStopAsync(double newStopPrice, CancellationToken cancellationToken = default);
    Task<ExecutionResult> CloseAsync(string reason, CancellationToken cancellationToken = default);
}

public sealed record ExecutionResult(bool Success, string Reason, string? BrokerCode = null, double? FillPrice = null, double? VolumeUnits = null);

public sealed class ShadowExecutionAdapter : IExecutionAdapter
{
    public bool HasManagedPosition { get; private set; }
    public List<ExecutionDecision> Decisions { get; } = new();

    public Task<ExecutionResult> SubmitAsync(ExecutionDecision decision, CancellationToken cancellationToken = default)
    {
        Decisions.Add(decision);
        return Task.FromResult(new ExecutionResult(true, "SHADOW_ORDER_NOT_SENT", "SHADOW"));
    }

    public Task<ExecutionResult> ModifyStopAsync(double newStopPrice, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExecutionResult(true, "SHADOW_STOP_NOT_SENT", "SHADOW", newStopPrice));

    public Task<ExecutionResult> CloseAsync(string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExecutionResult(true, "SHADOW_CLOSE_NOT_SENT", "SHADOW"));
}

/// <summary>Coordinates one closed-bar strategy decision and records the full outcome.</summary>
public sealed class ShadowDecisionRunner
{
    private readonly VwapConfluenceStateMachine _machine;
    private readonly DiagnosticLedger _ledger;
    private readonly RiskPolicy _riskPolicy;
    private readonly OperatingMode _mode;

    public ShadowDecisionRunner(VwapConfluenceStateMachine machine, DiagnosticLedger ledger, RiskPolicy riskPolicy, OperatingMode mode)
    {
        _machine = machine;
        _ledger = ledger;
        _riskPolicy = riskPolicy;
        _mode = mode;
    }

    public StrategyDecision Evaluate(IMarketDataAdapter market)
    {
        var decision = _machine.Process(market.CompletedBars, new StrategyParameters(), market.CompletedBars[^1].TimeUtc);
        var executionAttempted = false;
        var filled = false;
        if (decision.MayProceedToRisk && decision.Direction != TradeDirection.None)
        {
            executionAttempted = false;
        }
        _ledger.Record(new DecisionEvent(
            "runtime", Guid.NewGuid().ToString("N"), decision.BarTimeUtc, market.SymbolName, market.Timeframe, _mode,
            decision.Regime, decision.Setup.State, decision.Reason, decision.SignalAccepted, decision.MayProceedToRisk,
            executionAttempted, filled, decision.LongConfluence.Count, decision.ShortConfluence.Count,
            market.Snapshot.SpreadPips, "runtime-config"));
        return decision;
    }
}
