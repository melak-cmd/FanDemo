#nullable enable
using System;
using System.Collections.Generic;

namespace FanDemo;

/// <summary>A Bing daily wallpaper and the attribution metadata supplied by Bing.</summary>
public sealed record BingWallpaper(
    int Index,
    DateOnly? StartDate,
    string ImageUrl,
    string Copyright,
    string? CopyrightLink)
{
    public string DateLabel => Index < 0
        ? Copyright
        : StartDate?.ToString("D") ?? $"Bing wallpaper {Index + 1}";
}

public sealed record BingWallpaperFeedResult(
    IReadOnlyList<BingWallpaper> Wallpapers,
    int FailedCount);
