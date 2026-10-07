using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
#if !WINDOWS
// Aliased: the unqualified name "Map" clashes with Microsoft.Maui.ApplicationModel.Map.
using MauiMap = Microsoft.Maui.Controls.Maps.Map;
#endif

namespace RealSpeed2
{
    public partial class MainPage : ContentPage
    {
        private const string MaxSpeedPrefKey = "MaxSpeedKmh";

        private Location? _previousLocation;
        private readonly LocationViewModel _viewModel;
        private double _maxSpeedKmh;
#if WINDOWS
        private CancellationTokenSource? _pollingCts;
#else
        private MauiMap? _map;
#endif
#if IOS || MACCATALYST
        private readonly BeaconsMapLayer _beaconsLayer = new();
#endif

        public MainPage()
        {
            InitializeComponent();

            _viewModel = new LocationViewModel();
            BindingContext = _viewModel;

            // Restore the persisted max speed so it survives app restarts.
            _maxSpeedKmh = Preferences.Default.Get(MaxSpeedPrefKey, 0.0);
            _viewModel.MaxSpeed = FormatSpeed(_maxSpeedKmh);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            ReviewPrompt.StartSession();
            DeviceDisplay.KeepScreenOn = true;

            if (Window != null)
                Window.Resumed += OnWindowResumed;

            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    lblGPSLog.Text = "GPS permission denied.";
                    return;
                }

#if WINDOWS
                _pollingCts = new CancellationTokenSource();
                _ = PollLocationAsync(_pollingCts.Token);
#else
                Geolocation.Default.LocationChanged += OnLocationChanged;
                Geolocation.Default.ListeningFailed += OnListeningFailed;

                var request = new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(1));
                var started = await Geolocation.Default.StartListeningForegroundAsync(request);
                if (!started)
                    lblGPSLog.Text = "Could not start GPS listening.";
#endif
            }
            catch (Exception ex)
            {
                lblGPSLog.Text = $"An error occurred: {ex.Message}";
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            DeviceDisplay.KeepScreenOn = false;

            if (Window != null)
                Window.Resumed -= OnWindowResumed;

#if WINDOWS
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _pollingCts = null;
#else
            Geolocation.Default.LocationChanged -= OnLocationChanged;
            Geolocation.Default.ListeningFailed -= OnListeningFailed;
            Geolocation.Default.StopListeningForeground();
#endif

            _previousLocation = null;
            _viewModel.RealSpeed = "";
            _viewModel.CalculatedSpeed = "";
        }

