using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WPF.Properties;

namespace StarResonanceDpsAnalysis.WPF.Models;

public enum Language
{
    [LocalizedDescription(ResourcesKeys.Settings_Language_Auto)]
    Auto,
    [LocalizedDescription(ResourcesKeys.Settings_Language_Chinese)]
    [CultureAttribute.zh_CN]
    ZhCn,
    [LocalizedDescription(ResourcesKeys.Settings_Language_English)]
    [CultureAttribute.en_US]
    EnUs,
    [LocalizedDescription(ResourcesKeys.Settings_Language_Portuguese)]
    [CultureAttribute.pt_BR]
    PtBr,
    [LocalizedDescription(ResourcesKeys.Settings_Language_Japanese)]
    [CultureAttribute.ja_JP]
    JaJp,
    [LocalizedDescription("Settings_Language_Korean")]
    [CultureAttribute.ko_KR]
    KoKr
}

public abstract class CultureAttribute(CultureInfo info) : Attribute
{
    public CultureInfo Info { get; } = info;

    // ReSharper disable InconsistentNaming
    public sealed class zh_CN() : CultureAttribute(new CultureInfo("zh-CN"));
    public sealed class en_US() : CultureAttribute(new CultureInfo("en-US"));
    public sealed class pt_BR() : CultureAttribute(new CultureInfo("pt-BR"));
    public sealed class ja_JP() : CultureAttribute(new CultureInfo("ja-JP"));
    public sealed class ko_KR() : CultureAttribute(new CultureInfo("ko-KR"));
    // ReSharper restore InconsistentNaming
}

public static class CultureAttributeExtensions
{
    private static readonly CultureInfo SystemCultureInfo = CultureInfo.CurrentCulture;
    /// <summary>
    /// Gets the CultureInfo associated with a Language enum value
    /// </summary>
    /// <param name="language">The Language enum value</param>
    /// <returns>CultureInfo associated with the language, or null for Auto</returns>
    public static CultureInfo? GetCultureInfo(this Language language)
    {
        if (language == Language.Auto)
            return null;

        var fieldInfo = language.GetType().GetField(language.ToString());
        var attribute = fieldInfo?.GetCustomAttributes<CultureAttribute>(false).FirstOrDefault();
        Debug.Assert(attribute != null, nameof(attribute) + " != null");
        return attribute.Info;
    }

    /// <summary>
    /// Gets the CultureInfo associated with a Language enum value, with fallback for Auto
    /// </summary>
    /// <param name="language">The Language enum value</param>
    /// <param name="defaultCulture">Default culture to use for Auto (usually system culture)</param>
    /// <returns>CultureInfo associated with the language, or defaultCulture for Auto</returns>
    public static CultureInfo? GetCultureInfo(this Language language, CultureInfo defaultCulture)
    {
        if (language == Language.Auto)
            return defaultCulture;

        return language.GetCultureInfo();
    }

    /// <summary>
    /// Gets the CultureInfo name (e.g., "zh-CN", "en-US") for a Language enum value
    /// </summary>
    /// <param name="language">The Language enum value</param>
    /// <returns>Culture name string, or null for Auto</returns>
    public static string? GetCultureName(this Language language)
    {
        return language.GetCultureInfo()?.Name;
    }

    /// <summary>
    /// Gets the CultureInfo name with fallback for Auto
    /// </summary>
    /// <param name="language">The Language enum value</param>
    /// <param name="defaultCultureName">Default culture name to use for Auto</param>
    /// <returns>Culture name string</returns>
    public static string? GetCultureName(this Language language, string defaultCultureName)
    {
        if (language == Language.Auto)
            return defaultCultureName;

        return language.GetCultureName();
    }

    /// <summary>
    /// Converts a culture name to Language enum value
    /// </summary>
    /// <param name="cultureName">Culture name (e.g., "zh-CN", "en-US")</param>
    /// <returns>Matching Language enum value, or Language.Auto if no match found</returns>
    public static Language FromCultureName(string cultureName)
    {
        if (string.IsNullOrEmpty(cultureName))
            return Language.Auto;

        foreach (Language language in Enum.GetValues(typeof(Language)))
        {
            if (language == Language.Auto)
                continue;

            var culture = language.GetCultureInfo();
            if (culture?.Name.Equals(cultureName, StringComparison.OrdinalIgnoreCase) == true)
                return language;
        }

        return Language.Auto;
    }

    /// <summary>
    /// Converts a CultureInfo to Language enum value
    /// </summary>
    /// <param name="cultureInfo">The CultureInfo object</param>
    /// <returns>Matching Language enum value, or Language.Auto if no match found</returns>
    public static Language FromCultureInfo(CultureInfo? cultureInfo)
    {
        if (cultureInfo == null || Equals(cultureInfo, CultureInfo.InvariantCulture))
        {
            return Language.Auto;
        }
        return FromCultureName(cultureInfo.Name);
    }

    /// <summary>
    /// Gets the display name of the culture for a Language enum value
    /// </summary>
    /// <param name="language">The Language enum value</param>
    /// <returns>Culture display name, or "Auto" for Auto</returns>
    public static string? GetDisplayName(this Language language)
    {
        if (language == Language.Auto)
            return "Auto";

        return language.GetCultureInfo()?.DisplayName;
    }

    /// <summary>
    /// Gets the native name of the culture for a Language enum value
    /// </summary>
    /// <param name="language">The Language enum value</param>
    /// <returns>Culture native name, or "Auto" for Auto</returns>
    public static string? GetNativeName(this Language language)
    {
        if (language == Language.Auto)
            return "Auto";

        return language.GetCultureInfo()?.NativeName;
    }

    /// <summary>
    /// Gets all available cultures from the Language enum (excluding Auto)
    /// </summary>
    /// <returns>Array of CultureInfo objects for all defined languages</returns>
    public static CultureInfo[] GetAvailableCultures()
    {
        return Enum.GetValues(typeof(Language))
            .Cast<Language>()
            .Where(lang => lang != Language.Auto)
            .Select(lang => lang.GetCultureInfo())
            .Where(culture => culture != null)
            .Cast<CultureInfo>()
            .ToArray();
    }
}
