using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AntennasCalculator.WpfApp.Controls;

public partial class ProfileView : UserControl
{
	private readonly List<(double x, double r)> _samples = new();
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

	private void Redraw()
	{
		if (Canvas is null) return;
		Canvas.Children.Clear();

		double w = Math.Max(1, Canvas.ActualWidth);
		double h = Math.Max(1, Canvas.ActualHeight);

		if (_samples.Count < 2)
		{
			DrawAxes(w, h);
			return;
		}

		// Compute scaling: X=0.._totalMeters => 0..w, Y uses max radius*1.2
		var maxR = _samples.Max(s => s.r);
		if (maxR <= 0) maxR = 1;
		double yScale = (h * 0.4) / maxR; // 40% of height up and down
		double yMid = h * 0.5;

		// Draw midline (LOS)
		var los = new Line { X1 = 0, X2 = w, Y1 = yMid, Y2 = yMid, Stroke = Brushes.Gray, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 2, 2 } };
		Canvas.Children.Add(los);

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