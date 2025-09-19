using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StarResonanceDpsAnalysis.Core.Models;

namespace StarResonanceDpsAnalysis.Core.Data.Models
{
    public class PlayerInfo
    {
        public long UID { get; internal set; }
        public string? Name { get; internal set; }
        public int? ProfessionID { get; internal set; }
        public string? SubProfessionName { get; internal set; }
        /// <summary>
        /// 职业流派
        /// </summary>
        public ClassSpec Spec { get; internal set; }
        public int? CombatPower { get; internal set; }
        public int? Level { get; internal set; }
        public int? RankLevel { get; internal set; }
        public int? Critical { get; internal set; }
        public int? Lucky { get; internal set; }
        public long? MaxHP { get; internal set; }
        public long? HP { get; internal set; }
    }
}
