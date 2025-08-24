using System.Windows;
using System.IO;
using Fresnel.Core.Antennas;
using System.Linq;
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
using AntennasCalculator.WpfApp.Controls;
using System.Linq;
using System.Windows.Navigation;
using AntennasCalculator.WpfApp.Models;
using AntennasCalculator.WpfApp.Services;
using Fresnel.Core.Dem;

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

			// Load antenna catalog if available
			TryLoadAntennaCatalog();

			// Load persisted settings and apply to VM (after catalog is loaded)
			LoadSettingsIntoVm();
		}

		private void TryLoadAntennaCatalog()
		{
			var vm = Vm;
			string[] candidates = new[] {
					  System.IO.Path.Combine(AppContext.BaseDirectory, "Antennas", "antenna_catalog.json"),
					  System.IO.Path.Combine(AppContext.BaseDirectory, "..","..","..","Fresnel.Core","Antennas","antenna_catalog.json"),
					  System.IO.Path.Combine(AppContext.BaseDirectory, "..","..","..","..","Fresnel.Core","Antennas","antenna_catalog.json")
				  };
			string? path = candidates.FirstOrDefault(File.Exists);
			if (path is null) return;
			try
			{
				var list = AntennaCatalog.LoadFromJson(path);
				vm.AntennaCatalog.Clear();
				foreach (var it in list) vm.AntennaCatalog.Add(it);
				// Preselect two commonly used ones if found
				vm.SelectedApAntenna = vm.AntennaCatalog.FirstOrDefault(a => a.Brand.Contains("MikroTik", System.StringComparison.OrdinalIgnoreCase)) ?? vm.AntennaCatalog.FirstOrDefault();
				vm.SelectedStaAntenna = vm.AntennaCatalog.FirstOrDefault(a => a.Brand.Contains("Ubiquiti", System.StringComparison.OrdinalIgnoreCase)) ?? vm.AntennaCatalog.FirstOrDefault();
			}
			catch
			{
				// ignore load errors silently (non-breaking demo)
			}
		}

		private void LoadSettingsIntoVm()
		{
			var s = SettingsService.Load();
			var vm = Vm;
			vm.FreqGHz = s.FreqGHz;
			vm.ClearancePct = s.ClearancePct;
			vm.Band = s.Band;
			vm.Technology = s.Technology;
			vm.ChannelWidthMHz = s.ChannelWidthMHz;

			vm.ApHeightM = s.ApHeightM;
			vm.StaHeightM = s.StaHeightM;
			vm.DemFolder = s.DemFolder ?? string.Empty;

			// Resolve antennas by Code first, then Brand+Model
			if (vm.AntennaCatalog.Count > 0)
			{
				if (!string.IsNullOrWhiteSpace(s.ApAntennaCode))
					vm.SelectedApAntenna = vm.AntennaCatalog.FirstOrDefault(a => string.Equals(a.Code, s.ApAntennaCode, StringComparison.OrdinalIgnoreCase));
				if (vm.SelectedApAntenna is null && !string.IsNullOrWhiteSpace(s.ApAntennaBrand) && !string.IsNullOrWhiteSpace(s.ApAntennaModel))
					vm.SelectedApAntenna = vm.AntennaCatalog.FirstOrDefault(a => string.Equals(a.Brand, s.ApAntennaBrand, StringComparison.OrdinalIgnoreCase) && string.Equals(a.Model, s.ApAntennaModel, StringComparison.OrdinalIgnoreCase));


				if (!string.IsNullOrWhiteSpace(s.StaAntennaCode))
					vm.SelectedStaAntenna = vm.AntennaCatalog.FirstOrDefault(a => string.Equals(a.Code, s.StaAntennaCode, StringComparison.OrdinalIgnoreCase));
				if (vm.SelectedStaAntenna is null && !string.IsNullOrWhiteSpace(s.StaAntennaBrand) && !string.IsNullOrWhiteSpace(s.StaAntennaModel))
					vm.SelectedStaAntenna = vm.AntennaCatalog.FirstOrDefault(a => string.Equals(a.Brand, s.StaAntennaBrand, StringComparison.OrdinalIgnoreCase) && string.Equals(a.Model, s.StaAntennaModel, StringComparison.OrdinalIgnoreCase));

			}

			// Save on close
			this.Closing += (_, __) => SaveSettingsFromVm();
		}

		private void SaveSettingsFromVm()
		{
			var vm = Vm;
			var s = new AppSettings
			{
				FreqGHz = vm.FreqGHz,
				ClearancePct = vm.ClearancePct,
				Band = vm.Band,
				Technology = vm.Technology,
				ChannelWidthMHz = vm.ChannelWidthMHz,
				ApHeightM = vm.ApHeightM,
				StaHeightM = vm.StaHeightM,
				DemFolder = string.IsNullOrWhiteSpace(vm.DemFolder) ? null : vm.DemFolder,
				ApAntennaCode = vm.SelectedApAntenna?.Code,
				ApAntennaBrand = vm.SelectedApAntenna?.Brand,
				ApAntennaModel = vm.SelectedApAntenna?.Model,
				StaAntennaCode = vm.SelectedStaAntenna?.Code,
				StaAntennaBrand = vm.SelectedStaAntenna?.Brand,
				StaAntennaModel = vm.SelectedStaAntenna?.Model
			};
			SettingsService.Save(s);
		}

		// Handle hyperlink clicks from antenna details
		private void OnAntennaLinkNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				var uri = e.Uri?.ToString();
				if (!string.IsNullOrWhiteSpace(uri))
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
			}
			catch { /* ignore */ }
			e.Handled = true;
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

			var profile = new List<(double x, double r)>(samples + 1);
			double totalMeters = dist;

			var terrain = new List<(double x, double z)>(samples + 1); // aligned with profile
			var vm = Vm;
			IDemProvider dem = string.IsNullOrWhiteSpace(vm.DemFolder) ? new FlatDemProvider() : new HgtDemProvider(vm.DemFolder);

			// Endpoint ground elevations and antenna heights
			var groundA = dem.GetElevation(_p1.LatitudeDeg, _p1.LongitudeDeg);
			var groundB = dem.GetElevation(_p2.LatitudeDeg, _p2.LongitudeDeg);

			for (int i = 0; i <= samples; i++)
			{
				var t = i / (double)samples;
				var g = GeoUtils.Lerp(_p1, _p2, t);

				var d1 = dist * t;
				var d2 = dist - d1;
				var r1 = _fresnel.Radius1(freqHz, d1, d2);
				var radius = r1 * (clearancePct / 100.0);
				profile.Add((d1, r1));

				// Terrain elevation and straight-line LOS height
				var ground = dem.GetElevation(g.LatitudeDeg, g.LongitudeDeg);
				terrain.Add((d1, ground));// aligned index == i

				// LOS linear interpolation between endpoint antenna heights above sea level
				//var groundA = dem.GetElevation(_p1.LatitudeDeg, _p1.LongitudeDeg);
				//var groundB = dem.GetElevation(_p2.LatitudeDeg, _p2.LongitudeDeg);

				var world = SphericalMercator.FromLonLat(g.LongitudeDeg, g.LatitudeDeg);
				var meterScale = Math.Cos(g.LatitudeDeg * Math.PI / 180.0);
				var rMap = radius / Math.Max(1e-6, meterScale);
				coords.Add(new Coordinate(world.x, world.y + rMap));
			}
			for (int i = samples; i >= 0; i--)
			{
				var t = i / (double)samples;
				var g = GeoUtils.Lerp(_p1, _p2, t);

				//var ground = dem.GetElevation(g.LatitudeDeg, g.LongitudeDeg);
				//terrain.Add((dist - dist * t, ground)); // reversed x for closing polygon not needed but collected

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

			// Update profile view (if user switches to the tab)
			var zA = groundA + vm.ApHeightM;
			var zB = groundB + vm.StaHeightM;
			Profile?.SetDataWithTerrain(profile, clearancePct, totalMeters, terrain, zA, zB);
			TopTabs.SelectedIndex = 1; // switch to Profile tab for immediate feedback
									   // LOS / clearance indicator (stubbed: no terrain => theoretical check only)
			UpdateLosIndicator(clearancePct);

			// Compute actual min clearance along the path using terrain
			// Compute LOS height at endpoints:
			//var zA = dem.GetElevation(_p1.LatitudeDeg, _p1.LongitudeDeg) + vm.ApHeightM;
			//var zB = dem.GetElevation(_p2.LatitudeDeg, _p2.LongitudeDeg) + vm.StaHeightM;
			// (reuse zA, zB)
			double minPct = double.PositiveInfinity;
			for (int i = 0; i <= samples; i++)
			{
				var t = i / (double)samples;
				var d1i = dist * t;
				var d2i = dist - d1i;
				var r1i = _fresnel.Radius1(freqHz, d1i, d2i);
				var ground = dem.GetElevation(
					_p1.LatitudeDeg + (_p2.LatitudeDeg - _p1.LatitudeDeg) * t,
					_p1.LongitudeDeg + (_p2.LongitudeDeg - _p1.LongitudeDeg) * t);
				var losZi = zA + (zB - zA) * t; // straight line between endpoints
				var clearanceMeters = losZi - ground;
				var pct = r1i > 0 ? (clearanceMeters / r1i) * 100.0 : 0;
				if (pct < minPct) minPct = pct;
			}
			if (double.IsInfinity(minPct) || double.IsNaN(minPct)) minPct = 0;
			vm.MinClearancePctActual = minPct;

			// LOS / clearance indicator uses actual min clearance now
			UpdateLosIndicator(minPct);

			HighlightViolationSegments(dem, dist, freqHz, vm, samples);
		}

		private void HighlightViolationSegments(IDemProvider dem, double dist, double freqHz, LinkViewModel vm, int samples)
		{
			if (_p1 is null || _p2 is null) return;
			var zA = dem.GetElevation(_p1.LatitudeDeg, _p1.LongitudeDeg) + vm.ApHeightM;
			var zB = dem.GetElevation(_p2.LatitudeDeg, _p2.LongitudeDeg) + vm.StaHeightM;
			var ranges = new List<(double xs, double xe)>();
			bool inBad = false;
			double startBad = 0;

			for (int i = 0; i <= samples; i++)
			{
				double t = i / (double)samples;
				double d1 = dist * t;
				double d2 = dist - d1;
				var r1 = _fresnel.Radius1(freqHz, d1, d2);

				var ground = dem.GetElevation(
					_p1.LatitudeDeg + (_p2.LatitudeDeg - _p1.LatitudeDeg) * t,
					_p1.LongitudeDeg + (_p2.LongitudeDeg - _p1.LongitudeDeg) * t);

				var losZi = zA + (zB - zA) * t;
				var clearanceMeters = losZi - ground;
				double pct = r1 > 0 ? (clearanceMeters / r1) * 100.0 : 0.0;
				bool bad = pct < vm.ClearancePct; // violation if below target

				if (bad && !inBad)
				{
					inBad = true;
					startBad = d1;
				}
				else if (!bad && inBad)
				{
					inBad = false;
					ranges.Add((startBad, d1));
				}
			}
			if (inBad)
			{
				ranges.Add((startBad, dist));
			}

			// Pass to profile for drawing
			Profile?.SetViolations(ranges);
		}

		private void UpdateLosIndicator(double targetClearancePct)
		{
			// With DEM disabled we assume the path is unobstructed and evaluate only the target.
			// Policy:
			//  - OK:    target >= 60%
			//  - Warn:  40%..60%
			//  - Bad:   < 40%
			string text;
			System.Windows.Media.Brush brush;
			if (targetClearancePct >= 60)
			{
				text = $"LOS OK (target ≥60%)";
				brush = System.Windows.Media.Brushes.LimeGreen;
			}
			else if (targetClearancePct >= 40)
			{
				text = "Marginal clearance (target <60%)";
				brush = System.Windows.Media.Brushes.Orange;
			}
			else
			{
				text = "Insufficient clearance (target <40%)";
				brush = System.Windows.Media.Brushes.IndianRed;
			}
			LosText.Text = text;
			LosDot.Fill = brush;
		}


	}
}
