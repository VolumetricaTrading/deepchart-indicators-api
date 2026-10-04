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
    /// The price with the most volume of every bar (point of control), as a marker on the bar.
    /// It shows how to read the volume by price of the bars
    /// </summary>
    public class BarPoc : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Bar POC",
                Description = "Price with the highest volume of every bar. Example of volume by price.",
                Tags = new List<string> { "Example", "Volume" }
            };
        }

        #region Parameters
        [Category("General")]
        [DisplayName("Minimum volume")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 0, IncrementValue = 10)]
        public int MinimumVolume { get; set; } = 0;
        #endregion

        #region Series
        class PocSeriesAttribute : ChartSeriesAttribute
        {
            public PocSeriesAttribute() : base("POC")
            {
                // A marker on every bar instead of a line. The user can choose another marker
                CurrentStyle = SeriesType.Square;
                AvailableStyle = (int)(SeriesType.Square | SeriesType.Diamond | SeriesType.Cross | SeriesType.Cicle);
                LineWidth = 2;
                PrimaryColor = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);
            }
        }
        [PocSeries]
        public ChartSeriesDefinition Poc { get; private set; }
        #endregion

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            // The volume by price of the bars is built only for the indicators which ask for it
            NeedVbp = true;
            OnOpenCall = CallHandler.HistRT;
            OnEndCall = CallHandler.HistRT;
        }

        public override void OnOpen(bool isRt)
        {
            VAn.AddPointToSeries(Poc.BaseSeries, VAn.BarIndex, IsHidden: true);
            if (VAn.BarIndex > 0)
                Calculate(VAn.BarIndex - 1);
        }

        public override void OnEnd(bool isRt)
        {
            Calculate(VAn.LastIndex());
        }

        private void Calculate(int index)
        {
            var bar = VAn.BarVars[index];
            var point = Poc.BaseSeries.Points[index];
            // Without the volume by price (a symbol or a data feed which does not have it) there is nothing to show
            if (bar.PriceList == null || bar.VbpListAll == null)
            {
                point.IsHidden = true;
                return;
            }
            // PriceList has the prices traded in the bar, as integer ticks (PinT).
            // VbpListAll gives the volume of each one: element [0] is the volume of every trade
            int pocPinT = 0, pocVolume = -1;
            foreach (int pinT in bar.PriceList)
            {
                if (!bar.VbpListAll.TryGetValue(pinT, out var levels) || levels == null)
                    continue;
                if (levels[0].TotVol > pocVolume)
                {
                    pocVolume = levels[0].TotVol;
                    pocPinT = pinT;
                }
            }
            point.IsHidden = pocVolume < Math.Max(1, MinimumVolume);
            if (!point.IsHidden)
                // From integer ticks to the price
                point.Y[0] = VAn.FromPinT(pocPinT).Price;
        }
    }
}
