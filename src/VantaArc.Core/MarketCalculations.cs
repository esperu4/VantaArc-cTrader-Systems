namespace VantaArc.Core;

public static class VwapCalculator
{
    public static VwapValue Calculate(string name, IReadOnlyList<Candle> bars, DateTime anchorStartUtc, DateTime anchorEndUtc)
    {
        var included = bars.Where(b => b.TimeUtc >= anchorStartUtc && b.TimeUtc < anchorEndUtc && b.Volume > 0).ToArray();
        if (included.Length == 0)
            return new VwapValue(name, anchorStartUtc, anchorEndUtc, 0, 0, 0, false);

        var volumeTotal = included.Sum(b => b.Volume);
        if (volumeTotal <= 0)
            return new VwapValue(name, anchorStartUtc, anchorEndUtc, 0, 0, included.Length, false);

        var value = included.Sum(b => b.TypicalPrice * b.Volume) / volumeTotal;
        var variance = included.Sum(b => Math.Pow(b.TypicalPrice - value, 2) * b.Volume) / volumeTotal;
        return new VwapValue(name, anchorStartUtc, anchorEndUtc, value, Math.Sqrt(Math.Max(0, variance)), included.Length, true);
    }
}

public static class AtrCalculator
{
    public static double Calculate(IReadOnlyList<Candle> bars, int period)
    {
        if (period <= 0 || bars.Count < period + 1) return 0;
        var ordered = bars.OrderBy(b => b.TimeUtc).ToArray();
        var trs = new List<double>(ordered.Length - 1);
        for (var i = 1; i < ordered.Length; i++)
        {
            var previousClose = ordered[i - 1].Close;
            trs.Add(Math.Max(ordered[i].High - ordered[i].Low,
                Math.Max(Math.Abs(ordered[i].High - previousClose), Math.Abs(ordered[i].Low - previousClose))));
        }
        return trs.TakeLast(period).Average();
    }
}

public sealed record SessionWindow(DateTime StartUtc, DateTime EndUtc)
{
    public bool Contains(DateTime timeUtc) => timeUtc >= StartUtc && timeUtc < EndUtc;
}

public static class SessionClock
{
    public static SessionWindow ForBar(DateTime barTimeUtc, StrategyParameters parameters)
    {
        var date = barTimeUtc.Date;
        var startToday = date.AddHours(parameters.SessionStartHourUtc).AddMinutes(parameters.SessionStartMinuteUtc);
        var start = barTimeUtc >= startToday ? startToday : startToday.AddDays(-1);
        var end = start.Date.AddHours(parameters.SessionEndHourUtc).AddMinutes(parameters.SessionEndMinuteUtc);
        if (end <= start) end = end.AddDays(1);
        return new SessionWindow(start, end);
    }
}

public static class RegimeClassifier
{
    public static (MarketRegime Regime, bool Allowed, string Reason) Classify(double close, VwapValue daily, StrategyParameters parameters)
    {
        if (!daily.IsValid || daily.Sigma <= 0)
            return (MarketRegime.Unknown, false, "DAILY_VWAP_OR_SIGMA_INVALID");
        if (parameters.BalanceBandSigma <= 0 || parameters.DiscoveryBandSigma <= parameters.BalanceBandSigma)
            return (MarketRegime.Unknown, false, "REGIME_THRESHOLDS_INVALID");

        var distance = Math.Abs(close - daily.Value) / daily.Sigma;
        var regime = distance <= parameters.BalanceBandSigma
            ? MarketRegime.Balanced
            : distance < parameters.DiscoveryBandSigma ? MarketRegime.Directional : MarketRegime.Discovery;
        var allowed = regime switch
        {
            MarketRegime.Balanced => parameters.AllowBalanced,
            MarketRegime.Directional => parameters.AllowDirectional,
            MarketRegime.Discovery => parameters.AllowDiscovery,
            _ => false
        };
        return (regime, allowed, allowed ? "REGIME_ALLOWED" : "REGIME_NOT_ALLOWED");
    }
}

public static class MarketContextBuilder
{
    public static MarketContext Build(IReadOnlyList<Candle> completedBars, Candle currentBar, StrategyParameters parameters)
    {
        var ordered = completedBars.OrderBy(b => b.TimeUtc).ToArray();
        var dayStart = currentBar.TimeUtc.Date;
        var weekStart = dayStart.AddDays(-(int)currentBar.TimeUtc.DayOfWeek + (int)DayOfWeek.Monday);
        if (currentBar.TimeUtc.DayOfWeek == DayOfWeek.Sunday) weekStart = dayStart.AddDays(-6);
        var session = SessionClock.ForBar(currentBar.TimeUtc, parameters);

        var daily = VwapCalculator.Calculate("DailyVWAP", ordered, dayStart, dayStart.AddDays(1));
        var weekly = VwapCalculator.Calculate("WeeklyVWAP", ordered, weekStart, weekStart.AddDays(7));
        var sessionVwap = VwapCalculator.Calculate("SessionVWAP", ordered, session.StartUtc, session.EndUtc);
        var atr = AtrCalculator.Calculate(ordered, parameters.AtrPeriod);
        var (regime, allowed, reason) = RegimeClassifier.Classify(currentBar.Close, daily, parameters);
        return new MarketContext(currentBar.TimeUtc, daily, weekly, sessionVwap, atr, regime, allowed, reason);
    }
}

public static class CandlePatterns
{
    public static bool IsBearishEngulfing(Candle previous, Candle current, double minimumBodyToRange)
    {
        if (!previous.IsBullish || !current.IsBearish || current.Range <= 0) return false;
        return current.Open >= previous.Close && current.Close <= previous.Open && current.Body / current.Range >= minimumBodyToRange;
    }

    public static bool IsBullishEngulfing(Candle previous, Candle current, double minimumBodyToRange)
    {
        if (!previous.IsBearish || !current.IsBullish || current.Range <= 0) return false;
        return current.Open <= previous.Close && current.Close >= previous.Open && current.Body / current.Range >= minimumBodyToRange;
    }
}

public static class ConfluenceDetector
{
    public static ConfluenceResult Find(TradeDirection direction, MarketContext context, Candle interaction, StrategyParameters parameters)
    {
        var tolerance = Math.Max(context.Atr * parameters.ConfluenceToleranceAtr, parameters.ConfluenceTolerancePrice);
        var levels = new Dictionary<string, double>();
        if (parameters.DailyVwapEnabled && context.Daily.IsValid)
            levels[direction == TradeDirection.Long ? "DailyVWAP-Lower1Sigma" : "DailyVWAP-Upper1Sigma"] = direction == TradeDirection.Long ? context.Daily.LowerBand(1) : context.Daily.UpperBand(1);
        if (parameters.WeeklyVwapEnabled && context.Weekly.IsValid)
            levels["WeeklyVWAP"] = context.Weekly.Value;
        if (parameters.SessionVwapEnabled && context.Session.IsValid)
            levels[direction == TradeDirection.Long ? "SessionVWAP-Lower1Sigma" : "SessionVWAP-Upper1Sigma"] = direction == TradeDirection.Long ? context.Session.LowerBand(1) : context.Session.UpperBand(1);

        var touched = levels.Where(pair => interaction.High >= pair.Value - tolerance && interaction.Low <= pair.Value + tolerance).Select(pair => pair.Key).ToArray();
        return new ConfluenceResult(direction, touched.Length, touched, levels, tolerance, touched.Length >= Math.Max(1, parameters.MinimumConfluences));
    }
}
