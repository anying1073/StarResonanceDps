using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using StarResonanceDpsAnalysis.WPF.Localization;

namespace StarResonanceDpsAnalysis.WPF.Models;

/// <summary>
/// Custom attribute for localized display names
/// </summary>
/// <param name="resourceKey"></param>
public class LocalizedDescriptionAttribute(string resourceKey) : DescriptionAttribute
{
    public override string Description
    {
        get
        {
            // Try to resolve from DI
            var provider = App.Host?.Services;
            var loc = provider?.GetService<LocalizationManager>();
            if (loc != null) return loc.GetString(resourceKey);
            // Fallback for design-time
            var opts = new LocalizationConfiguration();
            return new LocalizationManager(opts).GetString(resourceKey);
        }
    }
}