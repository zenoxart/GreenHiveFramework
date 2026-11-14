using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GreenHive.Wpf.Framework.Commands;
using GreenHive.Wpf.Framework.Theming;
using GreenHive.Wpf.Framework.XamlMetadata;
using Path = System.Windows.Shapes.Path;

namespace GreenHive.Wpf.Framework.Navigation;

/// <summary>
/// The radial navigation wheel control: inner ring = modules, outer ring = open documents and floating
/// views; the selected segment is drawn in the Green Hive green. Pointer direction selects, click confirms.
/// </summary>
public sealed class NavigationWheel : Grid
{
    private const double Size = 460;
    private const double Center = Size / 2;
    private const double InnerFrom = 70;
    private const double InnerTo = 150;
    private const double OuterFrom = 156;
    private const double OuterTo = 222;
    private const double Gap = 1.2;

    private readonly NavigationWheelViewModel _viewModel;
    private readonly IconCatalog? _icons;
    private readonly Canvas _canvas = new() { Width = Size, Height = Size, Background = Brushes.Transparent };
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, FontSize = 16, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 120, MaxHeight = 44 };
    private readonly TextBlock _subtitle = new() { FontSize = 12, Opacity = 0.7, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 120 };
    private readonly TextBlock _hint = new() { FontSize = 12, Opacity = 0.85, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 440, Margin = new Thickness(0, 8, 0, 0) };

    public NavigationWheel(NavigationWheelViewModel viewModel, IconCatalog? icons)
    {
        _viewModel = viewModel;
        _icons = icons;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var centerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        centerPanel.Children.Add(_title);
        centerPanel.Children.Add(_subtitle);
        Children.Add(_canvas);
        Children.Add(centerPanel);
        Ui.SetText(_hint, "shell:wheel.hint");
        SetRow(_hint, 1);
        Children.Add(_hint);

        AutomationProperties.SetName(this, "Navigation wheel");
        _canvas.MouseMove += OnMouseMove;
        _canvas.MouseLeftButtonUp += (_, e) =>
        {
            _viewModel.Confirm();
            e.Handled = true;
        };
        _viewModel.Changed += (_, _) => Redraw();
    }

    private static bool IsDarkTheme =>
        Theme.Resolve(Theme.TextPrimary) is SolidColorBrush text && (text.Color.R + text.Color.G + text.Color.B) / 3 > 128;

    private static Point PointAt(double radius, double angle)
    {
        var radians = angle * Math.PI / 180;
        return new Point(Center + (radius * Math.Sin(radians)), Center - (radius * Math.Cos(radians)));
    }

    private static Path CreateSegment(double from, double to, double innerRadius, double outerRadius, Brush fill, Brush stroke)
    {
        var sweep = to - from;
        var figure = new PathFigure { StartPoint = PointAt(outerRadius, from), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(PointAt(outerRadius, to), new Size(outerRadius, outerRadius), 0, sweep > 180, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(PointAt(innerRadius, to), true));
        figure.Segments.Add(new ArcSegment(PointAt(innerRadius, from), new Size(innerRadius, innerRadius), 0, sweep > 180, SweepDirection.Counterclockwise, true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Path { Data = geometry, Fill = fill, Stroke = stroke, StrokeThickness = 1 };
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        var dark = IsDarkTheme;
        var background = new SolidColorBrush(dark ? Color.FromArgb(242, 32, 32, 32) : Color.FromArgb(245, 249, 249, 249));
        var segmentFill = new SolidColorBrush(dark ? Color.FromArgb(255, 45, 45, 45) : Colors.White);
        var stroke = new SolidColorBrush(dark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(50, 0, 0, 0));
        var selectedFill = Theme.BrandBrush;
        var text = new SolidColorBrush(dark ? Colors.White : Colors.Black);
        var selectedText = Brushes.White;
        _title.Foreground = text;
        _subtitle.Foreground = text;
        _hint.Foreground = Brushes.White;

        var hasOuter = _viewModel.Segments.Any(s => s.Ring == 1);
        var radius = (hasOuter ? OuterTo : InnerTo) + 8;
        var disc = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = background, Stroke = stroke, StrokeThickness = 1 };
        Canvas.SetLeft(disc, Center - radius);
        Canvas.SetTop(disc, Center - radius);
        _canvas.Children.Add(disc);

        for (var ring = 0; ring <= 1; ring++)
        {
            var segments = _viewModel.Segments.Where(s => s.Ring == ring).ToList();
            if (segments.Count == 0)
            {
                continue;
            }

            var (from, to) = ring == 0 ? (InnerFrom, InnerTo) : (OuterFrom, OuterTo);
            var sweep = 360.0 / segments.Count;
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                var isSelected = ReferenceEquals(segment, _viewModel.Selected);
                var start = (i * sweep) - (sweep / 2) + Gap;
                var end = (i * sweep) + (sweep / 2) - Gap;
                if (segments.Count == 1)
                {
                    start = -179.9;
                    end = 179.9;
                }

                _canvas.Children.Add(CreateSegment(start, end, from, to, isSelected ? selectedFill : segmentFill, stroke));
                AddLabel(segment, i * sweep, (from + to) / 2, isSelected ? selectedText : text, ring == 0, segments.Count);
            }
        }

        _title.Text = _viewModel.CenterTitle;
        _subtitle.Text = _viewModel.CenterSubtitle;
    }

    private void AddLabel(WheelSegment segment, double angle, double radius, Brush foreground, bool showText, int count)
    {
        var point = PointAt(radius, angle);
        var panel = new StackPanel { Width = 84, IsHitTestVisible = false };
        var icon = _icons?.CreateIcon(segment.IsFloating ? "detach" : segment.Icon, 18) ?? Theme.Glyph("\uE8A5", 18);
        if (icon is TextBlock glyph)
        {
            glyph.Foreground = foreground;
        }

        icon.HorizontalAlignment = HorizontalAlignment.Center;
        panel.Children.Add(icon);
        if (showText || count <= 8)
        {
            panel.Children.Add(new TextBlock
            {
                Text = segment.Label,
                Foreground = foreground,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        Canvas.SetLeft(panel, point.X - 42);
        Canvas.SetTop(panel, point.Y - 18);
        _canvas.Children.Add(panel);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(_canvas);
        var dx = position.X - Center;
        var dy = position.Y - Center;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance < InnerFrom)
        {
            return;
        }

        var angle = (Math.Atan2(dx, -dy) * 180 / Math.PI + 360) % 360;
        _viewModel.SelectAt(distance > OuterFrom - 3 ? 1 : 0, angle);
    }
}
