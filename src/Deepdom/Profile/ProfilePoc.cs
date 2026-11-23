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
    /// The point of control and the value area of the volume profile of the session, as horizontal lines on the price.
    /// It shows the volume profile of the session and the lines extended to the right edge of the chart
    /// </summary>
    public class ProfilePoc : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Profile POC",
                Description = "Point of control and value area of the session. Example of the volume profile.",
                Tags = new List<string> { "Example", "Profile" }
            };
        }

        #region Parameters
        [Category("General")]
        [DisplayName("Value area %")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 50, MaxValue = 100, IncrementValue = 1)]
        public int ValueAreaPercent { get; set; } = 70;

        [Category("General")]
        [DisplayName("Show value area")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1)]
        public bool ShowValueArea { get; set; } = true;

        [Category("Colors")]
        [DisplayName("POC color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public ColorRef PocColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);

        [Category("Colors")]
        [DisplayName("Value area color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 1)]
        public ColorRef ValueAreaColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);
        #endregion

        IAnnGroup lines;
        IAnnotation poc, valueAreaHigh, valueAreaLow;

        public ProfilePoc(BaseIndicatorPrms p_ind_p, int p_id) : base(p_ind_p, p_id) { }

        public override void OnSet(bool setDefault, bool haveToChangeTheme)
        {
            IndPrms.OnOpenCall = CallHandler.HistRT;
            IndPrms.OnEndCall = CallHandler.HistRT;
        }

        // The three lines are created once per load and moved as the profile changes
        public override void OnLoad(bool isFull)
        {
            lines = Vbk.AddAnnList();
            lines.Area = ChartAreaRef.Main;
            poc = CreateLine(PocColor, 2);
            valueAreaHigh = CreateLine(ValueAreaColor, 1);
            valueAreaLow = CreateLine(ValueAreaColor, 1);
            valueAreaHigh.Visible = valueAreaLow.Visible = ShowValueArea;
        }

        private IAnnotation CreateLine(ColorRef color, int width)
        {
            var line = Vbk.CreateAnnotation(AnnotationType.HorizontalLine);
            line.Area = ChartAreaRef.Main;
            // From the first bar to the right edge of the chart
            line.X = 1;
            line.IsExtended = true;
            line.LineColor = color;
            line.LineWidth = width;
            line.ShowLabel = true;
            Vbk.AddAnnotation(lines, line);
            return line;
        }

        public override void OnEnd(bool isRt)
        {
            // The volume profile of the session: the volume traded at every price, indexed by the price in ticks.
            // It is a snapshot of the whole session: during the history it already includes the trades after the current bar
            var profile = Vbk.DailyProfile;
            if (profile == null || profile.vbpItems == null)
                return;
            var levels = profile.vbpItems.SortedLHValues.ToList();
            if (levels.Count == 0)
                return;
            long total = 0;
            int pocIndex = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                total += levels[i].TotVol;
                if (levels[i].TotVol > levels[pocIndex].TotVol)
                    pocIndex = i;
            }
            if (total == 0)
                return;

            // The value area grows from the point of control, taking the neighbouring level with more volume,
            // until it holds the wanted share of the volume of the session
            int low = pocIndex, high = pocIndex;
            long inArea = levels[pocIndex].TotVol;
            long target = total * ValueAreaPercent / 100;
            while (inArea < target && (low > 0 || high < levels.Count - 1))
            {
                long below = low > 0 ? levels[low - 1].TotVol : -1;
                long above = high < levels.Count - 1 ? levels[high + 1].TotVol : -1;
                if (above > below)
                    inArea += levels[++high].TotVol;
                else
                    inArea += levels[--low].TotVol;
            }

            Place(poc, levels[pocIndex], "POC");
            Place(valueAreaHigh, levels[high], "VAH");
            Place(valueAreaLow, levels[low], "VAL");
        }

        private void Place(IAnnotation line, VbpClass level, string name)
        {
            // Prices are kept as integer ticks: FromPinT gives the price
            line.Y = Vbk.FromPinT(level.PriceInTicksLow).Price;
            line.Text = $"{name} {Vbk.FormatVolume(level.TotVol)}";
        }
    }
}
