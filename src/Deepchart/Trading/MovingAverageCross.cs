using System.ComponentModel;
using VolSysAPI;
using VolSysAPI.Indicators;
using VolSysAPI.Trading;
using VolumetricaAPI.Chart;
using VolumetricaControls;
using VolumetricaCore;
using static VolSysAPI.ExternalStructure;
using static VolSysAPI.Structure;

namespace Deepchart.Indicators.Examples
{
    /// <summary>
    /// A fast and a slow moving average, and an order at every cross, sent through the trading interface of the chart.
    /// It shows how an indicator trades: the entry order with its brackets (stop, targets, break-even, trailing),
    /// the position, a pending order, and where the orders are accepted.
    ///
    /// TradingApi is [DesktopOnly]: on the server it is null, and a package which contains this indicator is
    /// calculated on the desktop, never on the server. The orders are accepted only in two cases:
    /// - a backtest: chart menu Autotrading > Execute Backtest. The chart creates a backtesting account, loads the
    ///   history again with the Backtesting phase and fills the orders on the bars. At the end it shows the report;
    /// - a simulation (SIM) account selected on the chart, in realtime only.
    /// On a paper or live account every request is refused: InsertOrder returns -1 and the reason
    /// </summary>
    public class MovingAverageCross : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Moving Average Cross Trading",
                Description = "Fast and slow average, an order with brackets at every cross. Example of the trading interface.",
                Tags = new List<string> { "Example", "Trend", "Trading" }
            };
        }

        #region Parameters
        [Category("Averages")]
        [DisplayName("Fast length")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, MaxValue = 1000, IncrementValue = 1)]
        public int FastLength { get; set; } = 10;

        [Category("Averages")]
        [DisplayName("Slow length")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1, MinValue = 2, MaxValue = 1000, IncrementValue = 1)]
        public int SlowLength { get; set; } = 30;

        // The trading settings are nested objects: the settings window shows each one as a group.
        // Quantities are whole contracts and distances are ticks: the API takes a decimal quantity and ticks (or a price)
        public enum EntryTypeEnum { Market, Limit }

        public class EntrySettings
        {
            [VolCustom(PropertyIndex = 0)]
            public bool Enabled { get; set; } = false;
            [VolCustom(PropertyIndex = 1, MinValue = 1, MaxValue = 1000, IncrementValue = 1)]
            public int Quantity { get; set; } = 2;
            [DisplayName("Order type")]
            [VolCustom(PropertyIndex = 2)]
            public EntryTypeEnum Type { get; set; } = EntryTypeEnum.Market;
            [DisplayName("Limit: ticks better than the close")]
            [VolCustom(PropertyIndex = 3, MinValue = 0, MaxValue = 1000, IncrementValue = 1)]
            public int LimitTicks { get; set; } = 2;
            [DisplayName("Limit: expires after (bars, 0 = never)")]
            [VolCustom(PropertyIndex = 4, MinValue = 0, MaxValue = 1000, IncrementValue = 1)]
            public int ExpirationBars { get; set; } = 3;
            [DisplayName("Close the opposite position")]
            [VolCustom(PropertyIndex = 5)]
            public bool CloseOpposite { get; set; } = true;
            [DisplayName("Allow multiple entries")]
            [VolCustom(PropertyIndex = 6)]
            public bool AllowMultipleEntries { get; set; } = false;
        }

        public class StopSettings
        {
            [VolCustom(PropertyIndex = 0)]
            public bool Enabled { get; set; } = true;
            [VolCustom(PropertyIndex = 1, MinValue = 1, MaxValue = 10000, IncrementValue = 1)]
            public int Ticks { get; set; } = 20;
            [DisplayName("Break-even: enabled")]
            [VolCustom(PropertyIndex = 2)]
            public bool BreakEvenEnabled { get; set; } = false;
            [DisplayName("Break-even: trigger (ticks in profit)")]
            [VolCustom(PropertyIndex = 3, MinValue = 1, MaxValue = 10000, IncrementValue = 1)]
            public int BreakEvenTriggerTicks { get; set; } = 10;
            [DisplayName("Break-even: offset (ticks from the entry)")]
            [VolCustom(PropertyIndex = 4, MinValue = -10000, MaxValue = 10000, IncrementValue = 1)]
            public int BreakEvenOffsetTicks { get; set; } = 1;
            [DisplayName("Trailing: enabled")]
            [VolCustom(PropertyIndex = 5)]
            public bool TrailingEnabled { get; set; } = false;
            [DisplayName("Trailing: trigger (ticks in profit)")]
            [VolCustom(PropertyIndex = 6, MinValue = 1, MaxValue = 10000, IncrementValue = 1)]
            public int TrailingTriggerTicks { get; set; } = 10;
            [DisplayName("Trailing: offset (ticks from the price)")]
            [VolCustom(PropertyIndex = 7, MinValue = 1, MaxValue = 10000, IncrementValue = 1)]
            public int TrailingOffsetTicks { get; set; } = 10;
        }

        public class TargetSettings
        {
            [VolCustom(PropertyIndex = 0)]
            public bool Enabled { get; set; } = true;
            [VolCustom(PropertyIndex = 1, MinValue = 1, MaxValue = 10000, IncrementValue = 1)]
            public int Ticks { get; set; } = 20;
            [VolCustom(PropertyIndex = 2, MinValue = 1, MaxValue = 1000, IncrementValue = 1)]
            public int Quantity { get; set; } = 1;
            [DisplayName("Move the stop to break-even when filled")]
            [VolCustom(PropertyIndex = 3)]
            public bool MoveStopToBreakEven { get; set; } = false;
        }

        public class TimeFilterSettings
        {
            [VolCustom(PropertyIndex = 0)]
            public bool Enabled { get; set; } = false;
            [DisplayName("From (exchange time)")]
            [VolCustom(PropertyIndex = 1)]
            public TimeSpan Start { get; set; } = new TimeSpan(9, 30, 0);
            [DisplayName("To (exchange time)")]
            [VolCustom(PropertyIndex = 2)]
            public TimeSpan End { get; set; } = new TimeSpan(16, 0, 0);
        }

        [Category("Trading")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public EntrySettings Entry { get; set; } = new EntrySettings();

        [Category("Trading")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 1)]
        public StopSettings Stop { get; set; } = new StopSettings();

        [Category("Trading")]
        [DisplayName("Target 1")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 2)]
        public TargetSettings Target1 { get; set; } = new TargetSettings();

        [Category("Trading")]
        [DisplayName("Target 2")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 3)]
        public TargetSettings Target2 { get; set; } = new TargetSettings { Enabled = false, Ticks = 40 };

        [Category("Trading")]
        [DisplayName("Time filter")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 4)]
        public TimeFilterSettings TimeFilter { get; set; } = new TimeFilterSettings();
        #endregion

        #region Series
        class FastSeriesAttribute : ChartSeriesAttribute
        {
            public FastSeriesAttribute() : base("Fast average")
            {
                CurrentStyle = SeriesType.Line;
                AvailableStyle = (int)SeriesType.Line;
                LineWidth = 1;
                PrimaryColor = ColorRef.FromRgb(255, 165, 0);
            }
        }
        class SlowSeriesAttribute : ChartSeriesAttribute
        {
            public SlowSeriesAttribute() : base("Slow average")
            {
                CurrentStyle = SeriesType.Line;
                AvailableStyle = (int)SeriesType.Line;
                LineWidth = 2;
                PrimaryColor = ColorRef.FromRgb(30, 144, 255);
            }
        }
        [FastSeries]
        public ChartSeriesDefinition Fast { get; private set; }
        [SlowSeries]
        public ChartSeriesDefinition Slow { get; private set; }
        #endregion

        double fastSum, slowSum;
        int lastSide;           // +1 the fast average is above the slow one, -1 below, 0 not known yet
        long pendingEntryId;    // the last limit entry sent, -1 when there is none
        string settingsError;   // why the trading settings can not be used, null when they can

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            OnOpenCall = CallHandler.HistRT;
            OnCloseCall = CallHandler.HistRT;
            OnEndCall = CallHandler.HistRT;
            // Each target closes a part of the position: together they can not exceed the entry
            int targetQuantity = (Target1.Enabled ? Target1.Quantity : 0) + (Target2.Enabled ? Target2.Quantity : 0);
            settingsError = Entry.Enabled && targetQuantity > Entry.Quantity
                ? $"MA Cross: the targets ({targetQuantity}) exceed the entry quantity ({Entry.Quantity}). No order is sent"
                : null;
            Description = $"MA Cross ({FastLength}, {SlowLength}){(Entry.Enabled ? " - trading" : "")}";
        }

        public override void OnLoad()
        {
            fastSum = slowSum = 0;
            lastSide = 0;
            pendingEntryId = -1;
            // A message on the chart. TradingApi is null when the indicator is calculated on the server
            if (!Entry.Enabled)
                StatusMessage = null;
            else if (TradingApi == null)
                StatusMessage = "MA Cross: the trading interface is not available here";
            else
                StatusMessage = settingsError;
        }

        public override void OnOpen(bool isRt)
        {
            VAn.AddPointToSeries(Fast.BaseSeries, VAn.BarIndex, IsHidden: true);
            VAn.AddPointToSeries(Slow.BaseSeries, VAn.BarIndex, IsHidden: true);
        }

        // The bar is closed: the averages get their final value, and a cross is looked for.
        // Deciding on the close, and never on the open bar, gives the same signals in the history and in realtime
        public override void OnClose(bool isRt)
        {
            int index = VAn.BarIndex;
            var bars = VAn.BarVars;
            fastSum += bars[index].Close;
            slowSum += bars[index].Close;
            if (index >= FastLength)
                fastSum -= bars[index - FastLength].Close;
            if (index >= SlowLength)
                slowSum -= bars[index - SlowLength].Close;
            if (index < Math.Max(FastLength, SlowLength) - 1)
                return;
            double fast = fastSum / FastLength, slow = slowSum / SlowLength;
            SetPoints(index, fast, slow);

            // Equal averages keep the previous side. The first valid bar only sets the side: no cross yet
            int side = fast > slow ? 1 : fast < slow ? -1 : 0;
            if (side == 0)
                return;
            bool crossed = lastSide != 0 && side != lastSide;
            lastSide = side;
            if (crossed)
                Trade(side, isRt);
        }

        // The bar still open: the averages follow its last price. No signal is taken from it
        public override void OnEnd(bool isRt)
        {
            int index = VAn.LastIndex();
            if (index < Math.Max(FastLength, SlowLength) - 1)
                return;
            var bars = VAn.BarVars;
            double close = bars[index].Close;
            double fast = (fastSum + close - (index >= FastLength ? bars[index - FastLength].Close : 0)) / FastLength;
            double slow = (slowSum + close - (index >= SlowLength ? bars[index - SlowLength].Close : 0)) / SlowLength;
            SetPoints(index, fast, slow);
        }

        private void SetPoints(int index, double fast, double slow)
        {
            var fastPoint = Fast.BaseSeries.Points[index];
            fastPoint.Y[0] = fast;
            fastPoint.IsHidden = false;
            var slowPoint = Slow.BaseSeries.Points[index];
            slowPoint.Y[0] = slow;
            slowPoint.IsHidden = false;
        }

        // A cross on a closed bar. side: +1 the fast average is now above the slow one (buy), -1 below (sell)
        private void Trade(int side, bool isRt)
        {
            if (!Entry.Enabled || TradingApi == null || settingsError != null)
                return;
            // The orders of the history are accepted only in a backtest (Autotrading > Execute Backtest). On a SIM
            // account they are accepted in realtime only: the crosses of the history, and of every recalculation after
            // a change of a parameter, are skipped. So the same cross never trades twice
            bool isBacktest = VAn.ChartPhase.HasFlag(ChartPhaseEnum.Backtesting);
            if (!isRt && !isBacktest)
                return;
            var bar = VAn.BarVars[VAn.BarIndex];
            if (!InTimeFilter(bar.exchDt))
                return;

            // A limit entry of the previous cross still waiting is removed: the signal has changed.
            // GetOrderInfo returns null when the order is not pending any more (filled, cancelled, expired).
            // Removing it by id leaves the brackets of an open position alone, which CancelAllOrders would not
            if (pendingEntryId != -1 && TradingApi.GetOrderInfo(pendingEntryId) != null)
                TradingApi.RemoveOrder(pendingEntryId, out _);
            pendingEntryId = -1;

            // The position of the symbol of the chart on the account of the chart: positive long, negative short
            int position = Math.Sign(TradingApi.PositionNetQuantity);
            if (position == side && !Entry.AllowMultipleEntries)
                return;
            if (position == -side && Entry.CloseOpposite)
                TradingApi.FlatAllPositions();

            var request = new OrderInsertRequest
            {
                OrderType = Entry.Type == EntryTypeEnum.Market ? OrderTypeEnum.Market : OrderTypeEnum.Limit,
                // The sign of the quantity is the side of the order
                Quantity = Entry.Quantity * side,
                // A limit order waits for a better price: below the close for a buy, above it for a sell.
                // The price is required for every order which is not a market one
                Price = Entry.Type == EntryTypeEnum.Limit ? VAn.DoubleAddSubTicks(bar.Close, -Entry.LimitTicks * side) : null,
                // The chart cancels a limit order still pending when this many bars have closed
                BarsExpiration = Entry.Type == EntryTypeEnum.Limit && Entry.ExpirationBars > 0 ? Entry.ExpirationBars : null,
                Brackets = BuildBrackets()
            };
            long orderId = TradingApi.InsertOrder(request, out string reason);
            if (orderId == -1)
                // Refused: wrong account or phase, or wrong parameters. The reason says which
                VAn.AddToLog($"MA Cross: order refused. {reason}", VolumetricaAPI.LogLevelEnum.Warning, "MA Cross", showToast: isRt);
            else if (request.OrderType == OrderTypeEnum.Limit)
                pendingEntryId = orderId;
        }

        // The brackets of an entry: distances in ticks from the fill price, in the direction of the side of the entry.
        // They are OCO with each other: the stop protects the whole quantity, each target closes its own part and
        // reduces the stop by as much
        private List<BracketParam> BuildBrackets()
        {
            var brackets = new List<BracketParam>();
            if (Stop.Enabled)
                brackets.Add(new BracketParam
                {
                    IsTarget = false,
                    Quantity = Entry.Quantity,
                    Ticks = Stop.Ticks,
                    // Break-even: when the price is TriggerTicks in profit, the stop moves to the entry plus OffsetTicks
                    OcoStopBeEnabled = Stop.BreakEvenEnabled,
                    OcoStopBeTriggerTicks = Stop.BreakEvenEnabled ? Stop.BreakEvenTriggerTicks : null,
                    OcoStopBeOffsetTicks = Stop.BreakEvenEnabled ? Stop.BreakEvenOffsetTicks : null,
                    // Trailing: from TriggerTicks in profit, the stop follows the price at OffsetTicks
                    OcoStopTrailEnabled = Stop.TrailingEnabled,
                    OcoStopTrailTriggerTicks = Stop.TrailingEnabled ? Stop.TrailingTriggerTicks : null,
                    OcoStopTrailOffsetTicks = Stop.TrailingEnabled ? Stop.TrailingOffsetTicks : null
                });
            AddTarget(brackets, Target1);
            AddTarget(brackets, Target2);
            return brackets;
        }

        private static void AddTarget(List<BracketParam> brackets, TargetSettings target)
        {
            if (!target.Enabled)
                return;
            brackets.Add(new BracketParam
            {
                IsTarget = true,
                Quantity = target.Quantity,
                Ticks = target.Ticks,
                // When this target is filled, the stop of the rest moves to the entry price
                OcoTargetMoveBe = target.MoveStopToBreakEven
            });
        }

        // The window is in exchange time, like the session of the chart. It can cross midnight (22:00 - 02:00)
        private bool InTimeFilter(DateTime exchangeTime)
        {
            if (!TimeFilter.Enabled)
                return true;
            var time = exchangeTime.TimeOfDay;
            return TimeFilter.Start <= TimeFilter.End
                ? time >= TimeFilter.Start && time < TimeFilter.End
                : time >= TimeFilter.Start || time < TimeFilter.End;
        }
    }
}
