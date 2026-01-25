using System;
using System.Drawing;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using System.Windows.Forms;

using AntdUI;
using StarResonanceDpsAnalysis.Assets;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.WinForm.Control;
using StarResonanceDpsAnalysis.WinForm.Control.GDI;
using StarResonanceDpsAnalysis.WinForm.Extends;
using StarResonanceDpsAnalysis.WinForm.Plugin;

using Button = AntdUI.Button;
using System.Diagnostics;
using SharpPcap;
using StarResonanceDpsAnalysis.WinForm.Forms.PopUp;
using System.Numerics;

namespace StarResonanceDpsAnalysis.WinForm.Forms
{
    public partial class DpsStatisticsForm : BorderlessForm
    {
        private readonly Stopwatch _fullBattleTimer = new();
        private readonly Stopwatch _battleTimer = new();
        private Stopwatch InUsingTimer => _isShowFullData ? _fullBattleTimer : _battleTimer;
        public DpsStatisticsForm()
        {
            InitializeComponent();
            button_PcapOpen.Hide();
            button_PcapPause.Hide();
            button_PcapPlay.Hide();
            button_PcapStop.Hide();

            // 统一设置窗体默认 GUI 风格（字体、间距、阴影等）
            FormGui.SetDefaultGUI(this);
            //设置窗体颜色, 根据配置设置窗体的颜色主题（明亮/深色）
            FormGui.SetColorMode(this, AppConfig.IsLight);

            Text = FormManager.APP_NAME;

            // 从资源文件设置字体
            SetDefaultFontFromResources();

            // 安装键盘钩子，用于全局热键监听与处理
            RegisterKeyboardHook();

            // 初始化用户设置
            LoadAppConfig();

            // 读取玩家信息缓存
            LoadPlayerCache();

            // 加载技能配置
            LoadFromEmbeddedSkillConfig(); // 从内置资源读取并加载技能数据（元数据/图标/映射）

            SetStyle(); // 设置/应用本窗体的个性化样式（定义在同类/局部类的其他部分）

            // 监听服务器连接状态变更事件
            DataStorage.ServerConnectionStateChanged += DataStorage_ServerConnectionStateChanged;

            // 服务器变更事件
            DataStorage.ServerChanged += DataStorage_ServerChanged;

            // 启动新分段事件
            DataStorage.NewSectionCreated += DataStorage_NewSectionCreated;

            // 开始监听DPS更新事件
            DataStorage.DpsDataUpdated += DataStorage_DpsDataUpdated;

            Task.Run(async () =>
            {
                await Task.Delay(10000);
                if (DataStorage.IsServerConnected)
                {
                    return;
                }

                AppMessageBox.ShowMessage(
                    """
                    本次等待监听服务器比预想得耗时更久...
                    
                    没有启动游戏 或 网卡选择错误 也会造成监听不到服务器,
                    请确认您是否已经启动了游戏或您的网卡选择没有问题。
                    """,
                    this);

                await Task.Delay(10000);
                if (DataStorage.IsServerConnected)
                {
                    return;
                }

                AppMessageBox.ShowMessage(
                    """
                    本次等待监听服务器比预想得... 更加不顺利...
                    
                    如果您已经启动游戏, 那么我们强烈建议您检查网卡设置。
                    """,
                    this);
            });
        }

        /// <summary>
        /// 窗体加载事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DpsStatistics_Load(object sender, EventArgs e) // 窗体 Load 事件处理
        {
            // 启动网络抓包/数据采集
            StartCapture();

            // 重置为上次关闭前的位置与大小
            SetStartupPositionAndSize();

            EnsureTopMost();
        }

