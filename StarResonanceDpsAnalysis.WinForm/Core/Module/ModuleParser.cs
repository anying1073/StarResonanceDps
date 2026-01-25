using System;
using System.Collections.Generic;
using System.Linq;

using AntdUI;
using StarResonanceDpsAnalysis.WinForm.Forms;

using static StarResonanceDpsAnalysis.WinForm.Core.Module.ModuleCardDisplay;
using static StarResonanceDpsAnalysis.WinForm.Core.Module.ModuleOptimizer;
using static StarResonanceDpsAnalysis.WinForm.Forms.ModuleForm.ModuleCalculationForm;

// 0472 -> 针对可控类型与 null 的比较, 由于使用了 BlueprotobufPb2 的固定类型, 本页禁用该警告
#pragma warning disable 0472

namespace StarResonanceDpsAnalysis.WinForm.Core.Module
{
    public class BuildEliteCandidatePool
    {
        public static string type = "全部";
        public static List<string> Attributes = new List<string>();

        public static ModuleOptimizer.SortMode SortBy { get; set; } = ModuleOptimizer.SortMode.ByTotalAttr;

        public static Dictionary<int, string> WhitelistPickByRow { get; } = new(); // 行号→属性名
        public static Dictionary<string, int> DesiredLevels { get; } = new(StringComparer.Ordinal); // 属性名→目标等级(1~6)


        // === 筛选配置 ===
        public static HashSet<string> ExcludedAttributes { get; set; } = new(StringComparer.Ordinal); // 要排除的属性
        public static List<string> RequiredAttributes { get; set; } = new(); // 需要包含的词条集合
        public static int RequiredAttributeCount { get; set; } = 0;          // 需要命中的词条数量（全局组合维度）
        public static int? ExactPartCount { get; set; } = null;              // 精确词条数（每个模组）
        public static int? MinPartCount { get; set; } = null;                // 词条数下限
        public static int? MaxPartCount { get; set; } = null;                // 词条数上限
        public static bool MixCategories { get; set; } = false;              // 允许攻击/守护/辅助混搭

        /// <summary>
        /// 清空一次本次筛选中所有用户选择（白名单、排除、等级等）。
        /// keepSortMode=true 则保留当前排序模式；false 则回到默认。
        /// </summary>
        public static void ResetSelections(bool keepSortMode = true)
        {
            Attributes.Clear();
            ExcludedAttributes.Clear();
            WhitelistPickByRow.Clear();
            DesiredLevels.Clear();

            if (!keepSortMode)
            {
                SortBy = ModuleOptimizer.SortMode.ByTotalAttr; // 想恢复到“默认按属性”
                                                               // 如果你希望恢复到“默认按战力”，这里改为：ModuleOptimizer.SortMode.ByScore;
            }
        }

