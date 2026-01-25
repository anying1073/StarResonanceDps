using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zproto;

namespace StarResonanceDpsAnalysis.Core.Extends.BlueProto
{
    public static class EDamagePropertyExtends
    {
        /// <summary>
        /// 元素枚举转简短标签（含 emoji 图标）。
        /// </summary>
        /// <param name="damageProperty">EDamageProperty 枚举值</param>
        /// <returns>对应的标签字符串</returns>
        public static string GetDamageElement(this EDamageProperty damageProperty)
        {
            return damageProperty switch
            {
                EDamageProperty.General => "⚔️物",
                EDamageProperty.Fire => "🔥火",
                EDamageProperty.Water => "❄️冰",
                EDamageProperty.Electricity => "⚡雷",
                EDamageProperty.Wood => "🍀森",
                EDamageProperty.Wind => "💨风",
                EDamageProperty.Rock => "⛰️岩",
                EDamageProperty.Light => "🌟光",
                EDamageProperty.Dark => "🌑暗",
                EDamageProperty.Count => "❓？",// 未知/保留
                _ => "⚔️物",
            };
        }

        /// <summary>
        /// 元素枚举转简短标签（含 emoji 图标）。
        /// </summary>
        /// <param name="damageProperty">EDamageProperty 枚举值</param>
        /// <returns>对应的标签字符串</returns>
        public static string GetDamageElement(this int damageProperty)
        {
            return GetDamageElement((EDamageProperty)damageProperty);
        }
    }
}