        /// <summary>
        /// 数据包到达事件
        /// </summary>
        private void Device_OnPacketArrival(object sender, PacketCapture e)
        {
            // # 抓包事件：回调于数据包到达时（SharpPcap线程）
            try
            {
                var dev = (ICaptureDevice)sender;
                PacketAnalyzer.StartNewAnalyzer(dev, e.GetPacket());
            }
            catch (Exception ex)
            {
                // # 异常保护：避免抓包线程因未处理异常中断
                Console.WriteLine($"数据包到达后进行处理时发生异常 {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 监听服务器连接状态变更事件
        /// </summary>
        /// <param name="serverConnectionState"></param>
        private void DataStorage_ServerConnectionStateChanged(bool serverConnectionState)
        {
            if (serverConnectionState)
            {
                Invoke(() =>
                {
                    timer_BattleTimeLabelUpdater.Enabled = true;
                });
            }
            else
            {
                Invoke(() =>
                {
                    timer_BattleTimeLabelUpdater.Enabled = false;
                    label_BattleTimeText.Text = $"请稍等，正在准备监听服务器...";
                });
            }
        }

        #region 钩子
        private KeyboardHook KbHook { get; } = new();
        public void RegisterKeyboardHook()
        {
            KbHook.SetHook();
            KbHook.OnKeyDownEvent += kbHook_OnKeyDownEvent;
        }

        public void kbHook_OnKeyDownEvent(object? sender, KeyEventArgs e)
        {
            if (e.KeyData == AppConfig.MouseThroughKey)
            {
                HandleMouseThrough();
            }
            else if (e.KeyData == AppConfig.ClearDataKey)
            {
                Action clearHandler = _isShowFullData
                    ? HandleClearAllData
                    : HandleClearData;

                clearHandler();
            }
        }

        #endregion

        private void DataStorage_ServerChanged(string currentServer, string prevServer)
        {
            Console.WriteLine($"ServerChanged: {prevServer} => {currentServer}");

            if (AppConfig.ClearAllDataWhenSwitch)
            {
                HandleClearAllData();
            }
        }

        private void DataStorage_NewSectionCreated()
        {
            _battleTimer.Reset();
        }

        private readonly Dictionary<long, List<RenderContent>> _renderListDict = [];
        private void DataStorage_DpsDataUpdated()
        {
            if (!_fullBattleTimer.IsRunning)
            {
                _fullBattleTimer.Restart();
            }
            if (!_battleTimer.IsRunning)
            {
                _battleTimer.Restart();
            }

            UpdateSortProgressBarListData();
        }

        /// <summary>
        /// 更新主界面中的 DPS 进度条列表数据
        /// </summary>
        private void UpdateSortProgressBarListData()
        {
            // 根据选择，决定显示全程数据还是分段数据
            var dpsList = _isShowFullData
                ? DataStorage.ReadOnlyFullDpsDataList
                : DataStorage.ReadOnlySectionedDpsDataList;

            // 如果没有任何数据，清空界面显示
            if (dpsList.Count == 0)
            {
                sortedProgressBarList_MainList.Data = [];
                label_CurrentDps.Text = "-- (--)";
                return;
            }

            // 先对数据做筛选（比如过滤掉无效/被排除的玩家）
            var dpsIEnum = GetDefaultFilter(dpsList, _stasticsType);
            if (!dpsIEnum.Any())
            {
                sortedProgressBarList_MainList.Data = [];
                label_CurrentDps.Text = "-- (--)";
                return;
            }

            // 获取该类型下的最大值和总和（用于计算进度条比例和百分比）
            (var maxValue, var sumValue) = GetMaxSumValueByType(dpsIEnum, _stasticsType);

            // 遍历每个玩家的数据，生成进度条数据
            var progressBarDataList = dpsIEnum
                .Select(e =>
                {
                    // 根据 UID 找到玩家的额外信息（职业、名字、战力等）
                    DataStorage.ReadOnlyPlayerInfoDatas.TryGetValue(e.UID, out var playerInfo);
                    var professionName = playerInfo?.SubProfessionName
                                         ?? playerInfo?.ProfessionID?.GetProfessionNameById()
                                         ?? string.Empty;

                    // 如果之前没有渲染数据，先新建一份
                    if (!_renderListDict.TryGetValue(e.UID, out var renderContent))
                    {
                        var profBmp = professionName.GetProfessionBitmap();
                        renderContent = BuildNewRenderContent(profBmp);
                        _renderListDict[e.UID] = renderContent;
                    }

                    // 确保职业图标有值（有些玩家可能一开始没加载出来）
                    if (renderContent[0].Image == ProfessionThemeExtends.EmptyBitmap)
                    {
                        renderContent[0].Image = professionName.GetProfessionBitmap();
                    }

                    // 获取玩家在该统计类型下的数值（伤害/治疗/承伤）
                    var value = GetValueByType(e, _stasticsType);

                    // 昵称/职业/战力（或 UID）的显示文本
                    renderContent[1].Text = $"{(playerInfo?.Name == null ? string.Empty : $"{playerInfo.Name}-")}{playerInfo?.SubProfessionName ?? professionName}({playerInfo?.CombatPower?.ToString() ?? ($"UID: {e.UID}")})";

                    // 总数值 + 平均每秒（DPS/HPS等）
                    renderContent[2].Text = $"{Vsh(value)} ({Vsh(value / Math.Max(1, TimeSpan.FromTicks(e.LastLoggedTick - (e.StartLoggedTick ?? 0)).TotalSeconds))})";

                    // 团队占比（四舍五入为整数百分比）
                    renderContent[3].Text = $"{Math.Round(100d * value / sumValue, 0, MidpointRounding.AwayFromZero)}%";

                    // 返回进度条数据
                    return new ProgressBarData()
                    {
                        ID = e.UID, // 绑定玩家 ID
                        ProgressBarColor = professionName.GetProfessionThemeColor(Config.IsLight), // 进度条颜色随职业变化
                        ProgressBarCornerRadius = 3, // 圆角大小
                        ProgressBarValue = (float)value / maxValue, // 当前值/最大值 → 进度条比例
                        ContentList = renderContent // 渲染文本和图片
                    };
                }).ToList();

            // 把生成的进度条数据放到主列表控件上
            sortedProgressBarList_MainList.Data = progressBarDataList;

            // 当前玩家的 DPS 数据源（全程 or 分段）
            var dd = _isShowFullData
                ? DataStorage.ReadOnlyFullDpsDatas
                : DataStorage.ReadOnlySectionedDpsDatas;

            // 如果找不到当前玩家的数据，就显示占位符
            if (!dd.TryGetValue(DataStorage.CurrentPlayerInfo.UID, out DpsData? cpdd))
            {
                label_CurrentDps.Text = "-- (--)";
                return;
            }

            // 当前玩家的数值（比如当前玩家的总伤害/总治疗）
            var cv = GetValueByType(cpdd, _stasticsType);

            // 显示当前玩家的总数值 + 每秒平均值
            label_CurrentDps.Text = $"{Vsh(cv)} ({Vsh(cv / Math.Max(1, TimeSpan.FromTicks(cpdd.LastLoggedTick - (cpdd.StartLoggedTick ?? 0)).TotalSeconds))})";
        }

        /// <summary>
        /// 获取每个统计类别的默认筛选器
        /// </summary>
        /// <param name="list"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private IEnumerable<DpsData> GetDefaultFilter(IEnumerable<DpsData> list, int type)
        {
            return type switch
            {
                0 => list.Where(e => !e.IsNpcData && e.TotalAttackDamage != 0),
                1 => list.Where(e => !e.IsNpcData && e.TotalHeal != 0),
                2 => list.Where(e => !e.IsNpcData && e.TotalTakenDamage != 0),
                3 => list.Where(e => e.IsNpcData && e.TotalTakenDamage != 0),

                _ => list
            };
        }

        private (long max, long sum) GetMaxSumValueByType(IEnumerable<DpsData> list, int type)
        {
            return type switch
            {
                0 => (list.Max(e => e.TotalAttackDamage), list.Sum(e => e.TotalAttackDamage)),
                1 => (list.Max(e => e.TotalHeal), list.Sum(e => e.TotalHeal)),
                2 or 3 => (list.Max(e => e.TotalTakenDamage), list.Sum(e => e.TotalTakenDamage)),

                _ => (long.MaxValue, long.MaxValue)
            };
        }

        private Func<T, int, string> GetGroupingHandler<T>(int type) where T : INumber<T>
        {
            return type switch
            {
                0 => NumberExtends.ToCompactString,
                1 => NumberExtends.ToChineseUnitString,

                _ => (value, digit) => value.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// ValueShowHandler (Vsh)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="number"></param>
        /// <returns></returns>
        private string Vsh<T>(T number) where T : INumber<T>
        {
            return GetGroupingHandler<T>(AppConfig.DamageDisplayType).Invoke(number, 2);
        }

        private long GetValueByType(DpsData data, int type)
        {
            return type switch
            {
                0 => data.TotalAttackDamage,
                1 => data.TotalHeal,
                2 or 3 => data.TotalTakenDamage,

                _ => long.MaxValue
            };
        }

        private List<RenderContent> BuildNewRenderContent(Bitmap professionBmp)
        {
            return [
                new() { Type = RenderContent.ContentType.Image, Align = RenderContent.ContentAlign.MiddleLeft, Offset = AppConfig.ProgressBarImage, Image = professionBmp, ImageRenderSize = AppConfig.ProgressBarImageSize },
                new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleLeft, Offset = AppConfig.ProgressBarNmae, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
                new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleRight, Offset = AppConfig.ProgressBarHarm, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
                new() { Type = RenderContent.ContentType.Text, Align = RenderContent.ContentAlign.MiddleRight, Offset = AppConfig.ProgressBarProportion, ForeColor = AppConfig.colorText, Font = AppConfig.ProgressBarFont },
            ];
        }

        // # 顶部：置顶窗口按钮
        private void button_AlwaysOnTop_Click(object sender, EventArgs e) // 置顶按钮点击事件
        {
            TopMost = !TopMost; // 简化切换
            button_AlwaysOnTop.Toggle = TopMost; // 同步按钮的视觉状态
        }

        #region 切换显示类型（支持单次/全程伤害） // 折叠：视图标签与切换逻辑

        /// <summary>
        /// 获取当前统计类型 (伤害 / 治疗 / 承伤 / NPC承伤)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string StasticsTypeToName(int type)
        {
            return type switch
            {
                0 => "伤害",
                1 => "治疗",
                2 => "承伤",
                3 => "NPC承伤",

                _ => string.Empty
            };
        }

        /// <summary>
        /// 根据当前模式与索引更新顶部标签文本
        /// </summary>
        private void UpdateHeaderText()
        {
            pageHeader_MainHeader.SubText = $"{(_isShowFullData ? "全程" : "当前")} · {StasticsTypeToName(_stasticsType)}";
        }



        /// <summary>
        /// 单次 / 全程切换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_SwitchStatisticsMode_Click(object sender, EventArgs e) // 单次/全程切换按钮事件
        {
            _isShowFullData = !_isShowFullData;

            // 更新标题状态副文本
            UpdateHeaderText();

            // 更新战斗时长文本
            UpdateBattleTimerText();

            // 更新面板数据
            UpdateSortProgressBarListData();
            // button_LoadPcap_Click(sender, e);
        }
        #endregion

        /// <summary>
        /// 清空当前数据数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_RefreshDps_Click(object sender, EventArgs e) // 清空按钮点击：触发清空逻辑
        {
            Action clearHandler = _isShowFullData
                ? HandleClearAllData
                : HandleClearData;

            clearHandler();
        }


        private readonly IContextMenuStripItem[] _menulist =
        [
            new ContextMenuStripItem("基础设置"){ IconSvg = HandledAssets.Set_Up },
            new ContextMenuStripItem("关于"){ IconSvg = HandledAssets.HomeIcon },
            new ContextMenuStripItem("退出"){ IconSvg = HandledAssets.Quit, },
        ];
        /// <summary>
        /// 设置按钮点击
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_Settings_Click(object sender, EventArgs e)
        {
            AntdUI.ContextMenuStrip.open(this, it =>
            {
                switch (it.Text)
                {
                    case "基础设置":
                        OpenSettingsDialog();
                        break;

                    case "关于":
                        FormManager.MainForm.Show();
                        break;

                    case "退出":
                        Application.Exit();
                        break;
                }
            }, _menulist);
        }

        /// <summary>
        /// Pcap 打开按钮点击
        /// </summary>
        private void button_PcapOpen_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog()
            {
                Filter = "Capture files (*.pcap;*.pcapng)|*.pcap;*.pcapng|All files (*.*)|*.*",
                Title = "选择 Pcap/Pcapng 文件进行回放"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            _lastPcapFilePath = dlg.FileName;
            StartPcapReplay(_lastPcapFilePath, realtime: true, speed: 1.0);

            this.Invoke(() =>
            {
                label_BattleTimeText.Text = $"正在回放: {System.IO.Path.GetFileName(dlg.FileName)}";
            });
        }

        /// <summary>
        /// Pcap 播放按钮点击 - 重新加载最后一个文件
        /// </summary>
        private void button_PcapPlay_Click(object sender, EventArgs e)
        {
            if (_lastPcapFilePath == null)
            {
                AppMessageBox.ShowMessage("请先使用打开按钮选择一个 Pcap 文件", this);
                return;
            }

            StartPcapReplay(_lastPcapFilePath, realtime: true, speed: 1.0);
        }

        /// <summary>
        /// Pcap 暂停按钮点击 - 当前实现停止播放
        /// </summary>
        private void button_PcapPause_Click(object sender, EventArgs e)
        {
            StopPcapReplay();
            this.Invoke(() =>
            {
                label_BattleTimeText.Text = "回放已暂停";
            });
        }

        /// <summary>
        /// Pcap 停止按钮点击
        /// </summary>
        private void button_PcapStop_Click(object sender, EventArgs e)
        {
            StopPcapReplay();
            _lastPcapFilePath = null;
            this.Invoke(() =>
            {
                label_BattleTimeText.Text = "回放已停止";
            });
        }

        /// <summary>
        /// 打开基础设置面板
        /// </summary>
        private void OpenSettingsDialog()
        {
            FormManager.SettingsForm.Show();
        }

        /// <summary>
        /// 按钮提示气泡 (置顶)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_AlwaysOnTop_MouseEnter(object sender, EventArgs e)
        {
            // 显示 "置顶窗口" 的气泡提示
            ToolTip(button_AlwaysOnTop, "置顶窗口");
        }

        // # 按钮提示气泡（清空）
        private void button_RefreshDps_MouseEnter(object sender, EventArgs e) // 鼠标进入“清空”按钮时显示提示
        {
            ToolTip(button_RefreshDps, "清空当前数据"); // 显示“清空当前数据”的气泡提示
        }

        // # 按钮提示气泡（单次/全程切换）
        private void button_SwitchStatisticsMode_MouseEnter(object sender, EventArgs e) // 鼠标进入“单次/全程切换”按钮时显示提示
        {
            ToolTip(button_SwitchStatisticsMode, "点击切换：单次统计/全程统"); // 显示切换提示（原文如此，保留）
        }

        /// <summary>
        /// 主题切换
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DpsStatisticsForm_ForeColorChanged(object sender, EventArgs e)
        {
            if (Config.IsLight)
            {
                ChangeToLightTheme();
            }
            else
            {
                ChangeToDarkTheme();
            }

            UpdateSortProgressBarListData();

            SetSortedProgressBarListForeColor();
        }

        private List<Button> _stasticsTypeButtons => [button_TotalDamage, button_TotalTreatment, button_AlwaysInjured, button_NpcTakeDamage];
        private void ChangeToLightTheme()
        {
            AppConfig.colorText = Color.Black;

            sortedProgressBarList_MainList.BackColor = ColorTranslator.FromHtml("#F5F5F5");
            sortedProgressBarList_MainList.OrderColor = Color.Black;

            panel_Footer.Back = ColorTranslator.FromHtml("#F5F5F5");
            panel_ModeBox.Back = ColorTranslator.FromHtml("#F5F5F5");

            button_TotalDamage.Icon = HandledAssets.伤害;
            button_TotalTreatment.Icon = HandledAssets.治疗;
            button_AlwaysInjured.Icon = HandledAssets.承伤;
            button_NpcTakeDamage.Icon = HandledAssets.Npc;

            foreach (var item in _stasticsTypeButtons)
            {
                item.DefaultBack = Color.FromArgb(247, 247, 247);
            }
            _stasticsTypeButtons[_stasticsType].DefaultBack = Color.FromArgb(223, 223, 223);
        }

        private void ChangeToDarkTheme()
        {
            AppConfig.colorText = Color.White;
            sortedProgressBarList_MainList.BackColor = ColorTranslator.FromHtml("#252527");
            sortedProgressBarList_MainList.OrderColor = Color.White;

            panel_Footer.Back = ColorTranslator.FromHtml("#252527");
            panel_ModeBox.Back = ColorTranslator.FromHtml("#252527");

            button_TotalDamage.Icon = HandledAssets.伤害白色;
            button_TotalTreatment.Icon = HandledAssets.治疗白色;
            button_AlwaysInjured.Icon = HandledAssets.承伤白色;
            button_NpcTakeDamage.Icon = HandledAssets.NpcWhite;

            foreach (var item in _stasticsTypeButtons)
            {
                item.DefaultBack = Color.FromArgb(27, 27, 27);
            }
            _stasticsTypeButtons[_stasticsType].DefaultBack = Color.FromArgb(60, 60, 60);
        }

        private void SetSortedProgressBarListForeColor()
        {
            if (sortedProgressBarList_MainList.Data == null) return;

            lock (sortedProgressBarList_MainList.Data)
            {
                foreach (var data in sortedProgressBarList_MainList.Data)
                {
                    if (data.ContentList == null) continue;

                    foreach (var content in data.ContentList)
                    {
                        if (content.Type != RenderContent.ContentType.Text) continue;

                        content.ForeColor = AppConfig.colorText;
                    }
                }
            }
        }

        private void SetStartupPositionAndSize()
        {
            var startupRect = AppConfig.StartUpState;
            if (startupRect != null && startupRect != Rectangle.Empty)
            {
                Left = startupRect.Value.Left;
                Top = startupRect.Value.Top;
                Width = startupRect.Value.Width;
                Height = startupRect.Value.Height;
            }
        }



        private void button_Minimum_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void DpsStatisticsForm_Shown(object sender, EventArgs e)
        {

        }

        private void EnsureTopMost()
        {
            // 先关再开, 强制触发样式刷新
            FormManager.SetTopMost(false);
            FormManager.SetTopMost(true);

            Activate();
            BringToFront();

            // 同步按钮状态
            button_AlwaysOnTop.Toggle = TopMost;
        }

        private void TypeButtons_Click(object sender, EventArgs e)
        {
            Button button = (Button)sender;
            List<Button> buttonList = [button_TotalDamage, button_TotalTreatment, button_AlwaysInjured, button_NpcTakeDamage];
            Color colorBack = Color.FromArgb(60, 60, 60);
            Color colorWhite = Color.FromArgb(223, 223, 223);
            foreach (Button btn in buttonList)
            {
                btn.DefaultBack = btn.Name == button.Name
                    ? Config.IsLight ? colorWhite : colorBack
                    : Config.IsLight ? Color.FromArgb(247, 247, 247) : Color.FromArgb(27, 27, 27);
            }

            _stasticsType = button.Tag.ToInt();

            // 刷新顶部文本
            UpdateHeaderText();
            // 刷新表单数据
            UpdateSortProgressBarListData();
        }

        private void DpsStatisticsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            AppConfig.StartUpState = new Rectangle(Left, Top, Width, Height);

            DataStorage.DpsDataUpdated -= DataStorage_DpsDataUpdated;
            DataStorage.SavePlayerInfoToFile();

            try { KbHook?.UnHook(); }
            catch (Exception ex) { Console.WriteLine($"窗体关闭清理时出错: {ex.Message}"); }
        }

        private void button_ThemeSwitch_Click(object sender, EventArgs e)
        {
            // # 状态翻转：明/暗
            AppConfig.IsLight = !AppConfig.IsLight;

            button_ThemeSwitch.Toggle = !AppConfig.IsLight; // # UI同步：按钮切换状态

            // 通知其他窗口更新主题
            FormGui.SetColorMode(this, AppConfig.IsLight);
            FormGui.SetColorMode(FormManager.MainForm, AppConfig.IsLight);
            FormGui.SetColorMode(FormManager.SettingsForm, AppConfig.IsLight);
            FormGui.SetColorMode(FormManager.DpsStatistics, AppConfig.IsLight);
        }

        private void timer_BattleTimeLabelUpdater_Tick(object sender, EventArgs e)
        {
            UpdateBattleTimerText();
        }

        // PCAP replay helpers (insert into the DpsStatisticsForm partial class)
        private System.Threading.CancellationTokenSource? _replayCts;
        private Task? _replayTask;
        private string? _lastPcapFilePath;

        /// <summary>
        /// Start replaying a pcap/pcapng file into the existing PacketAnalyzer.
        /// Non-blocking: runs on a background task and uses a CancellationToken to stop.
        /// </summary>
        private void StartPcapReplay(string filePath, bool realtime = true, double speed = 1.0)
        {
            // stop any existing replay first
            StopPcapReplay();

            _replayCts = new System.Threading.CancellationTokenSource();
            var token = _replayCts.Token;

            // run replay in background so UI stays responsive
            _replayTask = Task.Run(async () =>
            {
                try
                {
                    // PcapReplay.ReplayFileAsync will call PacketAnalyzer.ProcessPacket for each packet
                    await PacketAnalyzer.ReplayFileAsync(filePath, realtime, speed, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // expected on stop
                }
                catch (Exception ex)
                {
                    // minimal logging; marshal to UI if needed
                    Console.WriteLine($"Pcap replay failed: {ex.Message}");
                }
                finally
                {
                    // ensure cleanup on completion
                    try { _replayCts?.Dispose(); } catch { }
                    _replayCts = null;
                    _replayTask = null;
                }
            }, token);
        }

        /// <summary>
        /// Stop any running pcap replay (cancels and waits briefly).
        /// </summary>
        private void StopPcapReplay()
        {
            if (_replayCts == null) return;

            try
            {
                _replayCts.Cancel();
                // wait a short time for graceful shutdown; avoid blocking UI thread
                _replayTask?.Wait(millisecondsTimeout: 3000);
            }
            catch (AggregateException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"StopPcapReplay error: {ex.Message}");
            }
            finally
            {
                try { _replayCts.Dispose(); } catch { }
                _replayCts = null;
                _replayTask = null;
            }
        }

        /// <summary>
        /// Example button handler: pick a pcap file and start replay.
        /// Wire this to a Button's Click event in the designer or call it from code.
        /// </summary>
        private void button_LoadPcap_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog()
            {
                Filter = "Capture files (*.pcap;*.pcapng)|*.pcap;*.pcapng|All files (*.*)|*.*",
                Title = "Open pcap/pcapng file to replay"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            // optional: ask user for realtime/speed settings; here we use defaults
            StartPcapReplay(dlg.FileName, realtime: true, speed: 1.0);

            // update UI state (invoke on UI thread)
            this.Invoke(() =>
            {
                // show simple feedback
                MessageBox.Show(this, $"Replaying {System.IO.Path.GetFileName(dlg.FileName)}...", "PCAP Replay", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }
    }
}
