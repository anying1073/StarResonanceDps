using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Models;

namespace StarResonanceDpsAnalysis.Core.Data.Models;

public class PlayerInfo
{
    private ClassSpec _spec;
    public long UID { get; internal set; }
    public string? Name { get; internal set; }
    public int? ProfessionID { get; internal set; }
    public string? SubProfessionName { get; internal set; }

    /// <summary>
    /// 职业流派
    /// </summary>
    public ClassSpec Spec
    {
        get => _spec;
        internal set
        {
            if (_spec == value) return;
            _spec = value;
            // Update classes
            ProfessionID = Class.GetProfessionID();
        }
    }

    public Classes Class => Spec.GetClasses();

    public int? CombatPower { get; internal set; }
    public int? Level { get; internal set; }
    public int? RankLevel { get; internal set; }
    public int? Critical { get; internal set; }
    public int? Lucky { get; internal set; }
    public long? MaxHP { get; internal set; }
    public long? HP { get; internal set; }
    public int ElementFlag { get; internal set; }
    public int ReductionLevel { get; internal set; }
    public int EnergyFlag { get; internal set; }
    public int NpcTemplateId { get; internal set; }
    public int SeasonStrength { get; internal set; }
    public int SeasonLevel { get; internal set; }
    public bool CombatState { get; internal set; }
    public int CombatStateTime { get; internal set; }
}