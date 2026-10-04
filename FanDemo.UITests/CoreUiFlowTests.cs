using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

namespace FanDemo.UITests;

[TestClass]
[DoNotParallelize]
public class CoreUiFlowTests
{
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan FeedTimeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    public void MainWindow_CompletesFanAndFavoritesFlow()
    {
        using var automation = new UIA3Automation();
        using var application = Application.Launch(ResolveApplicationPath());

        try
        {
            var window = application.GetMainWindow(automation, UiTimeout)
                ?? throw new AssertFailedException("FanDemo main window did not appear.");
            Assert.AreEqual("FanDemo", window.Title);
            window.Focus();
            Mouse.MoveTo(10, 10);
            var refreshButton = FindByName(window, "Refresh Bing wallpapers")
                ?? throw new AssertFailedException("The Bing wallpaper refresh action was not available.");
            Assert.IsTrue(WaitUntil(() => GetWallpaperStatus(window) is not null, FeedTimeout),
                $"The initial Bing wallpaper request did not reach a result or fallback state. Status='{GetWallpaperStatusText(window)}'; visible text={GetVisibleText(window)}");
            refreshButton.Click();
            Assert.IsTrue(WaitUntil(() => GetWallpaperStatus(window) is not null, FeedTimeout),
                "Refreshing the Bing wallpaper feed did not complete.");
            Thread.Sleep(500);

            var initialItems = FindFanItems(window);
            Assert.IsTrue(initialItems.Length is > 0 and <= 9,
                $"Expected Bing results or the nine bundled fallback photos, found {initialItems.Length} items.");
            var itemCount = initialItems.Length;
            var loadOlderButton = FindByName(window, "Load older (8)")
                ?? throw new AssertFailedException("The load-older wallpaper action was not available.");
            loadOlderButton.Click();
            Assert.IsTrue(WaitUntil(() => GetWallpaperStatusText(window).StartsWith("Loaded ", StringComparison.Ordinal), FeedTimeout),
                $"The next Bing wallpaper page did not load. Status: {GetWallpaperStatusText(window)}");
            var allWallpaperItems = FindFanItems(window);
            Assert.IsTrue(allWallpaperItems.Length > itemCount,
                "Loading older Bing wallpapers did not append additional gallery items.");
            var displayedCount = allWallpaperItems.Length;
            var stackedEnvelope = GetEnvelope(GetBounds(allWallpaperItems));

            var firstItem = GetBounds(allWallpaperItems)[0];
            Mouse.MoveTo(new Point(firstItem.Left + firstItem.Width / 2, firstItem.Top + firstItem.Height / 2));
            Assert.IsTrue(
                WaitUntil(() => LayoutSpreadDiffers(window, stackedEnvelope), UiTimeout),
                "Fan items did not spread after hovering the fan panel.");

            Mouse.MoveTo(10, 10);
            var returnedToStack = WaitUntil(() => LayoutSpreadMatches(window, stackedEnvelope, tolerance: 30), UiTimeout);
            var envelopeAfterLeave = GetEnvelope(GetBounds(FindFanItems(window)));
            Assert.IsTrue(
                returnedToStack,
                $"Fan items did not return to their stacked layout after pointer leave. " +
                $"Expected spread: {FormatEnvelope(stackedEnvelope)}; actual: {FormatEnvelope(envelopeAfterLeave)}; " +
                $"cursor: {Mouse.Position}.");

            var clickableItemBounds = FindFanItems(window)[0].BoundingRectangle;
            Mouse.Click(
                new Point(
                    clickableItemBounds.Left + clickableItemBounds.Width / 2,
                    clickableItemBounds.Top + clickableItemBounds.Height / 2),
                MouseButton.Left);
            Assert.IsTrue(
                WaitUntil(() => FindByName(window, "Close X") is not null, UiTimeout),
                "The favorites view did not open.");

            var selectedBounds = FindFanItems(window)[0].BoundingRectangle;
            Mouse.Click(new Point(selectedBounds.Left + selectedBounds.Width / 2, selectedBounds.Top + selectedBounds.Height / 2), MouseButton.Left);
            Assert.IsTrue(WaitUntil(() => FindByName(window, "Open Bing details") is not null, UiTimeout),
                "Selecting a wallpaper did not reveal its attribution and Bing details action.");

            var closeControl = FindByName(window, "Close X")
                ?? throw new AssertFailedException("The favorites close control was not available.");
            closeControl.Click();

            Assert.IsTrue(
                WaitUntil(
                    () => FindByName(window, "Close X") is null && FindByName(window, displayedCount.ToString()) is not null,
                    UiTimeout),
                "The favorites view did not return to its collapsed state.");
        }
        finally
        {
            if (!application.HasExited)
            {
                application.Close(killIfCloseFails: true);
            }
        }
    }

