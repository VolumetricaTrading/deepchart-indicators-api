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
    /// A box around the range of every session, with its size written over it.
    /// It shows the annotations: creating them, placing them on the bars, grouping them,
    /// updating the last one while the session goes on and removing the old ones
    /// </summary>
    public class SessionRangeBox : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Session Range Box",
                Description = "Box around the high and the low of every session. Example of annotations and groups.",
                Tags = new List<string> { "Example", "Session" }
            };
        }

        #region Parameters
        [Category("General")]
        [DisplayName("Sessions shown")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, MaxValue = 100, IncrementValue = 1)]
        public int MaxSessions { get; set; } = 10;

        [Category("General")]
        [DisplayName("Show size")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 1)]
        public bool ShowSize { get; set; } = true;

        [Category("Colors")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public ColorRef BoxColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);
        #endregion

        // A session on the chart: its box, its label and the bars it covers
        class SessionBox
        {
            public IAnnotation Box, Label;
            public int FirstIndex;
            public double High, Low;
        }
        readonly List<SessionBox> sessions = new List<SessionBox>();
        // The annotations of an indicator are kept in groups. A group holds either annotations or other groups:
        // here two groups, one for the boxes and one for the labels, inside the group of the indicator
        IAnnGroup boxes, labels;

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            OnOpenCall = CallHandler.HistRT;
            OnEndCall = CallHandler.HistRT;
        }

        public override void OnLoad()
        {
            // The group of the indicator (IndVars.Ann_List) is emptied by the chart before every calculation
            sessions.Clear();
            boxes = VAn.CreateAnnGroup();
            labels = VAn.CreateAnnGroup();
            IndVars.Ann_List.AddGroup(boxes);
            IndVars.Ann_List.AddGroup(labels);
        }

        public override void OnOpen(bool isRt)
        {
            int index = VAn.BarIndex;
            var bar = VAn.BarVars[index];
            // The previous bar is complete now
            if (index > 0 && sessions.Count > 0)
                Extend(index - 1);
            if (bar.IsNewDay || sessions.Count == 0)
                StartSession(index);
        }

        public override void OnEnd(bool isRt)
        {
            // The bar still open: the box of the current session follows it
            if (sessions.Count > 0)
                Extend(VAn.LastIndex());
        }

        private void StartSession(int index)
        {
            var bar = VAn.BarVars[index];
            var session = new SessionBox { FirstIndex = index, High = bar.High, Low = bar.Low };

            // An annotation is created, set, then added to a group: from then on it is drawn
            session.Box = VAn.CreateAnnotation(AnnotationType.Rectangle);
            session.Box.LineColor = BoxColor;
            session.Box.BackColor = BoxColor.WithOpacity(25);
            session.Box.LineWidth = 1;
            VAn.AddAnnotation(boxes, session.Box);

            if (ShowSize)
            {
                session.Label = VAn.CreateAnnotation(AnnotationType.Text);
                session.Label.ForeColor = BoxColor;
                session.Label.FontSize = 11;
                VAn.AddAnnotation(labels, session.Label);
            }
            sessions.Add(session);
            Place(session, index);

            // The oldest session leaves the chart
            if (sessions.Count > MaxSessions)
            {
                var oldest = sessions[0];
                boxes.RemoveAnnotation(oldest.Box);
                if (oldest.Label != null)
                    labels.RemoveAnnotation(oldest.Label);
                sessions.RemoveAt(0);
            }
        }

        private void Extend(int index)
        {
            var session = sessions[sessions.Count - 1];
            var bar = VAn.BarVars[index];
            session.High = Math.Max(session.High, bar.High);
            session.Low = Math.Min(session.Low, bar.Low);
            Place(session, index);
        }

        // An annotation which changes is simply set again: only what changed reaches the chart
        private void Place(SessionBox session, int lastIndex)
        {
            // On the X axis the bar of index i is at i + 1, and it is one unit wide: from i + 0.5 to i + 1.5
            session.Box.X = session.FirstIndex + 0.5;
            session.Box.X2 = lastIndex + 1.5;
            session.Box.Y = session.High;
            session.Box.Y2 = session.Low;
            if (session.Label != null)
            {
                session.Label.X = session.FirstIndex + 1;
                session.Label.Y = session.High;
                session.Label.Text = $"{VAn.GetDoubleTicksDiff(session.Low, session.High)} ticks";
            }
        }
    }
}
