using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;
using System.Windows.Documents;

namespace AntennasCalculator.WpfApp.Controls;

public partial class ProfileView : UserControl
{
	private readonly List<(double x, double r)> _samples = new();
	private readonly List<(double x, double z)> _terrain = new();
	private readonly List<(double xs, double xe)> _violations = new();
	private bool _hasPrecise = false;
	private (double zA, double zB) _losEndZ = (0, 0);
	private double _clearancePct = 60.0;
	private double _totalMeters = 0.0;

	public ProfileView()
	{
		InitializeComponent();
		SizeChanged += (_, __) => Redraw();
		Loaded += (_, __) => Redraw();
	}

	/// <summary>
	/// Provide samples along the link: x is distance (meters) from A, r is first Fresnel zone radius (meters).
	/// </summary>
	public void SetData(IEnumerable<(double x, double r)> samples, double clearancePct, double totalMeters)
	{
		_samples.Clear();
		_samples.AddRange(samples);
		_samples.Sort((a, b) => a.x.CompareTo(b.x));
		_clearancePct = clearancePct;
		_totalMeters = Math.Max(1, totalMeters);
		Redraw();
	}

	public void SetDataWithTerrain(IEnumerable<(double x, double r)> samples, double clearancePct, double totalMeters, IEnumerable<(double x, double z)> terrain, double zA = 0, double zB = 0)
	{
		SetData(samples, clearancePct, totalMeters);
		_terrain.Clear();
		_terrain.AddRange(terrain.OrderBy(t => t.x));
		_losEndZ = (zA, zB);
		_hasPrecise = zA != 0 || zB != 0;
		Redraw();
	}

	/// <summary>Legacy coarse violations (kept for compatibility). Ignored when precise mode is active.</summary>
	public void SetViolations(IEnumerable<(double xs, double xe)> segmentsMeters)
	{
		_violations.Clear();
		foreach (var (xs, xe) in segmentsMeters)
			if (xe > xs) _violations.Add((xs, xe));
		Redraw();
	}

