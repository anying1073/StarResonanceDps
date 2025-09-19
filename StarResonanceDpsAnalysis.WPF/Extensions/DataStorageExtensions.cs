using Microsoft.Extensions.DependencyInjection;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class DataStorageExtensions
{
    public static IServiceCollection AddDataStorage(this IServiceCollection services)
    {
        return services.AddSingleton<IDataStorage, InstantizedDataStorage>();
    }
}