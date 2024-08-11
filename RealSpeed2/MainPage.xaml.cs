using Microsoft.Maui.Devices.Sensors;
using static Microsoft.Maui.ApplicationModel.Permissions;
using Microsoft.Maui.Controls;

namespace RealSpeed2
{
    public partial class MainPage : ContentPage
    {
        private static bool _isTracking = false;
        private Location _previousLocation = null;
        private LocationViewModel _viewModel;
        private int _delay = 1000;

        public MainPage()
        {
            InitializeComponent();

            _viewModel = new LocationViewModel();
            BindingContext = _viewModel;

            // Empêche l'écran de se verrouiller automatiquement
            DeviceDisplay.KeepScreenOn = true;

            StartTracking(TimeSpan.Zero);

        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Réactiver le verrouillage automatique de l'écran lorsque la page disparaît
            DeviceDisplay.KeepScreenOn = false;

            _isTracking = false;
        }

        private async void StartTracking(TimeSpan timeout, GeolocationAccuracy geolocationAccuracy = GeolocationAccuracy.Default)
        {

            try
            {
                _isTracking = true;

                while (_isTracking)
                {
                    var request = new GeolocationRequest(geolocationAccuracy, timeout);
                    var location = await Geolocation.GetLocationAsync(request);

                    if (location != null)
                    {
                        // Check if the location has changed
                        if (_previousLocation == null ||
                            location.Timestamp != _previousLocation.Timestamp)
                        {
                            if (_previousLocation != null)
                            {
                                _viewModel.RealSpeed = ((location.Speed ?? 0.0) * 3.6).ToString("F1"); // m/s * 3.6 -> km/h
                                _viewModel.CalculatedSpeed = CalculGPS.CalculateSpeedKmh(_previousLocation.Latitude, _previousLocation.Longitude, _previousLocation.Timestamp.DateTime, location.Latitude, location.Longitude, location.Timestamp.DateTime).ToString("F1");
                            }

                            _previousLocation = location;
                            _viewModel.Location = location;
                        }

                    }

                    await Task.Delay(_delay); // Delay de X secondes avant la prochaine vérification
                }

            }
            catch (Exception ex)
            {
                lblGPSLog.Text = $"An error occurred: {ex.Message}";
            }
        }

        private void StopTracking()
        {
            _isTracking = false;
            _viewModel.RealSpeed = "";
            Thread.Sleep(_delay + 10); //tempo pour être certain que le tracking ce désactive
        }

        private void OnGetGPSInfoClicked(object sender, EventArgs e)
        {
            // Alterner la visibilité du panneau
            pnlGPSDetails.IsVisible = !pnlGPSDetails.IsVisible;

            // Mettre à jour le texte du bouton en fonction de la visibilité du panneau
            ((Button)sender).Text = pnlGPSDetails.IsVisible ? "Hide GPS details" : "Show GPS details";
        }

        private void OnChangeAccuracyClicked(object sender, EventArgs e)
        {
            // 0 iOS:     Default = Medium
            // 1 iOS:     Lowest = ThreeKilometers (3000m)
            // 2 iOS:     Low = Kilometer          (1000m)
            // 3 iOS:     Medium = HundredMeters   (100m)
            // 4 iOS:     High = NearestTenMeters  (10m)
            // 5 iOS:     Best                     (0m)

            if (_viewModel.Accuracy < GeolocationAccuracy.Medium)
                _viewModel.Accuracy = GeolocationAccuracy.Medium;
            else if (_viewModel.Accuracy < GeolocationAccuracy.Best)
                _viewModel.Accuracy++;
            else
                _viewModel.Accuracy = GeolocationAccuracy.Medium;

            ((Button)sender).Text = $"Change GPS Acc ({_viewModel.Accuracy.ToString()})";
            StopTracking();
            StartTracking(_viewModel.Timeout, _viewModel.Accuracy);
        }

        private void OnChangeTimeoutClicked(object sender, EventArgs e)
        {

            StopTracking();
            _viewModel.Timeout = _viewModel.Timeout.Add(new TimeSpan(0, 0, 0, 0, 500));

            if (TimeSpan.Compare(_viewModel.Timeout, new TimeSpan(0, 0, 0, 0, 1500)) == 1)
                _viewModel.Timeout = TimeSpan.Zero;

            ((Button)sender).Text = $"Change GPS Timeout ({_viewModel.Timeout.TotalMilliseconds.ToString()} ms)";
            StartTracking(_viewModel.Timeout, _viewModel.Accuracy);            
        }

    }

}
