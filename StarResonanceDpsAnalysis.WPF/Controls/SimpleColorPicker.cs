using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StarResonanceDpsAnalysis.WPF.Controls;

[TemplatePart(Name = PartSvCanvas, Type = typeof(Rectangle))]
[TemplatePart(Name = PartSvThumb, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PartHueBar, Type = typeof(Rectangle))]
[TemplatePart(Name = PartHueThumb, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PartHexTextBox, Type = typeof(TextBox))]
public class SimpleColorPicker : Control
{
    private const string PartSvCanvas = "PART_SV_Canvas";
    private const string PartSvThumb = "PART_SV_Thumb";
    private const string PartHueBar = "PART_Hue_Bar";
    private const string PartHueThumb = "PART_Hue_Thumb";
    private const string PartHexTextBox = "PART_HexTextBox";

    private Rectangle? _svCanvas;
    private FrameworkElement? _svThumb;
    private Rectangle? _hueBar;
    private FrameworkElement? _hueThumb;
    private TextBox? _hexTextBox;

    // 実際にマウス入力を受ける要素
    // PART_SV_Canvas / PART_Hue_Bar の上に他要素が重なっているため、
    // 親要素側でイベントを拾う
    private UIElement? _svInputElement;
    private UIElement? _hueInputElement;

    private bool _isDraggingSv;
    private bool _isDraggingHue;
    private bool _isUpdatingHexText;
    private bool _isInternalHsvUpdate;

    private double _hue;        // 0 - 360
    private double _saturation; // 0 - 1
    private double _value;      // 0 - 1

    private string _lastValidHexText = "#FFFFFF";

