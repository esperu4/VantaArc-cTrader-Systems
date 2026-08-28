#!/usr/bin/env python3
"""Static acceptance checks for the VantaArc cTrader repository."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = [
    "README.md",
    "VantaArc.cTrader.sln",
    "docs/PRD.md",
    "docs/CTRADER_SETUP.md",
    "docs/TEST_REPORT.md",
    "docs/IMPROVEMENTS.md",
    "docs/DECISIONS.md",
    "docs/research/PLATFORM_API_NOTES.md",
    "src/VantaArc.Core/Domain.cs",
    "src/VantaArc.Core/MarketCalculations.cs",
    "src/VantaArc.Core/SignalStateMachine.cs",
    "src/VantaArc.Core/RiskAndManagement.cs",
    "src/VantaArc.Core/Diagnostics.cs",
    "src/VantaArc.cTrader/AdapterContracts.cs",
    "src/VantaArc.cTrader/DecisionCoordinator.cs",
    "src/VantaArc.cTrader/cBot/VantaArcNas100VwapConfluenceBot.cs",
    "src/VantaArc.Analytics/DiagnosticExporter.cs",
    "tests/VantaArc.Core.Tests/MarketCalculationTests.cs",
    "tests/VantaArc.Core.Tests/StateMachineTests.cs",
    "tests/VantaArc.Core.Tests/DiagnosticsTests.cs",
    "tests/VantaArc.Integration.Tests/GuardrailTests.cs",
    "tests/VantaArc.Integration.Tests/DecisionCoordinatorTests.cs",
]
for relative in REQUIRED:
    path = ROOT / relative
    assert path.is_file() and path.stat().st_size > 0, f"missing or empty: {relative}"

core = (ROOT / "src/VantaArc.Core").read_text if False else "\n".join(p.read_text() for p in (ROOT / "src/VantaArc.Core").glob("*.cs"))
cbot = (ROOT / "src/VantaArc.cTrader/cBot/VantaArcNas100VwapConfluenceBot.cs").read_text()
readme = (ROOT / "README.md").read_text()

for token in ["ConfirmationWindowBars", "ConfluenceArmed", "CONFIRMATION_EXPIRED", "TickValuePerUnit", "MarginPerUnit", "STOP_UNCHANGED_OR_WOULD_WIDEN"]:
    assert token in core, f"core contract missing: {token}"
for token in ["OnBarClosed", "OnTick", "OperatingMode", "LiveReadinessAcknowledged", "SymbolToken", "GetEstimatedMargin", "SHADOW_ORDER_NOT_SENT", "DIAGNOSTIC_SUMMARY", "DrawStaticText"]:
    assert token in cbot, f"cBot contract missing: {token}"
assert "ClosePosition" not in cbot.split("private void ManagePosition", 1)[1], "partial/management close behavior must be reviewed separately"
for link in ["docs/PRD.md", "docs/CTRADER_SETUP.md", "docs/TEST_REPORT.md", "docs/IMPROVEMENTS.md", "docs/DECISIONS.md"]:
    assert link in readme, f"README missing documentation link: {link}"

for path in ROOT.rglob("*"):
    if path.is_file() and path.suffix in {".cs", ".csproj", ".md", ".py", ".sln"}:
        for number, line in enumerate(path.read_text(errors="replace").splitlines(), 1):
            assert not line.endswith((" ", "\t")), f"trailing whitespace: {path}:{number}"

print(f"Validation passed: {len(REQUIRED)} required artifacts and cTrader safety contracts verified.")
