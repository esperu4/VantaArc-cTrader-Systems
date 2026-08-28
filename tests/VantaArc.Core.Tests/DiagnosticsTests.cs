using VantaArc.Core;

namespace VantaArc.Core.Tests;

public sealed class DiagnosticsTests
{
    [Fact]
    public void LedgerSummarizesOpportunityFunnelAndReasons()
    {
        var ledger = new DiagnosticLedger();
        ledger.Record(Event("CONFLUENCE_ARMED", false, false, false, false));
        ledger.Record(Event("BEARISH_CONFIRMATION_ACCEPTED", true, true, true, true));
        ledger.Record(Event("SPREAD_TOO_WIDE", false, false, false, false));

        var summary = ledger.Summarize();
        Assert.Equal(3, summary.ContextBars);
        Assert.Equal(3, summary.ValidRegimeBars);
        Assert.Equal(3, summary.LevelTouchBars);
        Assert.Equal(1, summary.ConfluenceArms);
        Assert.Equal(1, summary.AcceptedSignals);
        Assert.Equal(1, summary.ExecutionAttempts);
        Assert.Equal(1, summary.Fills);
        Assert.Equal(1, summary.Reasons["SPREAD_TOO_WIDE"]);
    }

    [Fact]
    public void LedgerExportsStructuredJson()
    {
        var ledger = new DiagnosticLedger();
        ledger.Record(Event("WAITING_FOR_CONFIRMATION", false, false, false, false));
        var json = ledger.ToJson();
        Assert.Contains("WAITING_FOR_CONFIRMATION", json);
        Assert.Contains("DecisionId", json);
    }

    private static DecisionEvent Event(string reason, bool accepted, bool risk, bool attempted, bool filled) => new(
        "run", Guid.NewGuid().ToString("N"), new DateTime(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc),
        "USTEC_TEST", "M5", OperatingMode.Shadow, MarketRegime.Directional,
        accepted ? SetupState.CandidateValidated : SetupState.WaitingForConfirmation,
        reason, accepted, risk, attempted, filled, 1, 2, 0.5, "test");
}
