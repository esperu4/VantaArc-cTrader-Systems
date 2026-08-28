using VantaArc.Core;
using VantaArc.cTrader;

namespace VantaArc.Integration.Tests;

public sealed class DecisionCoordinatorTests
{
    [Fact]
    public async Task ShadowCoordinatorNeverSendsAnOrder()
    {
        var ledger = new DiagnosticLedger();
        var adapter = new ShadowExecutionAdapter();
        var coordinator = new DecisionCoordinator(
            new VwapConfluenceStateMachine(), ledger, adapter,
            new StrategyParameters { MinimumConfluences = 1, ConfluenceTolerancePrice = 0.25, ConfluenceToleranceAtr = 0, AtrPeriod = 1, SessionStartHourUtc = 9, SessionStartMinuteUtc = 0, SessionEndHourUtc = 17 },
            new RiskPolicy { EnableOrderExecution = false });

        var bars = new[]
        {
            Candle(9, 0, 100, 102, 99, 101),
            Candle(9, 5, 101, 105, 100, 104),
            Candle(9, 10, 106, 112, 105, 110),
            Candle(9, 15, 110, 111, 109, 110.5),
            Candle(9, 20, 110.5, 111, 104, 104.5)
        };
        var market = new FakeMarket(bars, Broker());
        var first = await coordinator.OnCompletedBarAsync(market);
        Assert.NotNull(first.Strategy);
        Assert.Single(ledger.Events);

        var result = await coordinator.OnCompletedBarAsync(market with { CompletedBars = bars });
        Assert.Equal("DUPLICATE_BAR_IGNORED", result.Strategy.Reason);
        Assert.Empty(adapter.Decisions);
        Assert.Equal(2, ledger.Events.Count);
    }

    private static Candle Candle(int hour, int minute, double open, double high, double low, double close) => new(new DateTime(2026, 1, 5, hour, minute, 0, DateTimeKind.Utc), open, high, low, close, 1);

    private static BrokerSnapshot Broker() => new("USTEC_TEST", 104.4, 104.6, 0.1, 0.1, 0.01, 1, 1, 10_000, 9_000, 1, 0, new BrokerVolumeSpec(1, 1000, 1), true, true, 0.01);

    private sealed record FakeMarket(IReadOnlyList<Candle> CompletedBars, BrokerSnapshot Snapshot) : IMarketDataAdapter
    {
        public string SymbolName => "USTEC_TEST";
        public string Timeframe => "M5";
    }
}
