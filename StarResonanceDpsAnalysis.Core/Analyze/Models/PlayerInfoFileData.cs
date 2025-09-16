using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Models;

namespace StarResonanceDpsAnalysis.Core.Analyze.Models
{
    public class PlayerInfoFileData
    {
        [JsonProperty("u")]
        public long UID { get; internal set; }
        [JsonProperty("n")]
        public string? Name { get; internal set; }
        [JsonProperty("p")]
        public int? ProfessionID { get; internal set; }
        [JsonProperty("s")]
        public string? SubProfessionName { get; internal set; }
        [JsonProperty]
        public ClassSpec? Spec { get; internal set; }
        [JsonProperty("c")]
        public int? CombatPower { get; internal set; }
        [JsonProperty("cr")]
        public int? Critical { get; internal set; }
        [JsonProperty("l")]
        public int? Lucky { get; internal set; }
        [JsonProperty("m")]
        public long? MaxHP { get; internal set; }
        [JsonProperty("h")]
        public byte[] Hash { get; internal set; } = [];

        private static byte[] CreateMD5(PlayerInfoFileData data) =>
            MD5.HashData($"{data.UID}_{data.Name}_{data.ProfessionID}_{data.SubProfessionName}_{data.CombatPower}_{data.Critical}_{data.Lucky}_{data.MaxHP}".GetBytes());
        public bool TestHash() => TestHash(this);
        public static bool TestHash(PlayerInfoFileData data) => data.Hash.SequenceEqual(CreateMD5(data));

        public static implicit operator PlayerInfoFileData(PlayerInfo p)
        { 
            var tmp = new PlayerInfoFileData()
            {
                UID = p.UID,
                Name = p.Name,
                ProfessionID = p.ProfessionID,
                SubProfessionName = p.SubProfessionName,
                Spec = p.Spec,
                CombatPower = p.CombatPower,
                Critical = p.Critical,
                Lucky = p.Lucky,
                MaxHP = p.MaxHP
            };
            tmp.Hash = CreateMD5(tmp);
            return tmp;
        }
    }
}
