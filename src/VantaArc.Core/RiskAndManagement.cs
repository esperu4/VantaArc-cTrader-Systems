namespace VantaArc.Core;

public static class RiskCalculator
{
    public static RiskResult Evaluate(TradeCandidate candidate, BrokerSnapshot broker, RiskPolicy policy, bool hasManagedPosition)
    {
        if (!broker.HasFreshQuote || broker.Bid <= 0 || broker.Ask <= 0) return Fail("QUOTE_INVALID");
        if (!broker.IsDemoAccount && (!policy.EnableOrderExecution || !policy.LiveReadyAcknowledged)) return Fail("LIVE_EXECUTION_NOT_ACKNOWLEDGED");
        if (policy.MaximumSpreadPips <= 0 || broker.SpreadPips > policy.MaximumSpreadPips) return Fail("SPREAD_TOO_WIDE");
        if (policy.OnePositionPerSymbol && hasManagedPosition) return Fail("POSITION_ALREADY_EXISTS");
        if (policy.RiskPercent <= 0 || policy.DailyLossLimitPercent <= 0) return Fail("RISK_POLICY_INVALID");
        if (broker.DayProfitAndLoss <= -(broker.Equity * policy.DailyLossLimitPercent / 100.0)) return Fail("DAILY_LOSS_LOCK");
        var stopDistance = Math.Abs(candidate.EntryPrice - candidate.StopPrice);
        if (stopDistance <= 0 || broker.MinimumStopDistancePrice <= 0 || stopDistance < broker.MinimumStopDistancePrice) return Fail("STOP_DISTANCE_INVALID");
        if (broker.Volume.StepUnits <= 0 || broker.Volume.MinimumUnits <= 0 || broker.Volume.MaximumUnits < broker.Volume.MinimumUnits) return Fail("VOLUME_SPEC_INVALID");
        if (broker.TickSize <= 0 || broker.TickValuePerUnit <= 0) return Fail("TICK_ECONOMICS_INVALID");
        if (broker.MarginPerUnit <= 0 || broker.FreeMargin <= 0) return Fail("MARGIN_ECONOMICS_INVALID");
        var riskMoney = broker.Equity * policy.RiskPercent / 100.0;
        // Price distance is converted into money using the broker's tick economics.
        // This is the critical boundary between strategy mathematics and broker execution.
        var lossPerUnit = stopDistance / broker.TickSize * broker.TickValuePerUnit;
        var rawUnits = riskMoney / lossPerUnit;
        var normalized = Math.Floor(rawUnits / broker.Volume.StepUnits) * broker.Volume.StepUnits;
        if (normalized < broker.Volume.MinimumUnits) return Fail("MIN_VOLUME_EXCEEDS_RISK_BUDGET");
        if (normalized > broker.Volume.MaximumUnits) normalized = broker.Volume.MaximumUnits;
        if (normalized <= 0) return Fail("NORMALIZED_VOLUME_INVALID");
        if (broker.FreeMargin - normalized * broker.MarginPerUnit < policy.MinimumFreeMarginAfterOrder) return Fail("FREE_MARGIN_RESERVE_BREACHED");
        return new RiskResult(true, "RISK_CHECKS_PASSED", normalized, normalized * lossPerUnit, lossPerUnit);

        RiskResult Fail(string reason) => new(false, reason, 0, 0, 0);
    }
}

public static class PositionManager
{
    public static PositionManagementDecision Evaluate(PositionSnapshot position, RiskPolicy policy, double breakevenAtR = 1.0, double lockAtR = 1.5, double lockProfitR = 0.5, double trailAtR = 2.0, double trailAtrMultiplier = 1.0, double structurePrice = 0)
    {
        if (position.InitialRiskPrice <= 0) return new(false, null, true, ManagementStage.None, "INVALID_INITIAL_RISK");
        var targetStage = position.CurrentR >= trailAtR ? ManagementStage.Trailing : position.CurrentR >= lockAtR ? ManagementStage.ProfitLock : position.CurrentR >= breakevenAtR ? ManagementStage.Breakeven : ManagementStage.InitialProtection;
        var proposed = position.StopPrice;
        if (targetStage == ManagementStage.Breakeven)
            proposed = position.Direction == TradeDirection.Long ? position.EntryPrice : position.EntryPrice;
        else if (targetStage == ManagementStage.ProfitLock)
            proposed = position.Direction == TradeDirection.Long ? position.EntryPrice + lockProfitR * position.InitialRiskPrice : position.EntryPrice - lockProfitR * position.InitialRiskPrice;
        else if (targetStage == ManagementStage.Trailing)
        {
            var atrStop = position.Direction == TradeDirection.Long ? position.CurrentPrice - trailAtrMultiplier * position.Atr : position.CurrentPrice + trailAtrMultiplier * position.Atr;
            proposed = structurePrice > 0 ? (position.Direction == TradeDirection.Long ? Math.Max(atrStop, structurePrice) : Math.Min(atrStop, structurePrice)) : atrStop;
        }

        var improves = position.Direction == TradeDirection.Long ? proposed > position.StopPrice : proposed < position.StopPrice;
        return improves
            ? new(true, proposed, false, targetStage, $"STOP_IMPROVED_TO_{targetStage.ToString().ToUpperInvariant()}")
            : new(false, null, false, position.Stage >= targetStage ? position.Stage : targetStage, "STOP_UNCHANGED_OR_WOULD_WIDEN");
    }
}
