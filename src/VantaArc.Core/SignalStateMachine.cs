namespace VantaArc.Core;

/// <summary>
/// Stateful signal engine. It intentionally separates the interaction candle
/// from the later confirmation candle so the strategy cannot accidentally
/// require both events to happen on one bar or use look-ahead information.
/// </summary>
public sealed class VwapConfluenceStateMachine
{
    private SetupSnapshot _setup = SetupSnapshot.Waiting("STARTUP");
    private DateTime? _lastProcessedBarUtc;

    public SetupSnapshot Setup => _setup;

    public StrategyDecision Process(IReadOnlyList<Candle> history, StrategyParameters parameters, DateTime barTimeUtc)
    {
        if (history.Count == 0) return Decision(barTimeUtc, MarketRegime.Unknown, "NO_COMPLETED_BARS", false, false, ConfluenceResult.Empty(TradeDirection.Long), ConfluenceResult.Empty(TradeDirection.Short), 0);
        var current = history.OrderBy(b => b.TimeUtc).Last();
        if (current.TimeUtc != barTimeUtc) return Decision(barTimeUtc, MarketRegime.Unknown, "BAR_TIME_MISMATCH", false, false, ConfluenceResult.Empty(TradeDirection.Long), ConfluenceResult.Empty(TradeDirection.Short), 0);
        if (_lastProcessedBarUtc == barTimeUtc) return Decision(barTimeUtc, MarketRegime.Unknown, "DUPLICATE_BAR_IGNORED", false, false, ConfluenceResult.Empty(TradeDirection.Long), ConfluenceResult.Empty(TradeDirection.Short), 0);
        _lastProcessedBarUtc = barTimeUtc;

        var context = MarketContextBuilder.Build(history, current, parameters);
        var session = SessionClock.ForBar(current.TimeUtc, parameters);
        if (!session.Contains(current.TimeUtc))
        {
            _setup = SetupSnapshot.Waiting("OUTSIDE_TRADING_WINDOW");
            return Decision(current.TimeUtc, context.Regime, "OUTSIDE_TRADING_WINDOW", false, false, ConfluenceResult.Empty(TradeDirection.Long), ConfluenceResult.Empty(TradeDirection.Short), context.Atr);
        }
        if (!context.Daily.IsValid || !context.Session.IsValid || context.Atr <= 0)
        {
            _setup = SetupSnapshot.Waiting("CONTEXT_INVALID");
            return Decision(current.TimeUtc, context.Regime, "CONTEXT_INVALID", false, false, ConfluenceResult.Empty(TradeDirection.Long), ConfluenceResult.Empty(TradeDirection.Short), context.Atr);
        }

        var longConfluence = ConfluenceDetector.Find(TradeDirection.Long, context, current, parameters);
        var shortConfluence = ConfluenceDetector.Find(TradeDirection.Short, context, current, parameters);
        if (_setup.InteractionTimeUtc is not null)
        {
            var barsSinceArm = history.Count(b => b.TimeUtc > _setup.InteractionTimeUtc.Value && b.TimeUtc <= current.TimeUtc);
            if (barsSinceArm > parameters.ConfirmationWindowBars)
            {
                _setup = SetupSnapshot.Waiting("CONFIRMATION_EXPIRED");
                return Decision(current.TimeUtc, context.Regime, "CONFIRMATION_EXPIRED", false, false, longConfluence, shortConfluence, context.Atr);
            }
            var confirmation = history.Count >= 2 ? CandlePatterns.IsBullishEngulfing(history[^2], current, parameters.MinimumBodyToRange) || CandlePatterns.IsBearishEngulfing(history[^2], current, parameters.MinimumBodyToRange) : false;
            if (_setup.Direction == TradeDirection.Long && history.Count >= 2 && CandlePatterns.IsBullishEngulfing(history[^2], current, parameters.MinimumBodyToRange))
            {
                _setup = _setup with { State = SetupState.CandidateValidated, BarsSinceArm = barsSinceArm, Reason = "BULLISH_CONFIRMATION_ACCEPTED" };
                return Decision(current.TimeUtc, context.Regime, "BULLISH_CONFIRMATION_ACCEPTED", true, true, longConfluence, shortConfluence, context.Atr);
            }
            if (_setup.Direction == TradeDirection.Short && history.Count >= 2 && CandlePatterns.IsBearishEngulfing(history[^2], current, parameters.MinimumBodyToRange))
            {
                _setup = _setup with { State = SetupState.CandidateValidated, BarsSinceArm = barsSinceArm, Reason = "BEARISH_CONFIRMATION_ACCEPTED" };
                return Decision(current.TimeUtc, context.Regime, "BEARISH_CONFIRMATION_ACCEPTED", true, true, longConfluence, shortConfluence, context.Atr);
            }
            _setup = _setup with { State = SetupState.WaitingForConfirmation, BarsSinceArm = barsSinceArm, Reason = confirmation ? "OPPOSITE_DIRECTION_CONFIRMATION" : "WAITING_FOR_CONFIRMATION" };
            return Decision(current.TimeUtc, context.Regime, _setup.Reason, false, false, longConfluence, shortConfluence, context.Atr);
        }

        if (!context.RegimeAllowed)
        {
            _setup = SetupSnapshot.Waiting(context.RegimeReason);
            return Decision(current.TimeUtc, context.Regime, context.RegimeReason, false, false, longConfluence, shortConfluence, context.Atr);
        }

        var longEligible = longConfluence.MeetsMinimum;
        var shortEligible = shortConfluence.MeetsMinimum;
        if (longEligible == shortEligible)
        {
            var reason = longEligible ? "CONFLICTING_LONG_SHORT_CONFLUENCE" : "WAITING_FOR_CONFLUENCE";
            _setup = SetupSnapshot.Waiting(reason);
            return Decision(current.TimeUtc, context.Regime, reason, false, false, longConfluence, shortConfluence, context.Atr);
        }

        var direction = longEligible ? TradeDirection.Long : TradeDirection.Short;
        var chosen = longEligible ? longConfluence : shortConfluence;
        _setup = new SetupSnapshot(SetupState.ConfluenceArmed, direction, context.Regime, current.TimeUtc, 0, chosen.Count, chosen.TouchedLevels, "CONFLUENCE_ARMED");
        return Decision(current.TimeUtc, context.Regime, "CONFLUENCE_ARMED", false, false, longConfluence, shortConfluence, context.Atr);
    }

    public void Reset(string reason = "RESET") => _setup = SetupSnapshot.Waiting(reason);

    private StrategyDecision Decision(DateTime time, MarketRegime regime, string reason, bool accepted, bool proceed, ConfluenceResult longConfluence, ConfluenceResult shortConfluence, double atr) =>
        new(time, accepted ? SetupState.CandidateValidated : _setup.State, _setup.Direction, regime, reason, accepted, proceed, _setup, longConfluence, shortConfluence, atr);
}
