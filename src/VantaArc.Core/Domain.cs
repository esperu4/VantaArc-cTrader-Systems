namespace VantaArc.Core;

/// <summary>Operating mode is deliberately explicit so shadow mode cannot silently place orders.</summary>
public enum OperatingMode { Shadow, Backtest, Demo, Live }
public enum TradeDirection { None, Long, Short }
public enum MarketRegime { Unknown, Balanced, Directional, Discovery }
public enum SetupState { WaitingForContext, WaitingForConfluence, ConfluenceArmed, WaitingForConfirmation, CandidateValidated, SetupExpired, SetupCancelled, ExecutionBlocked, PositionOpen }
public enum ManagementStage { None, InitialProtection, Breakeven, ProfitLock, Trailing }

public sealed record Candle(DateTime TimeUtc, double Open, double High, double Low, double Close, double Volume)
{
    public double TypicalPrice => (High + Low + Close) / 3.0;
    public double Range => Math.Max(0.0, High - Low);
    public double Body => Math.Abs(Close - Open);
    public bool IsBullish => Close > Open;
    public bool IsBearish => Close < Open;
}

public sealed record VwapValue(
    string Name,
    DateTime AnchorStartUtc,
    DateTime AnchorEndUtc,
    double Value,
    double Sigma,
    int BarsIncluded,
    bool IsValid)
{
    public double UpperBand(double multiplier) => Value + Sigma * multiplier;
    public double LowerBand(double multiplier) => Value - Sigma * multiplier;
}

public sealed record MarketContext(
    DateTime BarTimeUtc,
    VwapValue Daily,
    VwapValue Weekly,
    VwapValue Session,
    double Atr,
    MarketRegime Regime,
    bool RegimeAllowed,
    string RegimeReason);

public sealed record StrategyParameters
{
    public double BalanceBandSigma { get; init; } = 1.0;
    public double DiscoveryBandSigma { get; init; } = 2.0;
    public bool AllowBalanced { get; init; } = true;
    public bool AllowDirectional { get; init; } = true;
    public bool AllowDiscovery { get; init; } = true;
    public bool DailyVwapEnabled { get; init; } = true;
    public bool WeeklyVwapEnabled { get; init; } = true;
    public bool SessionVwapEnabled { get; init; } = true;
    public int MinimumConfluences { get; init; } = 2;
    public double ConfluenceToleranceAtr { get; init; } = 0.15;
    public double ConfluenceTolerancePrice { get; init; } = 2.0;
    public int ConfirmationWindowBars { get; init; } = 3;
    public double MinimumBodyToRange { get; init; } = 0.50;
    public double StopAtrBuffer { get; init; } = 0.10;
    public int AtrPeriod { get; init; } = 14;
    public int SessionStartHourUtc { get; init; } = 13;
    public int SessionStartMinuteUtc { get; init; } = 30;
    public int SessionEndHourUtc { get; init; } = 20;
    public int SessionEndMinuteUtc { get; init; } = 0;
}

public sealed record ConfluenceResult(
    TradeDirection Direction,
    int Count,
    IReadOnlyList<string> TouchedLevels,
    IReadOnlyDictionary<string, double> LevelValues,
    double Tolerance,
    bool MeetsMinimum)
{
    public static ConfluenceResult Empty(TradeDirection direction) => new(direction, 0, Array.Empty<string>(), new Dictionary<string, double>(), 0, false);
}

public sealed record SetupSnapshot(
    SetupState State,
    TradeDirection Direction,
    MarketRegime RegimeAtArm,
    DateTime? InteractionTimeUtc,
    int BarsSinceArm,
    int ConfluenceCount,
    IReadOnlyList<string> TouchedLevels,
    string Reason)
{
    public static SetupSnapshot Waiting(string reason) => new(SetupState.WaitingForConfluence, TradeDirection.None, MarketRegime.Unknown, null, 0, 0, Array.Empty<string>(), reason);
}

public sealed record StrategyDecision(
    DateTime BarTimeUtc,
    SetupState State,
    TradeDirection Direction,
    MarketRegime Regime,
    string Reason,
    bool SignalAccepted,
    bool MayProceedToRisk,
    SetupSnapshot Setup,
    ConfluenceResult LongConfluence,
    ConfluenceResult ShortConfluence,
    double Atr);

public sealed record BrokerVolumeSpec(double MinimumUnits, double MaximumUnits, double StepUnits);
public sealed record BrokerSnapshot(
    string SymbolName,
    double Bid,
    double Ask,
    double PointSize,
    double PipSize,
    double TickSize,
    double TickValuePerUnit,
    double SpreadPips,
    double Equity,
    double FreeMargin,
    double MarginPerUnit,
    double DayProfitAndLoss,
    BrokerVolumeSpec Volume,
    bool IsDemoAccount,
    bool HasFreshQuote,
    double MinimumStopDistancePrice);

public sealed record RiskPolicy
{
    public double RiskPercent { get; init; } = 0.25;
    public double DailyLossLimitPercent { get; init; } = 2.0;
    public double MaximumSpreadPips { get; init; } = 2.0;
    public bool OnePositionPerSymbol { get; init; } = true;
    public bool EnableOrderExecution { get; init; } = false;
    public bool LiveReadyAcknowledged { get; init; } = false;
    public double MinimumFreeMarginAfterOrder { get; init; } = 0.0;
}

public sealed record TradeCandidate(TradeDirection Direction, double EntryPrice, double StopPrice, double Atr, string Label);
public sealed record RiskResult(bool Passed, string Reason, double VolumeUnits, double RiskMoney, double LossPerUnitAtStop);
public sealed record ExecutionDecision(bool Allowed, string Reason, RiskResult Risk, TradeCandidate Candidate);

public sealed record PositionSnapshot(
    TradeDirection Direction,
    double EntryPrice,
    double StopPrice,
    double InitialRiskPrice,
    double Atr,
    double CurrentPrice,
    double CurrentR,
    int BarsOpen,
    ManagementStage Stage);

public sealed record PositionManagementDecision(
    bool ShouldModifyStop,
    double? NewStopPrice,
    bool ShouldClose,
    ManagementStage Stage,
    string Reason);
