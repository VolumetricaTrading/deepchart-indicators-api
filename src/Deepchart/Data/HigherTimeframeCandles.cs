using System.ComponentModel;
using VolSysAPI;
using VolSysAPI.Indicators;
using VolumetricaAPI.Chart;
using VolumetricaControls;
using VolumetricaCore;
using static VolSysAPI.ExternalStructure;
using static VolSysAPI.Structure;

namespace Deepchart.Indicators.Examples
{
    /// <summary>
    /// The bars of another resolution of the symbol of the chart (30 minutes on a 5 minutes chart, for example),
    /// drawn as candles which span the bars of the chart they cover, colored by their delta.
    /// It shows RequestResolution: where to ask for the resolution (OnInit), how to find the bar of the resolution
    /// a bar of the chart belongs to, and how to read its totals.
    /// On the server only the resolution of the chart itself is available for now: asking for another one throws
    /// </summary>
    public class HigherTimeframeCandles : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Higher Timeframe Candles",
                Description = "Candles of another resolution of the symbol, colored by delta. Example of RequestResolution.",
                Tags = new List<string> { "Example", "Multi timeframe" }
            };
        }

        #region Parameters
        // The resolution is described like the one of a chart: a type and up to two values
        // (Minute and 30 is a 30 minutes chart, Volume and 1000 a 1000 contracts chart)
        [Category("Resolution")]
        [DisplayName("Type")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0)]
        public ChartResolutionInfo.ParamTypeEnum ParamType { get; set; } = ChartResolutionInfo.ParamTypeEnum.Minute;

        [Category("Resolution")]
        [DisplayName("Value 1")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1, MinValue = 1, MaxValue = 1000000, IncrementValue = 1)]
        public int Param1 { get; set; } = 30;

        [Category("Resolution")]
        [DisplayName("Value 2")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 2, MinValue = 0, MaxValue = 1000000, IncrementValue = 1)]
        public int Param2 { get; set; } = 0;

        [Category("Resolution")]
        [DisplayName("Days to load (0 = as the chart)")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 3, MinValue = 0, MaxValue = 730, IncrementValue = 1)]
        public int DaysToLoad { get; set; } = 0;

        public enum DeltaInputEnum { Volume, Trades }

        [Category("Colors")]
        [DisplayName("Delta from")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public DeltaInputEnum DeltaInput { get; set; } = DeltaInputEnum.Volume;

        [Category("Colors")]
        [DisplayName("Border by open and close")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 1)]
        public bool BorderByOpenClose { get; set; } = false;
        #endregion

        #region Series
        // Four values per point (high, low, open, close). The series draws the close with the Line style; with the
        // Candlestick style it is hidden and the candles are drawn as annotations, each one as wide as the bars it covers.
        // Its four colors are the colors of the candles: border up and down, fill up and down
        class CandlesSeriesAttribute : ChartSeriesAttribute
        {
            public CandlesSeriesAttribute() : base("Candles")
            {
                CurrentStyle = SeriesType.Candlestick;
                AvailableStyle = (int)(SeriesType.Candlestick | SeriesType.Line);
                CustomNumOfPoints = 4;
                OverrideAutoColor = true;
                LineWidth = 1;
                PointWidth = 0.8;
                PrimaryColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Up, ColorTypeEnum.Stroke);
                SecondaryColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Down, ColorTypeEnum.Stroke);
                ThirdColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Up, ColorTypeEnum.Fill);
                FourthColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Down, ColorTypeEnum.Fill);
            }
        }
        [CandlesSeries]
        public ChartSeriesDefinition Candles { get; private set; }
        #endregion

        ResolutionData htf;     // the bars of the resolution asked for
        BarClass htfBar;        // the bar of the resolution the current bar of the chart belongs to
        IAnnotation candle;     // the candle drawn for htfBar
        IChartPoint point;      // the point of the current bar of the chart
        bool drawCandles;       // the user chose the Candlestick style
        bool axisSet;

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            // Prices of another resolution: an area of its own, the first time
            if (setDefault)
                IndPrms.C_Area = 2;
            OnOpenCall = CallHandler.HistRT;
            OnCloseCall = CallHandler.HistRT;
            OnEndCall = CallHandler.HistRT;
            // With the Candlestick style the series only holds the values (and gives the axis its range)
            drawCandles = GetSeries(Candles).CurrentStyle == SeriesType.Candlestick;
            Candles.ForceHidden = drawCandles;
            Description = $"HTF {ParamType} {Param1}{(Param2 != 0 ? $" {Param2}" : "")}";
        }

        // The resolution is asked for in OnInit, and only there: the chart prepares what has been asked here before
        // OnLoad. The data is not there yet when the call returns: it is loaded with the history, before the bars of the chart
        public override void OnInit()
        {
            // Days to load: the ones of the chart unless the parameter says otherwise. Time based bars are built from
            // the minute data (BasedOnMinute), the others from the trades
            htf = VAn.RequestResolution(ParamType, Param1, Param2,
                DaysToLoad > 0 ? DaysToLoad : null,
                ParamType <= ChartResolutionInfo.ParamTypeEnum.Montly ? true : null);
            // The totals of the bars (VolTotList[0]) of another resolution exist only when someone asks for them
            htf.AddNewFilter(0, 0, DataInputTypeEnum.Volume, VbpType.Bar, 0, false);
        }

        public override void OnLoad()
        {
            htfBar = null;
            candle = null;
            point = null;
            axisSet = false;
        }

        public override void OnOpen(bool isRt)
        {
            int index = VAn.BarIndex;
            Sync(index);
            // Hidden until a bar of the resolution is found
            point = VAn.AddPointToSeries(Candles.BaseSeries, index, htfBar?.Open ?? 0, htfBar == null);
            Update(index);
        }

        public override void OnClose(bool isRt)
        {
            int index = VAn.BarIndex;
            Sync(index);
            Update(index);
        }

        public override void OnEnd(bool isRt)
        {
            int index = VAn.LastIndex();
            Sync(index);
            Update(index);
            // The annotations do not count for the automatic range of the axis: the last candle is kept in view
            if (htfBar != null && AxisY.YAuto)
            {
                if (htfBar.High > AxisY.ViewMaximum)
                    AxisY.ViewMaximum = htfBar.High;
                if (htfBar.Low < AxisY.ViewMinimum)
                    AxisY.ViewMinimum = htfBar.Low;
            }
        }

        // The bar of the resolution a time belongs to. Exchange time, never the local one: the user can change it.
        // A time before the first bar of the resolution gives index 0, so it is checked first
        private BarClass Find(DateTime exchDt)
        {
            if (!htf.HasBar || exchDt < htf.BarVars[0].exchDt)
                return null;
            return htf.BarVars[htf.FindBarIndexByExchDt(exchDt)];
        }

        // The bar of the chart at index belongs to a bar of the resolution: when it is a new one, the previous candle
        // is closed and a new one starts. A bar of the chart is one unit wide, from index + 0.5 to index + 1.5:
        // the candles leave a small gap between them
        private void Sync(int index)
        {
            var found = Find(VAn.BarVars[index].exchDt);
            if (found == htfBar)
                return;
            if (candle != null)
                candle.X2 = index + 0.4;
            htfBar = found;
            candle = null;
            if (htfBar == null)
                return;
            if (!axisSet)
                SetAxis(htfBar.Open);
            if (drawCandles)
            {
                candle = VAn.CreateAnnotation(AnnotationType.Candlestick);
                candle.Area = ChartArea;
                candle.X = index + 0.6;
                candle.UseAxisWithSpace = false;
                candle.LineWidth = GetSeries(Candles).LineWidth;
                VAn.AddAnnotation(IndVars.Ann_List, candle);
            }
        }

        // The values of the bar of the resolution, as it is now: in the history it is already complete,
        // in realtime it grows with the bars of the chart
        private void Update(int index)
        {
            if (htfBar == null || point == null)
                return;
            point.Y[0] = htfBar.High;
            point.Y[1] = htfBar.Low;
            point.Y[2] = htfBar.Open;
            point.Y[3] = htfBar.Close;
            point.IsHidden = false;

            // The delta of the bar from its totals. Some symbols have no volume: the direction of the bar is used then
            var totals = htfBar.VolTotList?.Length > 0 ? htfBar.VolTotList[0] : null;
            double delta = totals == null ? htfBar.Close - htfBar.Open
                : DeltaInput == DeltaInputEnum.Volume ? totals.AskVol - totals.BidVol : totals.AskNTVol - totals.BidNTVol;
            bool borderUp = BorderByOpenClose ? htfBar.Close >= htfBar.Open : delta >= 0;
            ColorRef fill = delta >= 0 ? Candles.ThirdColor : Candles.FourthColor;
            ColorRef border = borderUp ? Candles.PrimaryColor : Candles.SecondaryColor;
            point.BackColor = fill;
            point.LineColor = border;

            if (candle != null)
            {
                // The body goes from Y to Y + Height, Y3 is the high and Y2 the low
                candle.Y = htfBar.Open;
                candle.Height = htfBar.Close - htfBar.Open;
                candle.Y2 = htfBar.Low;
                candle.Y3 = htfBar.High;
                candle.X2 = index + 1.4;
                candle.BackColor = fill;
                candle.LineColor = border;
            }
        }

        // The area is a new one: its axis takes the tick size and the decimals of the symbol
        private void SetAxis(double price)
        {
            axisSet = true;
            double tick = VAn.FromPrice(price).SubDouble;
            if (tick <= 0)
                return;
            int decimals = 0;
            for (double t = tick; decimals < 8 && Math.Abs(t - Math.Round(t)) > 1e-9; t *= 10)
                decimals++;
            AxisY.UseCustomTickSize = true;
            AxisY.CustomTickSize = tick;
            AxisY.DecimalPlaceShown = decimals;
        }
    }
}
