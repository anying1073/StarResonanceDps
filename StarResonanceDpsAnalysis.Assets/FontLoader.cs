using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.Assets
{
    public static class FontLoader
    {
        private static readonly PrivateFontCollection _pfc = new();
        private static readonly Dictionary<string, FontFamily> _fontFamilyCache = [];
        private static readonly Dictionary<(FontFamily, float, FontStyle), Font> _fontCache = [];

        public static FontFamily LoadFontFamilyFromBytesAndCache(string fontName, byte[] bytes)
        {
            try
            {
                if (!_fontFamilyCache.TryGetValue(fontName, out var fontFamily))
                {
                    fontFamily = LoadFontFamilyFromBytes(bytes, fontName);
                    _fontFamilyCache[fontName] = fontFamily;
                }
                return fontFamily!;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"字体从内存转换时出错: {fontName} {ex.Message}\r\n{ex.StackTrace}");

                return FontFamily.GenericSansSerif;
            }
        }

        public static Font LoadFontFromBytesAndCache(string fontName, byte[] bytes, float fontSize, FontStyle fontStyle = FontStyle.Regular)
        {
            try
            {
                var fontFamily = LoadFontFamilyFromBytesAndCache(fontName, bytes);

                if (!_fontCache.TryGetValue((fontFamily, fontSize, fontStyle), out var font))
                {
                    font = new Font(fontFamily, fontSize, fontStyle);
                    _fontCache[(fontFamily, fontSize, fontStyle)] = font;
                }

                return font;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"字体从内存转换时出错: {fontName}({fontSize}) {ex.Message}\r\n{ex.StackTrace}");

                return SystemFonts.DefaultFont;
            }
        }

        private static FontFamily LoadFontFamilyFromBytes(byte[] bytes, string fontName)
        {
            var fontPtr = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, fontPtr, bytes.Length);

            try
            {
                _pfc.AddMemoryFont(fontPtr, bytes.Length);

                for (var i = 0; i < _pfc.Families.Length; ++i)
                {
                    if (_pfc.Families[i].Name == fontName)
                    {
                        return _pfc.Families[i];
                    }
                }

                return FontFamily.GenericSansSerif;
            }
            finally
            {
                Marshal.FreeCoTaskMem(fontPtr);
            }
        }
    }
}
