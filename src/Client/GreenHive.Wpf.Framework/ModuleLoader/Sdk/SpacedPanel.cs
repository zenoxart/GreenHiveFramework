using System.Windows;
using System.Windows.Controls;

namespace GreenHive.Wpf.Framework.ModuleLoader.Sdk;

/// <summary>
/// A stack panel with <see cref="Spacing"/> between the children and inner <see cref="Padding"/> (the WPF StackPanel
/// has neither). Collapsed children take no space and no gap.
/// </summary>
public class SpacedPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(SpacedPanel), new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(SpacedPanel), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(Thickness), typeof(SpacedPanel), new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var horizontal = Orientation == Orientation.Horizontal;
        var inner = new Size(
            Math.Max(0, availableSize.Width - padding.Left - padding.Right),
            Math.Max(0, availableSize.Height - padding.Top - padding.Bottom));
        var childAvailable = horizontal ? new Size(double.PositiveInfinity, inner.Height) : new Size(inner.Width, double.PositiveInfinity);

        double along = 0;
        double across = 0;
        var visible = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(childAvailable);
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            if (visible++ > 0)
            {
                along += Spacing;
            }

            along += horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
            across = Math.Max(across, horizontal ? child.DesiredSize.Height : child.DesiredSize.Width);
        }

        return horizontal
            ? new Size(along + padding.Left + padding.Right, across + padding.Top + padding.Bottom)
            : new Size(across + padding.Left + padding.Right, along + padding.Top + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        var horizontal = Orientation == Orientation.Horizontal;
        double offset = horizontal ? padding.Left : padding.Top;
        var crossSize = horizontal
            ? Math.Max(0, finalSize.Height - padding.Top - padding.Bottom)
            : Math.Max(0, finalSize.Width - padding.Left - padding.Right);
        var visible = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(new Rect(0, 0, 0, 0));
                continue;
            }

            if (visible++ > 0)
            {
                offset += Spacing;
            }

            if (horizontal)
            {
                child.Arrange(new Rect(offset, padding.Top, child.DesiredSize.Width, crossSize));
                offset += child.DesiredSize.Width;
            }
            else
            {
                child.Arrange(new Rect(padding.Left, offset, crossSize, child.DesiredSize.Height));
                offset += child.DesiredSize.Height;
            }
        }

        return finalSize;
    }
}
