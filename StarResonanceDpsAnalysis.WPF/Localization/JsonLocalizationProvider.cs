using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using WPFLocalizeExtension.Providers;

namespace StarResonanceDpsAnalysis.WPF.Localization;

public class JsonLocalizationProvider : ILocalizationProvider
{
    private readonly string _basePath;
    private readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _resources = new();
    private readonly (string resourceName, string pattern)[] _filenamePatterns =
    [
        ("Monster","Monster\\monster.{0}.json"),
        ("DebugData","DebugData\\debugData.{0}.json")
    ];

    public JsonLocalizationProvider(string basePath)
    {
        _basePath = basePath;
        AvailableCultures = new ObservableCollection<CultureInfo>(GetAvailableCultures());
    }

    public FullyQualifiedResourceKeyBase? GetFullyQualifiedResourceKey(string key, DependencyObject target)
    {
        // Not used in this implementation
        return null;
    }

    public object? GetLocalizedObject(string? key, DependencyObject? target, CultureInfo? culture)
    {
        if (string.IsNullOrEmpty(key)) return null;

        // Support fully qualified keys like "Assembly:Resource:Key" or "Assembly:Resource:Sub:Key".
        // We'll treat the last segment as the actual lookup key and the second segment (if present)
        // as the resource name to filter JSON files.
        string lookupKey = key;
        string? resourceName = null;

        var parts = key.Split(':');
        if (parts.Length >= 2)
        {
            lookupKey = parts[^1]; // last segment
            resourceName = parts[1].ToLower(); // resource segment (common pattern: Assembly:Resource:Key)
        }

        var current = culture ?? CultureInfo.CurrentUICulture;

        // Walk up the culture chain: requested -> parent -> ... -> invariant
        while (!Equals(current, CultureInfo.InvariantCulture))
        {
            var lang = current.Name; // e.g. "zh-CN" or "zh"

            // Ensure resources are loaded for this culture
            EnsureResourcesLoaded(lang);

            if (_resources.TryGetValue(lang, out var resDicts))
            {
                string? value = null;
                if (!string.IsNullOrEmpty(resourceName) && resDicts.TryGetValue(resourceName, out var dict))
                {
                    // Lookup in the specific resource dictionary
                    dict.TryGetValue(lookupKey, out value);
                }
                else if (string.IsNullOrEmpty(resourceName))
                {
                    // If no resource name specified, search all resource dictionaries
                    foreach (var resDict in resDicts.Values)
                    {
                        if (resDict.TryGetValue(lookupKey, out value))
                            break;
                    }
                }

                if (value != null)
                {
                    // IMPORTANT: Do NOT raise ValueChanged here to avoid recursive re-evaluation and StackOverflow.
                    return value;
                }
            }

            current = current.Parent;
            // If parent is invariant (Name == "") the loop will exit next iteration
        }

        // No match found in the culture chain
        return null;
    }

    private void EnsureResourcesLoaded(string lang)
    {
        if (_resources.ContainsKey(lang))
            return;

        var resDicts = new Dictionary<string, Dictionary<string, string>>();
        _resources[lang] = resDicts;

        foreach (var (resName, pattern) in _filenamePatterns)
        {
            var path = Path.Combine(_basePath, string.Format(pattern, lang));
            if (!File.Exists(path))
                continue;

            try
            {
                var text = File.ReadAllText(path);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
                if (dict != null)
                {
                    resDicts[resName.ToLower()] = dict;
                }
            }
            catch
            {
                // Ignore malformed files
            }
        }
    }

    public ObservableCollection<CultureInfo>? AvailableCultures { get; }

    public event ProviderChangedEventHandler? ProviderChanged;

    public event ProviderErrorEventHandler? ProviderError;

    public event ValueChangedEventHandler? ValueChanged;

    public void UpdateCultureResources(CultureInfo culture)
    {
        // Evict by full name to be consistent with cache key
        var lang = culture.Name;
        _resources.Remove(lang);
        // Notify provider consumers that values may have changed (safe here; not during GetLocalizedObject)
        ProviderChanged?.Invoke(this, new ProviderChangedEventArgs(null));
    }

    private IEnumerable<CultureInfo> GetAvailableCultures()
    {
        if (!Directory.Exists(_basePath))
            yield break;

        var cultures = new HashSet<CultureInfo>();

        foreach (var (resName, pattern) in _filenamePatterns)
        {
            var dir = Path.GetDirectoryName(pattern);
            if (string.IsNullOrEmpty(dir))
                continue;

            var fullDir = Path.Combine(_basePath, dir);
            if (!Directory.Exists(fullDir))
                continue;

            foreach (var file in Directory.GetFiles(fullDir, $"*.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('.');
                if (parts.Length >= 2 && parts[0] == resName.ToLowerInvariant())
                {
                    var cultureCode = parts[1];
                    CultureInfo? culture = null;
                    try
                    {
                        culture = new CultureInfo(cultureCode);
                    }
                    catch
                    {
                        // Skip invalid culture codes
                    }

                    if (culture != null)
                        cultures.Add(culture);
                }
            }
        }

        foreach (var culture in cultures)
            yield return culture;
    }
}