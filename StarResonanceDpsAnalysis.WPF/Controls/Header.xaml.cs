using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using WPFLocalizeExtension.Engine;

namespace StarResonanceDpsAnalysis.WPF.Controls;

/// <summary>
/// Header.xaml 的交互逻辑
/// </summary>
public partial class Header : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(Header),
            new FrameworkPropertyMetadata(
                "Header",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTitleChanged));

    public Header()
    {
        InitializeComponent();

        // 初回表示時にも適用
        Loaded += (_, _) => ApplyHeaderFontByLanguage();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Header header)
        {
            // ローカライズでタイトル文字列が切り替わった時にフォント再適用
            header.ApplyHeaderFontByLanguage();
        }
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // 親から Language(xml:lang 相当) が変わった場合にも再適用
        if (e.Property == LanguageProperty)
        {
            ApplyHeaderFontByLanguage();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            Window.GetWindow(this)?.DragMove();
    }

    /// <summary>
    /// 現在のUI言語(推定)に応じてヘッダータイトルのフォントを切り替える
    /// </summary>
    private void ApplyHeaderFontByLanguage()
    {
        // Header.xaml 内のタイトル TextBlock を取得（今の実装では最初の TextBlock がタイトル）
        var titleTextBlock = FindVisualChild<TextBlock>(this);
        if (titleTextBlock == null)
            return;

        var culture = ResolveUiCulture();
        var bucket = DetectLanguageBucket(culture, Title);

        switch (bucket)
        {
            case "ko":
                // 韓国語UI
                titleTextBlock.FontFamily = SystemFonts.MessageFontFamily;
                titleTextBlock.FontWeight = FontWeights.Bold;
                titleTextBlock.Language = XmlLanguage.GetLanguage("ko-KR");
                break;

            case "ja":
                // 日本語UI
                titleTextBlock.FontFamily = SystemFonts.MessageFontFamily;
                titleTextBlock.FontWeight = FontWeights.Bold;
                titleTextBlock.Language = XmlLanguage.GetLanguage("ja-JP");
                break;

            default:
                // 中国語系 or その他:
                // Header.xaml の HeaderTitle スタイルに書いてある FontFamily
                // (SAO Welcome TT + 阿里妈妈数黑体) をそのまま使う
                titleTextBlock.ClearValue(TextBlock.FontFamilyProperty);
                titleTextBlock.Language = XmlLanguage.GetLanguage("zh-CN");
                break;
        }
    }

    /// <summary>
    /// WPF の Language → CurrentUICulture の順でUI言語を推定
    /// </summary>
    private CultureInfo ResolveUiCulture()
    {
        // まず WPFLocalizeExtension の UI 言語を使う
        var locCulture = LocalizeDictionary.Instance.Culture;
        if (locCulture != null)
            return locCulture;

        // 次に（必要なら）Language
        try
        {
            var tag = Language?.IetfLanguageTag;
            if (!string.IsNullOrWhiteSpace(tag))
                return CultureInfo.GetCultureInfo(tag);
        }
        catch
        {
            // ignore
        }

        // 最後に thread UI culture
        return CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// 言語コード + タイトル文字種から ja / ko / zh(default) を推定
    /// </summary>
    private static string DetectLanguageBucket(CultureInfo culture, string? title)
    {
        var name = culture.Name ?? string.Empty;

        if (name.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
            return "ko";

        if (name.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            return "ja";

        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return "zh";

        // デフォルトは既存デザイン重視（Header.xaml スタイルの中国語系フォント）
        return "zh";
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T t)
                return t;

            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }
}