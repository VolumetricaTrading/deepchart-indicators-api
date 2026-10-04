# Deepchart Indicators

A starting project for writing your own indicators for Deepchart in C#, with seven small examples and the
documentation which explains them.

The indicators here are **hybrid**: the same dll is calculated by the desktop chart and, when it uses only the
portable API, by the Deepchart server.

## Quick start

1. Install Deepchart and the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Set your developer id in `src/DeepchartIndicators.csproj` (`DeepchartDevId`). Deepchart loads a local dll only
   when it carries the id of the logged user. *Where to read your id is not documented yet: see
   `docs/hybrid/developer-id.mdx`.*
3. Build:

   ```bash
   dotnet build src/DeepchartIndicators.csproj
   ```

   The dll goes to `Documents\Deepchart\Indicators`. If Deepchart is not in
   `C:\Program Files\Volumetrica Trading\Deepchart\`, add `-p:DeepchartDir="your folder"`.
4. Open a chart: the examples are in the indicator list under **Personal**, named `Example - ...`.

## Examples

| Indicator | Shows | Calculated on |
|---|---|---|
| `Basics/SimpleMovingAverage.cs` | registration, one series, parameters, the calls of the chart | desktop or server |
| `Basics/PriceBands.cs` | several series, an optional series, point colors, nested parameters | desktop or server |
| `Annotations/SessionRangeBox.cs` | annotations on the bars, groups, realtime update, removal | desktop or server |
| `Annotations/InfoLabel.cs` | relative coordinates, foreground, description and status message | desktop or server |
| `Data/BarPoc.cs` | volume by price | desktop or server |
| `Data/LiveDelta.cs` | realtime trades, bar totals, Y axis | desktop or server |
| `DesktopOnly/BookImbalance.cs` | order book: a desktop only feature | desktop |

## Documentation

The `docs` folder is a [Mintlify](https://mintlify.com) site. To preview it:

```bash
cd docs
npx mint dev
```

## Layout

```
DeepchartIndicators.sln
src/    the project and the example indicators
docs/   the documentation (docs.json and .mdx pages)
```
