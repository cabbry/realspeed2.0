#if IOS || MACCATALYST
using CoreGraphics;
using MapKit;
using Microsoft.Maui.Platform;
using UIKit;

namespace RealSpeed2
{
    // Draws the Beacons app's beacons on the MapKit view behind the MAUI Map, with the same look as
    // the Beacons map overview (BeaconsMapPicker.mm in the Beacons project): a small glowing column
    // standing on the coordinate, with a dark name tag underneath.
    // Beacons are plain MapKit annotations rather than MAUI Pins: MAUI Pins can't have a custom view.
    internal sealed class BeaconsMapLayer
    {
        private const string ReuseId = "beacon";

        private BeaconAnnotation[] _annotations = [];

        // Replaces the beacons on the map without moving it, so FollowUser keeps control of the camera.
        public void Show(MKMapView mapView, IReadOnlyList<SharedBeacon> beacons)
        {
            // Set on every call rather than once: MAUI's iOS map handler leaves this delegate unset
            // (as of 10.0.0), but re-asserting it guards against it being reset.
            mapView.GetViewForAnnotation = ViewForAnnotation;

            if (_annotations.Length > 0)
                mapView.RemoveAnnotations(_annotations);
            _annotations = beacons.Select(b => new BeaconAnnotation(b)).ToArray();
            if (_annotations.Length > 0)
                mapView.AddAnnotations(_annotations);
        }

        private static MKAnnotationView ViewForAnnotation(MKMapView mapView, IMKAnnotation annotation)
        {
            // Null keeps MapKit's default view, notably the blue user-location dot.
            if (annotation is not BeaconAnnotation beacon)
                return null!;

            var view = mapView.DequeueReusableAnnotation(ReuseId) as BeaconAnnotationView
                ?? new BeaconAnnotationView(beacon, ReuseId);
            view.Annotation = beacon;
            return view;
        }
    }

    internal sealed class BeaconAnnotation : MKPointAnnotation
    {
        public BeaconAnnotation(SharedBeacon beacon)
        {
            Beacon = beacon;
            Title = beacon.Name;
            Coordinate = new CoreLocation.CLLocationCoordinate2D(beacon.Latitude, beacon.Longitude);
        }

        public SharedBeacon Beacon { get; }
    }

    internal sealed class BeaconAnnotationView : MKAnnotationView
    {
        // Beam geometry (points). The coordinate sits at the glow center, i.e. the base of the column.
        private const float BeamW = 30, BeamH = 64, BaseY = 56, TagGap = 2;

        private readonly UIImageView? _beam;
        private readonly UILabel? _nameTag;

        public BeaconAnnotationView(IMKAnnotation annotation, string reuseIdentifier)
            : base(annotation, reuseIdentifier)
        {
            CanShowCallout = false;
            // Every beacon stays visible, even when zoomed out onto a cluster.
            DisplayPriority = MKFeatureDisplayPriority.Required;
            CollisionMode = MKAnnotationViewCollisionMode.None;

            _beam = new UIImageView();
            AddSubview(_beam);

            _nameTag = new UILabel
            {
                Font = UIFont.SystemFontOfSize(12f, UIFontWeight.Semibold),
                TextAlignment = UITextAlignment.Center,
                BackgroundColor = UIColor.FromWhiteAlpha(0f, 0.6f),
            };
            _nameTag.Layer.CornerRadius = 8f;
            _nameTag.Layer.MasksToBounds = true;
            AddSubview(_nameTag);

            // The base constructor may set the annotation before the subviews exist.
            Configure();
        }

        public override IMKAnnotation? Annotation
        {
            get => base.Annotation;
            set
            {
                base.Annotation = value;
                Configure();
            }
        }

