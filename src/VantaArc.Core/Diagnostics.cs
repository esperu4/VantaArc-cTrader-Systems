using System.Text.Json;

namespace VantaArc.Core;

public sealed record DecisionEvent(
    string RunId,
    string DecisionId,
    DateTime TimestampUtc,
    string Symbol,
    string Timeframe,
    OperatingMode Mode,
    MarketRegime Regime,
    SetupState SetupState,
    string Reason,
    bool SignalAccepted,
    bool RiskPassed,
    bool ExecutionAttempted,
    bool Filled,
    int LongConfluenceCount,
    int ShortConfluenceCount,
    double SpreadPips,
    string ConfigurationHash);

public sealed class DiagnosticLedger
{
    private readonly List<DecisionEvent> _events = new();
    public IReadOnlyList<DecisionEvent> Events => _events;

    public void Record(DecisionEvent item) => _events.Add(item);

    public DiagnosticSummary Summarize()
    {
        var signals = _events.Where(e => e.SignalAccepted).ToArray();
        return new DiagnosticSummary(
            _events.Count,
            _events.Count(e => e.Regime != MarketRegime.Unknown),
            _events.Count(e => e.LongConfluenceCount > 0 || e.ShortConfluenceCount > 0),
            _events.Count(e => e.Reason == "CONFLUENCE_ARMED"),
            _events.Count(e => e.Reason.Contains("CONFIRMATION", StringComparison.Ordinal)),
            signals.Length,
            _events.Count(e => e.ExecutionAttempted),
            _events.Count(e => e.Filled),
            _events.GroupBy(e => e.Reason).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()));
    }

    public string ToJson(bool indented = true) => JsonSerializer.Serialize(_events, new JsonSerializerOptions { WriteIndented = indented });
}

public sealed record DiagnosticSummary(
    int ContextBars,
    int ValidRegimeBars,
    int LevelTouchBars,
    int ConfluenceArms,
    int ConfirmationEvents,
    int AcceptedSignals,
    int ExecutionAttempts,
    int Fills,
    IReadOnlyDictionary<string, int> Reasons);
