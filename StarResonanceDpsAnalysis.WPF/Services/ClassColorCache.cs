using System.Windows;
using System.Windows.Media;
using StarResonanceDpsAnalysis.Core.Models;

namespace StarResonanceDpsAnalysis.WPF.Services;

public static class ClassColorCache
{
    private static readonly Dictionary<Classes, Brush> BrushCache = new();
    private static readonly Dictionary<Classes, Brush> DefaultBrushCache = new();

    public static IReadOnlyDictionary<Classes, Brush> GetCache => BrushCache;
    public static IReadOnlyDictionary<Classes, Brush> GetDefault => DefaultBrushCache;

    private static IEnumerable<Classes> GetClasses()
    {
        var classes = Enum.GetValues<Classes>();

        return classes;
    }

    public static void InitDefaultColors()
    {
        var classes = GetClasses();
        foreach (var cls in classes)
        {
            var brush = GetDefaultBrushByKey(cls);
            if (brush != null)
            {
                if (brush.CanFreeze) brush.Freeze();
                DefaultBrushCache[cls] = brush;
            }
        }
    }

    public static void ResetCache(Classes cls)
    {
        if (!DefaultBrushCache.TryGetValue(cls, out var defaultBrush))
            return;

        if (defaultBrush is SolidColorBrush defaultSolid)
        {
            if (BrushCache.TryGetValue(cls, out var existingBrush) &&
                existingBrush is SolidColorBrush existingSolid &&
                !existingSolid.IsFrozen)
            {
                existingSolid.Color = defaultSolid.Color;
                return;
            }

            BrushCache[cls] = new SolidColorBrush(defaultSolid.Color);
            return;
        }

        BrushCache[cls] = defaultBrush.Clone();
    }

    public static void ResetAllCache()
    {
        var classes = GetClasses();
        foreach (var cls in classes)
        {
            ResetCache(cls);
        }
    }

    private static Brush? GetDefaultBrushByKey(Classes cls)
    {
        var app = Application.Current;
        var keysToTry = new object?[]
        {
            cls,
            $"Classes{cls}Brush",
            $"{cls}Brush",
            $"Classes{cls}Color",
            $"{cls}Color"
        };

        foreach (var key in keysToTry)
        {
            var resource = app?.TryFindResource(key!);
            if (resource is Brush brush)
            {
                // If the resource brush is frozen, we can't update it later.
                // For customizability, we might want to wrap it or clone it if it's solid.
                if (brush is SolidColorBrush { IsFrozen: true } solidBrush)
                {
                    var clone = solidBrush.Clone();
                    return clone;
                }

                return brush;
            }

            if (resource is Color color)
            {
                var solidBrush = new SolidColorBrush(color);
                // Do not freeze if we want to allow updates
                // if (solidBrush.CanFreeze)
                // {
                //     solidBrush.Freeze();
                // }

                return solidBrush;
            }
        }

        return null;
    }

    public static void UpdateColor(Classes classes, Color color)
    {
        if (BrushCache.TryGetValue(classes, out var brush) && brush is SolidColorBrush solidBrush &&
            !solidBrush.IsFrozen)
        {
            solidBrush.Color = color;
        }
        else
        {
            BrushCache[classes] = new SolidColorBrush(color);
        }
    }

    public static void UpdateColors(IDictionary<Classes, string> colors)
    {
        foreach (var (key, value) in colors)
        {
            try
            {
                var cc = (Color)ColorConverter.ConvertFromString(value);
                UpdateColor(key, cc);
            }
            catch
            {
                // Ignore
            }
        }
    }
}