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
    /// A text which stays at the same place of the chart area while the chart scrolls, with the change of the session.
    /// It shows the relative coordinates, the annotations on the foreground, and the two texts an indicator
    /// gives to the chart: its description and its status message
    /// </summary>
    public class InfoLabel : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Info Label",
                Description = "Fixed label with the change of the session. Example of relative coordinates.",
                Tags = new List<string> { "Example", "Session" }
            };
        }

        #region Parameters
        // Position of the label in the chart area, from 0 to 1 on both axes
        [Category("General")]
        [DisplayName("Horizontal position")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 0, MaxValue = 1, IncrementValue = 0.05, DecimalPosToShow = 2)]
        public double HorizontalPosition { get; set; } = 0.02;

        [Category("General")]
        [DisplayName("Vertical position")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1, MinValue = 0, MaxValue = 1, IncrementValue = 0.05, DecimalPosToShow = 2)]
        public double VerticalPosition { get; set; } = 0.95;

        [Category("General")]
        [DisplayName("Font size")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 2, MinValue = 6, MaxValue = 40, IncrementValue = 1)]
        public int FontSize { get; set; } = 12;
        #endregion

        IAnnotation label;
        double sessionOpen;

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            OnOpenCall = CallHandler.HistRT;
            OnEndCall = CallHandler.HistRT;
            // Shown next to the name of the indicator on the chart
            Description = "Session change";
        }

        public override void OnLoad()
        {
            // A message for the user, shown on the chart when it has loaded: null when there is nothing to say.
            // It has to be set here or on OnInit
            StatusMessage = VAn.ParamType == ChartResolutionInfo.ParamTypeEnum.Minute ? null : "Info Label: the session change is meant for time based charts";

            label = VAn.CreateAnnotation(AnnotationType.Text);
            // Relative coordinates: a position in the chart area instead of a bar and a price.
            // The label does not move when the chart scrolls
            label.CoordinateXType = CoordinateTypeEnum.Relative;
            label.CoordinateYType = CoordinateTypeEnum.Relative;
            label.X = HorizontalPosition;
            label.Y = VerticalPosition;
            label.FontSize = FontSize;
            label.FontBold = true;
            // Over the bars and the other indicators
            VAn.AddAnnotation(IndVars.FrontAnnList, label);
        }

        public override void OnOpen(bool isRt)
        {
            var bar = VAn.BarVars[VAn.BarIndex];
            if (bar.IsNewDay || VAn.BarIndex == 0)
                sessionOpen = bar.Open;
        }

        public override void OnEnd(bool isRt)
        {
            if (sessionOpen == 0)
                return;
            double last = VAn.BarVars[VAn.LastIndex()].Close;
            double change = (last - sessionOpen) / sessionOpen * 100;
            label.Text = $"{VAn.SymbolName}  {change:+0.00;-0.00}%";
            // Theme colors: the right green and red for the theme of the user
            label.ForeColor = IMethodAPI.GetColorRefForTheme(change >= 0 ? ColorReferenceEnum.Up : ColorReferenceEnum.Down, ColorTypeEnum.Text);
        }
    }
}
