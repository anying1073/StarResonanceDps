using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using AntdUI;
using SharpPcap;
using StarResonanceDpsAnalysis.Assets;
using StarResonanceDpsAnalysis.Core.Analyze;
using StarResonanceDpsAnalysis.Core.Analyze.Exceptions;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.WinForm.Control.GDI;
using StarResonanceDpsAnalysis.WinForm.Core;
using StarResonanceDpsAnalysis.WinForm.Forms.PopUp;
using StarResonanceDpsAnalysis.WinForm.Plugin;
using StarResonanceDpsAnalysis.WinForm.Plugin.DamageStatistics;

namespace StarResonanceDpsAnalysis.WinForm.Forms
{
    public partial class DpsStatisticsForm
    {
        private bool _isShowFullData = false;
        private int _stasticsType = 0;

        private void SetDefaultFontFromResources()
        {
            pageHeader_MainHeader.Font = AppConfig.SaoFont;
            pageHeader_MainHeader.SubFont = AppConfig.ContentFont;
            label_CurrentDps.Font = label_CurrentOrder.Font = AppConfig.ContentFont;

            button_TotalDamage.Font = AppConfig.BoldHarmonyFont;
            button_TotalTreatment.Font = AppConfig.BoldHarmonyFont;
            button_AlwaysInjured.Font = AppConfig.BoldHarmonyFont;
            button_NpcTakeDamage.Font = AppConfig.BoldHarmonyFont;
        }

        #region 加载 网卡 启动设备/初始化 统计数据/ 启动 抓包/停止抓包/清空数据/ 关闭 事件

        #region —— 抓包设备/统计 —— 

        public static ICaptureDevice? SelectedDevice { get; set; } = null; // # 抓包设备：程序选中的网卡设备（可能为null，依据设置初始化）

        /// <summary>
        /// 分析器
        /// </summary>
        private PacketAnalyzer PacketAnalyzer { get; } = new(null); // # 抓包/分析器：每个到达的数据包交由该分析器处理
        #endregion

        private void LoadAppConfig()
        {
            DataStorage.SectionTimeout = TimeSpan.FromSeconds(AppConfig.CombatTimeClearDelaySeconds);
        }

        /// <summary>
        /// 读取用户缓存
        /// </summary>
        private void LoadPlayerCache()
        {
            try
            {
                DataStorage.LoadPlayerInfoFromFile();
            }
            catch (FileNotFoundException)
            {
                // 没有缓存
            }
            catch (DataTamperedException)
            {
                AppMessageBox.ShowMessage("用户缓存被篡改，或文件损坏。为软件正常运行，将清空用户缓存。", this);

                DataStorage.ClearAllPlayerInfos();
                DataStorage.SavePlayerInfoToFile();
            }
        }

        /// <summary>
        /// 软件开启后读取技能列表
        /// </summary>
        private void LoadFromEmbeddedSkillConfig()
        {
            // 1) 先用 int 键的表（已经解析过字符串）
            foreach (var kv in EmbeddedSkillConfig.AllByInt)
            {
                var id = (long)kv.Key;
                var def = kv.Value;

                // 将一条技能元数据（SkillMeta）写入 SkillBook 的全局字典中
                // 这里用的是整条更新（SetOrUpdate），如果该技能 ID 已存在则覆盖，不存在则添加
                SkillBook.SetOrUpdate(new SkillMeta
                {
                    Id = id,                         // 技能 ID（唯一标识一个技能）
                    Name = def.Name,                 // 技能名称（字符串，例如 "火球术"）
                                                     //School = def.Element.ToString(), // 技能所属元素或流派（枚举转字符串）
                                                     //Type = def.Type,                 // 技能类型（Damage/Heal/其他）——用于区分伤害技能和治疗技能
                                                     // Element = def.Element            // 技能元素类型（枚举，例如 火/冰/雷）
                });


            }

            // 2) 有些 ID 可能超出 int 或不在 AllByInt，可以再兜底遍历字符串键
            foreach (var kv in EmbeddedSkillConfig.AllByString)
            {
                if (kv.Key.TryToInt64(out var id))
                {
                    // 如果 int 表已覆盖，这里会覆盖同名；没关系，等价
                    var def = kv.Value;
                    // 将一条技能元数据（SkillMeta）写入 SkillBook 的全局字典中
                    // 这里用的是整条更新（SetOrUpdate），如果该技能 ID 已存在则覆盖，不存在则添加
                    SkillBook.SetOrUpdate(new SkillMeta
                    {
                        Id = id,                         // 技能 ID（唯一标识一个技能）
                        Name = def.Name,                 // 技能名称（字符串，例如 "火球术"）
                        //School = def.Element.ToString(), // 技能所属元素或流派（枚举转字符串）
                        //Type = def.Type,                 // 技能类型（Damage/Heal/其他）——用于区分伤害技能和治疗技能
                        //Element = def.Element            // 技能元素类型（枚举，例如 火/冰/雷）
                    });

                }
            }

            // MonsterNameResolver.Initialize(AppConfig.MonsterNames);//初始化怪物ID与名称的映射关系



            // 你也可以在这里写日志：加载了多少条技能
            // Console.WriteLine($"SkillBook loaded {EmbeddedSkillConfig.AllByInt.Count} + {EmbeddedSkillConfig.AllByString.Count} entries.");
        }