        /// <summary>
        /// 解析模组信息（等价于 Python 版 parse_module_info）
        /// - 从 protobuf payload 中提取 CharSerialize
        /// - 遍历所有包裹 Items → 筛选出有 ModNewAttr 的物品
        /// - 构造 ModuleInfo + ModulePart
        /// - 进行属性数量、白名单、排除属性的过滤
        /// - 最终调用 FilterModulesByAttributes 进入优化流程
        /// </summary>
        public static void ParseModuleInfo(byte[] payloadBuffer)
        {
            ModuleResultMemory.Clear();

            // 解析 protobuf，同 Python: v_data = syncContainerData.VData
            var syncContainerData = Zproto.WorldNtf.Types.SyncContainerData.Parser.ParseFrom(payloadBuffer);
            var Serialize = syncContainerData.VData;

            // 如果没有 ModInfos，直接返回
            if (Serialize?.Mod?.ModInfos == null) return;
            var mod_infos = Serialize?.Mod?.ModInfos;
            if (Serialize?.ItemPackage.Packages == null) return;

            var modules = new List<ModuleInfo>();

            // 遍历背包 Packages
            foreach (var kv in Serialize.ItemPackage.Packages)
            {
                int packageType = kv.Key;
                var package = kv.Value;

                foreach (var item in package.Items)
                {
                    var key = item.Key;
                    var value = item.Value;

                    // 如果 item 有 ModNewAttr 且包含 ModParts → 说明是模组
                    if (value != null && value.ModNewAttr.ModParts.Count > 0)
                    {
                        int config_id = value.ConfigId;

                        // 模组中文名（从映射表里取）
                        string module_name = ModuleMaps.MODULE_NAMES[config_id];

                        var modParts = item.Value.ModNewAttr.ModParts;

                        // 取 ModInfos 里对应的属性值表
                        var modInfoVal = mod_infos?.GetValueOrDefault(key) ?? default;

                        // 构造 ModuleInfo
                        var module_info = new ModuleInfo
                        {
                            Name = module_name,
                            ConfigId = config_id,
                            Uuid = value.Uuid,
                            Quality = value.Quality,
                            Parts = new List<ModulePart>()
                        };

                        // 词条数 = min(ModParts, InitLinkNums)
                        var init_link_nums = modInfoVal?.InitLinkNums;
                        int n = Math.Min(modParts.Count, init_link_nums?.Count ?? 0);

                        for (int i = 0; i < n; i++)
                        {
                            int partId = modParts[i];

                            // 属性中文名，如果没找到就标记未知属性
                            string attrName = ModuleMaps.MODULE_ATTR_NAMES.TryGetValue(partId, out var name)
                                ? name
                                : $"未知属性({partId})";

                            int attrValue = init_link_nums?[i] ?? 0;

                            // 构造 ModulePart
                            var modulePart = new ModulePart
                            {
                                Id = partId,
                                Name = attrName,
                                Value = attrValue,
                            };

                            module_info.Parts.Add(modulePart);
                        }

                        modules.Add(module_info);
                    }
                    else
                    {
                        // 不是模组包，跳过
                        break;
                    }
                }
            }



            // 如果解析到了模组
            if (modules.Count > 0)
            {

                // 这里存放筛选后的模组列表
                List<ModuleInfo> filteredModules;


                // 判断是否给了“包含属性清单”和“排除属性清单”
                bool hasIncludeAttributes = (Attributes != null && Attributes.Count > 0);
                bool hasExcludeAttributes = (ExcludedAttributes != null && ExcludedAttributes.Count > 0);

                if (hasIncludeAttributes || hasExcludeAttributes)
                {
                    // 至少需要命中的属性数量
                    // 在 Python 中 match_count 默认是 1
                    // 在这里我们使用 RequiredAttributeCount，如果它小于 1，就退化为 1
                    int minimumMatchCount;
                    if (BuildEliteCandidatePool.RequiredAttributeCount < 1)
                    {
                        minimumMatchCount = 1;
                    }
                    else
                    {
                        minimumMatchCount = BuildEliteCandidatePool.RequiredAttributeCount;
                    }

                    // 转换排除属性集合（HashSet → List）
                    List<string>? excludeList = null;
                    if (ExcludedAttributes != null)
                    {
                        excludeList = ExcludedAttributes.ToList();
                    }

                    // 调用我们写好的属性筛选函数
                    // 参数解释：
                    //   modules           → 原始模组列表
                    //   attributes        → 需要包含的属性词条（白名单）
                    //   excludeAttributes → 需要排除的属性词条（黑名单）
                    //   matchCount        → 至少需要命中的数量
                    filteredModules = BuildEliteCandidatePool.FilterModulesByAttributes(
                        modules,
                        Attributes,
                        excludeList,
                        minimumMatchCount
                    );

                    // 如果需要调试日志，可以在这里输出数量
                    // Console.WriteLine("属性筛选后剩余 " + filteredModules.Count + " 个模组");
                }
                else
                {
                    // 如果既没有包含属性清单，也没有排除属性清单
                    // 那么不做筛选，直接使用原始列表
                    filteredModules = modules;
                }

                // 最后一步：调用优化函数（等价于 Python 里的 _optimize_module_combinations）
                // 把筛选后的模组列表传进去，和指定的类型一起交给优化器处理
                FilterModulesByAttributes(filteredModules, BuildEliteCandidatePool.type);
            }

        }

