using FanDemo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FanDemo.Tests;

[TestClass]
public class BingWallpaperServiceTests
{
    [TestMethod]
    public async Task LoadRecentAsync_UsesDocumentedParametersAndMapsWallpaperMetadataWithoutApiKey()
    {
        var requests = new ConcurrentBag<Uri>();
        var handler = new StubHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(JsonResponse("""{"start_date":"20261004","end_date":"20261005","url":"https://www.bing.com/th?id=OHR.Test_en-US.jpg","copyright":"Landscape © Photographer","copyright_link":"https://www.bing.com/search?q=landscape"}"""));
        });
        var service = new BingWallpaperService(new HttpClient(handler));

        var result = await service.LoadRecentAsync();

        Assert.AreEqual(8, result.Wallpapers.Count);
        Assert.AreEqual(0, result.FailedCount);
        Assert.AreEqual(new DateOnly(2026, 10, 4), result.Wallpapers[0].StartDate);
        Assert.AreEqual("Landscape © Photographer", result.Wallpapers[0].Copyright);
        Assert.AreEqual("https://www.bing.com/search?q=landscape", result.Wallpapers[0].CopyrightLink);
        Assert.AreEqual(8, requests.Count);
        foreach (var request in requests)
        {
            Assert.AreEqual("https", request.Scheme);
            StringAssert.Contains(request.Query, "format=json");
            StringAssert.Contains(request.Query, "image_format=jpg");
            StringAssert.Contains(request.Query, "mkt=en-US");
            StringAssert.Contains(request.Query, "resolution=1920");
        }
        CollectionAssert.AreEqual(Enumerable.Range(0, 8).Select(index => index.ToString()).ToArray(),
            requests.Select(request => GetQueryValue(request, "index")).OrderBy(value => int.Parse(value)).ToArray());
    }

    [TestMethod]
    public async Task LoadRecentAsync_LoadsNextOlderPageByRequestedIndices()
    {
        var requestedIndices = new ConcurrentBag<int>();
        var handler = new StubHandler((request, _) =>
        {
            var index = int.Parse(GetQueryValue(request.RequestUri!, "index"));
            requestedIndices.Add(index);
            return Task.FromResult(JsonResponse($"{{\"start_date\":\"202609{26 - index:D2}\",\"url\":\"https://www.bing.com/{index}.jpg\",\"copyright\":\"Index {index}\"}}"));
        });
        var service = new BingWallpaperService(new HttpClient(handler));

        var result = await service.LoadRecentAsync(startIndex: 8, count: 8);

        Assert.AreEqual(8, result.Wallpapers.Count);
        CollectionAssert.AreEqual(Enumerable.Range(8, 8).ToArray(), requestedIndices.OrderBy(index => index).ToArray());
        CollectionAssert.AreEqual(Enumerable.Range(8, 8).ToArray(), result.Wallpapers.Select(wallpaper => wallpaper.Index).ToArray());
    }

    [TestMethod]
    public async Task LoadRecentAsync_RejectsInvalidPageRanges()
    {
        var service = new BingWallpaperService(new HttpClient(new StubHandler((_, _) => throw new AssertFailedException("HTTP should not be called"))));

        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => service.LoadRecentAsync(startIndex: -1));
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => service.LoadRecentAsync(count: 0));
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => service.LoadRecentAsync(count: 9));
    }

    [TestMethod]
    public async Task LoadRecentAsync_RejectsNonHttpsImageAndIgnoresUnsafeCopyrightLink()
    {
        var handler = new StubHandler((request, _) =>
        {
            var index = GetQueryValue(request.RequestUri!, "index");
            var image = index == "0" ? "http://unsafe.example/image.jpg" : "https://www.bing.com/image.jpg";
            return Task.FromResult(JsonResponse($"{{\"start_date\":\"20261004\",\"url\":\"{image}\",\"copyright\":\"Test\",\"copyright_link\":\"javascript:alert(1)\"}}"));
        });
        var service = new BingWallpaperService(new HttpClient(handler));

        var result = await service.LoadRecentAsync();

        Assert.AreEqual(7, result.Wallpapers.Count);
        Assert.AreEqual(1, result.FailedCount);
        Assert.IsTrue(result.Wallpapers.All(wallpaper => wallpaper.CopyrightLink is null));
    }

    [TestMethod]
    public async Task LoadRecentAsync_CountsMalformedAndFailedDaysAndReturnsPartialResults()
    {
        var handler = new StubHandler((request, _) =>
        {
            var index = int.Parse(GetQueryValue(request.RequestUri!, "index"));
            if (index == 0)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            if (index == 1)
                return Task.FromResult(JsonResponse("not json"));
            return Task.FromResult(JsonResponse($"{{\"start_date\":\"2026100{4 - index}\",\"url\":\"https://www.bing.com/{index}.jpg\",\"copyright\":\"Index {index}\"}}"));
        });
        var service = new BingWallpaperService(new HttpClient(handler));

        var result = await service.LoadRecentAsync();

        Assert.AreEqual(6, result.Wallpapers.Count);
        Assert.AreEqual(2, result.FailedCount);
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5, 6, 7 }, result.Wallpapers.Select(wallpaper => wallpaper.Index).ToArray());
    }

    [TestMethod]
    public async Task LoadRecentAsync_LimitsRequestConcurrency()
    {
        var active = 0;
        var maximumActive = 0;
        var handler = new StubHandler(async (_, _) =>
        {
            var current = Interlocked.Increment(ref active);
            int observed;
            do
            {
                observed = maximumActive;
                if (current <= observed)
                    break;
            } while (Interlocked.CompareExchange(ref maximumActive, current, observed) != observed);

            await Task.Delay(20);
            Interlocked.Decrement(ref active);
            return JsonResponse("""{"url":"https://www.bing.com/image.jpg","copyright":"Test"}""");
        });
        var service = new BingWallpaperService(new HttpClient(handler));

        var result = await service.LoadRecentAsync();

        Assert.AreEqual(8, result.Wallpapers.Count);
        Assert.IsTrue(maximumActive > 1);
        Assert.IsTrue(maximumActive <= 3, $"Observed {maximumActive} concurrent requests.");
    }

    [TestMethod]
    public async Task LoadRecentAsync_ReportsAllDaysFailedWhenNoWallpaperCanBeLoaded()
    {
        var service = new BingWallpaperService(new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));

        var result = await service.LoadRecentAsync();

        Assert.AreEqual(0, result.Wallpapers.Count);
        Assert.AreEqual(8, result.FailedCount);
    }

    [TestMethod]
    public async Task LoadRecentAsync_HonorsCancellation()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return JsonResponse("{}");
        });
        var service = new BingWallpaperService(new HttpClient(handler));
        using var cancellation = new CancellationTokenSource();
        var request = service.LoadRecentAsync(cancellationToken: cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => request);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string GetQueryValue(Uri uri, string name) => uri.Query.TrimStart('?').Split('&')
        .Select(part => part.Split('=', 2))
        .Where(pair => Uri.UnescapeDataString(pair[0]) == name)
        .Select(pair => Uri.UnescapeDataString(pair[1]))
        .Single();

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
