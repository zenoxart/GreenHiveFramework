using GreenHive.Wpf.Framework.Mvvm;
using System.Windows;

namespace GreenHive.Wpf.Framework.Windowing;

/// <summary>
/// Makes layouts responsive: observes the size of a layout host, computes its size class with hysteresis
/// and reports changes. Views switch layouts through visual states named after the size classes.
/// </summary>
public sealed class ResponsiveLayoutManager : AppObject
{
    /// <summary>Observes the host and calls <paramref name="onChanged"/> whenever its size class changes.</summary>
    public void Attach(FrameworkElement host, Action<SizeClass, HeightClass> onChanged)
    {
        SizeClass? current = null;
        HeightClass? currentHeight = null;

        void Update(double width, double height)
        {
            if (width <= 0)
            {
                return;
            }

            var sizeClass = SizeClasses.FromWidth(width, current);
            var heightClass = SizeClasses.FromHeight(height);
            if (sizeClass != current || heightClass != currentHeight)
            {
                current = sizeClass;
                currentHeight = heightClass;
                onChanged(sizeClass, heightClass);
            }
        }

        host.SizeChanged += (_, e) => Update(e.NewSize.Width, e.NewSize.Height);
        Update(host.ActualWidth, host.ActualHeight);
    }

    /// <summary>Moves a control to the visual state named after the size class (Compact/Medium/Expanded/Wide).</summary>
    public static void ApplyVisualState(FrameworkElement control, SizeClass sizeClass) =>
        VisualStateManager.GoToState(control, sizeClass.ToString(), true);
}
