using System.ComponentModel;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using StarResonanceDpsAnalysis.WPF.Converters;
using StarResonanceDpsAnalysis.WPF.Settings;
using KeyBinding = StarResonanceDpsAnalysis.WPF.Models.KeyBinding;

namespace StarResonanceDpsAnalysis.WPF.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replace a registered singleton service with a new instance
    /// </summary>
    /// <typeparam name="TService">The service type</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="newInstance">New instance to replace with</param>
    /// <returns>Updated service collection</returns>
    public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, TService newInstance)
        where TService : class
    {
        // Remove existing registration
        var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (existingDescriptor != null)
        {
            services.Remove(existingDescriptor);
        }

        // Add new singleton instance
        services.AddSingleton<TService>(newInstance);
        return services;
    }

    /// <summary>
    /// Replace a registered singleton service with a new factory
    /// </summary>
    /// <typeparam name="TService">The service type</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="factory">Factory function to create new instance</param>
    /// <returns>Updated service collection</returns>
    public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> factory)
        where TService : class
    {
        // Remove existing registration
        var existingDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (existingDescriptor != null)
        {
            services.Remove(existingDescriptor);
        }

        // Add new singleton factory
        services.AddSingleton<TService>(factory);
        return services;
    }

    /// <summary>
    /// Configure JSON serializer options with custom converters for the application
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Updated service collection</returns>
    public static IServiceCollection AddJsonConfiguration(this IServiceCollection services)
    {
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.WriteIndented = true;
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            // options.Converters.Add(new JsonModifierKeysConverter());
            // Add the factory converter as well for completeness
            // options.Converters.Add(new JsonModifierKeysConverterFactory());
        });
        // TypeDescriptor.AddAttributes(typeof(KeyBinding), new TypeConverterAttribute(typeof(KeyBindingTypeConverter)));
        TypeDescriptor.AddAttributes(typeof(ModifierKeys), new TypeConverterAttribute(typeof(ModifierKeysTypeConverter)));

        return services;
    }
}