    private static string ResolveApplicationPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("FANDEMO_EXE_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }

            throw new FileNotFoundException($"FANDEMO_EXE_PATH does not exist: {fullPath}", fullPath);
        }

        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "FanDemo.csproj")))
        {
            repository = repository.Parent;
        }

        if (repository is null)
        {
            throw new FileNotFoundException(
                "Could not locate FanDemo.csproj from the UI-test output directory. Set FANDEMO_EXE_PATH to FanDemo.exe.");
        }

        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var applicationPath = Path.Combine(repository.FullName, "bin", configuration, "net9.0-windows", "FanDemo.exe");
        if (!File.Exists(applicationPath))
        {
            throw new FileNotFoundException(
                $"Could not find FanDemo.exe at {applicationPath}. Build the solution or set FANDEMO_EXE_PATH.",
                applicationPath);
        }

        return applicationPath;
    }

    private static AutomationElement[] FindFanItems(Window window) =>
        window.FindAllDescendants(condition => condition.ByControlType(ControlType.DataItem));

    private static AutomationElement? FindByName(Window window, string name) =>
        window.FindFirstDescendant(condition => condition.ByName(name));

    private static string? GetWallpaperStatus(Window window) =>
        GetWallpaperStatusText(window) is var status &&
        (status.StartsWith("Showing ", StringComparison.Ordinal) || status.StartsWith("Could not load Bing wallpapers", StringComparison.Ordinal))
            ? status
            : null;

    private static string GetWallpaperStatusText(Window window) =>
        window.FindAllDescendants(condition => condition.ByControlType(ControlType.Text))
            .Select(element => element.Name)
            .FirstOrDefault(name => name.StartsWith("Loading Bing wallpapers", StringComparison.Ordinal) ||
                name.StartsWith("Loading older Bing wallpapers", StringComparison.Ordinal) ||
                name.StartsWith("Showing ", StringComparison.Ordinal) ||
                name.StartsWith("Loaded ", StringComparison.Ordinal) ||
                name.StartsWith("Could not load Bing wallpapers", StringComparison.Ordinal) ||
                name.StartsWith("Could not load older Bing wallpapers", StringComparison.Ordinal)) ?? string.Empty;

    private static string GetVisibleText(Window window) => string.Join(" | ",
        window.FindAllDescendants(condition => condition.ByControlType(ControlType.Text))
            .Select(element => element.Name));

    private static Rectangle[] GetBounds(AutomationElement[] elements) =>
        elements.Select(element => element.BoundingRectangle)
            .OrderBy(bounds => bounds.Left)
            .ThenBy(bounds => bounds.Top)
            .ToArray();

    private static bool LayoutSpreadDiffers(Window window, Rectangle original)
    {
        var elements = FindFanItems(window);
        if (elements.Length == 0)
        {
            return false;
        }

        var current = GetEnvelope(GetBounds(elements));
        return current.Width > original.Width + 24 || current.Height > original.Height + 24;
    }

    private static bool LayoutSpreadMatches(Window window, Rectangle expected, int tolerance)
    {
        var elements = FindFanItems(window);
        if (elements.Length == 0)
        {
            return false;
        }

        var current = GetEnvelope(GetBounds(elements));
        return Math.Abs(current.Width - expected.Width) <= tolerance &&
            Math.Abs(current.Height - expected.Height) <= tolerance;
    }

    private static Rectangle GetEnvelope(Rectangle[] bounds)
    {
        if (bounds.Length == 0)
        {
            return Rectangle.Empty;
        }

        var left = bounds.Min(rectangle => rectangle.Left);
        var top = bounds.Min(rectangle => rectangle.Top);
        var right = bounds.Max(rectangle => rectangle.Right);
        var bottom = bounds.Max(rectangle => rectangle.Bottom);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static string FormatEnvelope(Rectangle rectangle) =>
        $"{rectangle.Width}x{rectangle.Height}";

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return condition();
    }
}
