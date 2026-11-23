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
    /// Bands around an average, at a number of standard deviations.
    /// It shows more series (one of them optional, one with two values per point), parameters of several kinds
    /// (number, switch, color, nested object) and the ways to give a color to a point
    /// </summary>
    public class PriceBands : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Price Bands",
                Description = "Average with an upper and a lower band. Example of several series, parameters and point colors.",
                Tags = new List<string> { "Example", "Volatility" }
            };
        }

        #region Parameters
        [Category("General")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 2, MaxValue = 1000, IncrementValue = 1)]
        public int Length { get; set; } = 20;

        [Category("General")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1, MinValue = 0.1, MaxValue = 10, IncrementValue = 0.1, DecimalPosToShow = 1)]
        public double Deviations { get; set; } = 2;

        // A switch which decides whether a series exists (see OnSet)
        [Category("General")]
        [DisplayName("Show average")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 2)]
        public bool ShowAverage { get; set; } = true;

        // A color chosen by the user. The default is a color of the theme, so it follows the light and the dark theme
        [Category("Colors")]
        [DisplayName("Squeeze color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public ColorRef SqueezeColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);

        // A parameter can be an object with its own parameters: the settings show it as a group
        public class SqueezeSettings
        {
            public bool Enabled { get; set; } = true;
            [DisplayName("Width under (% of price)")]
            public double WidthPercent { get; set; } = 0.5;
        }
        [Category("Colors")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 1)]
        public SqueezeSettings Squeeze { get; set; } = new SqueezeSettings();
        #endregion

        #region Series
        class AverageSeriesAttribute : ChartSeriesAttribute
        {
            public AverageSeriesAttribute() : base("Average")
            {
                CurrentStyle = SeriesType.Line;
                AvailableStyle = (int)SeriesType.Line;
                LineWidth = 1;
                PrimaryColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);
            }
        }
        // Two values per point: the upper and the lower band. The area between them is filled.
        // OverrideAutoColor: the indicator gives the color to every point
        class BandSeriesAttribute : ChartSeriesAttribute
        {
            public BandSeriesAttribute() : base("Band")
            {
                CurrentStyle = SeriesType.Range;
                AvailableStyle = (int)SeriesType.Range;
                LineWidth = 1;
                OverrideAutoColor = true;
                PrimaryColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Up, ColorTypeEnum.Stroke);
                SecondaryColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Down, ColorTypeEnum.Stroke);
            }
        }
        // Declared first, plotted first: the average is drawn over the band
        [BandSeries]
        public ChartSeriesDefinition Band { get; private set; }
        [AverageSeries]
        public ChartSeriesDefinition Average { get; private set; }
        #endregion

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            // A series which is not enabled is not created: its BaseSeries stays null
            Average.Enabled = ShowAverage;
            OnOpenCall = CallHandler.HistRT;
            OnEndCall = CallHandler.HistRT;
            Description = $"Bands ({Length}, {Deviations:0.0})";
        }

        public override void OnOpen(bool isRt)
        {
            VAn.AddPointToSeries(Band.BaseSeries, VAn.BarIndex, IsHidden: true);
            if (Average.BaseSeries != null)
                VAn.AddPointToSeries(Average.BaseSeries, VAn.BarIndex, IsHidden: true);
            // The bar which has just closed gets its final value
            if (VAn.BarIndex > 0)
                Calculate(VAn.BarIndex - 1);
        }

        public override void OnEnd(bool isRt)
        {
            Calculate(VAn.LastIndex());
        }

        private void Calculate(int index)
        {
            if (index < Length - 1)
                return;
            var bars = VAn.BarVars;
            double sum = 0, sumOfSquares = 0;
            for (int i = index - Length + 1; i <= index; i++)
            {
                sum += bars[i].Close;
                sumOfSquares += bars[i].Close * bars[i].Close;
            }
            double average = sum / Length;
            double deviation = Math.Sqrt(Math.Max(0, sumOfSquares / Length - average * average));
            double upper = average + deviation * Deviations, lower = average - deviation * Deviations;

            if (Average.BaseSeries != null)
            {
                var averagePoint = Average.BaseSeries.Points[index];
                averagePoint.Y[0] = average;
                averagePoint.IsHidden = false;
            }

            var bandPoint = Band.BaseSeries.Points[index];
            bandPoint.Y[0] = upper;
            bandPoint.Y[1] = lower;
            bandPoint.IsHidden = false;

            // The colors of a point are references: to a color of the series (what the user chose in the settings),
            // to a parameter, to the theme. They can be mixed (fade) and made transparent (opacity).
            // The chart resolves them, so they follow the settings and the theme without recalculating
            bool rising = index > 0 && bars[index].Close >= bars[index - 1].Close;
            ColorRef color = rising ? Band.PrimaryColor : Band.SecondaryColor;
            bool isSqueeze = Squeeze.Enabled && average > 0 && (upper - lower) / average * 100 < Squeeze.WidthPercent;
            if (isSqueeze)
                color = VAn.GetFadedColor(color, SqueezeColor, 0.7);
            bandPoint.LineColor = color;
            bandPoint.BackColor = color.WithOpacity(40);
        }
    }
}
