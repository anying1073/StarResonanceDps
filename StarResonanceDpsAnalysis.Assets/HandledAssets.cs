using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StarResonanceDpsAnalysis.Assets.Properties;

namespace StarResonanceDpsAnalysis.Assets
{
    public static class HandledAssets
    {
        #region AlimamaShuHeiTi
        private const string ALIMAMASHUHEITI_FONT_KEY = "阿里妈妈数黑体";
        public static FontFamily AliMaMaShuHeiTi()
            => FontLoader.LoadFontFamilyFromBytesAndCache(ALIMAMASHUHEITI_FONT_KEY, Resources.AlimamaShuHeiTi);
        public static Font AliMaMaShuHeiTi(float fontSize = 9, FontStyle fontStyle = FontStyle.Regular)
            => FontLoader.LoadFontFromBytesAndCache(ALIMAMASHUHEITI_FONT_KEY, Resources.AlimamaShuHeiTi, fontSize, fontStyle);
        #endregion

        #region ApplicationIcon_256x256
        private static Image? _applicationIcon_256x256;
        public static Image ApplicationIcon_256x256
        {
            get
            {
                _applicationIcon_256x256 ??= new Bitmap(new MemoryStream(Resources.ApplicationIcon_256x256));
                return _applicationIcon_256x256;
            }
        }
        #endregion

        #region cancel_hover
        private static Image? _cancel_hover;
        public static Image Cancel_Hover
        {
            get
            {
                _cancel_hover ??= new Bitmap(new MemoryStream(Resources.cancel_hover));
                return _cancel_hover;
            }
        }
        #endregion

        #region cancel_normal
        private static Image? _cancel_normal;
        public static Image Cancel_Normal
        {
            get
            {
                _cancel_normal ??= new Bitmap(new MemoryStream(Resources.cancel_normal));
                return _cancel_normal;
            }
        }
        #endregion

        #region data-display
        public static string Data_Display => Resources.data_display;
        #endregion

        #region diaryIcon
        public static string DiaryIcon => Resources.diaryIcon;
        #endregion

        #region exclude
        public static string Exclude => Resources.exclude;
        #endregion

        #region flushed_hover
        private static Image? _flushed_hover;
        public static Image Flushed_Hover
        {
            get
            {
                _flushed_hover ??= new Bitmap(new MemoryStream(Resources.flushed_hover));
                return _flushed_hover;
            }
        }
        #endregion

        #region flushed_normal
        private static Image? _flushed_normal;
        public static Image Flushed_Normal
        {
            get
            {
                _flushed_normal ??= new Bitmap(new MemoryStream(Resources.flushed_normal));
                return _flushed_normal;
            }
        }
        #endregion

        #region handoff_hover
        private static Image? _handoff_hover;
        public static Image Handoff_Hover
        {
            get
            {
                _handoff_hover ??= new Bitmap(new MemoryStream(Resources.handoff_hover));
                return _handoff_hover;
            }
        }
        #endregion

        #region handoff_normal
        private static Image? _handoff_normal;
        public static Image Handoff_Normal
        {
            get
            {
                _handoff_normal ??= new Bitmap(new MemoryStream(Resources.handoff_normal));
                return _handoff_normal;
            }
        }
        #endregion

        #region IconFont
        private const string ICON_FONT_KEY = "IconFont";
        public static FontFamily IconFont()
            => FontLoader.LoadFontFamilyFromBytesAndCache(ICON_FONT_KEY, Resources.IconFont);
        public static Font IconFont(float fontSize = 9, FontStyle fontStyle = FontStyle.Regular)
            => FontLoader.LoadFontFromBytesAndCache(ICON_FONT_KEY, Resources.IconFont, fontSize, fontStyle);
        #endregion

        #region HarmonyOS_Sans
        private const string HARMONY_OS_SANS_FONT_KEY = "HarmonyOS Sans SC";
        public static FontFamily HarmonyOS_Sans()
            => FontLoader.LoadFontFamilyFromBytesAndCache(HARMONY_OS_SANS_FONT_KEY, Resources.HarmonyOS_Sans);
        public static Font HarmonyOS_Sans(float fontSize = 9, FontStyle fontStyle = FontStyle.Regular)
            => FontLoader.LoadFontFromBytesAndCache(HARMONY_OS_SANS_FONT_KEY, Resources.HarmonyOS_Sans, fontSize, fontStyle);
        #endregion

