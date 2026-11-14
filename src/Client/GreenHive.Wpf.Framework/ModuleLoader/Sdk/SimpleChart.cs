using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GreenHive.Wpf.Framework.Theming;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

public sealed record ChartValue(string Label, decimal Value);

/// <summary>A small bar or line chart drawn on a canvas; redraws on resize.</summary>
public sealed class SimpleChart : UserControl
{
    private readonly Canvas _canvas = new() { ClipToBounds = true };
    private IReadOnlyList<ChartValue> _values = [];
    private bool _line;

    public SimpleChart()
    {
        Content = _canvas;
        MinHeight = 180;
        SizeChanged += (_, _) => Draw();
    }

    public void SetValues(IReadOnlyList<ChartValue> values, bool line)
    {
        _values = values;
        _line = line;
        Draw();
    }

    private void Draw()
    {
        _canvas.Children.Clear();
        var width = ActualWidth;
        var height = ActualHeight;
        if (_values.Count == 0 || width < 40 || height < 60)
        {
            return;
        }

        const double labelHeight = 22;
        const double top = 18;
        var plotHeight = height - labelHeight - top;
        var max = (double)Math.Max(_values.Max(v => v.Value), 0.0001m);
        var slot = width / _values.Count;
        var accent = Theme.BrandBrush;
        var text = Theme.Resolve(Theme.TextSecondary);
        var points = new PointCollection();

        for (var i = 0; i < _values.Count; i++)
        {
            var value = _values[i];
            var barHeight = Math.Max(1, (double)value.Value / max * plotHeight);
            var x = i * slot;
            if (_line)
            {
                points.Add(new Point(x + (slot / 2), top + plotHeight - barHeight));
                var dot = new Ellipse { Width = 6, Height = 6, Fill = accent, ToolTip = $"{value.Label}: {value.Value:N2}" };
                Canvas.SetLeft(dot, x + (slot / 2) - 3);
                Canvas.SetTop(dot, top + plotHeight - barHeight - 3);
                _canvas.Children.Add(dot);
            }
            else
            {
                var bar = new Rectangle { Width = Math.Max(4, slot * 0.6), Height = barHeight, Fill = accent, RadiusX = 3, RadiusY = 3, ToolTip = $"{value.Label}: {value.Value:N2}" };
                Canvas.SetLeft(bar, x + (slot * 0.2));
                Canvas.SetTop(bar, top + plotHeight - barHeight);
                _canvas.Children.Add(bar);
            }

            var label = new TextBlock { Text = value.Label, FontSize = 11, Foreground = text, Width = slot, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, height - labelHeight + 4);
            _canvas.Children.Add(label);
        }

        if (_line && points.Count > 1)
        {
            _canvas.Children.Insert(0, new Polyline { Points = points, Stroke = accent, StrokeThickness = 2 });
        }

        _canvas.Children.Add(new TextBlock { Text = max.ToString("N0"), FontSize = 11, Foreground = text });
    }
}