        private void Configure()
        {
            if (Annotation is not BeaconAnnotation annotation || _beam == null || _nameTag == null)
                return;

            var color = annotation.Beacon.Color;
            _beam.Image = BeamImage(color.ToPlatform());
            _nameTag.Text = annotation.Beacon.Name;
            _nameTag.TextColor = TagTextColor(color).ToPlatform();

            var textSize = _nameTag.SizeThatFits(new CGSize(220f, 20f));
            float tagW = MathF.Min(MathF.Ceiling((float)textSize.Width) + 14f, 220f), tagH = 18f;
            float w = MathF.Max(BeamW, tagW), h = BeamH + TagGap + tagH;

            // Bounds, not Frame: MapKit owns the view's center.
            Bounds = new CGRect(0f, 0f, w, h);
            _beam.Frame = new CGRect((w - BeamW) * 0.5f, 0f, BeamW, BeamH);
            _nameTag.Frame = new CGRect((w - tagW) * 0.5f, BeamH + TagGap, tagW, tagH);
            // Put the beam base, not the view center, on the coordinate.
            CenterOffset = new CGPoint(0f, h * 0.5f - BaseY);
        }

        // A miniature of the Beacons AR beacon: tapered column fading upward, white-hot core, and a
        // soft halo at the base. The white core keeps dark colors (navy) visible on satellite imagery.
        private static UIImage BeamImage(UIColor color)
        {
            var renderer = new UIGraphicsImageRenderer(new CGSize(BeamW, BeamH));
            return renderer.CreateImage(ctx =>
            {
                var g = ctx.CGContext;
                using var colorSpace = CGColorSpace.CreateDeviceRGB();
                const float cx = BeamW * 0.5f, top = 3f;

                using (var halo = FadeGradient(colorSpace, color, 0.85f))
                    g.DrawRadialGradient(halo, new CGPoint(cx, BaseY), 0f, new CGPoint(cx, BaseY), 8f, CGGradientDrawingOptions.None);

                DrawTaper(g, colorSpace, cx, top, 3.5f, 0.8f, color);              // column
                DrawTaper(g, colorSpace, cx, top, 1.2f, 0.3f, UIColor.White);      // core

                // Bright dot at the exact position.
                g.SetFillColor(UIColor.White.CGColor);
                g.FillEllipseInRect(new CGRect(cx - 2f, BaseY - 2f, 4f, 4f));
            });
        }

        // Tapered quad from half-width `baseHalfW` at the base up to half-width `tipHalfW` at `top`,
        // filled with `color` fading out toward the tip.
        private static void DrawTaper(CGContext g, CGColorSpace colorSpace, float cx, float top,
                                      float baseHalfW, float tipHalfW, UIColor color)
        {
            g.SaveState();
            g.MoveTo(cx - baseHalfW, BaseY);
            g.AddLineToPoint(cx - tipHalfW, top);
            g.AddLineToPoint(cx + tipHalfW, top);
            g.AddLineToPoint(cx + baseHalfW, BaseY);
            g.ClosePath();
            g.Clip();
            using (var gradient = FadeGradient(colorSpace, color, 0.95f))
                g.DrawLinearGradient(gradient, new CGPoint(cx, BaseY), new CGPoint(cx, top), CGGradientDrawingOptions.None);
            g.RestoreState();
        }

        // Two-stop gradient from `color` at `alpha` to fully transparent.
        private static CGGradient FadeGradient(CGColorSpace colorSpace, UIColor color, float alpha) =>
            new CGGradient(colorSpace, new[] { color.ColorWithAlpha(alpha).CGColor, color.ColorWithAlpha(0f).CGColor });

        // Name tag text: the beacon color, lifted toward white when too dark to read on the dark tag.
        private static Color TagTextColor(Color c)
        {
            var luminance = 0.2126f * c.Red + 0.7152f * c.Green + 0.0722f * c.Blue;
            if (luminance >= 0.4f)
                return c;
            return new Color(c.Red + (1 - c.Red) * 0.55f, c.Green + (1 - c.Green) * 0.55f, c.Blue + (1 - c.Blue) * 0.55f);
        }
    }
}
#endif
