using FanDemo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace FanDemo.Tests;

[TestClass]
public class BingWallpaperTests
{
    [TestMethod]
    public void BingWallpaper_PreservesDateImageAndAttributionMetadata()
    {
        var wallpaper = new BingWallpaper(
            0,
            new DateOnly(2026, 10, 4),
            "https://www.bing.com/th?id=OHR.Test_en-US.jpg",
            "Photo © Photographer",
            "https://www.bing.com/search?q=wallpaper");

        var json = JsonSerializer.Serialize(wallpaper);
        var restored = JsonSerializer.Deserialize<BingWallpaper>(json);

        Assert.AreEqual(wallpaper, restored);
        Assert.AreEqual(new DateOnly(2026, 10, 4), restored!.StartDate);
    }

    [TestMethod]
    public void BingWallpaper_UsesIndexLabelWhenDateIsUnavailable()
    {
        var wallpaper = new BingWallpaper(2, null, "https://www.bing.com/image.jpg", "Copyright", null);

        Assert.AreEqual("Bing wallpaper 3", wallpaper.DateLabel);
    }
}
