using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Models;

namespace StarResonanceDpsAnalysis.Core.Data.Models;

public class PlayerInfo
{
    private ClassSpec _spec;
    public long UID { get; set; }
    public string? Name { get; set; }
    public int? ProfessionID { get; set; }
    public string? SubProfessionName { get; set; }

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

    public Classes Class => Spec != ClassSpec.Unknown ? Spec.GetClasses() : ProfessionID.GetClassNameById();

    public int? CombatPower { get; set; }
    public int? Level { get; set; }
    public int? RankLevel { get; set; }
    public int? Critical { get; set; }
    public int? Lucky { get; set; }
    public long? MaxHP { get; set; }
    public long? HP { get; set; }
    public int ElementFlag { get; set; }
    public int ReductionLevel { get; set; }
    public int EnergyFlag { get; set; }
    public int NpcTemplateId { get; set; }
    public int? SeasonStrength { get; set; }
    public int SeasonLevel { get; set; }
    public bool CombatState { get; set; }
    public long CombatStateTime { get; set; }
}