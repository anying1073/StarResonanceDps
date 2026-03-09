using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StarResonanceDpsAnalysis.WPF.Converters;

public sealed class OutsideImageAreaClipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3)
            return Geometry.Empty;

        if (values[0] is not double containerWidth || containerWidth <= 0 ||
            values[1] is not double containerHeight || containerHeight <= 0)
        {
            return Geometry.Empty;
        }

        var imagePath = values[2] as string;

        // 画像がないときは全面を描画
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return new RectangleGeometry(new Rect(0, 0, containerWidth, containerHeight));
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
            {
                return new RectangleGeometry(new Rect(0, 0, containerWidth, containerHeight));
            }

            double imageWidth = bitmap.PixelWidth;
            double imageHeight = bitmap.PixelHeight;

            // Image Stretch="Uniform" と同じ計算
            double scale = Math.Min(containerWidth / imageWidth, containerHeight / imageHeight);
            double drawnWidth = imageWidth * scale;
            double drawnHeight = imageHeight * scale;

            double left = (containerWidth - drawnWidth) / 2.0;
            double top = (containerHeight - drawnHeight) / 2.0;
            double right = left + drawnWidth;
            double bottom = top + drawnHeight;

            var group = new GeometryGroup();

            // 上
            if (top > 0)
                group.Children.Add(new RectangleGeometry(new Rect(0, 0, containerWidth, top)));

            // 下
            if (bottom < containerHeight)
                group.Children.Add(new RectangleGeometry(new Rect(0, bottom, containerWidth, containerHeight - bottom)));

            // 左
            if (left > 0 && drawnHeight > 0)
                group.Children.Add(new RectangleGeometry(new Rect(0, top, left, drawnHeight)));

            // 右
            if (right < containerWidth && drawnHeight > 0)
                group.Children.Add(new RectangleGeometry(new Rect(right, top, containerWidth - right, drawnHeight)));

            group.Freeze();
            return group;
        }
        catch
        {
            return new RectangleGeometry(new Rect(0, 0, containerWidth, containerHeight));
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}