using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
#if IOS
using StoreKit;
using UIKit;
#endif

namespace RealSpeed2
{
    // App Store review request, never on discovery: only once the app has been used on several different
    // days and has measured several real trips. Never while moving, as the user may be driving: only right
    // after opening the app, once the GPS has reported a standstill for a few seconds. At most once per
    // version; iOS then decides whether to show its dialog (3 times a year at most, never on TestFlight).
    public static class ReviewPrompt
    {
        private const int MinDays = 3;
        private const int MinTrips = 3;
        private const double TripSpeedKmh = 15.0;  // a session that reaches this speed counts as a trip
        private const double StillSpeedKmh = 2.0;
        private static readonly TimeSpan StillFor = TimeSpan.FromSeconds(10);

        private const string LastDayKey = "ReviewLastActiveDay";
        private const string DaysKey = "ReviewActiveDays";
        private const string TripsKey = "ReviewTrips";
        private const string AskedVersionKey = "ReviewAskedVersion";

        private static bool _tripCounted;
        private static bool _moved;
        private static DateTime? _stillSince;

        // On opening the app or returning to it.
        public static void StartSession()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (Preferences.Default.Get(LastDayKey, "") != today)
            {
                Preferences.Default.Set(LastDayKey, today);
                Preferences.Default.Set(DaysKey, Preferences.Default.Get(DaysKey, 0) + 1);
            }
            _tripCounted = false;
            _moved = false;
            _stillSince = null;
        }

        // Each GPS fix. An invalid speed (null or negative) never counts as standing still.
        public static void OnSpeed(double? metersPerSecond)
        {
            if (metersPerSecond is not double mps || mps < 0)
            {
                _stillSince = null;
                return;
            }

            var kmh = mps * 3.6;
            if (kmh >= TripSpeedKmh && !_tripCounted)
            {
                _tripCounted = true;
                Preferences.Default.Set(TripsKey, Preferences.Default.Get(TripsKey, 0) + 1);
            }
            if (kmh > StillSpeedKmh)
            {
                // Once on the move, nothing more until the next opening.
                _moved = true;
                _stillSince = null;
                return;
            }
            if (_moved)
                return;

            _stillSince ??= DateTime.UtcNow;
            if (DateTime.UtcNow - _stillSince.Value >= StillFor)
                RequestIfDue();
        }

        private static void RequestIfDue()
        {
            var version = AppInfo.Current.VersionString;
            if (Preferences.Default.Get(DaysKey, 0) < MinDays
                || Preferences.Default.Get(TripsKey, 0) < MinTrips
                || Preferences.Default.Get(AskedVersionKey, "") == version)
                return;

#if IOS
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (UIApplication.SharedApplication.ApplicationState != UIApplicationState.Active)
                    return;
                var scene = UIApplication.SharedApplication.ConnectedScenes
                    .OfType<UIWindowScene>()
                    .FirstOrDefault(s => s.ActivationState == UISceneActivationState.ForegroundActive);
                if (scene == null)
                    return;
                Preferences.Default.Set(AskedVersionKey, version);
                SKStoreReviewController.RequestReview(scene);
            });
#endif
        }
    }
}
