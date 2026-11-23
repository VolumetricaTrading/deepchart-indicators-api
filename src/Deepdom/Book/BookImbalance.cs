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
    /// The share of the order book which is on the bid side, over the first levels of each side: a line in an area of
    /// its own and a label on the chart. It shows the order book: the initial snapshot, the updates and LiquiditySumHelper
    /// </summary>
    public class BookImbalance : Indicator
    {
        public static IndicatorDescriptionBase Register()
        {
            return new IndicatorDescriptionBase
            {
                Name = "Example - Book Imbalance",
                Description = "Share of the order book on the bid side. Example of the order book.",
                Tags = new List<string> { "Example", "Book" }
            };
        }

        #region Parameters
        [Category("General")]
        [DisplayName("Levels per side")]
        [VolCustom(CategoryIndex = 0, PropertyIndex = 0, MinValue = 1, MaxValue = 100, IncrementValue = 1)]
        public int Levels { get; set; } = 10;

        [Category("Colors")]
        [DisplayName("Line color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 0)]
        public ColorRef LineColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Neutral, ColorTypeEnum.Stroke);

        [Category("Colors")]
        [DisplayName("Bid text color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 1)]
        public ColorRef BidColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Bid, ColorTypeEnum.Text);

        [Category("Colors")]
        [DisplayName("Ask text color")]
        [VolCustom(CategoryIndex = 1, PropertyIndex = 2)]
        public ColorRef AskColor { get; set; } = IMethodAPI.GetColorRefForTheme(ColorReferenceEnum.Ask, ColorTypeEnum.Text);
        #endregion

        ChartAreaRef area;
        IChartSeries bidShare;
        IAnnGroup labels;
        IAnnotation label;
        // Sums the quantity of the first Levels levels of each side of the book
        LiquiditySumHelper book;
        bool seeded;

        public BookImbalance(BaseIndicatorPrms p_ind_p, int p_id) : base(p_ind_p, p_id) { }

        public override void OnSet(bool setDefault, bool haveToChangeTheme)
        {
            IndPrms.OnOpenCall = CallHandler.HistRT;
            IndPrms.OnBookCall = CallHandler.HistRT;   // the updates of the order book
            IndPrms.OnEndCall = CallHandler.HistRT;
            if (setDefault)
                return;
            area = CreateCA(false, "Bid share %");
            Vbk.GetAxisY(area).DecimalPlaceShown = 0;
            bidShare = Vbk.AddSeries(area, "Bid %", SeriesType.Line, LineColor, 2);
        }

        public override void OnLoad(bool isFull)
        {
            book = new LiquiditySumHelper(Levels, false);
            seeded = false;

            labels = Vbk.AddAnnList();
            labels.Area = ChartAreaRef.Main;
            labels.PlotForeground = true;
            // Relative coordinates: a position in the chart area instead of a bar and a price.
            // The label does not move when the chart scrolls
            label = Vbk.CreateAnnotation(AnnotationType.Text);
            label.Area = ChartAreaRef.Main;
            label.CoordinateXType = CoordinateTypeEnum.Relative;
            label.CoordinateYType = CoordinateTypeEnum.Relative;
            label.X = 0.02;
            label.Y = 0.95;
            label.FontSize = 12;
            label.FontBold = true;
            Vbk.AddAnnotation(labels, label);
        }

        public override void OnOpen(bool isRt)
        {
            Vbk.AddPointToSeries(bidShare, Vbk.BookIndex, 0, true);
        }

        // The changes of the order book since the previous call: a BookInfo per price, with the quantity now at that
        // price (zero when the level is gone). The first time, the book as it was at the first bar is applied before them
        public override void OnBook(DateTime exDt, IEnumerable<Feed.BookInfo> mbpUpdates, IEnumerable<Feed.MboInfo> mboUpdates, bool isRt)
        {
            if (!seeded)
            {
                seeded = true;
                book.Update(Vbk.FullIniDepth.Values);
            }
            book.Update(mbpUpdates);
            book.Flush();
            Draw();
        }

        public override void OnEnd(bool isRt)
        {
            Draw();
        }

        private void Draw()
        {
            long bid = book.BidTotSum, ask = book.AskTotSum;
            if (bid + ask == 0)
                return;
            double share = 100.0 * bid / (bid + ask);
            if (Vbk.BookIndex >= 0 && Vbk.BookIndex < bidShare.Points.Count)
            {
                var point = bidShare.Points[Vbk.BookIndex];
                point.Y[0] = share;
                point.IsHidden = false;
            }
            label.Text = $"Bid {Vbk.FormatVolume(bid)}   Ask {Vbk.FormatVolume(ask)}   ({share:0}% bid)";
            label.ForeColor = share >= 50 ? BidColor : AskColor;
        }
    }
}
