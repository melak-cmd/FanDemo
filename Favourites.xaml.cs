#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace FanDemo
{
    /// <summary>
    /// Interaction logic for Favourites.xaml
    /// </summary>

    public partial class Favourites : Canvas
    {
        private readonly ObservableCollection<BingWallpaper> galleryPhotos = new();
        private readonly List<BingWallpaper> samplePhotos = new();
        private readonly IBingWallpaperService wallpaperService;
        private CancellationTokenSource? activeLoad;
        private BingWallpaper? selectedWallpaper;
        private bool initialLoadStarted;
        private int nextWallpaperIndex = BingWallpaperService.PageSize;

        public Favourites()
            : this(new BingWallpaperService())
        {
        }

        public Favourites(IBingWallpaperService wallpaperService)
        {
            this.wallpaperService = wallpaperService;
            InitializeComponent();
            this.close.MouseLeftButtonUp += new MouseButtonEventHandler(close_MouseLeftButtonUp);
            this.detailSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(detailSlider_ValueChanged);
            Loaded += Favourites_Loaded;
            var provider = (XmlDataProvider)FindResource("Things");
            provider.Refresh();
            if (provider.Document?.DocumentElement?.SelectNodes("Thing") is XmlNodeList nodes)
            {
                foreach (XmlNode node in nodes)
                {
                    var image = node.Attributes?["Image"]?.Value;
                    if (!string.IsNullOrWhiteSpace(image))
                        samplePhotos.Add(new BingWallpaper(-1, null, image, System.IO.Path.GetFileNameWithoutExtension(image), null));
                }
            }
            if (samplePhotos.Count == 0)
            {
                foreach (var image in new[] { "Aquarium.jpg", "Ascent.jpg", "Autumn.jpg", "Crystal.jpg", "DaVinci.jpg", "Follow.jpg", "Friend.jpg", "Home.jpg", "Moon flower.jpg" })
                    samplePhotos.Add(new BingWallpaper(-1, null, image, System.IO.Path.GetFileNameWithoutExtension(image), null));
            }
            SetGalleryItems(samplePhotos);
        }

        FanPanel? fan;

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            fan = (FanPanel)sender;
            fan.Refresh += new RoutedEventHandler(fan_Refresh);
            UpdateCount();
        }

        void OnClick(object sender, MouseButtonEventArgs e)
        {
            if (fan is null)
                return;
            if (!fan.IsWrapPanel)
            {
                this.grid.Width = 660;
                this.grid.Height = 550;
                this.text.Visibility = Visibility.Collapsed;
                fan.IsWrapPanel = true;
                this.BeginStoryboard((Storyboard)grid.FindResource("expandPanel"));
            }
        }

        void close_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Hide();
        }

        public void Hide()
        {
            if (fan is null)
                return;
            fan.IsWrapPanel = false;
            detailSlider.Value = detailSlider.Maximum;
            this.BeginStoryboard((Storyboard)grid.FindResource("collapsePanel"));
        }

        void OnCompleted(object sender, RoutedEventArgs e)
        {
            if (fan is null)
                return;
            if (!fan.IsWrapPanel && this.grid.Width != 230)
            {
                this.grid.Width = 230;
                this.grid.Height = 210;
                this.text.Visibility = Visibility.Visible;
                modal.Visibility = Visibility.Collapsed;
            }
        }

        void detailSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int n = (int)(detailSlider.Value * 100 / detailSlider.Maximum);
            textDetail.Text = String.Format("DETAIL LEVEL: {0}%", n);
        }

        void fan_Refresh(object sender, RoutedEventArgs e)
        {
            UpdateCount();
        }

        void UpdateCount()
        {
            if (fan is not null)
                counter.Text = fan.Children.Count.ToString();
        }

        void OnItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BingWallpaper wallpaper)
            {
                selectedWallpaper = wallpaper;
                wallpaperAttribution.Text = string.IsNullOrWhiteSpace(wallpaper.Copyright)
                    ? wallpaper.DateLabel
                    : $"{wallpaper.DateLabel} — {wallpaper.Copyright}";
                openBingPage.IsEnabled = wallpaper.Index >= 0 && TryGetHttpsUri(wallpaper.CopyrightLink, out _);
                photoDetails.Visibility = Visibility.Visible;
            }
            e.Handled = true;
        }

        private void Favourites_Loaded(object sender, RoutedEventArgs e)
        {
            if (initialLoadStarted)
                return;
            initialLoadStarted = true;
            _ = LoadWallpapersAsync();
        }

        private async void OnRefreshWallpapers(object sender, RoutedEventArgs e) => await LoadWallpapersAsync();

        private async void OnLoadOlderWallpapers(object sender, RoutedEventArgs e) => await LoadWallpapersAsync(loadOlder: true);

        private async Task LoadWallpapersAsync(bool loadOlder = false)
        {
            activeLoad?.Cancel();
            activeLoad?.Dispose();
            var load = new CancellationTokenSource();
            activeLoad = load;
            var startIndex = loadOlder ? nextWallpaperIndex : 0;
            if (!loadOlder)
                nextWallpaperIndex = BingWallpaperService.PageSize;
            if (loadOlder)
                loadOlderButton.IsEnabled = false;
            photoDetails.Visibility = Visibility.Collapsed;
            selectedWallpaper = null;
            wallpaperStatus.Text = loadOlder ? "Loading older Bing wallpapers…" : "Loading Bing wallpapers…";

            try
            {
                var result = await wallpaperService.LoadRecentAsync(startIndex, BingWallpaperService.PageSize, load.Token);
                if (load != activeLoad || load.IsCancellationRequested)
                    return;

                if (result.Wallpapers.Count == 0)
                {
                    wallpaperStatus.Text = loadOlder
                        ? "Could not load older Bing wallpapers. The current gallery is unchanged; load older to retry."
                        : "Could not load Bing wallpapers. Bundled samples are shown; refresh to retry.";
                    if (!loadOlder)
                        SetGalleryItems(samplePhotos);
                    return;
                }

                if (loadOlder)
                {
                    var added = AppendGalleryItems(result.Wallpapers);
                    nextWallpaperIndex = startIndex + BingWallpaperService.PageSize;
                    wallpaperStatus.Text = result.FailedCount == 0
                        ? $"Loaded {added} older Bing wallpaper(s); {galleryPhotos.Count} total."
                        : $"Loaded {added} older Bing wallpaper(s); some days could not be loaded. {galleryPhotos.Count} total.";
                }
                else
                {
                    SetGalleryItems(result.Wallpapers);
                    nextWallpaperIndex = BingWallpaperService.PageSize;
                    wallpaperStatus.Text = result.FailedCount == 0
                        ? $"Showing {result.Wallpapers.Count} Bing wallpaper(s)."
                        : $"Showing {result.Wallpapers.Count} Bing wallpaper(s); some days could not be loaded.";
                }
            }
            catch (OperationCanceledException) when (load.IsCancellationRequested)
            {
                // A later refresh superseded this request.
            }
            catch (Exception exception)
            {
                if (load != activeLoad)
                    return;
                if (loadOlder)
                    wallpaperStatus.Text = $"Could not load older Bing wallpapers: {exception.Message} The current gallery is unchanged; load older to retry.";
                else
                {
                    SetGalleryItems(samplePhotos);
                    wallpaperStatus.Text = $"Could not load Bing wallpapers: {exception.Message} Bundled samples are shown; refresh to retry.";
                }
            }
            finally
            {
                if (load == activeLoad)
                    loadOlderButton.IsEnabled = true;
            }
        }

        private void OnOpenBingPage(object sender, RoutedEventArgs e)
        {
            if (selectedWallpaper is null || !TryGetHttpsUri(selectedWallpaper.CopyrightLink, out var uri))
                return;

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }

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

        private void SetGalleryItems(IEnumerable<BingWallpaper> wallpapers)
        {
            galleryPhotos.Clear();
            foreach (var wallpaper in wallpapers)
                galleryPhotos.Add(wallpaper);
            someFavourites.ItemsSource = galleryPhotos;
            UpdateCount();
        }

        private int AppendGalleryItems(IEnumerable<BingWallpaper> wallpapers)
        {
            var existingIndices = new HashSet<int>(galleryPhotos.Where(photo => photo.Index >= 0).Select(photo => photo.Index));
            var added = 0;
            foreach (var wallpaper in wallpapers)
            {
                if (wallpaper.Index >= 0 && existingIndices.Add(wallpaper.Index))
                {
                    galleryPhotos.Add(wallpaper);
                    added++;
                }
            }
            UpdateCount();
            return added;
        }
    }
}
