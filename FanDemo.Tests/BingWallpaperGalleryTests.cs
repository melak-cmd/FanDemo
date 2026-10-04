using FanDemo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace FanDemo.Tests;

[TestClass]
public class BingWallpaperGalleryTests
{
    [STATestMethod]
    public void StartupAndRefresh_LoadRecentWallpapersAndKeepTheUiResponsive()
    {
        EnsureApplication();
        var wallpapers = new[] { CreateWallpaper(0), CreateWallpaper(1) };
        var service = new FixedWallpaperService((_, _, _) => Task.FromResult(new BingWallpaperFeedResult(wallpapers, 0)));
        var gallery = new Favourites(service);
        var items = (ItemsControl)gallery.FindName("someFavourites")!;

        Assert.AreEqual(9, items.Items.Count, "Bundled photos should be available before the feed returns.");
        gallery.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

        Assert.AreEqual(1, service.Calls);
        Assert.AreEqual(2, items.Items.Count);
        Assert.AreEqual("Showing 2 Bing wallpaper(s).", ((TextBlock)gallery.FindName("wallpaperStatus")!).Text);

        ((Button)gallery.FindName("refreshButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(2, service.Calls);
    }

    [STATestMethod]
    public void PartialAndFailedFeeds_ShowStatusAndRetainBundledFallbackWhenEmpty()
    {
        EnsureApplication();
        var responses = new Queue<BingWallpaperFeedResult>(new[]
        {
            new BingWallpaperFeedResult(new[] { CreateWallpaper(0) }, 2),
            new BingWallpaperFeedResult(Array.Empty<BingWallpaper>(), 8),
            new BingWallpaperFeedResult(new[] { CreateWallpaper(0), CreateWallpaper(1) }, 0)
        });
        var gallery = new Favourites(new FixedWallpaperService((_, _, _) => Task.FromResult(responses.Dequeue())));
        var refresh = (Button)gallery.FindName("refreshButton")!;
        var items = (ItemsControl)gallery.FindName("someFavourites")!;

        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        StringAssert.Contains(((TextBlock)gallery.FindName("wallpaperStatus")!).Text, "some days could not be loaded");
        Assert.AreEqual(1, items.Items.Count);

        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        StringAssert.Contains(((TextBlock)gallery.FindName("wallpaperStatus")!).Text, "Bundled samples are shown; refresh to retry");
        Assert.AreEqual(9, items.Items.Count);

        refresh.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(2, items.Items.Count);
    }

    [STATestMethod]
    public void SelectedWallpaper_ShowsAttributionAndOnlyEnablesSafeBingLinks()
    {
        EnsureApplication();
        var gallery = new Favourites(new FixedWallpaperService((_, _, _) => Task.FromResult(new BingWallpaperFeedResult(Array.Empty<BingWallpaper>(), 8))));
        var image = new Image { DataContext = CreateWallpaper(0) };

        SelectWallpaper(gallery, image);

        StringAssert.Contains(((TextBlock)gallery.FindName("wallpaperAttribution")!).Text, "© Test photographer");
        Assert.IsTrue(((Button)gallery.FindName("openBingPage")!).IsEnabled);

        image.DataContext = new BingWallpaper(1, null, "https://www.bing.com/test.jpg", "Unlinked photo", "javascript:alert(1)");
        SelectWallpaper(gallery, image);
        Assert.IsFalse(((Button)gallery.FindName("openBingPage")!).IsEnabled);
    }

    [STATestMethod]
    public void LoadOlder_AppendsUniqueItemsAndRetriesTheSamePageAfterFailure()
    {
        EnsureApplication();
        var pages = new Queue<BingWallpaperFeedResult>(new[]
        {
            new BingWallpaperFeedResult(Enumerable.Range(0, 8).Select(CreateWallpaper).ToArray(), 0),
            new BingWallpaperFeedResult(Array.Empty<BingWallpaper>(), 8),
            new BingWallpaperFeedResult(new[] { CreateWallpaper(8), CreateWallpaper(9) }, 0)
        });
        var calls = new List<(int Start, int Count)>();
        var service = new FixedWallpaperService((start, count, _) =>
        {
            calls.Add((start, count));
            return Task.FromResult(pages.Dequeue());
        });
        var gallery = new Favourites(service);
        var items = (ItemsControl)gallery.FindName("someFavourites")!;
        ((Button)gallery.FindName("refreshButton")!).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var loadOlder = (Button)gallery.FindName("loadOlderButton")!;

        loadOlder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.AreEqual(8, items.Items.Count);
        StringAssert.Contains(((TextBlock)gallery.FindName("wallpaperStatus")!).Text, "Could not load older");

        loadOlder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.AreEqual(10, items.Items.Count);
        Assert.AreEqual(8, ((BingWallpaper)items.Items[8]).Index);
        Assert.AreEqual(9, ((BingWallpaper)items.Items[9]).Index);
        CollectionAssert.AreEqual(new[] { (0, 8), (8, 8), (8, 8) }, calls.Select(call => (call.Start, call.Count)).ToArray());
    }

    private static void SelectWallpaper(Favourites gallery, Image image) =>
        typeof(Favourites).GetMethod("OnItemClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(gallery, new object[] { image, new RoutedEventArgs(UIElement.MouseLeftButtonUpEvent) });

    private static BingWallpaper CreateWallpaper(int index) => new(
        index,
        new DateOnly(2026, 10, 4).AddDays(-index),
        $"https://www.bing.com/{index}.jpg",
        "© Test photographer",
        "https://www.bing.com/search?q=wallpaper");

    private static void EnsureApplication()
    {
        if (Application.Current is null)
        {
            var app = new App();
            app.InitializeComponent();
        }
    }

    private sealed class FixedWallpaperService(Func<int, int, CancellationToken, Task<BingWallpaperFeedResult>> load) : IBingWallpaperService
    {
        public int Calls { get; private set; }

        public Task<BingWallpaperFeedResult> LoadRecentAsync(int startIndex = 0, int count = BingWallpaperService.PageSize, CancellationToken cancellationToken = default)
        {
            Calls++;
            return load(startIndex, count, cancellationToken);
        }
    }
}
