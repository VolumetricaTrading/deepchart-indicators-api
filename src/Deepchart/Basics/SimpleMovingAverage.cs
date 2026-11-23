using System.ComponentModel;
using VolSysAPI.Indicators;
using VolumetricaAPI.Chart;
using VolumetricaControls;
using VolumetricaCore;
using static VolSysAPI.ExternalStructure;

namespace Deepchart.Indicators.Examples
{
    /// <summary>
    /// The smallest useful indicator: one series, two parameters.
    /// It shows the parts every indicator has: Register, the parameters, a series, and the calls of the chart
    /// </summary>
    public class SimpleMovingAverage : Indicator
    {
        // How the indicator is listed in Deepchart. The chart finds the indicators of a dll by this static method
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Simple Moving Average",
                Description = "Average of the last bars. The smallest example of a series.",
                Tags = new List<string> { "Example", "Trend" }
            };
        }

        #region Parameters
        // Every public property with a getter and a setter is a parameter: it is shown in the settings of the
        // indicator and saved with the workspace. Category groups the parameters, VolCustom orders and limits them
        public enum PriceSourceEnum { Close, Open, High, Low }

        [Category("General")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, MaxValue = 1000, IncrementValue = 1)]
        public int Length { get; set; } = 20;

        [Category("General")]
        [DisplayName("Price source")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1)]
        public PriceSourceEnum Source { get; set; } = PriceSourceEnum.Close;
        #endregion

        #region Series
        // A series is declared with a property of type ChartSeriesDefinition, marked with its own attribute class.
        // The constructor of the attribute sets the defaults of what the user can then change (style, width, colors)
        class AverageSeriesAttribute : ChartSeriesAttribute
        {
            public AverageSeriesAttribute() : base("Average")
            {
                CurrentStyle = SeriesType.Line;
                AvailableStyle = (int)SeriesType.Line;
                LineWidth = 2;
                PrimaryColor = ColorRef.FromRgb(255, 165, 0);
            }
        }
        [AverageSeries]
        public ChartSeriesDefinition Average { get; private set; }
        #endregion

        // Sum of the source of the last Length closed bars
        double sum;

        // Called before every load of the chart, and when the user changes a parameter.
        // Here the indicator says which calls it needs: Off (default), RT (realtime only) or HistRT (history and realtime)
        public override void OnSet(bool setDefault, bool themeOverride)
        {
            OnOpenCall = CallHandler.HistRT;
            OnCloseCall = CallHandler.HistRT;
            OnEndCall = CallHandler.HistRT;
            Description = $"SMA ({Length})";
        }

        // Called before the bars are calculated: the state of a previous calculation is reset here
        public override void OnLoad()
        {
            sum = 0;
        }

        // A new bar: a point is added to the series for it. It stays hidden until it has a value
        public override void OnOpen(bool isRt)
        {
            VAn.AddPointToSeries(Average.BaseSeries, VAn.BarIndex, IsHidden: true);
        }

        // The bar is closed: its value will not change any more
        public override void OnClose(bool isRt)
        {
            int index = VAn.BarIndex;
            sum += GetSource(index);
            if (index >= Length)
                sum -= GetSource(index - Length);
            if (index >= Length - 1)
                SetPoint(index, sum / Length);
        }

        // After the history has been calculated, and after every batch of realtime updates: the bar still open is updated
        public override void OnEnd(bool isRt)
        {
            int index = VAn.LastIndex();
            if (index < Length - 1)
                return;
            // The closed bars are already in the sum: the oldest leaves, the open one enters
            double window = sum + GetSource(index) - (index >= Length ? GetSource(index - Length) : 0);
            SetPoint(index, window / Length);
        }

        private void SetPoint(int index, double value)
        {
            // The point of index i is the one of the bar of index i
            var point = Average.BaseSeries.Points[index];
            point.Y[0] = value;
            point.IsHidden = false;
        }

        private double GetSource(int index)
        {
            var bar = VAn.BarVars[index];
            switch (Source)
            {
                case PriceSourceEnum.Open: return bar.Open;
                case PriceSourceEnum.High: return bar.High;
                case PriceSourceEnum.Low: return bar.Low;
                default: return bar.Close;
            }
        }
    }
}
