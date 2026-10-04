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
    /// Share of the order book on the bid side, as a label on the chart.
    /// It shows what makes an indicator a desktop one: the order book (OnBook, RebuildDom) exists only on the
    /// desktop chart, so these members are marked [DesktopOnly] in the API. The indicator still compiles and works
    /// on the chart, but a package which contains it is calculated on the desktop, never on the server
    /// </summary>
    public class BookImbalance : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Book Imbalance",
                Description = "Bid share of the visible order book. Example of a desktop only indicator.",
                Tags = new List<string> { "Example", "Order book" }
            };
        }

        #region Parameters
        [Category("General")]
        [DisplayName("Levels per side")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, MaxValue = 100, IncrementValue = 1)]
        public int Levels { get; set; } = 10;
        #endregion

        // Quantity at every price of the book, by integer ticks (PinT)
        readonly SortedDictionary<int, int> bids = new SortedDictionary<int, int>();
        readonly SortedDictionary<int, int> asks = new SortedDictionary<int, int>();
        IAnnotation label;
        bool bookLoaded;

        public override void OnSet(bool setDefault, bool themeOverride)
        {
            OnEndCall = CallHandler.HistRT;
            // [DesktopOnly]: the updates of the order book
            OnBookCall = CallHandler.RT;
        }

        public override void OnLoad()
        {
            bids.Clear();
            asks.Clear();
            bookLoaded = false;
            label = VAn.CreateAnnotation(AnnotationType.Text);
            label.CoordinateXType = CoordinateTypeEnum.Relative;
            label.CoordinateYType = CoordinateTypeEnum.Relative;
            label.X = 0.02;
            label.Y = 0.9;
            label.FontSize = 12;
            VAn.AddAnnotation(IndVars.FrontAnnList, label);
        }

        public override void OnEnd(bool isRt)
        {
            // The first time, the whole book as it is now: from then on OnBook receives its changes.
            // [DesktopOnly]
            if (!bookLoaded)
            {
                bookLoaded = true;
                Apply(VAn.RebuildDom());
            }
            Draw();
        }

        // [DesktopOnly]: a batch of changes of the book
        public override void OnBook(Feed.BookUpdate updates)
        {
            Apply(updates);
        }

        private void Apply(Feed.BookUpdate updates)
        {
            if (updates?.MbpUpdates == null)
                return;
            if (updates.NeedResetMbp)
            {
                bids.Clear();
                asks.Clear();
            }
            foreach (var level in updates.MbpUpdates)
            {
                var side = level.isAsk ? asks : bids;
                if (level.Qty > 0)
                    side[level.PinT] = level.Qty;
                else
                    side.Remove(level.PinT);
            }
        }

        private void Draw()
        {
            // The best levels: the highest bids, the lowest asks
            long bidQuantity = bids.Reverse().Take(Levels).Sum(x => (long)x.Value);
            long askQuantity = asks.Take(Levels).Sum(x => (long)x.Value);
            if (bidQuantity + askQuantity == 0)
            {
                label.Text = "";
                return;
            }
            double bidShare = bidQuantity * 100.0 / (bidQuantity + askQuantity);
            label.Text = $"Book: {bidShare:0}% bid";
            label.ForeColor = IMethodAPI.GetColorRefForTheme(bidShare >= 50 ? ColorReferenceEnum.Up : ColorReferenceEnum.Down, ColorTypeEnum.Text);
        }
    }
}
