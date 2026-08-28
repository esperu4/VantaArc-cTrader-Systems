using VantaArc.Core;
using VantaArc.cTrader;

namespace VantaArc.Integration.Tests;

public sealed class GuardrailTests
{
    [Fact]
    public async Task RiskLayerCanPassBeforeShadowAdapterBlocksOrder()
    {
        var risk = RiskCalculator.Evaluate(Candidate(99.97), Broker(isDemo: true), new RiskPolicy { EnableOrderExecution = false }, false);
        Assert.True(risk.Passed);
        var decision = new ExecutionDecision(true, "READY", risk, Candidate(99.97));
        var adapter = new ShadowExecutionAdapter();
        var result = await adapter.SubmitAsync(decision);
        Assert.True(result.Success);
        Assert.Equal("SHADOW_ORDER_NOT_SENT", result.Reason);
    }

    [Fact]
    public void LiveExecutionRequiresExplicitAcknowledgement()
    {
        var result = RiskCalculator.Evaluate(Candidate(), Broker(isDemo: false), new RiskPolicy { EnableOrderExecution = true }, false);
        Assert.False(result.Passed);
        Assert.Equal("LIVE_EXECUTION_NOT_ACKNOWLEDGED", result.Reason);
    }

    [Fact]
    public void ZeroOrInvalidSpreadPolicyFailsClosed()
    {
        var result = RiskCalculator.Evaluate(Candidate(), Broker(isDemo: true), new RiskPolicy { MaximumSpreadPips = 0 }, false);
        Assert.False(result.Passed);
        Assert.Equal("SPREAD_TOO_WIDE", result.Reason);
    }

    [Fact]
    public void DailyLossLimitStopsNewEntries()
    {
        var result = RiskCalculator.Evaluate(Candidate(), Broker(isDemo: true) with { DayProfitAndLoss = -250 }, new RiskPolicy { DailyLossLimitPercent = 2 }, false);
        Assert.False(result.Passed);
        Assert.Equal("DAILY_LOSS_LOCK", result.Reason);
    }

    [Fact]
    public void PositionLimitStopsDuplicateEntry()
    {
        var result = RiskCalculator.Evaluate(Candidate(), Broker(isDemo: true), new RiskPolicy { OnePositionPerSymbol = true }, true);
        Assert.False(result.Passed);
        Assert.Equal("POSITION_ALREADY_EXISTS", result.Reason);
    }

    [Fact]
    public void TickEconomicsAreRequiredForSizing()
    {
        var result = RiskCalculator.Evaluate(Candidate(), Broker(isDemo: true) with { TickValuePerUnit = 0 }, new RiskPolicy(), false);
        Assert.False(result.Passed);
        Assert.Equal("TICK_ECONOMICS_INVALID", result.Reason);
    }

    [Fact]
    public void FreeMarginReserveCanBlockAnOtherwiseValidTrade()
    {
        var result = RiskCalculator.Evaluate(Candidate(99.97), Broker(isDemo: true) with { FreeMargin = 9, MarginPerUnit = 2 }, new RiskPolicy { MinimumFreeMarginAfterOrder = 8 }, false);
        Assert.False(result.Passed);
        Assert.Equal("FREE_MARGIN_RESERVE_BREACHED", result.Reason);
    }

    [Fact]
    public void VolumeIsRoundedDownAndNeverUp()
    {
        var result = RiskCalculator.Evaluate(Candidate(99.97), Broker(isDemo: true) with { Volume = new BrokerVolumeSpec(1, 1000, 3) }, new RiskPolicy { RiskPercent = 0.25 }, false);
        Assert.True(result.Passed);
        Assert.Equal(6, result.VolumeUnits);
        Assert.True(result.RiskMoney <= 25.0);
    }

    [Fact]
    public void LongStopCanImproveButCannotWiden()
    {
        var position = new PositionSnapshot(TradeDirection.Long, 100, 95, 5, 1, 112, 2.5, 10, ManagementStage.InitialProtection);
        var decision = PositionManager.Evaluate(position, new RiskPolicy(), trailAtR: 2, trailAtrMultiplier: 1);
        Assert.True(decision.ShouldModifyStop);
        Assert.True(decision.NewStopPrice > position.StopPrice);
    }

    [Fact]
    public void ShortStopDoesNotWidenWhenProposedStopIsLessProtective()
    {
        var position = new PositionSnapshot(TradeDirection.Short, 100, 90, 10, 1, 92, 2.5, 10, ManagementStage.InitialProtection);
        var decision = PositionManager.Evaluate(position, new RiskPolicy(), trailAtR: 2, trailAtrMultiplier: 1, structurePrice: 110);
        Assert.False(decision.ShouldModifyStop);
        Assert.Equal("STOP_UNCHANGED_OR_WOULD_WIDEN", decision.Reason);
    }

    private static TradeCandidate Candidate(double stop = 99.97) => new(TradeDirection.Long, 100, stop, 1, "TEST");

    private static BrokerSnapshot Broker(bool isDemo) => new(
        "USTEC_TEST", 99.9, 100.1, 0.1, 0.1, 0.01, 1.0, 1.0,
        10_000, 9_000, 1.0, 0, new BrokerVolumeSpec(1, 1000, 1), isDemo, true, 0.01);
}
