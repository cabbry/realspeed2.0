using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace RealSpeed2
{
    public partial class MainPage : ContentPage
    {
        private Location? _previousLocation;
        private readonly LocationViewModel _viewModel;
#if WINDOWS
        private CancellationTokenSource? _pollingCts;
#endif

        public MainPage()
        {
            InitializeComponent();

            _viewModel = new LocationViewModel();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            DeviceDisplay.KeepScreenOn = true;

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
            _viewModel.UpdateCount++;
            _viewModel.LastUpdate = DateTime.Now;

            if (_previousLocation != null && location.Timestamp != _previousLocation.Timestamp)
            {
                _viewModel.RealSpeed = ((location.Speed ?? 0.0) * 3.6).ToString("F1");
                _viewModel.CalculatedSpeed = CalculGPS.CalculateSpeedKmh(
                    _previousLocation.Latitude, _previousLocation.Longitude, _previousLocation.Timestamp.DateTime,
                    location.Latitude, location.Longitude, location.Timestamp.DateTime).ToString("F1");
            }

            _previousLocation = location;
            _viewModel.Location = location;
        }

        private void OnGetGPSInfoClicked(object sender, EventArgs e)
        {
            pnlGPSDetails.IsVisible = !pnlGPSDetails.IsVisible;
            ((Button)sender).Text = pnlGPSDetails.IsVisible ? "Hide GPS details" : "Show GPS details";
        }
    }
}
