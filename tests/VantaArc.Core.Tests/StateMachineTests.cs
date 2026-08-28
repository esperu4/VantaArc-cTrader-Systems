using VantaArc.Core;

namespace VantaArc.Core.Tests;

public sealed class StateMachineTests
{
    [Fact]
    public void ConfluenceArmsBeforeConfirmationAndAcceptsALaterClosedBar()
    {
        var parameters = TestParameters();
        var machine = new VwapConfluenceStateMachine();
        var first = Bars();
        var arm = machine.Process(first, parameters, first[^1].TimeUtc);
        Assert.Equal("CONFLUENCE_ARMED", arm.Reason);
        Assert.Equal(SetupState.ConfluenceArmed, machine.Setup.State);

        var previous = new Candle(Utc(10, 15), 110, 111, 109, 110.5, 1);
        var confirmation = new Candle(Utc(10, 20), 110.5, 111, 104, 104.5, 1);
        var history = first.Concat(new[] { previous, confirmation }).ToArray();
        var decision = machine.Process(history, parameters, confirmation.TimeUtc);
        Assert.True(decision.SignalAccepted);
        Assert.Equal("BEARISH_CONFIRMATION_ACCEPTED", decision.Reason);
    }

    [Fact]
    public void InteractionCandleAloneDoesNotConfirm()
    {
        var parameters = TestParameters();
        var machine = new VwapConfluenceStateMachine();
        var bars = Bars();
        var decision = machine.Process(bars, parameters, bars[^1].TimeUtc);
        Assert.False(decision.SignalAccepted);
        Assert.Equal("CONFLUENCE_ARMED", decision.Reason);
    }

    [Fact]
    public void SetupExpiresAfterConfiguredCompletedBarWindow()
    {
        var parameters = TestParameters() with { ConfirmationWindowBars = 2 };
        var machine = new VwapConfluenceStateMachine();
        var bars = Bars();
        machine.Process(bars, parameters, bars[^1].TimeUtc);
        var next = bars.Concat(new[]
        {
            new Candle(Utc(10, 15), 110, 111, 108, 109, 1),
            new Candle(Utc(10, 20), 109, 110, 107, 108, 1),
            new Candle(Utc(10, 25), 108, 109, 106, 107, 1)
        }).ToArray();
        var decision = machine.Process(next, parameters, next[^1].TimeUtc);
        Assert.Equal("CONFIRMATION_EXPIRED", decision.Reason);
        Assert.False(decision.SignalAccepted);
    }

    [Fact]
    public void SameBarIsProcessedOnlyOnce()
    {
        var parameters = TestParameters();
        var machine = new VwapConfluenceStateMachine();
        var bars = Bars();
        machine.Process(bars, parameters, bars[^1].TimeUtc);
        var duplicate = machine.Process(bars, parameters, bars[^1].TimeUtc);
        Assert.Equal("DUPLICATE_BAR_IGNORED", duplicate.Reason);
    }

    [Fact]
    public void UnknownContextFailsClosed()
    {
        var parameters = TestParameters();
        var machine = new VwapConfluenceStateMachine();
        var bars = Bars(0);
        var decision = machine.Process(bars, parameters, bars[^1].TimeUtc);
        Assert.Equal("CONTEXT_INVALID", decision.Reason);
        Assert.False(decision.MayProceedToRisk);
    }

    private static StrategyParameters TestParameters() => new()
    {
        ConfirmationWindowBars = 3,
        MinimumConfluences = 1,
        ConfluenceToleranceAtr = 0,
        ConfluenceTolerancePrice = 0.25,
        MinimumBodyToRange = 0.5,
        SessionStartHourUtc = 9,
        SessionStartMinuteUtc = 0,
        SessionEndHourUtc = 17,
        SessionEndMinuteUtc = 0,
        AtrPeriod = 1
    };

    private static Candle[] Bars(double volume = 1) => new[]
    {
        new Candle(Utc(9, 0), 100, 102, 99, 101, volume),
        new Candle(Utc(9, 5), 101, 105, 100, 104, volume),
        new Candle(Utc(9, 10), 106, 112, 105, 110, volume)
    };

    private static DateTime Utc(int hour, int minute) => new(2026, 1, 5, hour, minute, 0, DateTimeKind.Utc);
}