        #region HarmonyOS_Sans_SC_Bold
        private const string HARMONU_OS_SANS_SC_BOLD_FONT_KEY = "HarmonyOS Sans SC";
        public static FontFamily HarmonyOS_Sans_Bold()
            => FontLoader.LoadFontFamilyFromBytesAndCache(HARMONU_OS_SANS_SC_BOLD_FONT_KEY, Resources.HarmonyOS_Sans_SC_Bold);
        public static Font HarmonyOS_Sans_Bold(float fontSize = 9, FontStyle fontStyle = FontStyle.Bold)
            => FontLoader.LoadFontFromBytesAndCache(HARMONU_OS_SANS_SC_BOLD_FONT_KEY, Resources.HarmonyOS_Sans_SC_Bold, fontSize, fontStyle);
        #endregion

        #region historicalRecords
        public static string HistoricalRecords => Resources.historicalRecords;
        #endregion

        #region HomeIcon
        public static string HomeIcon => Resources.HomeIcon;
        #endregion

        #region hp_icon
        private static Image? _hp_icon;
        public static Image Hp_Icon
        {
            get
            {
                _hp_icon ??= new Bitmap(new MemoryStream(Resources.hp_icon));
                return _hp_icon;
            }
        }
        #endregion

        #region left_hover
        private static Image? _left_hover;
        public static Image Left_Hover
        {
            get
            {
                _left_hover ??= new Bitmap(new MemoryStream(Resources.left_hover));
                return _left_hover;
            }
        }
        #endregion

        #region left_normal
        private static Image? _left_normal;
        public static Image Left_Normal
        {
            get
            {
                _left_normal ??= new Bitmap(new MemoryStream(Resources.left_normal));
                return _left_normal;
            }
        }
        #endregion

        #region minimize_normal
        private static Image? _minimize_normal;
        public static Image Minimize_Normal
        {
            get
            {
                _minimize_normal ??= new Bitmap(new MemoryStream(Resources.minimize_normal));
                return _minimize_normal;
            }
        }
        #endregion

        #region moduleIcon
        public static string ModuleIcon => Resources.moduleIcon;
        #endregion

        #region Npc
        private static Image? _npc;
        public static Image Npc
        {
            get
            {
                _npc ??= new Bitmap(new MemoryStream(Resources.Npc));
                return _npc;
            }
        }
        #endregion

        #region NpcWhite
        private static Image? _npcWhite;
        public static Image NpcWhite
        {
            get
            {
                _npcWhite ??= new Bitmap(new MemoryStream(Resources.NpcWhite));
                return _npcWhite;
            }
        }
        #endregion

        #region ok_hover
        private static Image? _ok_hover;
        public static Image Ok_Hover
        {
            get
            {
                _ok_hover ??= new Bitmap(new MemoryStream(Resources.ok_hover));
                return _ok_hover;
            }
        }
        #endregion

        #region ok_normal
        private static Image? _ok_normal;
        public static Image Ok_Normal
        {
            get
            {
                _ok_normal ??= new Bitmap(new MemoryStream(Resources.ok_normal));
                return _ok_normal;
            }
        }
        #endregion

        #region quit
        public static string Quit => Resources.quit;
        #endregion

        #region reference
        public static string Reference => Resources.reference;
        #endregion

        #region right_hover
        private static Image? _right_hover;
        public static Image Right_Hover
        {
            get
            {
                _right_hover ??= new Bitmap(new MemoryStream(Resources.right_hover));
                return _right_hover;
            }
        }
        #endregion

        #region right_normal
        private static Image? _right_normal;
        public static Image Right_Normal
        {
            get
            {
                _right_normal ??= new Bitmap(new MemoryStream(Resources.right_normal));
                return _right_normal;
            }
        }
        #endregion

        #region SAOWelcomeTT
        private const string SAO_WELCOME_TT_FONT_KEY = "SAO Welcome TT";
        public static FontFamily SAOWelcomeTT()
            => FontLoader.LoadFontFamilyFromBytesAndCache(SAO_WELCOME_TT_FONT_KEY, Resources.SAOWelcomeTT);
        public static Font SAOWelcomeTT(float fontSize = 9, FontStyle fontStyle = FontStyle.Regular)
            => FontLoader.LoadFontFromBytesAndCache(SAO_WELCOME_TT_FONT_KEY, Resources.SAOWelcomeTT, fontSize, fontStyle);
        #endregion

