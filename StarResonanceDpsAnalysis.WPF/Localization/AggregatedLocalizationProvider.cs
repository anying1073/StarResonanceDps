using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using WPFLocalizeExtension.Providers;

namespace StarResonanceDpsAnalysis.WPF.Localization;

/// <summary>
/// Aggregates multiple localization providers (ResX and JSON) to provide a unified localization experience.
/// </summary>
public class AggregatedLocalizationProvider : ILocalizationProvider
{
    private readonly JsonLocalizationProvider _jsonProvider;
    private readonly ILocalizationProvider _resxProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregatedLocalizationProvider"/> class.
    /// </summary>
    /// <param name="resxProvider">The ResX localization provider.</param>
    /// <param name="jsonProvider">The JSON localization provider.</param>
    public AggregatedLocalizationProvider(ResxLocalizationProvider resxProvider, JsonLocalizationProvider jsonProvider)
    {
        _jsonProvider = jsonProvider ?? throw new ArgumentNullException(nameof(jsonProvider));
        _resxProvider = resxProvider; // ?? ResxLocalizationProvider.Instance;

        // Subscribe to inner providers to bubble up events
        _jsonProvider.ProviderChanged += (s, e) => OnProviderChanged(e);
        _jsonProvider.ProviderError += (s, e) => OnProviderError(e);
        _jsonProvider.ValueChanged += (s, e) => OnValueChanged(e);

        _resxProvider.ProviderChanged += (s, e) => OnProviderChanged(e);
        _resxProvider.ProviderError += (s, e) => OnProviderError(e);
        _resxProvider.ValueChanged += (s, e) => OnValueChanged(e);

        RefreshAvailableCultures();
    }

    /// <summary>
    /// Gets the fully qualified resource key for the specified key and target.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <param name="target">The dependency object target.</param>
    /// <returns>The fully qualified resource key.</returns>
    public FullyQualifiedResourceKeyBase GetFullyQualifiedResourceKey(string key, DependencyObject target)
    {
        // Prefer resx provider for fully qualified key resolution
        return _resxProvider.GetFullyQualifiedResourceKey(key, target);
    }

    /// <summary>
    /// Retrieves the localized object for the given key, target, and culture.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <param name="target">The dependency object target.</param>
    /// <param name="culture">The culture to use for localization.</param>
    /// <returns>The localized object, or null if not found.</returns>
    public object? GetLocalizedObject(string? key, DependencyObject? target, CultureInfo? culture)
    {
        if (string.IsNullOrEmpty(key)) return null;

        // Start from the requested culture, walk up to parent cultures, then invariant.
        var current = culture ?? CultureInfo.CurrentUICulture;

        while (!Equals(current, CultureInfo.InvariantCulture))
        {
            // Try JSON first
            var fromJson = _jsonProvider.GetLocalizedObject(key, target, current);
            if (fromJson != null) return fromJson;

            // Fallback to resx
            var fromResx = _resxProvider.GetLocalizedObject(key, target, current);
            if (fromResx != null) return fromResx;

            // Move to parent culture
            current = current.Parent;
            // Safety: break if parent has no name and equals invariant
            if (Equals(current, CultureInfo.InvariantCulture)) break;
        }

        // Final attempt with invariant/neutral culture
        var invariantJson = _jsonProvider.GetLocalizedObject(key, target, CultureInfo.InvariantCulture);
        return invariantJson ?? _resxProvider.GetLocalizedObject(key, target, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the collection of available cultures from all aggregated providers.
    /// </summary>
    public ObservableCollection<CultureInfo> AvailableCultures { get; } = new();

    /// <summary>
    /// Occurs when the provider changes.
    /// </summary>
    public event ProviderChangedEventHandler? ProviderChanged;

    /// <summary>
    /// Occurs when a provider error occurs.
    /// </summary>
    public event ProviderErrorEventHandler? ProviderError;

    /// <summary>
    /// Occurs when a value changes.
    /// </summary>
    public event ValueChangedEventHandler? ValueChanged;

    private void OnProviderChanged(ProviderChangedEventArgs e)
    {
        RefreshAvailableCultures();
        ProviderChanged?.Invoke(this, e);
    }

    private void OnProviderError(ProviderErrorEventArgs e)
    {
        ProviderError?.Invoke(this, e);
    }

    private void OnValueChanged(ValueChangedEventArgs e)
    {
        ValueChanged?.Invoke(this, e);
    }

    private void RefreshAvailableCultures()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var union = new List<CultureInfo?>();

        void AddRange(IEnumerable<CultureInfo?> cultures)
        {
            foreach (var c in cultures)
            {
                if (c == null) continue;
                if (set.Add(c.Name)) union.Add(c);
            }
        }

        if (_jsonProvider.AvailableCultures != null)
            AddRange(_jsonProvider.AvailableCultures);
        if (_resxProvider.AvailableCultures != null)
            AddRange(_resxProvider.AvailableCultures);

        AvailableCultures.Clear();
        foreach (var c in union.OrderBy(c => c.Name))
            AvailableCultures.Add(c);
    }
}