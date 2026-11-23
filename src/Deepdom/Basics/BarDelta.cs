using System.ComponentModel;
using VolBookAPI;
using VolBookAPI.Indicators;
using VolumetricaAPI.Chart;
using VolumetricaControls;
using VolumetricaCore;
using static VolBookAPI.ExternalStructure;
using static VolBookAPI.Structure;

namespace Deepdom.Indicators.Examples
{
    /// <summary>
    /// The delta of every bar, ask volume minus bid volume, as a histogram in an area of its own.
    /// It shows the parts every indicator has: Register, the parameters, a chart area, a series and the calls of the chart
    /// </summary>
    public class BarDelta : Indicator
    {
        // How the indicator is listed in Deepdom. The chart finds the indicators of a dll by this static method
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Bar Delta",
                Description = "Ask volume minus bid volume of every bar. The smallest example of a series.",
                Tags = new List<string> { "Example", "Volume" }
            };
        }

        #region Parameters
        // Every public property with a getter and a setter is a parameter: it is shown in the settings of the
        // indicator and saved with the workspace. Category groups the parameters, VolCustom orders and limits them
        [Category("General")]
        [DisplayName("Minimum volume")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 0, MaxValue = 100000, IncrementValue = 1)]
        public int MinVolume { get; set; } = 0;

        // A color parameter is a ColorRef. A theme color follows the light and the dark theme of the user
        [Category("Colors")]
        [DisplayName("Ask color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public ColorRef AskColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Ask, ColorTypeEnum.Fill);

        [Category("Colors")]
        [DisplayName("Bid color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 1)]
        public ColorRef BidColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Bid, ColorTypeEnum.Fill);
        #endregion

        ChartAreaRef area;
        IChartSeries delta;

        // The chart creates the indicator with its parameters and an id: the base class keeps them
        public BarDelta(BaseIndicatorPrms p_ind_p, int p_id) : base(p_ind_p, p_id) { }

        // Called when the indicator is created from the list (setDefault is true) and before every load of the chart.
        // Here the indicator says which calls it needs: Off (default), RT (realtime only) or HistRT (history and realtime)
        public override void OnSet(bool setDefault, bool haveToChangeTheme)
        {
            IndPrms.OnOpenCall = CallHandler.HistRT;       // a new bar
            IndPrms.OnCloseTickCall = CallHandler.HistRT;  // the trades of a bar are complete
            IndPrms.OnEndCall = CallHandler.HistRT;        // after the history and after every batch of updates
            // When the indicator is being created the chart is not there yet: nothing else can be done
            if (setDefault)
                return;
            // The chart is built again from scratch at every load: the area and the series are created here every time
            area = CreateCA(false, "Delta");
            Vbk.GetAxisY(area).FormatAutoNumber = true;
            delta = Vbk.AddSeries(area, "Delta", SeriesType.Bars, AskColor, 2);
        }

        // A new bar: a point is added to the series for it, hidden until the bar has its trades.
        // The point of index i is the one of the bar of index i
        public override void OnOpen(bool isRt)
        {
            Vbk.AddPointToSeries(delta, Vbk.BookIndex, 0, true);
        }

        // The trades of a bar are complete: its value will not change any more
        public override void OnTickClose(bool isRt)
        {
            SetPoint(Vbk.TickIndex);
        }

        // After the history, and after every batch of realtime updates: the bar still open is updated
        public override void OnEnd(bool isRt)
        {
            if (Vbk.TickIndex < delta.Points.Count)
                SetPoint(Vbk.TickIndex);
        }

        private void SetPoint(int index)
        {
            var bar = Vbk.BookBars[index];
            int value = bar.AskVol - bar.BidVol;
            var point = delta.Points[index];
            // Volumes are integers: VolMultiplier gives the real size of the symbol (cryptocurrencies)
            point.Y[0] = Math.Abs(value) >= MinVolume ? value * Vbk.VolMultiplier : 0;
            point.LineColor = value >= 0 ? AskColor : BidColor;
            point.IsHidden = false;
        }

        // The tooltip of the chart asks every indicator for a line: only for its own area, and only when there is something to say
        public override string OnChartBuildTooltip(int CAIndex, int IndX, double Price)
        {
            if (!CA.HasValue || CAIndex != CA.Value.Index || IndX < 0 || IndX >= Vbk.BookBars.Count)
                return null;
            var bar = Vbk.BookBars[IndX];
            return $"Delta {Vbk.FormatVolume(bar.AskVol - bar.BidVol)}";
        }
    }
}