        /// <summary>
        /// 根据属性词条筛选模组 —— 与 Python 版 _filter_modules_by_attributes 等价
        /// Args:
        ///   modules: 模组列表
        ///   attributes: 要筛选的属性词条列表（命中 >= matchCount）
        ///   excludeAttributes: 要排除的属性词条列表（命中任一即排除）
        ///   matchCount: 模组需要包含的指定词条数量，默认 1
        /// Returns:
        ///   筛选后的模组列表
        /// </summary>
        public static List<ModuleInfo> FilterModulesByAttributes(
            List<ModuleInfo> modules,
            List<string>? attributes = null,
            List<string>? excludeAttributes = null,
            int matchCount = 1)
        {
            // 筛选后的结果集合
            List<ModuleInfo> filteredModules = [];

            // 如果模块列表为空，直接返回空结果
            if (modules == null || modules.Count == 0)
            {
                return filteredModules;
            }

            // 用于快速查询的 HashSet（包含属性）
            HashSet<string>? includeSet = null;
            if (attributes != null && attributes.Count > 0)
            {
                includeSet = new HashSet<string>(attributes, StringComparer.Ordinal);
            }

            // 用于快速查询的 HashSet（排除属性）
            HashSet<string>? excludeSet = null;
            if (excludeAttributes != null && excludeAttributes.Count > 0)
            {
                excludeSet = new HashSet<string>(excludeAttributes, StringComparer.Ordinal);
            }

            // 至少需要命中的数量，和 Python 一样，最少是 1
            int minimumMatchCount;
            if (matchCount < 1)
            {
                minimumMatchCount = 1;
            }
            else
            {
                minimumMatchCount = matchCount;
            }

            // 遍历每一个模组
            foreach (ModuleInfo module in modules)
            {
                if (module == null)
                {
                    continue;
                }

                // 收集模组的所有属性名称
                List<string> moduleAttributes = new List<string>();
                if (module.Parts != null)
                {
                    foreach (ModulePart part in module.Parts)
                    {
                        if (part != null && !string.IsNullOrEmpty(part.Name))
                        {
                            moduleAttributes.Add(part.Name);
                        }
                    }
                }

                // 步骤 1：检查是否包含排除属性
                if (excludeSet != null)
                {
                    bool hasExcluded = false;
                    foreach (string attr in moduleAttributes)
                    {
                        if (excludeSet.Contains(attr))
                        {
                            hasExcluded = true;
                            break;
                        }
                    }

                    if (hasExcluded)
                    {
                        // 如果包含排除属性，就跳过该模组
                        continue;
                    }
                }

                // 步骤 2：检查包含属性数量
                if (includeSet != null)
                {
                    int hitCount = 0;
                    foreach (string attr in moduleAttributes)
                    {
                        if (includeSet.Contains(attr))
                        {
                            hitCount++;
                        }
                    }

                    if (hitCount < minimumMatchCount)
                    {
                        // 如果命中数量不足要求，就跳过该模组
                        continue;
                    }

                    // 否则通过筛选
                }
                else
                {
                    // 没有提供包含属性时，直接通过筛选
                }

                // 如果通过所有检查，加入结果集合
                filteredModules.Add(module);
            }

            // 返回筛选后的模组集合
            return filteredModules;
        }


        /// 筛选模组并调用优化器
        /// （对应 Python 版 _optimize_module_combinations）
        /// </summary>
        public static void FilterModulesByAttributes(List<ModuleInfo> modules, string category)
        {
            // 0) 保护：数量不足 4 无法组成一套
            if (modules == null || modules.Count < 4)
            {
                ModuleCardDisplay.ModuleResultMemory.Clear();
                return;
            }

            // 1) 把“中文类别字符串” → 你的外部枚举 ModuleCategory
            ModuleCategory uiCategory = BuildEliteCandidatePool.MixCategories ? ModuleCategory.ALL
                : category switch
                {
                    "攻击" => ModuleCategory.ATTACK,
                    "守护" => ModuleCategory.GUARDIAN,
                    "辅助" => ModuleCategory.SUPPORT,
                    "全部" => ModuleCategory.ALL,
                    _ => ModuleCategory.ATTACK
                };

            // 2) 再把“你的外部枚举” → 优化器内部的枚举 ModuleOptimizer.ModuleCategory
            ModuleOptimizer.ModuleCategory optCategory = uiCategory switch
            {
                ModuleCategory.ATTACK => ModuleOptimizer.ModuleCategory.ATTACK,
                ModuleCategory.GUARDIAN => ModuleOptimizer.ModuleCategory.DEFENSE, // 守护 → DEFENSE（优化器的命名）
                ModuleCategory.SUPPORT => ModuleOptimizer.ModuleCategory.SUPPORT,
                ModuleCategory.ALL => ModuleOptimizer.ModuleCategory.ALL,
                _ => ModuleOptimizer.ModuleCategory.ATTACK
            };

            // 3) 把“配置ID→分类”的映射，也从你的外部枚举 转成 优化器的内部枚举
            //    Dictionary<int, ModuleCategory> -> IReadOnlyDictionary<int, ModuleOptimizer.ModuleCategory>
            var categoryMapForOptimizer = ModuleMaps.MODULE_CATEGORY_MAP
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value switch
                    {
                        ModuleCategory.ATTACK => ModuleOptimizer.ModuleCategory.ATTACK,
                        ModuleCategory.GUARDIAN => ModuleOptimizer.ModuleCategory.DEFENSE,
                        ModuleCategory.SUPPORT => ModuleOptimizer.ModuleCategory.SUPPORT,
                        ModuleCategory.ALL => ModuleOptimizer.ModuleCategory.ALL,
                        _ => ModuleOptimizer.ModuleCategory.ATTACK
                    }
                );

