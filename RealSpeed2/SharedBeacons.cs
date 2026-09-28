using System.Globalization;
using System.Text.Json;

namespace RealSpeed2
{
    public sealed record SharedBeacon(string Name, double Latitude, double Longitude, Color Color);

    // Beacons published by the Beacons app (com.cabbry.beacons) in the App Group container it shares
    // with RealSpeed. Contract v1, whose source of truth is SharedBeacons.ToJson in the Beacons project:
    //   {"v":1,"beacons":[{"name":"Romans","lat":45.0497,"lon":5.047,"color":"#00FFFF"}]}
    // Unknown fields are ignored; any other "v" is a breaking change and the file is skipped.
    public static class SharedBeacons
    {
        public const string AppGroupId = "group.com.cabbry.beacons";
        public const string FileName = "beacons-shared.json";

        // Empty, never an error, when there is nothing to show: no container (build without the
        // App Group entitlement), no file yet, or content that doesn't match the contract.
        public static IReadOnlyList<SharedBeacon> Load()
        {
#if IOS || MACCATALYST
            var container = Foundation.NSFileManager.DefaultManager.GetContainerUrl(AppGroupId)?.Path;
            if (container == null)
                return [];

            try
            {
                var path = Path.Combine(container, FileName);
                return File.Exists(path) ? Parse(File.ReadAllText(path)) : [];
            }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
#else
            return [];
#endif
        }

        public static IReadOnlyList<SharedBeacon> Parse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !TryGetNumber(root, "v", out var version) || version != 1
                    || !root.TryGetProperty("beacons", out var items) || items.ValueKind != JsonValueKind.Array)
                    return [];

                var beacons = new List<SharedBeacon>();
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object
                        || !TryGetNumber(item, "lat", out var lat) || lat < -90 || lat > 90
                        || !TryGetNumber(item, "lon", out var lon) || lon < -180 || lon > 180)
                        continue;

                    beacons.Add(new SharedBeacon(
                        TryGetString(item, "name") ?? "",
                        lat,
                        lon,
                        ParseColor(TryGetString(item, "color"))));
                }
                return beacons;
            }
            catch (JsonException) { return []; }
        }

        private static bool TryGetNumber(JsonElement obj, string name, out double value)
        {
            value = 0;
            return obj.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out value);
        }

        private static string? TryGetString(JsonElement obj, string name) =>
            obj.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

        // "#RRGGBB" in sRGB; cyan when malformed, like the Beacons app's own map.
        private static Color ParseColor(string? hex)
        {
            var digits = hex != null && hex.StartsWith('#') ? hex[1..] : hex;
            return digits?.Length == 6 && int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)
                ? Color.FromRgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)
                : Colors.Cyan;
        }
    }
}