        #region set_up
        public static string Set_Up => Resources.set_up;
        #endregion

        #region setting_hover
        private static Image? _setting_hover;
        public static Image Setting_Hover
        {
            get
            {
                _setting_hover ??= new Bitmap(new MemoryStream(Resources.setting_hover));
                return _setting_hover;
            }
        }
        #endregion

        #region setting_normal
        private static Image? _setting_normal;
        public static Image Setting_Normal
        {
            get
            {
                _setting_normal ??= new Bitmap(new MemoryStream(Resources.setting_normal));
                return _setting_normal;
            }
        }
        #endregion

        #region Stakes
        public static string Stakes => Resources.Stakes;
        #endregion

        #region top_hover
        private static Image? _top_hover;
        public static Image Top_Hover
        {
            get
            {
                _top_hover ??= new Bitmap(new MemoryStream(Resources.top_hover));
                return _top_hover;
            }
        }
        #endregion

        #region top_normal
        private static Image? _top_normal;
        public static Image Top_Normal
        {
            get
            {
                _top_normal ??= new Bitmap(new MemoryStream(Resources.top_normal));
                return _top_normal;
            }
        }
        #endregion

        #region userUid
        public static string UserUid => Resources.userUid;
        #endregion

        #region 伤害
        private static Image? _伤害;
        public static Image 伤害
        {
            get
            {
                _伤害 ??= new Bitmap(new MemoryStream(Resources.伤害));
                return _伤害;
            }
        }
        #endregion

        #region 伤害白色
        private static Image? _伤害白色;
        public static Image 伤害白色
        {
            get
            {
                _伤害白色 ??= new Bitmap(new MemoryStream(Resources.伤害白色));
                return _伤害白色;
            }
        }
        #endregion

        #region 冰魔导师
        private static Image? _冰魔导师;
        public static Image 冰魔导师
        {
            get
            {
                _冰魔导师 ??= new Bitmap(new MemoryStream(Resources.冰魔导师));
                return _冰魔导师;
            }
        }
        #endregion

        #region 冰魔导师_Opacity10
        private static Image? _冰魔导师_Opacity10;
        public static Image 冰魔导师_Opacity10
        {
            get
            {
                _冰魔导师_Opacity10 ??= new Bitmap(new MemoryStream(Resources.冰魔导师_Opacity10));
                return _冰魔导师_Opacity10;
            }
        }
        #endregion

        #region 巨刃守护者
        private static Image? _巨刃守护者;
        public static Image 巨刃守护者
        {
            get
            {
                _巨刃守护者 ??= new Bitmap(new MemoryStream(Resources.巨刃守护者));
                return _巨刃守护者;
            }
        }
        #endregion

        #region 巨刃守护者_Opacity10
        private static Image? _巨刃守护者_Opacity10;
        public static Image 巨刃守护者_Opacity10
        {
            get
            {
                _巨刃守护者_Opacity10 ??= new Bitmap(new MemoryStream(Resources.巨刃守护者_Opacity10));
                return _巨刃守护者_Opacity10;
            }
        }
        #endregion

        #region 承伤
        private static Image? _承伤;
        public static Image 承伤
        {
            get
            {
                _承伤 ??= new Bitmap(new MemoryStream(Resources.承伤));
                return _承伤;
            }
        }
        #endregion

        #region 承伤白色
        private static Image? _承伤白色;
        public static Image 承伤白色
        {
            get
            {
                _承伤白色 ??= new Bitmap(new MemoryStream(Resources.承伤白色));
                return _承伤白色;
            }
        }
        #endregion

        #region 森语者
        private static Image? _森语者;
        public static Image 森语者
        {
            get
            {
                _森语者 ??= new Bitmap(new MemoryStream(Resources.森语者));
                return _森语者;
            }
        }
        #endregion

        #region 森语者_Opacity10
        private static Image? _森语者_Opacity10;
        public static Image 森语者_Opacity10
        {
            get
            {
                _森语者_Opacity10 ??= new Bitmap(new MemoryStream(Resources.森语者_Opacity10));
                return _森语者_Opacity10;
            }
        }
        #endregion

