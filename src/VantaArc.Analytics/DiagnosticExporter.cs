using System.Text;
using System.Text.Json;
using VantaArc.Core;

namespace VantaArc.Analytics;

public static class DiagnosticExporter
{
    public static async Task ExportJsonAsync(DiagnosticLedger ledger, string path, CancellationToken cancellationToken = default)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(path, ledger.ToJson(options.WriteIndented), Encoding.UTF8, cancellationToken);
    }

    public static string BuildSummaryMarkdown(DiagnosticLedger ledger, string symbol, string timeframe)
    {
        var summary = ledger.Summarize();
        var builder = new StringBuilder();
        builder.AppendLine($"# Diagnostic summary — {symbol} {timeframe}");
        builder.AppendLine();
        builder.AppendLine("| Funnel stage | Count |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| Context bars | {summary.ContextBars} |");
        builder.AppendLine($"| Valid regime bars | {summary.ValidRegimeBars} |");
        builder.AppendLine($"| Bars with level touches | {summary.LevelTouchBars} |");
        builder.AppendLine($"| Confluence arms | {summary.ConfluenceArms} |");
        builder.AppendLine($"| Confirmation events | {summary.ConfirmationEvents} |");
        builder.AppendLine($"| Accepted signals | {summary.AcceptedSignals} |");
        builder.AppendLine($"| Execution attempts | {summary.ExecutionAttempts} |");
        builder.AppendLine($"| Fills | {summary.Fills} |");
        builder.AppendLine();
        builder.AppendLine("## Reasons");
        builder.AppendLine();
        builder.AppendLine("| Reason | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var reason in summary.Reasons.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key))
            builder.AppendLine($"| `{reason.Key}` | {reason.Value} |");
        return builder.ToString();
    }
}