        public void SetStyle()
        {
            // # 启动与初始化事件：界面样式与渲染设置（仅 UI 外观，不涉及数据）
            // ======= 单个进度条（textProgressBar1）的外观设置 =======
            sortedProgressBarList_MainList.OrderImageOffset = new RenderContent.ContentOffset { X = 6, Y = 0 };
            sortedProgressBarList_MainList.OrderImageRenderSize = new Size(22, 22);
            sortedProgressBarList_MainList.OrderOffset = new RenderContent.ContentOffset { X = 32, Y = 0 };
            sortedProgressBarList_MainList.OrderCallback = (i) => $"{i:d2}.";
            sortedProgressBarList_MainList.OrderImages = [HandledAssets.皇冠];


            sortedProgressBarList_MainList.OrderColor =
                Config.IsLight ? Color.Black : Color.White;

            sortedProgressBarList_MainList.OrderFont = AppConfig.SaoFont;

            // ======= 进度条列表（sortedProgressBarList1）的初始化与外观 =======
            sortedProgressBarList_MainList.ProgressBarHeight = AppConfig.ProgressBarHeight;  // 每行高度
        }

        /// <summary>
        /// 通用提示气泡
        /// </summary>
        /// <param name="control"></param>
        /// <param name="text"></param>
        /// <remarks>
        /// 通用封装：在指定控件上显示提示文本
        /// </remarks>
        private void ToolTip(System.Windows.Forms.Control control, string text)
        {
            var tooltip = new TooltipComponent()
            {
                Font = HandledAssets.HarmonyOS_Sans(8),
                ArrowAlign = TAlign.TL
            };
            tooltip.SetTip(control, text);
        }
        #region StartCapture() 抓包：开始/停止/事件/统计
        /// <summary>
        /// 开始抓包
        /// </summary>
        public void StartCapture()
        {
            // 检查是否有可抓包设备
            var devices = CaptureDeviceList.Instance;
            if (devices == null || devices.Count == 0)
            {
                AppMessageBox.ShowMessage("没有找到可用的网络抓包设备, 请检查您的系统设置", this);
                return;
            }

            var netcardName = AppConfig.NetworkCardName;
            int netcardIndex;
            // 检查是否设置过网卡设备
            if (string.IsNullOrEmpty(netcardName))
            {
                // 首次自动设置网卡设备

                netcardIndex = CaptureDeviceHelper.GetBestNetworkCardIndex(devices);
                if (netcardIndex < 0)
                {
                    AppMessageBox.ShowMessage("我们未能为您自动设置网卡设备，请前往设置界面手动设置", this);
                    return;
                }

                AppConfig.NetworkCardName = devices[netcardIndex].Description;
            }
            else
            {
                // 已经设置过网卡设备
                netcardIndex = AppConfig.GetNetworkCardIndex(devices);
            }

            // 检查网卡设置变动
            // (首次如果设置失败会 return, 不会走到这里, 这里的再次判断防止网卡设备变动)
            if (netcardIndex < 0)
            {
                netcardIndex = CaptureDeviceHelper.GetBestNetworkCardIndex(devices);
                if (netcardIndex < 0)
                {
                    AppMessageBox.ShowMessage("网卡信息发生变动，我们未能为您自动设置网卡设备，请前往设置界面手动设置", this);
                    return;
                }
                else
                {
                    AppMessageBox.ShowMessage("网卡信息发生变动，已为您重新设置网卡设备，如有软件无法识别等情况，请手动重设设备", this);
                }
            }

            // 设置选择的网卡设备
            SelectedDevice = devices[netcardIndex];
            if (SelectedDevice == null)
            {
                AppMessageBox.ShowMessage($"获取网卡设备失败，[索引]名称: [{netcardIndex}]{netcardName}", this);
                return;
            }

            // 打开并启动设备监听 —— 绑定回调、设置过滤器
            SelectedDevice.Open(new DeviceConfiguration
            {
                Mode = DeviceModes.Promiscuous,
                Immediate = true,
                ReadTimeout = 1000,
                BufferSize = 1024 * 1024 * 4
            });
            SelectedDevice.Filter = "ip and tcp";
            SelectedDevice.OnPacketArrival += new PacketArrivalEventHandler(Device_OnPacketArrival);
            SelectedDevice.StartCapture();

            Console.WriteLine("开始抓包...");
        }

        #endregion
        #endregion

        private void HandleMouseThrough()
        {
            if (!MousePenetrationHelper.IsPenetrating(this.Handle))
            {
                // 方案 O：AppConfig.Transparency 现在表示“不透明度百分比”
                MousePenetrationHelper.SetMousePenetrate(this, enable: true, opacityPercent: AppConfig.Transparency);
            }
            else
            {
                MousePenetrationHelper.SetMousePenetrate(this, enable: false);
            }
        }

        private void HandleClearAllData()
        {
            DataStorage.ClearAllDpsData();

            _fullBattleTimer.Reset();
            _battleTimer.Reset();
        }

        private void HandleClearData()
        {
            DataStorage.ClearDpsData();

            _battleTimer.Reset();
        }

        private void UpdateBattleTimerText()
        {
            label_BattleTimeText.Text = TimeSpan.FromTicks(InUsingTimer.ElapsedTicks).ToString(@"hh\:mm\:ss");
        }

    }
}
