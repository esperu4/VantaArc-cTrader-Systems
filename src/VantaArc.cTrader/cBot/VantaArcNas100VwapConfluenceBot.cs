// VantaArc NAS100 VWAP Confluence cBot
//
// Platform boundary only:
// - The strategy rules live in VantaArc.Core.
// - cTrader types are used only here to translate bars, symbol economics,
//   account state, orders, positions, and chart output.
// - The sandbox cannot compile cAlgo.API because cTrader supplies that
//   proprietary assembly inside the target Algo environment.
//
// Target-platform verification is mandatory after copying this file into a
// cTrader Algo project. Do not enable live execution from an unverified build.

using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using VantaArc.Core;

namespace VantaArc.cTrader;

[Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
public class VantaArcNas100VwapConfluenceBot : Robot
{
    [Parameter("Operating mode", DefaultValue = "Shadow", Group = "Safety")]
    public string OperatingModeName { get; set; } = "Shadow";

    [Parameter("Enable order execution", DefaultValue = false, Group = "Safety")]
    public bool EnableOrderExecution { get; set; }

    [Parameter("Live readiness acknowledged", DefaultValue = false, Group = "Safety")]
    public bool LiveReadinessAcknowledged { get; set; }

    [Parameter("Emergency stop", DefaultValue = false, Group = "Safety")]
    public bool EmergencyStop { get; set; }

    [Parameter("Symbol token", DefaultValue = "", Group = "Identity")]
    public string SymbolToken { get; set; } = "";

    [Parameter("Daily VWAP enabled", DefaultValue = true, Group = "VWAP")]
    public bool DailyVwapEnabled { get; set; }

    [Parameter("Weekly VWAP enabled", DefaultValue = true, Group = "VWAP")]
    public bool WeeklyVwapEnabled { get; set; }

    [Parameter("Session VWAP enabled", DefaultValue = true, Group = "VWAP")]
    public bool SessionVwapEnabled { get; set; }

    [Parameter("Balance band sigma", DefaultValue = 1.0, MinValue = 0.1, Group = "Regime")]
    public double BalanceBandSigma { get; set; }

    [Parameter("Discovery band sigma", DefaultValue = 2.0, MinValue = 0.2, Group = "Regime")]
    public double DiscoveryBandSigma { get; set; }

    [Parameter("Allow balanced", DefaultValue = true, Group = "Regime")]
    public bool AllowBalanced { get; set; }

    [Parameter("Allow directional", DefaultValue = true, Group = "Regime")]
    public bool AllowDirectional { get; set; }

    [Parameter("Allow discovery", DefaultValue = true, Group = "Regime")]
    public bool AllowDiscovery { get; set; }

    [Parameter("Minimum confluences", DefaultValue = 2, MinValue = 1, MaxValue = 3, Group = "Confluence")]
    public int MinimumConfluences { get; set; }

    [Parameter("Tolerance ATR", DefaultValue = 0.15, MinValue = 0, Group = "Confluence")]
    public double ConfluenceToleranceAtr { get; set; }

    [Parameter("Tolerance price", DefaultValue = 0.0, MinValue = 0, Group = "Confluence")]
    public double ConfluenceTolerancePrice { get; set; }

    [Parameter("Confirmation window bars", DefaultValue = 3, MinValue = 1, MaxValue = 20, Group = "Confirmation")]
    public int ConfirmationWindowBars { get; set; }

    [Parameter("Minimum body/range", DefaultValue = 0.50, MinValue = 0.01, MaxValue = 1, Group = "Confirmation")]
    public double MinimumBodyToRange { get; set; }

    [Parameter("Session start hour UTC", DefaultValue = 13, MinValue = 0, MaxValue = 23, Group = "Session")]
    public int SessionStartHourUtc { get; set; }

    [Parameter("Session start minute UTC", DefaultValue = 30, MinValue = 0, MaxValue = 59, Group = "Session")]
    public int SessionStartMinuteUtc { get; set; }

    [Parameter("Session end hour UTC", DefaultValue = 20, MinValue = 0, MaxValue = 23, Group = "Session")]
    public int SessionEndHourUtc { get; set; }

    [Parameter("Session end minute UTC", DefaultValue = 0, MinValue = 0, MaxValue = 59, Group = "Session")]
    public int SessionEndMinuteUtc { get; set; }

    [Parameter("ATR period", DefaultValue = 14, MinValue = 2, MaxValue = 200, Group = "Volatility")]
    public int AtrPeriod { get; set; }

    [Parameter("Stop ATR buffer", DefaultValue = 0.10, MinValue = 0, Group = "Volatility")]
    public double StopAtrBuffer { get; set; }

    [Parameter("Risk percent", DefaultValue = 0.25, MinValue = 0.01, MaxValue = 5, Group = "Risk")]
    public double RiskPercent { get; set; }

    [Parameter("Daily loss limit percent", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 20, Group = "Risk")]
    public double DailyLossLimitPercent { get; set; }

    [Parameter("Minimum free-margin reserve", DefaultValue = 0.0, MinValue = 0, Group = "Risk")]
    public double MinimumFreeMarginAfterOrder { get; set; }

    [Parameter("Maximum spread pips", DefaultValue = 2.0, MinValue = 0.0001, Group = "Broker")]
    public double MaximumSpreadPips { get; set; }

    [Parameter("One position per symbol", DefaultValue = true, Group = "Broker")]
    public bool OnePositionPerSymbol { get; set; }

    [Parameter("Breakeven at R", DefaultValue = 1.0, MinValue = 0.1, Group = "Management")]
    public double BreakevenAtR { get; set; }

    [Parameter("Profit lock at R", DefaultValue = 1.5, MinValue = 0.2, Group = "Management")]
    public double ProfitLockAtR { get; set; }

    [Parameter("Profit lock R", DefaultValue = 0.5, MinValue = 0, Group = "Management")]
    public double ProfitLockR { get; set; }

    [Parameter("Trail activation R", DefaultValue = 2.0, MinValue = 0.5, Group = "Management")]
    public double TrailActivationR { get; set; }

    [Parameter("Trail ATR multiplier", DefaultValue = 1.0, MinValue = 0.1, Group = "Management")]
    public double TrailAtrMultiplier { get; set; }

    [Parameter("Structure lookback bars", DefaultValue = 5, MinValue = 2, MaxValue = 50, Group = "Management")]
    public int StructureLookbackBars { get; set; }

    [Parameter("Force flat at session end", DefaultValue = true, Group = "Management")]
    public bool ForceFlatAtSessionEnd { get; set; }

    [Parameter("Allow overnight", DefaultValue = false, Group = "Management")]
    public bool AllowOvernight { get; set; }

    private const string Label = "VantaArc-NAS100-VWAP";
    private VwapConfluenceStateMachine _machine = null!;
    private DiagnosticLedger _ledger = null!;
    private DateTime _lastBarUtc;
    private double _dayStartEquity;
    private OperatingMode _mode;
    private readonly Dictionary<long, double> _initialRiskByPosition = new();

    protected override void OnStart()
    {
        if (string.IsNullOrWhiteSpace(SymbolToken))
            throw new ArgumentException("SYMBOL_TOKEN_REQUIRED: enter the broker's exact NAS100/US Tech 100 token");
        if (!SymbolName.Contains(SymbolToken, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"SYMBOL_TOKEN_MISMATCH: {SymbolName} does not contain {SymbolToken}");
        if (!Enum.TryParse(OperatingModeName, true, out _mode))
            throw new ArgumentException("OPERATING_MODE_INVALID: use Shadow, Backtest, Demo, or Live");
        if (SessionStartHourUtc == SessionEndHourUtc && SessionStartMinuteUtc == SessionEndMinuteUtc)
            throw new ArgumentException("SESSION_WINDOW_INVALID: start and end cannot be equal");
        if (MaximumSpreadPips <= 0)
            throw new ArgumentException("MAXIMUM_SPREAD_ZERO_OR_INVALID");
        if (!Symbol.IsTradingEnabled)
            throw new ArgumentException("SYMBOL_TRADING_DISABLED");
        if (_mode == OperatingMode.Live && (!EnableOrderExecution || !LiveReadinessAcknowledged || !Account.IsLive))
            throw new ArgumentException("LIVE_EXECUTION_NOT_ACKNOWLEDGED");
        if (_mode == OperatingMode.Demo && Account.IsLive)
            throw new ArgumentException("DEMO_MODE_REQUIRES_DEMO_ACCOUNT");

        _machine = new VwapConfluenceStateMachine();
        _ledger = new DiagnosticLedger();
        _dayStartEquity = Account.Equity;
        Print("VantaArc started | mode={0} | symbol={1} | timeframe={2} | accountLive={3}", _mode, SymbolName, Bars.TimeFrame, Account.IsLive);
        Print("Shadow mode is the default. Strategy decisions use OnBarClosed; OnTick is only for protection.");
        DrawStatus("STARTED | waiting for completed bars");
    }

    protected override void OnBarClosed()
    {
        if (_machine is null || Bars.Count < AtrPeriod + 5) return;
        var completed = BuildCompletedBars();
        var current = completed[^1];
        if (current.TimeUtc == _lastBarUtc) return;
        _lastBarUtc = current.TimeUtc;

        var strategy = BuildStrategyParameters();
        var decision = _machine.Process(completed, strategy, current.TimeUtc);
        var context = MarketContextBuilder.Build(completed, current, strategy);
        var spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
        DrawStatus($"{_mode} | {SymbolName} | regime={decision.Regime} | state={decision.State} | " +
                   $"direction={decision.Direction} | long={decision.LongConfluence.Count}/3 | short={decision.ShortConfluence.Count}/3 | " +
                   $"spread={spreadPips:F2}/{MaximumSpreadPips:F2} | reason={decision.Reason}");
        Print("{0} | state={1} | regime={2} | direction={3} | long={4} | short={5} | spread={6:F2} pips",
            decision.Reason, decision.State, decision.Regime, decision.Direction,
            decision.LongConfluence.Count, decision.ShortConfluence.Count, spreadPips);

        DrawVwapContext(context);
        if (!decision.SignalAccepted || !decision.MayProceedToRisk || EmergencyStop)
        {
            Record(decision, riskPassed: false, executionAttempted: false, filled: false);
            return;
        }
        var previous = completed[^2];
        var entry = decision.Direction == TradeDirection.Long ? Symbol.Ask : Symbol.Bid;
        var stop = decision.Direction == TradeDirection.Long
            ? previous.Low - decision.Atr * StopAtrBuffer
            : previous.High + decision.Atr * StopAtrBuffer;
        var tradeType = decision.Direction == TradeDirection.Long ? TradeType.Buy : TradeType.Sell;
        var candidate = new TradeCandidate(decision.Direction, entry, stop, decision.Atr, Label);
        var risk = RiskCalculator.Evaluate(candidate, BuildBrokerSnapshot(tradeType), BuildRiskPolicy(), HasManagedPosition());
        if (!risk.Passed)
        {
            Print("RISK_BLOCKED | {0}", risk.Reason);
            Record(decision, riskPassed: false, executionAttempted: false, filled: false);
            return;
        }

        if (!EnableOrderExecution || _mode is OperatingMode.Shadow or OperatingMode.Backtest)
        {
            Print("SHADOW_ORDER_NOT_SENT | direction={0} | volume={1} | stop={2}", decision.Direction, risk.VolumeUnits, stop);
            Record(decision, riskPassed: true, executionAttempted: false, filled: false);
            return;
        }

        var stopLossPips = Math.Abs(entry - stop) / Symbol.PipSize;
        var result = ExecuteMarketOrder(tradeType, SymbolName, risk.VolumeUnits, Label, stopLossPips, null, "VantaArc VWAP confirmation");
        var filled = result.IsSuccessful;
        Print(filled ? "ORDER_FILLED | volume={0} | entry={1} | stopPips={2}" : $"ORDER_REJECTED_{result.Error}", risk.VolumeUnits, entry, stopLossPips);
        Record(decision, riskPassed: true, executionAttempted: true, filled: filled);
        if (filled && result.Position is not null)
            _initialRiskByPosition[result.Position.Id] = stopLossPips * Symbol.PipSize;
    }

    protected override void OnTick()
    {
        foreach (var position in Positions.FindAll(Label, SymbolName))
        {
            if (!_initialRiskByPosition.ContainsKey(position.Id) && position.StopLoss.HasValue)
                _initialRiskByPosition[position.Id] = Math.Abs(position.EntryPrice - position.StopLoss.Value);
            ManagePosition(position);
        }

        if (ForceFlatAtSessionEnd && !AllowOvernight)
        {
            var now = Server.Time.ToUniversalTime();
            var end = now.Date.AddHours(SessionEndHourUtc).AddMinutes(SessionEndMinuteUtc);
            if (now >= end)
                foreach (var position in Positions.FindAll(Label, SymbolName))
                    ClosePosition(position);
        }
    }

    protected override void OnStop()
    {
        if (_ledger is not null)
        {
            var summary = _ledger.Summarize();
            Print("DIAGNOSTIC_SUMMARY | bars={0} | arms={1} | confirmations={2} | accepted={3} | attempts={4} | fills={5}",
                summary.ContextBars, summary.ConfluenceArms, summary.ConfirmationEvents, summary.AcceptedSignals, summary.ExecutionAttempts, summary.Fills);
        }
        Print("VantaArc stopped");
    }

    private StrategyParameters BuildStrategyParameters() => new()
    {
        DailyVwapEnabled = DailyVwapEnabled,
        WeeklyVwapEnabled = WeeklyVwapEnabled,
        SessionVwapEnabled = SessionVwapEnabled,
        BalanceBandSigma = BalanceBandSigma,
        DiscoveryBandSigma = DiscoveryBandSigma,
        AllowBalanced = AllowBalanced,
        AllowDirectional = AllowDirectional,
        AllowDiscovery = AllowDiscovery,
        MinimumConfluences = MinimumConfluences,
        ConfluenceToleranceAtr = ConfluenceToleranceAtr,
        ConfluenceTolerancePrice = ConfluenceTolerancePrice > 0 ? ConfluenceTolerancePrice : Symbol.TickSize,
        ConfirmationWindowBars = ConfirmationWindowBars,
        MinimumBodyToRange = MinimumBodyToRange,
        StopAtrBuffer = StopAtrBuffer,
        AtrPeriod = AtrPeriod,
        SessionStartHourUtc = SessionStartHourUtc,
        SessionStartMinuteUtc = SessionStartMinuteUtc,
        SessionEndHourUtc = SessionEndHourUtc,
        SessionEndMinuteUtc = SessionEndMinuteUtc
    };

    private RiskPolicy BuildRiskPolicy() => new()
    {
        RiskPercent = RiskPercent,
        DailyLossLimitPercent = DailyLossLimitPercent,
        MaximumSpreadPips = MaximumSpreadPips,
        OnePositionPerSymbol = OnePositionPerSymbol,
        EnableOrderExecution = EnableOrderExecution,
        LiveReadyAcknowledged = LiveReadinessAcknowledged,
        MinimumFreeMarginAfterOrder = MinimumFreeMarginAfterOrder
    };

    private BrokerSnapshot BuildBrokerSnapshot(TradeType tradeType)
    {
        var tickValuePerUnit = Symbol.LotSize > 0 ? Symbol.TickValue / Symbol.LotSize : 0;
        var marginPerUnit = Symbol.GetEstimatedMargin(tradeType, 1.0);
        var minStopDistancePrice = Symbol.MinDistanceType == SymbolMinDistanceType.Pips
            ? Symbol.MinStopLossDistance * Symbol.PipSize
            : Symbol.Ask * Symbol.MinStopLossDistance / 100.0;
        return new BrokerSnapshot(
            SymbolName, Symbol.Bid, Symbol.Ask, Symbol.PipSize, Symbol.PipSize,
            Symbol.TickSize, tickValuePerUnit, (Symbol.Ask - Symbol.Bid) / Symbol.PipSize,
            Account.Equity, Account.FreeMargin, marginPerUnit, Account.Equity - _dayStartEquity,
            new BrokerVolumeSpec(Symbol.VolumeInUnitsMin, Symbol.VolumeInUnitsMax, Symbol.VolumeInUnitsStep),
            !Account.IsLive, Symbol.Bid > 0 && Symbol.Ask > 0, minStopDistancePrice);
    }

    private bool HasManagedPosition() => Positions.FindAll(Label, SymbolName).Length > 0;

    private void ManagePosition(Position position)
    {
        if (!_initialRiskByPosition.TryGetValue(position.Id, out var initialRisk) || initialRisk <= 0) return;
        var currentPrice = position.TradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask;
        var favorableMove = position.TradeType == TradeType.Buy ? currentPrice - position.EntryPrice : position.EntryPrice - currentPrice;
        var currentR = favorableMove / initialRisk;
        if (currentR < BreakevenAtR) return;

        var proposed = position.EntryPrice;
        if (currentR >= ProfitLockAtR)
            proposed = position.TradeType == TradeType.Buy ? position.EntryPrice + ProfitLockR * initialRisk : position.EntryPrice - ProfitLockR * initialRisk;
        if (currentR >= TrailActivationR)
        {
            var atr = AtrCalculator.Calculate(BuildCompletedBars(), AtrPeriod);
            var atrStop = position.TradeType == TradeType.Buy ? currentPrice - TrailAtrMultiplier * atr : currentPrice + TrailAtrMultiplier * atr;
            proposed = position.TradeType == TradeType.Buy ? Math.Max(proposed, atrStop) : Math.Min(proposed, atrStop);
        }

        if (!position.StopLoss.HasValue) return;
        var improves = position.TradeType == TradeType.Buy ? proposed > position.StopLoss.Value : proposed < position.StopLoss.Value;
        if (!improves) return;
        var result = ModifyPosition(position, proposed, position.TakeProfit);
        if (!result.IsSuccessful) Print("STOP_MODIFY_REJECTED_{0}", result.Error);
        else Print("STOP_IMPROVED | position={0} | stage={1} | stop={2}", position.Id, currentR >= TrailActivationR ? "TRAILING" : currentR >= ProfitLockAtR ? "PROFIT_LOCK" : "BREAKEVEN", proposed);
    }

    private List<Candle> BuildCompletedBars()
    {
        var list = new List<Candle>(Bars.Count);
        for (var i = 0; i < Bars.Count; i++)
            list.Add(new Candle(Bars.OpenTimes[i].ToUniversalTime(), Bars.OpenPrices[i], Bars.HighPrices[i], Bars.LowPrices[i], Bars.ClosePrices[i], Bars.TickVolumes[i]));
        return list;
    }

    private void Record(StrategyDecision decision, bool riskPassed, bool executionAttempted, bool filled)
    {
        var spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
        _ledger.Record(new DecisionEvent(
            "runtime", Guid.NewGuid().ToString("N"), decision.BarTimeUtc, SymbolName, Bars.TimeFrame.ToString(), _mode,
            decision.Regime, decision.Setup.State, decision.Reason, decision.SignalAccepted, riskPassed,
            executionAttempted, filled, decision.LongConfluence.Count, decision.ShortConfluence.Count,
            spreadPips, "runtime-config"));
    }

    private void DrawStatus(string text) => Chart.DrawStaticText("VantaArc.Status", text, VerticalAlignment.Top, HorizontalAlignment.Left, Color.White);

    private void DrawVwapContext(MarketContext context)
    {
        if (context.Daily.IsValid)
        {
            Chart.DrawHorizontalLine("VantaArc.DailyVWAP", context.Daily.Value, Color.DodgerBlue);
            Chart.DrawHorizontalLine("VantaArc.DailyUpper1", context.Daily.UpperBand(1), Color.CornflowerBlue);
            Chart.DrawHorizontalLine("VantaArc.DailyLower1", context.Daily.LowerBand(1), Color.CornflowerBlue);
        }
        if (context.Weekly.IsValid)
            Chart.DrawHorizontalLine("VantaArc.WeeklyVWAP", context.Weekly.Value, Color.Gold);
        if (context.Session.IsValid)
        {
            Chart.DrawHorizontalLine("VantaArc.SessionVWAP", context.Session.Value, Color.LimeGreen);
            Chart.DrawHorizontalLine("VantaArc.SessionUpper1", context.Session.UpperBand(1), Color.Green);
            Chart.DrawHorizontalLine("VantaArc.SessionLower1", context.Session.LowerBand(1), Color.Green);
        }
    }
}
