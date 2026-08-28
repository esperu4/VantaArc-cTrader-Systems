using VantaArc.Core;

namespace VantaArc.Core.Tests;

public sealed class MarketCalculationTests
{
    [Fact]
    public void VwapUsesVolumeWeightedTypicalPricesAndPopulationSigma()
    {
        var bars = new[]
        {
            new Candle(Utc(9, 0), 99, 101, 99, 100, 1),
            new Candle(Utc(9, 5), 109, 111, 109, 110, 3)
        };
        var result = VwapCalculator.Calculate("test", bars, Utc(9, 0), Utc(10, 0));
        Assert.True(result.IsValid);
        Assert.Equal(107.5, result.Value, 8);
        Assert.Equal(Math.Sqrt(18.75), result.Sigma, 8);
        Assert.Equal(2, result.BarsIncluded);
    }

    [Fact]
    public void VwapUsesHalfOpenAnchorWindow()
    {
        var bars = new[]
        {
            new Candle(Utc(9, 0), 99, 101, 99, 100, 1),
            new Candle(Utc(10, 0), 109, 111, 109, 110, 3)
        };
        var result = VwapCalculator.Calculate("test", bars, Utc(9, 0), Utc(10, 0));
        Assert.Equal(100, result.Value, 8);
        Assert.Equal(1, result.BarsIncluded);
    }

    [Fact]
    public void RegimeClassifierReturnsBalancedDirectionalDiscoveryAndUnknown()
    {
        var parameters = new StrategyParameters();
        var daily = new VwapValue("daily", Utc(0, 0), Utc(1, 0), 100, 10, 10, true);
        Assert.Equal(MarketRegime.Balanced, RegimeClassifier.Classify(105, daily, parameters).Regime);
        Assert.Equal(MarketRegime.Directional, RegimeClassifier.Classify(115, daily, parameters).Regime);
        Assert.Equal(MarketRegime.Discovery, RegimeClassifier.Classify(125, daily, parameters).Regime);
        Assert.Equal(MarketRegime.Unknown, RegimeClassifier.Classify(105, daily with { Sigma = 0 }, parameters).Regime);
    }

    [Fact]
    public void DisabledRegimeRemainsVisibleButIsNotAllowed()
    {
        var parameters = new StrategyParameters { AllowDirectional = false };
        var daily = new VwapValue("daily", Utc(0, 0), Utc(1, 0), 100, 10, 10, true);
        var result = RegimeClassifier.Classify(115, daily, parameters);
        Assert.Equal(MarketRegime.Directional, result.Regime);
        Assert.False(result.Allowed);
        Assert.Equal("REGIME_NOT_ALLOWED", result.Reason);
    }

    [Fact]
    public void CandlePatternChecksBodyEngulfingAndConfirmationBodyQuality()
    {
        var bullish = new Candle(Utc(10, 0), 100, 103, 99, 102, 1);
        var bearish = new Candle(Utc(10, 5), 103, 104, 96, 98, 1);
        Assert.True(CandlePatterns.IsBearishEngulfing(bullish, bearish, 0.5));
        Assert.False(CandlePatterns.IsBullishEngulfing(bullish, bearish, 0.5));

        var weak = bearish with { High = 120, Low = 90 };
        Assert.False(CandlePatterns.IsBearishEngulfing(bullish, weak, 0.5));
    }

    [Fact]
    public void ConfluenceRequiresTheConfiguredNumberOfTouchedLevels()
    {
        var parameters = new StrategyParameters { MinimumConfluences = 2, ConfluenceTolerancePrice = 0.5, ConfluenceToleranceAtr = 0 };
        var context = new MarketContext(Utc(10, 0),
            new VwapValue("daily", Utc(0, 0), Utc(1, 0), 100, 5, 10, true),
            new VwapValue("weekly", Utc(0, 0), Utc(7, 0), 101, 3, 20, true),
            new VwapValue("session", Utc(9, 0), Utc(17, 0), 101.5, 2, 10, true), 1, MarketRegime.Balanced, true, "REGIME_ALLOWED");
        var interaction = new Candle(Utc(10, 0), 100, 101.2, 99.4, 100.5, 1);
        var result = ConfluenceDetector.Find(TradeDirection.Long, context, interaction, parameters);
        Assert.Equal(2, result.Count);
        Assert.True(result.MeetsMinimum);
    }

    private static DateTime Utc(int hour, int minute) => new(2026, 1, 5, hour, minute, 0, DateTimeKind.Utc);
}