#if WINDOWS
        private async Task PollLocationAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));
                    var location = await Geolocation.Default.GetLocationAsync(request, ct);
                    if (location != null)
                        ProcessLocation(location);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    lblGPSLog.Text = $"GPS poll error: {ex.Message}";
                }

                try { await Task.Delay(1000, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
#endif

        private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
        {
            if (e.Location != null) ProcessLocation(e.Location);
        }

        private void OnListeningFailed(object? sender, GeolocationListeningFailedEventArgs e)
        {
            lblGPSLog.Text = $"GPS listening failed: {e.Error}";
        }

        private void ProcessLocation(Location location)
        {
            ReviewPrompt.OnSpeed(location.Speed);
            _viewModel.UpdateCount++;
            _viewModel.LastUpdate = DateTime.Now;

            if (_previousLocation != null && location.Timestamp != _previousLocation.Timestamp)
            {
                var realSpeedKmh = (location.Speed ?? 0.0) * 3.6;
                _viewModel.RealSpeed = FormatSpeed(realSpeedKmh);
                _viewModel.CalculatedSpeed = FormatSpeed(CalculGPS.CalculateSpeedKmh(
                    _previousLocation.Latitude, _previousLocation.Longitude, _previousLocation.Timestamp.DateTime,
                    location.Latitude, location.Longitude, location.Timestamp.DateTime));

                if (realSpeedKmh > _maxSpeedKmh)
                {
                    _maxSpeedKmh = realSpeedKmh;
                    _viewModel.MaxSpeed = FormatSpeed(_maxSpeedKmh);
                    Preferences.Default.Set(MaxSpeedPrefKey, _maxSpeedKmh);
                }
            }

            _previousLocation = location;
            _viewModel.Location = location;
        }

        // Speeds under 100 km/h are shown with one decimal (e.g. "85.3");
        // at 100 km/h and above the decimal is dropped (e.g. "120") to keep the display compact.
        private static string FormatSpeed(double kmh) => kmh.ToString(kmh >= 100.0 ? "F0" : "F1");

        private void OnGetGPSInfoClicked(object sender, EventArgs e)
        {
            pnlGPSDetails.IsVisible = !pnlGPSDetails.IsVisible;
            ((Button)sender).Text = pnlGPSDetails.IsVisible ? "Hide GPS details" : "Show GPS details";
        }

        private void OnResetMaxSpeedClicked(object sender, EventArgs e)
        {
            _maxSpeedKmh = 0.0;
            _viewModel.MaxSpeed = FormatSpeed(0.0);
            Preferences.Default.Set(MaxSpeedPrefKey, 0.0);
        }

        private void OnShowMapClicked(object sender, EventArgs e) => SetMapVisible(true);

        private void OnHideMapClicked(object sender, EventArgs e) => SetMapVisible(false);

        // Map mode: the bottom two thirds show the map, and the top third keeps only the speed so nothing needs scrolling.
        private void SetMapVisible(bool show)
        {
#if !WINDOWS
            // Created on opening and released on closing (see ReleaseMap), so MapKit only runs while the map is shown.
            if (show && _map == null)
            {
                _map = new MauiMap { IsShowingUser = true };
                _map.HandlerChanged += (_, _) =>
                {
                    FollowUser();
                    ShowBeacons();
                };
                mapHost.Content = _map;
            }

            mapSection.IsVisible = show;
            pnlControls.IsVisible = !show;
            CompactSpeedDisplay(show);
            // Rows 1* and 2* give a third to the speed and two thirds to the map; a 0-height row gives the full
            // screen back to the speed display.
            rootGrid.RowDefinitions[1].Height = show ? new GridLength(2, GridUnitType.Star) : new GridLength(0);

            if (show)
            {
                // The speed must be in view: the user may have scrolled down to reach "Show Map".
                _ = mainScroll.ScrollToAsync(0, 0, false);
                FollowUser();
                ShowBeacons();
            }
            else
            {
                ReleaseMap();
            }
#endif
        }

#if !WINDOWS
        // A closed map must not use data. Merely hidden, MapKit would keep tracking the user and could keep
        // loading tiles as they move, so the map is torn down instead; reopening builds a fresh one.
        private void ReleaseMap()
        {
            if (_map == null)
                return;

            mapHost.Content = null;
            _map.Handler?.DisconnectHandler();
            _map = null;
        }
#endif

        // Open24DisplaySt draws its glyphs well inside the line box: per the font's metrics, 0.335 em of empty
        // ascent above the digits and 0.132 em of descent below them (about 64 pt and 25 pt at the speed's size).
        // Compact mode trims all of it and centres the result, so the speed and its unit fit in a third of the
        // screen (even on an iPhone SE) with the same gap above the digits as below "Km/h".
        private void CompactSpeedDisplay(bool compact)
        {
            lblRealSpeed.Margin = compact ? TrimmedLineBox(lblRealSpeed.FontSize) : Thickness.Zero;
            lblSpeedUnit.Margin = compact ? TrimmedLineBox(lblSpeedUnit.FontSize) : Thickness.Zero;
            pnlMain.Spacing = compact ? 18 : 25; // 25 = Spacing in MainPage.xaml
            // In a ScrollView, content smaller than the viewport is positioned by its VerticalOptions.
            pnlMain.VerticalOptions = compact ? LayoutOptions.Center : LayoutOptions.Fill;
        }

        private static Thickness TrimmedLineBox(double fontSize) => new(0, -0.335 * fontSize, 0, -0.132 * fontSize);

        // The user may have edited their beacons in the Beacons app while RealSpeed was in the background.
        private void OnWindowResumed(object? sender, EventArgs e)
        {
            ReviewPrompt.StartSession();
            ShowBeacons();
        }

        // Beacons shared by the Beacons app, re-read each time the map is opened or the app comes back
        // to the foreground. Added without moving the map, so following the user is unaffected.
        private void ShowBeacons()
        {
#if IOS || MACCATALYST
            if (_map?.Handler?.PlatformView is MapKit.MKMapView mapView)
                _beaconsLayer.Show(mapView, SharedBeacons.Load());
#endif
        }

#if !WINDOWS
        // Let MapKit keep the map centred on the user while preserving the zoom level they pick.
        // Panning the map suspends following; hiding and showing the map again resumes it.
        private void FollowUser()
        {
#if IOS || MACCATALYST
            if (_map?.Handler?.PlatformView is MapKit.MKMapView mapView)
                mapView.SetUserTrackingMode(MapKit.MKUserTrackingMode.Follow, true);
#endif
        }
#endif
    }
}
