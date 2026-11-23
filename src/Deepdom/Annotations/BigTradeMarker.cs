using System.ComponentModel;
using VolBookAPI;
using VolBookAPI.Indicators;
using VolumetricaAPI.Chart;
using VolumetricaControls;
using VolumetricaCore;
using static VolBookAPI.ExternalStructure;
using static VolBookAPI.Structure;
using Feed = VolumetricaAPI.Connection.Structure;

namespace Deepdom.Indicators.Examples
{
    /// <summary>
    /// A circle on the price for every aggregated trade of at least a given size, with a message in realtime.
    /// It shows the aggregated trades, the annotations on the main area, their removal and the alerts
    /// </summary>
    public class BigTradeMarker : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Big Trade Marker",
                Description = "Marks the aggregated trades above a size. Example of annotations and alerts.",
                Tags = new List<string> { "Example", "Trades" }
            };
        }

        #region Parameters
        [Category("General")]
        [DisplayName("Minimum size")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, MaxValue = 1000000, IncrementValue = 1)]
        public int MinSize { get; set; } = 50;

        [Category("General")]
        [DisplayName("Markers kept")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1, MinValue = 1, MaxValue = 5000, IncrementValue = 10)]
        public int MaxMarkers { get; set; } = 200;

        [Category("General")]
        [DisplayName("Marker size")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 2, MinValue = 2, MaxValue = 30, IncrementValue = 1)]
        public int MarkerSize { get; set; } = 8;

        [Category("Alert")]
        [DisplayName("Show a message")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public bool AlertEnabled { get; set; } = false;

        [Category("Colors")]
        [DisplayName("Buy color")]
        [VolCustom(CategoryIndex = 2, PropertyIndex = 0)]
        public ColorRef BuyColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Ask, ColorTypeEnum.Fill);

        [Category("Colors")]
        [DisplayName("Sell color")]
        [VolCustom(CategoryIndex = 2, PropertyIndex = 1)]
        public ColorRef SellColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Bid, ColorTypeEnum.Fill);
        #endregion

        // The annotations of an indicator are kept in groups: one group here, with the markers in the order they were added
        IAnnGroup markers;
        readonly List<IAnnotation> shown = new List<IAnnotation>();

        public BigTradeMarker(BaseIndicatorPrms p_ind_p, int p_id) : base(p_ind_p, p_id) { }

        public override void OnSet(bool setDefault, bool haveToChangeTheme)
        {
            IndPrms.OnTickCall = CallHandler.HistRT;
        }

        // Called once per load, before the bars: the place to ask for the aggregated trades
        public override void OnInit()
        {
            Vbk.SetCalculateAggrTrades();
        }

        // The chart is built again from scratch at every load: the group and the list start empty
        public override void OnLoad(bool isFull)
        {
            shown.Clear();
            markers = Vbk.AddAnnList();
            markers.Area = ChartAreaRef.Main;
        }

        public override void OnTick(Feed.TickByTick newTick, bool isRt, bool isNewDay, AggrInfo aggrInfo)
        {
            // aggrInfo describes the aggregated trade which has just been completed: trades of the same side at the same
            // price within a few milliseconds, seen as one. It is null when this trade did not complete one
            if (aggrInfo == null || aggrInfo.Vol < MinSize)
                return;
            bool isBuy = aggrInfo.TradeDir == TradeDirStatus.Ask || aggrInfo.TradeDir == TradeDirStatus.AboveAsk;
            double price = Vbk.FromPinT(aggrInfo.PinT).Price;
            ColorRef color = isBuy ? BuyColor : SellColor;

            // An annotation is created, set, then added to a group: from then on it is drawn.
            // On the X axis the bar of index i is at i + 1
            var mark = Vbk.CreateAnnotation(AnnotationType.Circle);
            mark.Area = ChartAreaRef.Main;
            mark.X = Vbk.TickIndex + 1;
            mark.Y = price;
            mark.Width = MarkerSize;
            mark.LineColor = color;
            mark.BackColor = color.WithOpacity(120);
            mark.Tooltip = $"{(isBuy ? "Buy" : "Sell")} {Vbk.FormatVolume(aggrInfo.Vol)} at {price}";
            Vbk.AddAnnotation(markers, mark);
            shown.Add(mark);

            // The oldest marker leaves the chart
            if (shown.Count > MaxMarkers)
            {
                markers.RemoveAnnotation(shown[0]);
                shown.RemoveAt(0);
            }

            // Messages only in realtime: during the history there would be one for every past trade
            if (isRt && AlertEnabled)
                Vbk.ShowMessage(mark.Tooltip, Vbk.SymbolName);
        }
    }
}