    static SimpleColorPicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SimpleColorPicker),
            new FrameworkPropertyMetadata(typeof(SimpleColorPicker)));
    }

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(SimpleColorPicker),
            new FrameworkPropertyMetadata(
                Colors.White,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public override void OnApplyTemplate()
    {
        UnhookTemplatePartEvents();

        base.OnApplyTemplate();

        _svCanvas = GetTemplateChild(PartSvCanvas) as Rectangle;
        _svThumb = GetTemplateChild(PartSvThumb) as FrameworkElement;
        _hueBar = GetTemplateChild(PartHueBar) as Rectangle;
        _hueThumb = GetTemplateChild(PartHueThumb) as FrameworkElement;
        _hexTextBox = GetTemplateChild(PartHexTextBox) as TextBox;

        _svInputElement = _svCanvas?.Parent as UIElement ?? _svCanvas;
        _hueInputElement = _hueBar?.Parent as UIElement ?? _hueBar;

        HookTemplatePartEvents();

        UpdateHsvFromColor(SelectedColor);
        UpdateAllVisuals();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SimpleColorPicker picker || e.NewValue is not Color color)
            return;

        // 自分でHSVから色を更新した直後は、RGB→HSVへ戻し直さない
        // これでHueドラッグ時の丸め誤差によるカクつきを減らす
        if (!picker._isInternalHsvUpdate)
        {
            picker.UpdateHsvFromColor(color);
        }

        picker.UpdateAllVisuals();
    }

    private void HookTemplatePartEvents()
    {
        if (_svInputElement != null)
        {
            _svInputElement.MouseLeftButtonDown += SvInputElement_MouseLeftButtonDown;
            _svInputElement.MouseMove += SvInputElement_MouseMove;
            _svInputElement.MouseLeftButtonUp += SvInputElement_MouseLeftButtonUp;
            _svInputElement.MouseLeave += SvInputElement_MouseLeave;
        }

        if (_hueInputElement != null)
        {
            _hueInputElement.MouseLeftButtonDown += HueInputElement_MouseLeftButtonDown;
            _hueInputElement.MouseMove += HueInputElement_MouseMove;
            _hueInputElement.MouseLeftButtonUp += HueInputElement_MouseLeftButtonUp;
            _hueInputElement.MouseLeave += HueInputElement_MouseLeave;
        }

        if (_svCanvas != null)
        {
            _svCanvas.SizeChanged += Part_SizeChanged;
        }

        if (_hueBar != null)
        {
            _hueBar.SizeChanged += Part_SizeChanged;
        }

        if (_hexTextBox != null)
        {
            _hexTextBox.PreviewKeyDown += HexTextBox_PreviewKeyDown;
            _hexTextBox.LostFocus += HexTextBox_LostFocus;
        }
    }

    private void UnhookTemplatePartEvents()
    {
        if (_svInputElement != null)
        {
            _svInputElement.MouseLeftButtonDown -= SvInputElement_MouseLeftButtonDown;
            _svInputElement.MouseMove -= SvInputElement_MouseMove;
            _svInputElement.MouseLeftButtonUp -= SvInputElement_MouseLeftButtonUp;
            _svInputElement.MouseLeave -= SvInputElement_MouseLeave;
        }

        if (_hueInputElement != null)
        {
            _hueInputElement.MouseLeftButtonDown -= HueInputElement_MouseLeftButtonDown;
            _hueInputElement.MouseMove -= HueInputElement_MouseMove;
            _hueInputElement.MouseLeftButtonUp -= HueInputElement_MouseLeftButtonUp;
            _hueInputElement.MouseLeave -= HueInputElement_MouseLeave;
        }

        if (_svCanvas != null)
        {
            _svCanvas.SizeChanged -= Part_SizeChanged;
        }

        if (_hueBar != null)
        {
            _hueBar.SizeChanged -= Part_SizeChanged;
        }

        if (_hexTextBox != null)
        {
            _hexTextBox.PreviewKeyDown -= HexTextBox_PreviewKeyDown;
            _hexTextBox.LostFocus -= HexTextBox_LostFocus;
        }
    }

    private void Part_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateThumbPositions();
    }

    private void SvInputElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_svInputElement == null)
            return;

        _isDraggingSv = true;
        _svInputElement.CaptureMouse();
        UpdateSvFromPoint(e.GetPosition(_svInputElement));
    }

    private void SvInputElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSv || _svInputElement == null)
            return;

        UpdateSvFromPoint(e.GetPosition(_svInputElement));
    }

    private void SvInputElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_svInputElement == null)
            return;

        _isDraggingSv = false;
        _svInputElement.ReleaseMouseCapture();
    }

    private void SvInputElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_svInputElement == null || e.LeftButton != MouseButtonState.Released)
            return;

        _isDraggingSv = false;
        _svInputElement.ReleaseMouseCapture();
    }

    private void HueInputElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_hueInputElement == null)
            return;

        _isDraggingHue = true;
        _hueInputElement.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(_hueInputElement));
    }

    private void HueInputElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingHue || _hueInputElement == null)
            return;

        UpdateHueFromPoint(e.GetPosition(_hueInputElement));
    }

    private void HueInputElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_hueInputElement == null)
            return;

        _isDraggingHue = false;
        _hueInputElement.ReleaseMouseCapture();
    }

    private void HueInputElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_hueInputElement == null || e.LeftButton != MouseButtonState.Released)
            return;

        _isDraggingHue = false;
        _hueInputElement.ReleaseMouseCapture();
    }

    private void UpdateSvFromPoint(Point point)
    {
        var width = Math.Max(1.0, GetSvWidth());
        var height = Math.Max(1.0, GetSvHeight());

        var x = Clamp(point.X, 0, width);
        var y = Clamp(point.Y, 0, height);

        _saturation = x / width;
        _value = 1.0 - (y / height);

        ApplyCurrentHsvToSelectedColor();
    }

    private void UpdateHueFromPoint(Point point)
    {
        var height = Math.Max(1.0, GetHueHeight());
        var y = Clamp(point.Y, 0, height);

        _hue = (1.0 - (y / height)) * 360.0;
        if (_hue >= 360.0)
            _hue = 359.999;

        ApplyCurrentHsvToSelectedColor();
    }

    private void ApplyCurrentHsvToSelectedColor()
    {
        var newColor = ColorFromHsv(_hue, _saturation, _value);

        try
        {
            _isInternalHsvUpdate = true;

            var oldColor = SelectedColor;
            SelectedColor = newColor;

            // 灰色系では Hue を動かしても RGB が同じになり得る
            // その場合でも Thumb / 背景は更新する
            if (oldColor.Equals(newColor))
            {
                UpdateAllVisuals();
            }
        }
        finally
        {
            _isInternalHsvUpdate = false;
        }
    }

    private void UpdateAllVisuals()
    {
        UpdateSvBaseColor();
        UpdateThumbPositions();
        UpdateHexTextFromSelectedColor();
    }

    private void UpdateSvBaseColor()
    {
        if (_svCanvas == null)
            return;

        _svCanvas.Fill = new SolidColorBrush(ColorFromHsv(_hue, 1.0, 1.0));
    }

    private void UpdateThumbPositions()
    {
        if (_svThumb != null)
        {
            var width = Math.Max(1.0, GetSvWidth());
            var height = Math.Max(1.0, GetSvHeight());

            var thumbWidth = _svThumb.ActualWidth > 0 ? _svThumb.ActualWidth : _svThumb.Width;
            var thumbHeight = _svThumb.ActualHeight > 0 ? _svThumb.ActualHeight : _svThumb.Height;

            var x = _saturation * width;
            var y = (1.0 - _value) * height;

            Canvas.SetLeft(_svThumb, x - thumbWidth / 2.0);
            Canvas.SetTop(_svThumb, y - thumbHeight / 2.0);
        }

        if (_hueThumb != null)
        {
            var height = Math.Max(1.0, GetHueHeight());
            var thumbHeight = _hueThumb.ActualHeight > 0 ? _hueThumb.ActualHeight : _hueThumb.Height;

            var y = (1.0 - (_hue / 360.0)) * height;
            Canvas.SetTop(_hueThumb, y - thumbHeight / 2.0);
        }
    }

    private double GetSvWidth()
    {
        if (_svInputElement is FrameworkElement fe && fe.ActualWidth > 0)
            return fe.ActualWidth;

        return _svCanvas?.ActualWidth ?? 1.0;
    }

    private double GetSvHeight()
    {
        if (_svInputElement is FrameworkElement fe && fe.ActualHeight > 0)
            return fe.ActualHeight;

        return _svCanvas?.ActualHeight ?? 1.0;
    }

    private double GetHueHeight()
    {
        if (_hueInputElement is FrameworkElement fe && fe.ActualHeight > 0)
            return fe.ActualHeight;

        return _hueBar?.ActualHeight ?? 1.0;
    }

    private void UpdateHsvFromColor(Color color)
    {
        RgbToHsv(color, _hue, out var hue, out var saturation, out var value);
    }

    private void UpdateHexTextFromSelectedColor()
    {
        var hex = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        _lastValidHexText = hex;

        if (_hexTextBox == null)
            return;

        try
        {
            _isUpdatingHexText = true;
            _hexTextBox.Text = hex;
            _hexTextBox.CaretIndex = _hexTextBox.Text.Length;
        }
        finally
        {
            _isUpdatingHexText = false;
        }
    }

    private void HexTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_hexTextBox == null)
            return;

        if (e.Key == Key.Enter)
        {
            ApplyHexOrRevert();
            e.Handled = true;
        }
    }

    private void HexTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingHexText)
            return;

        ApplyHexOrRevert();
    }

    private void ApplyHexOrRevert()
    {
        if (_hexTextBox == null)
            return;

        if (!TryNormalizeHexInput(_hexTextBox.Text, out var normalizedHex))
        {
            RevertHexText();
            return;
        }

        if (!TryParseHex6(normalizedHex, out var color))
        {
            RevertHexText();
            return;
        }

        try
        {
            _isUpdatingHexText = true;
            SelectedColor = color;
            _lastValidHexText = normalizedHex;
            _hexTextBox.Text = normalizedHex;
            _hexTextBox.CaretIndex = _hexTextBox.Text.Length;
        }
        finally
        {
            _isUpdatingHexText = false;
        }
    }

    private void RevertHexText()
    {
        if (_hexTextBox == null)
            return;

        try
        {
            _isUpdatingHexText = true;
            _hexTextBox.Text = _lastValidHexText;
            _hexTextBox.CaretIndex = _hexTextBox.Text.Length;
        }
        finally
        {
            _isUpdatingHexText = false;
        }
    }

    private static bool TryNormalizeHexInput(string raw, out string normalizedHex)
    {
        normalizedHex = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();

        if (text.StartsWith("#", StringComparison.Ordinal))
            text = text[1..];

        // 入力中は自由だが、確定時はここで検証
        foreach (var ch in text)
        {
            if (!IsHexChar(ch))
                return false;
        }

        // 7文字目以降はカット
        if (text.Length > 6)
            text = text[..6];

        if (text.Length != 6)
            return false;

        normalizedHex = "#" + text.ToUpperInvariant();
        return true;
    }

    private static bool TryParseHex6(string normalizedHex, out Color color)
    {
        color = Colors.White;

        if (string.IsNullOrWhiteSpace(normalizedHex))
            return false;

        var text = normalizedHex.StartsWith("#", StringComparison.Ordinal)
            ? normalizedHex[1..]
            : normalizedHex;

        if (text.Length != 6)
            return false;

        if (!byte.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r))
            return false;
        if (!byte.TryParse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g))
            return false;
        if (!byte.TryParse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            return false;

        color = Color.FromRgb(r, g, b);
        return true;
    }

    private static bool IsHexChar(char ch)
    {
        return (ch >= '0' && ch <= '9') ||
               (ch >= 'A' && ch <= 'F') ||
               (ch >= 'a' && ch <= 'f');
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        hue %= 360.0;
        if (hue < 0)
            hue += 360.0;

        saturation = Clamp(saturation, 0.0, 1.0);
        value = Clamp(value, 0.0, 1.0);

        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60.0 % 2) - 1));
        var m = value - c;

        double r1, g1, b1;

        if (hue < 60)
        {
            r1 = c; g1 = x; b1 = 0;
        }
        else if (hue < 120)
        {
            r1 = x; g1 = c; b1 = 0;
        }
        else if (hue < 180)
        {
            r1 = 0; g1 = c; b1 = x;
        }
        else if (hue < 240)
        {
            r1 = 0; g1 = x; b1 = c;
        }
        else if (hue < 300)
        {
            r1 = x; g1 = 0; b1 = c;
        }
        else
        {
            r1 = c; g1 = 0; b1 = x;
        }

        var r = (byte)Math.Round((r1 + m) * 255);
        var g = (byte)Math.Round((g1 + m) * 255);
        var b = (byte)Math.Round((b1 + m) * 255);

        return Color.FromRgb(r, g, b);
    }

    private static void RgbToHsv(Color color, double preserveHueWhenUndefined, out double hue, out double saturation, out double value)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        hue = 0.0;

        if (delta > 0.00001)
        {
            if (Math.Abs(max - r) < 0.00001)
            {
                hue = 60.0 * (((g - b) / delta) % 6.0);
            }
            else if (Math.Abs(max - g) < 0.00001)
            {
                hue = 60.0 * (((b - r) / delta) + 2.0);
            }
            else
            {
                hue = 60.0 * (((r - g) / delta) + 4.0);
            }
        }

        if (hue < 0.0)
            hue += 360.0;

        saturation = max <= 0.0 ? 0.0 : delta / max;
        value = max;
    }
}