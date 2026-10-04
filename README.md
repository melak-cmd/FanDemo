# FanDemo

FanDemo is a Windows desktop application built with Windows Presentation Foundation (WPF). It demonstrates an animated fan-style panel for browsing image-backed items, with an expandable favorites view.

## Features

- Fan-style item layout with pointer-triggered expansion and animated transitions.
- Expandable favorites view with a detail-level slider.
- Sample image resources and XAML-based sample data.
- Current and recent Bing daily wallpapers, including copyright attribution and links to Bing.

## Bing wallpaper feed

FanDemo loads today's Bing wallpaper and up to seven previous days from the public JSON API at [`bing.biturl.top`](https://bing.biturl.top/). The default market is `en-US` and image resolution is `1920`; no API key or account is required. Use **Load older (8)** to append the next eight historical days, or **Refresh Bing wallpapers** to return to the latest page. If the service cannot return usable wallpapers, the bundled sample photos remain available.

## Requirements

- Windows
- Visual Studio with the **.NET desktop development** workload
- .NET 9 SDK with Windows desktop/WPF targeting support

> The application targets **.NET 9 for Windows (WPF)**.

## Build and run

1. Open `FanDemo.sln` in Visual Studio on Windows.
2. Restore any required components and build the solution in Debug or Release configuration.
3. Set `FanDemo` as the startup project and run it.

Alternatively, build from a terminal with the .NET 9 SDK:

```powershell
dotnet build FanDemo.sln --configuration Release
```

## Tests

Run the WPF unit tests on Windows with the .NET 9 SDK:

```powershell
dotnet test FanDemo.Tests/FanDemo.Tests.csproj
```

The unit-test project targets `net9.0-windows` and runs WPF layout tests on STA threads.

Run the FlaUI end-to-end UI test from an **interactive Windows desktop session**:

```powershell
dotnet test FanDemo.UITests/FanDemo.UITests.csproj
```

The UI test launches `FanDemo.exe` from the current configuration's build output. To use a different executable path, set `FANDEMO_EXE_PATH` before running the test:

```powershell
$env:FANDEMO_EXE_PATH = "C:\path\to\FanDemo.exe"
dotnet test FanDemo.UITests/FanDemo.UITests.csproj
```

`dotnet test FanDemo.sln` runs both test projects and therefore also requires an interactive Windows desktop session.

## Project layout

- `FanDemo.csproj` — WPF project configuration.
- `App.xaml` and `Window1.xaml` — application resources and main window.
- `FanPanel.cs` — animated fan-style WPF panel.
- `Favourites.xaml` — expandable favorites view.
- `Images/` — sample image assets.
- `Resources/TestData.xaml` — sample item data.

## License

No license is currently specified. Unless a license is added, reuse and redistribution are not granted by this repository.
