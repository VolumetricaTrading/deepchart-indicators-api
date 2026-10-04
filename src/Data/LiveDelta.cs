using System.ComponentModel;
using VolSysAPI;
using VolSysAPI.Indicators;
using VolumetricaAPI.Chart;
// Trades and order book updates are in VolumetricaAPI.Connection.Structure
using Feed = VolumetricaAPI.Connection.Structure;
using VolumetricaControls;
using VolumetricaCore;
using static VolSysAPI.ExternalStructure;
using static VolSysAPI.Structure;

namespace Deepchart.Indicators.Examples
{
    /// <summary>
    /// Delta of every bar: the volume traded at the ask minus the one traded at the bid.
    /// It shows the realtime trades (OnTick) next to the totals of the closed bars, and the settings of the Y axis.
    /// Place it in an area of its own: its values are volumes, not prices
    /// </summary>
    public class LiveDelta : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Live Delta",
                Description = "Ask volume minus bid volume of every bar, updated at every trade. Example of realtime trades.",
                Tags = new List<string> { "Example", "Volume", "Order flow" }
            };
        }

        #region Parameters
        [Category("General")]
        [DisplayName("Minimum trade size")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, IncrementValue = 1)]
        public int MinimumSize { get; set; } = 1;
        #endregion

        #region Series
        class DeltaSeriesAttribute : ChartSeriesAttribute
        {
            public DeltaSeriesAttribute() : base("Delta")
            {
                CurrentStyle = SeriesType.Bars;
                AvailableStyle = (int)(SeriesType.Bars | SeriesType.Line);
                OverrideAutoColor = true;
                PrimaryColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Up, ColorTypeEnum.Fill);
                SecondaryColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Down, ColorTypeEnum.Fill);
            }
        }
        [DeltaSeries]
        public ChartSeriesDefinition Delta { get; private set; }
        #endregion

        // Delta of the bar still open, from the realtime trades
        long liveDelta;

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            OnOpenCall = CallHandler.HistRT;
            OnCloseCall = CallHandler.HistRT;
            // The trades only in realtime: the history uses the totals of the bars.
            // An indicator which asks the trades of the history (HistRT) is calculated only on the desktop
            OnTickCall = CallHandler.RT;
            Description = "Delta";
        }

        public override void OnInit()
        {
            // The axes are available from here on. Values around zero: the same distance above and below it,
            // whole numbers
            AxisY.YSame = true;
            AxisY.DecimalPlaceShown = 0;
            AxisY.FormatAutoNumber = true;
        }

        public override void OnOpen(bool isRt)
        {
            liveDelta = 0;
            VAn.AddPointToSeries(Delta.BaseSeries, VAn.BarIndex, IsHidden: true);
        }

        // A trade of the bar still open
        public override void OnTick(Feed.TickByTick tick, bool isRt, AggrInfo aggrInfo)
        {
            if (tick.Vol < MinimumSize)
                return;
            if (tick.AggrSide == Feed.AggressorSideEnum.Ask)
                liveDelta += tick.Vol;
            else if (tick.AggrSide == Feed.AggressorSideEnum.Bid)
                liveDelta -= tick.Vol;
            SetPoint(VAn.BarIndex, liveDelta);
        }

        // The bar is closed: its totals are final. VolTotList[0] has the volumes of every trade of the bar
        public override void OnClose(bool isRt)
        {
            // With a minimum size the value built from the trades is kept: the totals have no size filter
            if (isRt && MinimumSize > 1)
                return;
            var totals = VAn.BarVars[VAn.BarIndex].VolTotList[0];
            SetPoint(VAn.BarIndex, totals.AskVol - totals.BidVol);
        }

        private void SetPoint(int index, long delta)
        {
            var point = Delta.BaseSeries.Points[index];
            point.Y[0] = delta;
            point.IsHidden = false;
            point.BackColor = point.LineColor = delta >= 0 ? Delta.PrimaryColor : Delta.SecondaryColor;
        }
    }
}