        #region 治疗
        private static Image? _治疗;
        public static Image 治疗
        {
            get
            {
                _治疗 ??= new Bitmap(new MemoryStream(Resources.治疗));
                return _治疗;
            }
        }
        #endregion

        #region 治疗白色
        private static Image? _治疗白色;
        public static Image 治疗白色
        {
            get
            {
                _治疗白色 ??= new Bitmap(new MemoryStream(Resources.治疗白色));
                return _治疗白色;
            }
        }
        #endregion

        #region 灵魂乐手
        private static Image? _灵魂乐手;
        public static Image 灵魂乐手
        {
            get
            {
                _灵魂乐手 ??= new Bitmap(new MemoryStream(Resources.灵魂乐手));
                return _灵魂乐手;
            }
        }
        #endregion

        #region 灵魂乐手_Opacity10
        private static Image? _灵魂乐手_Opacity10;
        public static Image 灵魂乐手_Opacity10
        {
            get
            {
                _灵魂乐手_Opacity10 ??= new Bitmap(new MemoryStream(Resources.灵魂乐手_Opacity10));
                return _灵魂乐手_Opacity10;
            }
        }
        #endregion

        #region 皇冠
        private static Image? _皇冠;
        public static Image 皇冠
        {
            get
            {
                _皇冠 ??= new Bitmap(new MemoryStream(Resources.皇冠));
                return _皇冠;
            }
        }
        #endregion

        #region 皇冠白
        private static Image? _皇冠白;
        public static Image 皇冠白
        {
            get
            {
                _皇冠白 ??= new Bitmap(new MemoryStream(Resources.皇冠白));
                return _皇冠白;
            }
        }
        #endregion

        #region 神射手
        private static Image? _神射手;
        public static Image 神射手
        {
            get
            {
                _神射手 ??= new Bitmap(new MemoryStream(Resources.神射手));
                return _神射手;
            }
        }
        #endregion

        #region 神射手_Opacity10
        private static Image? _神射手_Opacity10;
        public static Image 神射手_Opacity10
        {
            get
            {
                _神射手_Opacity10 ??= new Bitmap(new MemoryStream(Resources.神射手_Opacity10));
                return _神射手_Opacity10;
            }
        }
        #endregion

        #region 神盾骑士
        private static Image? _神盾骑士;
        public static Image 神盾骑士
        {
            get
            {
                _神盾骑士 ??= new Bitmap(new MemoryStream(Resources.神盾骑士));
                return _神盾骑士;
            }
        }
        #endregion

        #region 神盾骑士_Opacity10
        private static Image? _神盾骑士_Opacity10;
        public static Image 神盾骑士_Opacity10
        {
            get
            {
                _神盾骑士_Opacity10 ??= new Bitmap(new MemoryStream(Resources.神盾骑士_Opacity10));
                return _神盾骑士_Opacity10;
            }
        }
        #endregion

        #region 雷影剑士
        private static Image? _雷影剑士;
        public static Image 雷影剑士
        {
            get
            {
                _雷影剑士 ??= new Bitmap(new MemoryStream(Resources.雷影剑士));
                return _雷影剑士;
            }
        }
        #endregion

        #region 雷影剑士_Opacity10
        private static Image? _雷影剑士_Opacity10;
        public static Image 雷影剑士_Opacity10
        {
            get
            {
                _雷影剑士_Opacity10 ??= new Bitmap(new MemoryStream(Resources.雷影剑士_Opacity10));
                return _雷影剑士_Opacity10;
            }
        }
        #endregion

        #region 青岚骑士
        private static Image? _青岚骑士;
        public static Image 青岚骑士
        {
            get
            {
                _青岚骑士 ??= new Bitmap(new MemoryStream(Resources.青岚骑士));
                return _青岚骑士;
            }
        }
        #endregion

        #region 青岚骑士_Opacity10
        private static Image? _青岚骑士_Opacity10;
        public static Image 青岚骑士_Opacity10
        {
            get
            {
                _青岚骑士_Opacity10 ??= new Bitmap(new MemoryStream(Resources.青岚骑士_Opacity10));
                return _青岚骑士_Opacity10;
            }
        }
        #endregion  

    }
}
