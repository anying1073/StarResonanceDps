using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace StarResonanceDpsAnalysis.WPF.Converters;

public class ModifierKeysTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str)
        {
            if (Enum.TryParse<ModifierKeys>(str, out var result))
            {
                return result;
            }
            return ModifierKeys.None;
        }
        return base.ConvertFrom(context, culture, value);
    }
}