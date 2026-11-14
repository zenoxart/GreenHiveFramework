namespace GreenHive.Wpf.Framework.Mvvm;

/// <summary>Responsive width classes, based on the space a view actually gets (not the screen).</summary>
public enum SizeClass
{
    /// <summary>&lt; 640 epx: one column, navigation collapsed, panels as overlays.</summary>
    Compact,

    /// <summary>640-1007 epx: two columns where useful.</summary>
    Medium,

    /// <summary>1008-1919 epx: full layout.</summary>
    Expanded,

    /// <summary>&gt;= 1920 epx: extra columns.</summary>
    Wide,
}

/// <summary>Responsive height classes.</summary>
public enum HeightClass
{
    Short,
    Regular,
}

/// <summary>Implemented by ViewModels; the hosting frame or window pushes the classes in.</summary>
public interface IResponsiveView
{
    SizeClass SizeClass { get; set; }

    HeightClass HeightClass { get; set; }
}

/// <summary>Breakpoints and size-class calculation with hysteresis.</summary>
public static class SizeClasses
{
    public const double MediumMinWidth = 640;
    public const double ExpandedMinWidth = 1008;
    public const double WideMinWidth = 1920;
    public const double RegularMinHeight = 500;

    private static readonly double[] Breakpoints = [MediumMinWidth, ExpandedMinWidth, WideMinWidth];

    /// <summary>
    /// Computes the class for a width. With a previous class, a breakpoint must be passed by
    /// <paramref name="hysteresis"/> pixels before the class changes, so layouts do not flicker.
    /// </summary>
    public static SizeClass FromWidth(double width, SizeClass? previous = null, double hysteresis = 8)
    {
        var result = 0;
        for (var i = 0; i < Breakpoints.Length; i++)
        {
            var threshold = Breakpoints[i];
            if (previous is { } p)
            {
                threshold += (int)p > i ? -hysteresis : hysteresis;
            }

            if (width >= threshold)
            {
                result = i + 1;
            }
        }

        return (SizeClass)result;
    }

    public static HeightClass FromHeight(double height) => height < RegularMinHeight ? HeightClass.Short : HeightClass.Regular;
}