            // 4) 你的实体列表 -> 优化器接口列表（小适配：把每个 ModuleInfo 包成 IModuleInfo）
            var adaptedList = modules.Select(m => (ModuleOptimizer.IModuleInfo)new AdaptInfo(m)).ToList();

            // 5) 创建优化器实例，注入各种表
            var optimizer = new ModuleOptimizer(
                  moduleCategoryMap: categoryMapForOptimizer,
                  attrThresholds: Array.AsReadOnly(ModuleMaps.ATTR_THRESHOLDS),
                  basicAttrPowerMap: ModuleMaps.BASIC_ATTR_POWER_MAP,
                  specialAttrPowerMap: ModuleMaps.SPECIAL_ATTR_POWER_MAP,
                  totalAttrPowerMap: ModuleMaps.TOTAL_ATTR_POWER_MAP,
                  basicAttrIds: ModuleMaps.BASIC_ATTR_IDS,
                  specialAttrIds: ModuleMaps.SPECIAL_ATTR_IDS,
                  attrNameTypeMap: ModuleMaps.ATTR_NAME_TYPE_MAP,
                  priorityAttrNames: BuildEliteCandidatePool.Attributes,   // ← 新增
                  resultLogFile: null
              );

            optimizer.SetDesiredLevels(BuildEliteCandidatePool.DesiredLevels);

            // 6) 跑优化（与 Python 一致：取前 40 个最优解），并写入 UI 内存显示
            // 原来：var solutions = optimizer.OptimizeModules(adaptedList, optCategory, 40);
            var solutions = optimizer.OptimizeModules(adaptedList, optCategory, 40, BuildEliteCandidatePool.SortBy);
            ModuleCardDisplay.ModuleResultMemory.FromSolutions(solutions);

        }

        /// <summary>
        /// 小适配器：把你的 ModulePart 包装成优化器的 IModulePart
        /// </summary>
        private sealed class AdaptPart : ModuleOptimizer.IModulePart
        {
            private readonly ModulePart _p;
            public AdaptPart(ModulePart p) { _p = p ?? throw new ArgumentNullException(nameof(p)); }
            public string Name => _p.Name;
            public int Id => _p.Id;
            public int Value => _p.Value;
        }

        /// <summary>
        /// 小适配器：把你的 ModuleInfo 包装成优化器的 IModuleInfo
        /// </summary>
        private sealed class AdaptInfo : ModuleOptimizer.IModuleInfo
        {
            private readonly ModuleInfo _m;
            private readonly IReadOnlyList<ModuleOptimizer.IModulePart> _parts;

            public AdaptInfo(ModuleInfo m)
            {
                _m = m ?? throw new ArgumentNullException(nameof(m));
                // 预先把内部部件也适配成接口列表
                _parts = (_m.Parts ?? new List<ModulePart>())
                         .Select(p => (ModuleOptimizer.IModulePart)new AdaptPart(p))
                         .ToList();
            }

            public string Uuid => _m.Uuid.ToString(); // 你的 Uuid 是 long，这里转成优化器需要的 string
            public string Name => _m.Name;
            public int Quality => _m.Quality;
            public int ConfigId => _m.ConfigId;
            public IReadOnlyList<ModuleOptimizer.IModulePart> Parts => _parts;
        }



    }
}
