using StarResonanceDpsAnalysis.Core.Models;

namespace StarResonanceDpsAnalysis.Core.Extends.Data;

public static class ProfessionExtends
{
    /// <summary>
    /// 职业ID映射为职业名称
    /// </summary>
    public static string GetProfessionNameById(this int professionId)
    {
        return professionId switch
        {
            1 => "雷影剑士",
            2 => "冰魔导师",
            3 => "涤罪恶火_战斧",
            4 => "青岚骑士",
            5 => "森语者",
            8 => "雷霆一闪_手炮",
            9 => "巨刃守护者",
            10 => "暗灵祈舞_仪刀_仪仗",
            11 => "神射手",
            12 => "神盾骑士",
            13 => "灵魂乐手",

            _ => string.Empty
        };
    }

    public static string GetSubProfessionBySkillId(this long skillId)
    {
        return skillId switch
        {
            // 神射手
            2292 or 1700820 or 1700825 or 1700827 => "狼弓",
            220112 or 2203622 or 220106 => "鹰弓",

            // 森语者
            1518 or 1541 or 21402 => "惩戒",
            20301 => "愈合",

            // 雷影剑士
            1714 or 1734 => "居合",
            44701 or 179906 => "月刃",

            // 冰魔导师
            120901 or 120902 => "冰矛",
            1241 => "射线",

            // 青岚骑士
            1405 or 1418 => "重装",
            1419 => "空枪",

            // 巨刃守护者
            199902 => "岩盾",
            1930 or 1931 or 1934 or 1935 => "格挡",

            // 神盾骑士
            2405 => "防盾",
            2406 => "光盾",

            // 灵魂乐手
            2306 => "狂音",
            2307 or 2361 or 55302 => "协奏",

            _ => string.Empty
        };
    }
}

public static class ClassExtensions
{
    /// <summary>
    /// 职业ID映射为职业名称 
    /// </summary>
    public static Classes GetClassNameById(this int classId)
    {
        // 1 => "雷影剑士",
        // 2 => "冰魔导师",
        // 3 => "涤罪恶火_战斧",
        // 4 => "青岚骑士",
        // 5 => "森语者",
        // 8 => "雷霆一闪_手炮",
        // 9 => "巨刃守护者",
        // 10 => "暗灵祈舞_仪刀_仪仗",
        // 11 => "神射手",
        // 12 => "神盾骑士",
        // 13 => "灵魂乐手",
        return classId switch
        {
            1 => Classes.Stormblade,
            2 => Classes.FrostMage,
            4 => Classes.WindKnight,
            5 => Classes.VerdantOracle,
            9 => Classes.HeavyGuardian,
            11 => Classes.Marksman,
            12 => Classes.ShieldKnight,
            13 => Classes.SoulMusician,
            // 8 => Classes.Unknown,
            // 3 => Classes.Unknown,
            // 10 => Classes.Unknown,
            _ => Classes.Unknown
        };
    }

    /// <summary>
    /// 根据技能ID获取职业专精
    /// </summary>
    /// <param name="skillId"></param>
    /// <returns></returns>
    public static ClassSpec GetClassSpecBySkillId(this long skillId)
    {
        return skillId switch
        {
            // 神射手
            2292 or 1700820 or 1700825 or 1700827 => ClassSpec.MarksmanWildpack, // "狼弓"
            220112 or 2203622 or 220106 => ClassSpec.MarksmanFalconry, // "鹰弓"

            // 森语者
            1518 or 1541 or 21402 => ClassSpec.VerdantOracleSmite, // "惩戒"
            20301 => ClassSpec.VerdantOracleLifeBind, // "愈合"

            // 雷影剑士
            1714 or 1734 => ClassSpec.StormbladeIaidoSlash, // "居合"
            44701 or 179906 => ClassSpec.StormbladeMoonStrike, // "月刃"

            // 冰魔导师
            120901 or 120902 => ClassSpec.FrostMageIcicle, // "冰矛"
            1241 => ClassSpec.FrostMageFrostBeam, // "射线"

            // 青岚骑士
            1405 or 1418 => ClassSpec.WindKnightVanGuard, // "重装"
            1419 => ClassSpec.WindKnightSkyward, // "空枪"

            // 巨刃守护者
            199902 => ClassSpec.HeavyGuardianEarthfort, // "岩盾"
            1930 or 1931 or 1934 or 1935 => ClassSpec.HeavyGuardianBlock, // "格挡"

            // 神盾骑士
            2405 => ClassSpec.ShieldKnightRecovery, // "防盾"
            2406 => ClassSpec.ShieldKnightShield, // "光盾"

            // 灵魂乐手
            2306 => ClassSpec.SoulMusicianDissonance, // "狂音"
            2307 or 2361 or 55302 => ClassSpec.SoulMusicianConcerto, // "协奏"

            _ => ClassSpec.Unknown
        };
    }
}