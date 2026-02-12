using StarResonanceDpsAnalysis.WPF.ViewModels;

namespace StarResonanceDpsAnalysis.WPF.Models;

public sealed class StatisticDictionary : Dictionary<StatisticType, Dictionary<long, DpsDataProcessed>>
{
    public StatisticDictionary()
    {
        Add(StatisticType.Damage, new Dictionary<long, DpsDataProcessed>());
        Add(StatisticType.Healing, new Dictionary<long, DpsDataProcessed>());
        Add(StatisticType.TakenDamage, new Dictionary<long, DpsDataProcessed>());
        Add(StatisticType.NpcTakenDamage, new Dictionary<long, DpsDataProcessed>());
    }
}