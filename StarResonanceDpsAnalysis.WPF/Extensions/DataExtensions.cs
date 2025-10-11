using Microsoft.Extensions.DependencyInjection;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.WPF.Extensions;

public static class DataExtensions
{
    public static IServiceCollection AddPacketAnalyzer(this IServiceCollection services)
    {
        // return services.AddSingleton<IDataStorage, InstantizedDataStorage>()
        return services.AddSingleton<IDataStorage, DataStorageV2>()
            .AddSingleton<IPacketAnalyzer,PacketAnalyzerV2>()
            // .AddSingleton<IPacketAnalyzer, PacketAnalyzer>()
            .AddSingleton<MessageAnalyzerV2>();
    }
}