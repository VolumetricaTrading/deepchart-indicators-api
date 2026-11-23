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
    /// The cumulative delta of the session: every trade at the ask adds its size, every trade at the bid subtracts it.
    /// The chart shows the last part of the session only, so the trades before the first bar are given to the
    /// indicator in a parallel pass: it shows how to accumulate per core and merge
    /// </summary>
    public class SessionCvd : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Session CVD",
                Description = "Cumulative volume delta of the session. Example of the trades and of the parallel pass.",
                Tags = new List<string> { "Example", "Volume" }
            };
        }

        #region Parameters
        [Category("Colors")]
        [DisplayName("Line color")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0)]
        public ColorRef LineColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);
        #endregion

        ChartAreaRef area;
        IChartSeries cvd;
        // The delta of the trades before the first bar, one total per core of the parallel pass
        long[] coreDelta;
        bool merged;
        // The delta of the session up to the current trade
        long delta;

        public SessionCvd(BaseIndicatorPrms p_ind_p, int p_id) : base(p_ind_p, p_id) { }

        public override void OnSet(bool setDefault, bool haveToChangeTheme)
        {
            IndPrms.OnOpenCall = CallHandler.HistRT;
            // Full: the trades of the history and of the realtime, and the trades before the first bar (OnIniParallelTick)
            IndPrms.OnTickCall = CallHandler.Full;
            IndPrms.OnCloseTickCall = CallHandler.HistRT;
            IndPrms.OnEndCall = CallHandler.HistRT;
            if (setDefault)
                return;
            area = CreateCA(false, "CVD");
            Vbk.GetAxisY(area).FormatAutoNumber = true;
            cvd = Vbk.AddSeries(area, "CVD", SeriesType.Line, LineColor, 2);
        }

        // Called before the bars are calculated: the state of a previous calculation is reset here
        public override void OnLoad(bool isFull)
        {
            delta = 0;
            merged = false;
            coreDelta = new long[Vbk.NumCore];
        }

        // The trades before the first bar of the chart, split between the cores of the computer and given in parallel:
        // each core has its own range of trades, in order. Keep one total per core and nothing shared
        public override void OnIniParallelTick(int indCore, int tickBaseIndex, Feed.TickByTick newTick, AggrInfo aggrInfo)
        {
            coreDelta[indCore] += Side(newTick);
        }

        public override void OnOpen(bool isRt)
        {
            Vbk.AddPointToSeries(cvd, Vbk.BookIndex, 0, true);
        }

        // The trades of the bars of the chart, one at a time
        public override void OnTick(Feed.TickByTick newTick, bool isRt, bool isNewDay, AggrInfo aggrInfo)
        {
            Merge();
            delta += Side(newTick);
        }

        public override void OnTickClose(bool isRt)
        {
            Merge();
            SetPoint(Vbk.TickIndex);
        }

        public override void OnEnd(bool isRt)
        {
            Merge();
            if (Vbk.TickIndex < cvd.Points.Count)
                SetPoint(Vbk.TickIndex);
        }

        // A trade at the ask (or above) is a buy, at the bid (or below) a sell
        private static long Side(Feed.TickByTick tick)
        {
            if (tick.price >= tick.ask)
                return tick.Vol;
            if (tick.price <= tick.bid)
                return -tick.Vol;
            return 0;
        }

        // The totals of the parallel pass are added once, in the order of the cores, before the first trade of the chart
        private void Merge()
        {
            if (merged)
                return;
            merged = true;
            foreach (long coreTotal in coreDelta)
                delta += coreTotal;
        }

        private void SetPoint(int index)
        {
            var point = cvd.Points[index];
            point.Y[0] = delta * Vbk.VolMultiplier;
            point.IsHidden = false;
        }
    }
}