	private void Redraw()
	{
		if (Canvas is null) return;
		Canvas.Children.Clear();

		double w = Math.Max(1, Canvas.ActualWidth);
		double h = Math.Max(1, Canvas.ActualHeight);

		double yMid = h * 0.5;

		bool drawPrecise = _hasPrecise && _terrain.Count == _samples.Count;

		if (_samples.Count < 2)
		{
			DrawAxes(w, h);
			return;
		}

		// Compute scaling: X=0.._totalMeters => 0..w, Y uses max radius*1.2
		var maxR = _samples.Max(s => s.r);
		if (maxR <= 0) maxR = 1;
		double yScale = (h * 0.4) / maxR; // 40% of height up and down

		// If we have precise data, compute z-required curve and draw filled polygons where terrain exceeds it.
		if (drawPrecise)
		{
			// Build arrays aligned by index
			int n = _samples.Count;
			var xs = new double[n];
			var zr = new double[n]; // required clearance z
			var zt = new double[n]; // terrain z
			for (int i = 0; i < n; i++)
			{
				xs[i] = _samples[i].x;
				double t = xs[i] / Math.Max(1, _totalMeters);
				double losZ = _losEndZ.zA + (_losEndZ.zB - _losEndZ.zA) * t;
				zr[i] = losZ - (_clearancePct / 100.0) * _samples[i].r;
				zt[i] = _terrain[i].z;
			}
			// Vertical mapping based on both terrain and required clearance ranges
			double zMin = Math.Min(zr.Min(), zt.Min());
			double zMax = Math.Max(zr.Max(), zt.Max());
			if (Math.Abs(zMax - zMin) < 1e-6) { zMax = zMin + 1; }
			double topPad = 10, bottomPad = 18;
			double usable = Math.Max(1, h - topPad - bottomPad);
			double Y(double z) => topPad + (zMax - z) / (zMax - zMin) * usable;
			double X(double x) => x / Math.Max(1, _totalMeters) * w;

			// Find intervals where zt > zr and build polygons
			int start = -1;
			for (int i = 0; i < n; i++)
			{
				bool bad = zt[i] > zr[i];
				if (bad && start < 0) start = i;
				if ((!bad || i == n - 1) && start >= 0)
				{
					int end = bad && i == n - 1 ? i : i - 1;
					// Build polygon: terrain from start..end, then required from end..start (reverse)
					var pg = new Polygon
					{
						Fill = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0x00, 0x00)),
						Stroke = Brushes.Transparent
					};
					var geom = new StreamGeometry();
					using (var ctx = geom.Open())
					{
						ctx.BeginFigure(new Point(X(xs[start]), Y(zt[start])), isFilled: true, isClosed: true);
						for (int k = start + 1; k <= end; k++)
							ctx.LineTo(new Point(X(xs[k]), Y(zt[k])), false, false);
						for (int k = end; k >= start; k--)
							ctx.LineTo(new Point(X(xs[k]), Y(zr[k])), false, false);
					}
					var path = new System.Windows.Shapes.Path
					{
						Data = geom,
						Fill = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0x00, 0x00)),
						Stroke = Brushes.Transparent
					};
					Canvas.Children.Add(path);
					start = -1;
				}
			}

			// Draw required clearance curve (dashed red) and terrain curve
			var req = new Polyline { Stroke = Brushes.Red, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 3 } };
			var terr = new Polyline { Stroke = Brushes.DimGray, StrokeThickness = 1.5 };
			for (int i = 0; i < n; i++)
			{
				req.Points.Add(new Point(X(xs[i]), Y(zr[i])));
				terr.Points.Add(new Point(X(xs[i]), Y(zt[i])));
			}
			Canvas.Children.Add(terr);
			Canvas.Children.Add(req);
		}
		else
		{
			// Draw coarse violation shading first (so lines overlay them)
			if (_violations.Count > 0)
			{
				foreach (var (xs, xe) in _violations)
				{
					double sx = xs / _totalMeters * w;
					double ex = xe / _totalMeters * w;
					var rect = new Rectangle
					{
						Width = Math.Max(1, ex - sx),
						Height = h,
						Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x00, 0x00))
					};
					Canvas.Children.Add(rect);
					Canvas.SetLeft(rect, sx);
					Canvas.SetTop(rect, 0);
				}
			}
			// Draw terrain in coarse mode
			if (_terrain.Count >= 2)
			{
				var terr = new Polyline { Stroke = Brushes.DimGray, StrokeThickness = 1.5 };
				var zMin = _terrain.Min(t => t.z);
				var zMax = _terrain.Max(t => t.z);
				var zSpan = Math.Max(1, zMax - zMin);
				foreach (var (x, z) in _terrain)
				{
					double sx = x / _totalMeters * w;
					double sy = yMid - ((z - zMin) / zSpan) * (h * 0.6 - 20) + 20;
					terr.Points.Add(new Point(sx, sy));
				}
				Canvas.Children.Add(terr);
			}
		}

		// Draw midline (LOS)
		var los = new Line { X1 = 0, X2 = w, Y1 = yMid, Y2 = yMid, Stroke = Brushes.Gray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 2, 2 } };
		Canvas.Children.Add(los);

		// Draw terrain
		if (_terrain.Count >= 2)
		{
			var terr = new Polyline { Stroke = Brushes.DimGray, StrokeThickness = 1.5 };
			foreach (var (x, z) in _terrain)
			{
				double sx = x / _totalMeters * w;
				double sy = yMid - (z / (Math.Max(1, _terrain.Max(t => t.z) - _terrain.Min(t => t.z))) * (h * 0.6 - 20)) + 20;
				terr.Points.Add(new Point(sx, sy));
			}
			Canvas.Children.Add(terr);
		}


		// Build top and bottom polylines of Fresnel tube
		var top = new Polyline { Stroke = Brushes.SteelBlue, StrokeThickness = 1.5 };
		var bot = new Polyline { Stroke = Brushes.SteelBlue, StrokeThickness = 1.5 };
		var clrTop = new Polyline { Stroke = Brushes.OrangeRed, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 2 } };
		var clrBot = new Polyline { Stroke = Brushes.OrangeRed, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 2 } };

		foreach (var (x, r) in _samples)
		{
			double sx = x / _totalMeters * w;
			double ry = r * yScale;
			double cr = r * (_clearancePct / 100.0);
			double cry = cr * yScale;
			top.Points.Add(new Point(sx, yMid - ry));
			bot.Points.Add(new Point(sx, yMid + ry));
			clrTop.Points.Add(new Point(sx, yMid - cry));
			clrBot.Points.Add(new Point(sx, yMid + cry));
		}

		Canvas.Children.Add(top);
		Canvas.Children.Add(bot);
		Canvas.Children.Add(clrTop);
		Canvas.Children.Add(clrBot);

		//// Red line across violating spans at LOS level for emphasis
		//if (_violations.Count > 0)
		//{
		//	foreach (var (xs, xe) in _violations)
		//	{
		//		double sx = xs / _totalMeters * w;
		//		double ex = xe / _totalMeters * w;
		//		var vline = new Line { X1 = sx, X2 = ex, Y1 = yMid, Y2 = yMid, Stroke = Brushes.Red, StrokeThickness = 2.0 };
		//		Canvas.Children.Add(vline);
		//	}
		//}


		DrawAxes(w, h);

		Title.Text = "Fresnel Profile";
		Subtitle.Text = $"Max R1≈{_samples.Max(s => s.r):0.##} m, clearance={_clearancePct:0.#}%";
	}

	private void DrawAxes(double w, double h)
	{
		// X axis labels (0 km, mid, end)
		var yBase = h - 16;
		AddText("0 km", 4, yBase);
		AddText($"{_totalMeters / 2000:0.##} km", w / 2 - 20, yBase);
		AddText($"{_totalMeters / 1000:0.##} km", w - 60, yBase);
	}

	private void AddText(string text, double x, double y)
	{
		var tb = new TextBlock { Text = text, Foreground = Brushes.Gray };
		Canvas.Children.Add(tb);
		Canvas.SetLeft(tb, x);
		Canvas.SetTop(tb, y);
	}
}