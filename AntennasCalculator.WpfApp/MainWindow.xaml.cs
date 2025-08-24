using System.Windows;
using Fresnel.Core;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Wpf;
using Mapsui.Widgets.ScaleBar;
using NetTopologySuite.Geometries;
using Point = NetTopologySuite.Geometries.Point;
using AntennasCalculator.WpfApp.ViewModels;

namespace AntennasCalculator.WpfApp
{
	public partial class MainWindow : Window
	{
		private readonly MemoryLayer _pointsLayer = new() { Name = "Points" };
		private readonly MemoryLayer _lineLayer = new() { Name = "Link" };
		private readonly MemoryLayer _fresnelLayer = new() { Name = "Fresnel" };
		private readonly IFresnelCalculator _fresnel = new FresnelCalculator();

		private GeoPoint? _p1;
		private GeoPoint? _p2;

		private LinkViewModel Vm => (LinkViewModel)DataContext;
		public MainWindow()
		{
			InitializeComponent();

			// Base map
			MapCtrl.Map?.Layers.Add(OpenStreetMap.CreateTileLayer("FresnelRelayDemo/0.1 (+https://example.local)"));

			// Layers (draw order: fresnel under, then line, then points)
			MapCtrl.Map?.Layers.Add(_fresnelLayer);
			MapCtrl.Map?.Layers.Add(_lineLayer);
			MapCtrl.Map?.Layers.Add(_pointsLayer);

			// Optional scalebar widget
			if (MapCtrl.Map is not null)
				MapCtrl.Map.Widgets.Add(new ScaleBarWidget(MapCtrl.Map));

			// Handle clicks
			MapCtrl.Info += MapCtrl_Info;

			StatusText.Text = "Виберіть 2 точки кліком на мапі.";
		}

		private void MapCtrl_Info(object? sender, Mapsui.MapInfoEventArgs e)
		{
			var mapView = sender as MapControl;
			var mapInfo = e.GetMapInfo(mapView.Map.Layers);
			var world = mapInfo?.WorldPosition;
			if (world is null) return;

			// Store as geographic for distance math
			var lonlat = SphericalMercator.ToLonLat(world.X, world.Y);
			var gp = new GeoPoint(lonlat.lat, lonlat.lon);

			if (_p1 is null) _p1 = gp;
			else if (_p2 is null) _p2 = gp;
			else { _p1 = gp; _p2 = null; }

			RedrawPointsAndLine();
		}

		private void RedrawPointsAndLine()
		{
			// Convert geo to world (WebMercator) for drawing
			var features = new List<IFeature>();
			if (_p1 is not null)
			{
				var w = SphericalMercator.FromLonLat(_p1.LongitudeDeg, _p1.LatitudeDeg);
				features.Add(new GeometryFeature(new Point(w.x, w.y)));
			}
			if (_p2 is not null)
			{
				var w = SphericalMercator.FromLonLat(_p2.LongitudeDeg, _p2.LatitudeDeg);
				features.Add(new GeometryFeature(new Point(w.x, w.y)));
			}
			_pointsLayer.Features = features;
			_pointsLayer.DataHasChanged();

			// Link line
			var lineFeats = new List<IFeature>();
			if (_p1 is not null && _p2 is not null)
			{
				var w1 = SphericalMercator.FromLonLat(_p1.LongitudeDeg, _p1.LatitudeDeg);
				var w2 = SphericalMercator.FromLonLat(_p2.LongitudeDeg, _p2.LatitudeDeg);
				lineFeats.Add(new GeometryFeature(new LineString(new[] { new Coordinate(w1.x, w1.y), new Coordinate(w2.x, w2.y) })));
			}
			_lineLayer.Features = lineFeats;
			_lineLayer.DataHasChanged();

			MapCtrl.RefreshGraphics();
		}

		private void ClearBtn_Click(object sender, RoutedEventArgs e)
		{
			_p1 = null; _p2 = null;
			_pointsLayer.Features = Array.Empty<IFeature>();
			_lineLayer.Features = Array.Empty<IFeature>();
			_fresnelLayer.Features = Array.Empty<IFeature>();
			_pointsLayer.DataHasChanged();
			_lineLayer.DataHasChanged();
			_fresnelLayer.DataHasChanged();
			StatusText.Text = "Очищено. Клікніть 2 точки.";
			MapCtrl.RefreshGraphics();
		}

		private void ComputeBtn_Click(object sender, RoutedEventArgs e)
		{
			if (_p1 is null || _p2 is null)
			{
				MessageBox.Show("Клікніть дві точки на мапі.");
				return;
			}

			var fGHz = Vm.FreqGHz;
			var freqHz = fGHz * 1e9;
			var clearancePct = Vm.ClearancePct > 0 ? Vm.ClearancePct : 60.0;
			var dist = GeoUtils.HaversineMeters(_p1, _p2);
			int samples = 64;

			var coords = new List<Coordinate>(samples * 2 + 2);

			for (int i = 0; i <= samples; i++)
			{
				var t = i / (double)samples;
				var g = GeoUtils.Lerp(_p1, _p2, t);
				var d1 = dist * t;
				var d2 = dist - d1;
				var r1 = _fresnel.Radius1(freqHz, d1, d2);
				var radius = r1 * (clearancePct / 100.0);

				var world = SphericalMercator.FromLonLat(g.LongitudeDeg, g.LatitudeDeg);
				var meterScale = Math.Cos(g.LatitudeDeg * Math.PI / 180.0);
				var rMap = radius / Math.Max(1e-6, meterScale);
				coords.Add(new Coordinate(world.x, world.y + rMap));
			}
			for (int i = samples; i >= 0; i--)
			{
				var t = i / (double)samples;
				var g = GeoUtils.Lerp(_p1, _p2, t);
				var d1 = dist * t;
				var d2 = dist - d1;
				var r1 = _fresnel.Radius1(freqHz, d1, d2);
				var radius = r1 * (clearancePct / 100.0);

				var world = SphericalMercator.FromLonLat(g.LongitudeDeg, g.LatitudeDeg);
				var meterScale = Math.Cos(g.LatitudeDeg * Math.PI / 180.0);
				var rMap = radius / Math.Max(1e-6, meterScale);
				coords.Add(new Coordinate(world.x, world.y - rMap));
			}
			if (coords.Count < 4)
			{
				MessageBox.Show("Надто коротка лінія для побудови полігона.");
				return;
			}
			// Close the ring
			var first = coords[0];
			var last = coords[coords.Count - 1];
			if (first.X != last.X || first.Y != last.Y)
				coords.Add(new Coordinate(first.X, first.Y));

			var ring = new LinearRing(coords.ToArray());
			var poly = new Polygon(ring);
			_fresnelLayer.Features = new List<IFeature> { new GeometryFeature(poly) };
			_fresnelLayer.DataHasChanged();

			StatusText.Text = $"D≈{dist / 1000:0.###} km, f={fGHz:0.###} GHz, clearance={clearancePct:0.#}%";
			MapCtrl.RefreshGraphics();
		}
	}
}
