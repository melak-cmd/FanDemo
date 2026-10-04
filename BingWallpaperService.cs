#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FanDemo;

public interface IBingWallpaperService
{
    Task<BingWallpaperFeedResult> LoadRecentAsync(
        int startIndex = 0,
        int count = BingWallpaperService.PageSize,
        CancellationToken cancellationToken = default);
}

public sealed class BingWallpaperService : IBingWallpaperService
{
    private const string Endpoint = "https://bing.biturl.top/";
    public const int PageSize = 8;
    private const int MaximumConcurrency = 3;
    private readonly HttpClient _httpClient;

    public BingWallpaperService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<BingWallpaperFeedResult> LoadRecentAsync(
        int startIndex = 0,
        int count = PageSize,
        CancellationToken cancellationToken = default)
    {
        if (startIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startIndex), "Wallpaper index cannot be negative.");
        if (count is < 1 or > PageSize)
            throw new ArgumentOutOfRangeException(nameof(count), $"A wallpaper page must contain between 1 and {PageSize} items.");

        using var concurrency = new SemaphoreSlim(MaximumConcurrency);
        var requests = Enumerable.Range(startIndex, count)
            .Select(index => LoadDaySafelyAsync(index, concurrency, cancellationToken));
        var responses = await Task.WhenAll(requests).ConfigureAwait(false);
        var wallpapers = responses
            .Where(response => response.Wallpaper is not null)
            .Select(response => response.Wallpaper!)
            .OrderBy(wallpaper => wallpaper.Index)
            .ToArray();
        return new BingWallpaperFeedResult(wallpapers, responses.Count(response => response.Wallpaper is null));
    }

    private async Task<(BingWallpaper? Wallpaper, Exception? Error)> LoadDaySafelyAsync(
        int index,
        SemaphoreSlim concurrency,
        CancellationToken cancellationToken)
    {
        try
        {
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return (await LoadDayAsync(index, cancellationToken).ConfigureAwait(false), null);
            }
            finally
            {
                concurrency.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private async Task<BingWallpaper?> LoadDayAsync(int index, CancellationToken cancellationToken)
    {
        var url = $"{Endpoint}?format=json&image_format=jpg&index={index}&mkt=en-US&resolution=1920";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var imageUrl = GetString(root, "url");
        if (!TryGetHttpsUri(imageUrl, out var imageUri))
            return null;

        var copyrightLink = GetString(root, "copyright_link");
        var safeCopyrightLink = TryGetHttpsUri(copyrightLink, out var linkUri) ? linkUri.AbsoluteUri : null;
        var startDateText = GetString(root, "start_date");
        DateOnly? startDate = DateOnly.TryParseExact(
            startDateText,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate) ? parsedDate : null;

        return new BingWallpaper(
            index,
            startDate,
            imageUri.AbsoluteUri,
            GetString(root, "copyright") ?? "Bing daily wallpaper",
            safeCopyrightLink);
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryGetHttpsUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate) && candidate.Scheme == Uri.UriSchemeHttps)
        {
            uri = candidate;
            return true;
        }

        uri = null!;
        return false;
    }
}
