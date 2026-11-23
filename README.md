# Deepchart and Deepdom Indicators

Starting projects for writing your own indicators for Deepchart and for Deepdom in C#, with small examples and the
documentation which explains them. Clone it to start your own library:

```bash
git clone https://github.com/VolumetricaTrading/deepchart-indicators-api.git
```

The two products share the drawing interfaces, the colors and the parameter attributes, and each has its own API and
its own example project:

| | Deepchart | Deepdom |
|---|---|---|
| Project | `src/Deepchart/DeepchartIndicators.csproj` | `src/Deepdom/DeepdomIndicators.csproj` |
| Library | `VolSysAPI.dll` | `VolBookAPI.dll` |
| Developer id property | `DeepchartDevId` | `DeepdomDevId` |
| Output | `Documents\Deepchart\Indicators` | `Documents\Deepdom\Indicators` |
| Calculated on | the desktop chart or, when the dll uses only the portable API, the Deepchart server | the desktop chart |

## Quick start

1. Clone this repository (see above), install the product and the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Set your developer id in the project file (`DeepchartDevId` or `DeepdomDevId`). A local dll is loaded only when it
   carries the id of the logged user. *Where to read your id is not documented yet: see `docs/hybrid/developer-id.mdx`.*
3. Build:

   ```bash
   dotnet build src/Deepchart/DeepchartIndicators.csproj
   dotnet build src/Deepdom/DeepdomIndicators.csproj
   ```

   The dll goes to the `Indicators` folder of the product. If the product is not in
   `C:\Program Files\Volumetrica Trading\<product>\`, add `-p:DeepchartDir="your folder"` or `-p:DeepdomDir="your folder"`.
4. Open a chart: the examples are in the indicator list under **Personal**, named `Example - ...`.

## Deepchart examples

The indicators are **hybrid**: the same dll is calculated by the desktop chart and, when it uses only the portable API,
by the Deepchart server.

| Indicator | Shows | Calculated on |
|---|---|---|
| `Basics/SimpleMovingAverage.cs` | registration, one series, parameters, the calls of the chart | desktop or server |
| `Basics/PriceBands.cs` | several series, an optional series, point colors, nested parameters | desktop or server |
| `Annotations/SessionRangeBox.cs` | annotations on the bars, groups, realtime update, removal | desktop or server |
| `Annotations/InfoLabel.cs` | relative coordinates, foreground, description and status message | desktop or server |
| `Data/BarPoc.cs` | volume by price | desktop or server |
| `Data/LiveDelta.cs` | realtime trades, bar totals, Y axis | desktop or server |
| `Data/HigherTimeframeCandles.cs` | another resolution of the symbol (`RequestResolution`), candles colored by delta | desktop or server |
| `DesktopOnly/BookImbalance.cs` | order book: a desktop only feature | desktop |
| `Trading/MovingAverageCross.cs` | orders with brackets through `TradingApi`, in a backtest or on a SIM account | desktop |

## Deepdom examples

| Indicator | Shows |
|---|---|
| `Basics/BarDelta.cs` | registration, a chart area, one series, parameters, the calls of the chart |
| `Basics/SessionCvd.cs` | the trades, the parallel pass over the trades before the first bar |
| `Annotations/BigTradeMarker.cs` | aggregated trades, annotations on the price, removal, alerts |
| `Book/BookImbalance.cs` | the order book: initial snapshot, updates, `LiquiditySumHelper`, a label |
| `Profile/ProfilePoc.cs` | the volume profile of the session, lines extended to the right |

## Documentation

The `docs` folder is a [Mintlify](https://mintlify.com) site with a tab for each product. To preview it:

```bash
cd docs
npx mint dev
```

## Layout

```
Indicators.sln
src/Deepchart/   the Deepchart project and its example indicators
src/Deepdom/     the Deepdom project and its example indicators
docs/            the documentation (docs.json and .mdx pages; the Deepdom pages are in docs/deepdom)
```
