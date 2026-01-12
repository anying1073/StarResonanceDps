using System;
using System.Collections.Concurrent;
using System.Timers;
using System.Xml.Linq;

using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Models;
using StarResonanceDpsAnalysis.WinForm.Core;

using static StarResonanceDpsAnalysis.WinForm.Plugin.DamageStatistics.PlayerDataManager;

namespace StarResonanceDpsAnalysis.WinForm.Plugin.DamageStatistics
{
    /// <summary>
    /// 通用统计类：用于伤害或治疗的数据累计、次数统计、实时窗口统计，以及总 DPS/HPS 计算
    /// 设计要点：
    /// 1. 写入路径：通过 <see cref="AddRecord"/> 进行统一写入，确保“累计值/次数/极值/实时窗口/时间范围”一致推进。
    /// 2. 实时统计：使用固定秒级窗口（默认 1s）衡量 RealtimeValue（可作为瞬时DPS/HPS展示）。
    /// 3. 总平均：通过首末记录时间计算总平均每秒，避免依赖外部时钟。
    /// 4. 线程模型：本类型未加锁，默认在同一线程上下文使用；如并发写入，请在调用方序列化或加锁。
    /// </summary>
    public class StatisticData
    {
        #region 常量
        //锁对象
        private readonly object _realtimeLock = new();

        /// <summary>
        /// 实时统计的时间窗口（秒），用于计算实时值与峰值。
        /// 注意：窗口越短越敏捷，但波动越明显；越长越平滑，但延迟越大。
        /// </summary>
        private const double 实时窗口秒数 = 1.0;

        #endregion

        #region 静态成员

        /// <summary>
        /// 全局玩家数据管理器（按你原代码保持不变）。
        /// 用于跨玩家聚合、定时刷新、快照与战斗时钟等。
        /// </summary>
        public static readonly PlayerDataManager _manager = new PlayerDataManager();

        /// <summary>
        /// 全局 NPC 数据管理器：用于统计 NPC 承伤与对NPC的玩家排名
        /// </summary>
        public static readonly NpcManager _npcManager = new NpcManager(_manager);
        #endregion

        #region 数值累计（只读属性，内部递增）

        /// <summary>
        /// Miss 次数（未命中次数）
        /// - 统计该技能在承伤统计中被判定为 Miss 的次数
        /// - Miss 不会累加到伤害值，只是计数，用于命中率统计
        /// </summary>
        public int CountMiss { get; private set; }

        /// <summary>
        /// 击杀次数（目标死亡次数）
        /// - 统计该技能在承伤统计中导致目标死亡的次数
        /// - 用于显示“该技能打死过几次”的额外信息
        /// - 注意：死亡是承伤结果，伤害依然正常累加
        /// </summary>
        public int CountDead { get; private set; }





        /// <summary>
        /// 因果幸运总伤害值
        /// - 当一次命中既是幸运触发（isLucky=true）
        ///   且由因果效果导致（isCauseLucky=true）时
        ///   就把伤害数额累加到这里。
        /// - 用来区分「普通幸运伤害」和「因果触发的幸运伤害」
        /// </summary>
        public ulong CauseLucky { get; private set; }

        /// <summary>
        /// 因果幸运命中次数
        /// - 统计触发「因果幸运」的命中次数（计数）
        /// - 对比 CountLucky 可以看出其中有多少次幸运是由因果触发的
        /// </summary>
        public int CountCauseLucky { get; private set; }


        /// <summary>普通命中数值总和。</summary>
        public ulong Normal { get; private set; }

        /// <summary>暴击数值总和。</summary>
        public ulong Critical { get; private set; }

        /// <summary>幸运命中数值总和。</summary>
        public ulong Lucky { get; private set; }

        public ulong LuckyAndCritical { get; private set; }      // 幸运伤害总和（= Lucky + CritLucky）


        /// <summary>暴击且幸运数值总和。</summary>
        public ulong CritLucky { get; private set; }

        /// <summary>HP 减少总和（伤害统计专用）。用于承伤时记录真实扣血值（可与 <see cref="Total"/> 不同）。</summary>
        public ulong HpLessen { get; private set; }

        /// <summary>所有命中数值总和（Damage/HPS 的基础累计）。</summary>
        public ulong Total { get; private set; }

        /// <summary>单次命中最大值（写入时更新）。</summary>
        public ulong MaxSingleHit { get; private set; }

        /// <summary>单次命中最小值（非 0；初值为 <see cref="ulong.MaxValue"/> 以便正确取 min）。</summary>
        public ulong MinSingleHit { get; private set; } = ulong.MaxValue;

        #endregion

        #region 次数统计（只读属性，内部递增）

        /// <summary>普通命中次数。</summary>
        public int CountNormal { get; private set; }

        /// <summary>暴击次数。</summary>
        public int CountCritical { get; private set; }

        /// <summary>幸运命中次数。</summary>
        public int CountLucky { get; private set; }

        /// <summary>总命中次数（普通/暴击/幸运的合计，按逻辑累加）。</summary>
        public int CountTotal { get; private set; }

        #endregion

        #region 实时统计窗口

        /// <summary>
        /// 最近时间窗口内的记录（用于实时 DPS/HPS）。
        /// 元组包含（记录时间戳，数值）。
        /// </summary>
        private readonly List<(DateTime Time, ulong Value)> _realtimeWindow = new();

        /// <summary>窗口内实时累计值（例如用于瞬时 DPS/HPS 展示）。</summary>
        public ulong RealtimeValue { get; private set; }

        /// <summary>历史窗口最大峰值（用于“本场最高瞬时值”展示）。</summary>
        public ulong RealtimeMax { get; private set; }

        #endregion

        #region 时间范围（用于总平均每秒值）
        // 首次 AddRecord 触发
        private DateTime? _startTime;

        // 最近一次 AddRecord 的时间（也是“最后一次记录时间”）
        private DateTime? _endTime;

        /// <summary>最后一次记录时间（对外暴露只读）。</summary>
        public DateTime? LastRecordTime => _endTime;

        #endregion



        #region 公开方法
        public void RegisterMiss() { CountMiss++; }
        public void RegisterKill() { CountDead++; }



        /// <summary>
        /// 添加一条新的统计记录（伤害或治疗）。此方法是唯一写入口，负责推进所有派生统计。
        /// </summary>
        /// <param name="value">记录数值（伤害量或治疗量）。为 0 时不参与最小值计算。</param>
        /// <param name="isCrit">是否暴击。会影响 <see cref="Critical"/> 或 <see cref="CritLucky"/> 累加与计数。</param>
        /// <param name="isLucky">是否幸运。会影响 <see cref="Lucky"/> 或 <see cref="CritLucky"/> 累加与计数。</param>
        /// <param name="hpLessenValue">
        /// HP 减少值（仅伤害/承伤场景传入）。
        /// 当统计承伤时可用以与 <see cref="Total"/> 区分（例如溢出伤、护盾、减伤后真实掉血量）。
        /// </param>
        public void AddRecord(ulong value, bool isCrit, bool isLucky, ulong hpLessenValue = 0, bool isCauseLucky = false) // ★ 新增参数：是否因果幸运
        {
            var now = DateTime.Now;

            // —— 原有累计/计数/极值逻辑保持不变 —— 
            if (isCrit && isLucky)
            {
                CritLucky += value;
                LuckyAndCritical += value;
            }
            else if (isCrit) Critical += value;
            else if (isLucky)
            {
                Lucky += value;
                LuckyAndCritical += value;
            }
            else Normal += value;


            Total += value;
            HpLessen += hpLessenValue;

            if (isCrit) CountCritical++;
            if (isLucky) CountLucky++;
            if (!isCrit && !isLucky) CountNormal++;
            CountTotal++;

            if (value > 0)
            {
                if (value > MaxSingleHit) MaxSingleHit = value;
                if (value < MinSingleHit) MinSingleHit = value;
            }




            if (isLucky && isCauseLucky)
            {
                CauseLucky += value;
                CountCauseLucky++;
            }


            // —— 仅这行需要加锁 —— 
            lock (_realtimeLock)
            {
                _realtimeWindow.Add((now, value));
            }

            _startTime ??= now;
            _endTime = now;
        }

        // PlayerData.AddTakenDamage(...) 里



        /// <summary>
        /// 刷新实时统计：剔除超过窗口期的数据，并计算实时值与峰值。
        /// 建议由外部定时器（1s Tick）周期性调用，或在关键渲染点手工调用。
        /// </summary>
        public void UpdateRealtimeStats()
        {
            var now = DateTime.Now;

            List<(DateTime Time, ulong Value)> snapshot = null;

            // 1) 锁内：剔除过期 + 复制快照
            lock (_realtimeLock)
            {
                _realtimeWindow.RemoveAll(e => (now - e.Time).TotalSeconds > 实时窗口秒数);

                if (_realtimeWindow.Count > 0)
                    snapshot = new List<(DateTime, ulong)>(_realtimeWindow);
            }

            // 2) 锁外：计算实时累计
            ulong sum = 0;
            if (snapshot != null)
            {
                for (int i = 0; i < snapshot.Count; i++)
                    sum += snapshot[i].Value;
            }

            RealtimeValue = sum;
            if (RealtimeValue > RealtimeMax) RealtimeMax = RealtimeValue;
        }



        /// <summary>
        /// 获取总平均每秒值（Total / 总时长），用于总体 DPS 或 HPS。
        /// 时长取自本实例“首条与末条记录”的时间差。
        /// </summary>
        /// <returns>当无有效时间范围时返回 0。</returns>
        public double GetTotalPerSecond()
        {
            if (_startTime == null || _endTime == null || _startTime == _endTime) return 0;
            var seconds = (_endTime.Value - _startTime.Value).TotalSeconds;
            return seconds > 0 ? Total / seconds : 0;
        }

        /// <summary>平均每次命中值（Total / CountTotal）。</summary>
        /// <returns>当 <see cref="CountTotal"/> 为 0 时返回 0。</returns>
        public double GetAveragePerHit() => CountTotal > 0 ? (double)Total / CountTotal : 0.0;

        /// <summary>暴击率（百分比 0~100，四舍五入 2 位小数）。</summary>
        /// <returns>当 <see cref="CountTotal"/> 为 0 时返回 0。</returns>
        public double GetCritRate() =>
            CountTotal > 0 ? Math.Round((double)CountCritical / CountTotal * 100.0, 2) : 0.0;

        /// <summary>幸运率（百分比 0~100，四舍五入 2 位小数）。</summary>
        /// <returns>当 <see cref="CountTotal"/> 为 0 时返回 0。</returns>
        public double GetLuckyRate() =>
            CountTotal > 0 ? Math.Round((double)CountLucky / CountTotal * 100.0, 2) : 0.0;


        /// <summary>
        /// 重置所有统计数据与状态。
        /// 注意：仅清空数据本身，不影响外部容器（如所属 PlayerData 的引用关系）。
        /// </summary>
        public void Reset()
        {
            Normal = Critical = Lucky = CritLucky = HpLessen = Total = 0;
            CountNormal = CountCritical = CountLucky = CountTotal = 0;

            MaxSingleHit = 0;
            MinSingleHit = ulong.MaxValue;

            _realtimeWindow.Clear();
            _startTime = _endTime = null;

            RealtimeValue = RealtimeMax = 0;
        }

        #endregion
    }

    #region 玩家数据管理器
    // ------------------------------------------------------------
    // # 分类：技能元数据（静态信息，如名称/图标/是否DOT等）
    // ------------------------------------------------------------

    /// <summary>技能元数据（静态）。供 UI/导出与统计汇总引用。</summary>
    public sealed class SkillMeta
    {
        /// <summary>技能 ID。</summary>
        public long Id { get; init; }

        /// <summary>技能名称（可从资源/协议注入）。</summary>
        public string Name { get; init; } = "未知技能";

        /// <summary>流派/元素系（可选）。</summary>
        public string School { get; init; } = "";

        /// <summary>图标路径（可选）。</summary>
        public string IconPath { get; init; } = "";

        // 新增
        /// <summary>技能类型（伤害/治疗/未知等，来自 Core.SkillType）。</summary>
        public Core.SkillType Type { get; init; } =
            Core.SkillType.Unknown;

        /// <summary>元素类型（来自 Core.ElementType）。</summary>
        public Core.ElementType Element { get; init; } =
            Core.ElementType.Unknown;

        /// <summary>是否为 DOT 技能（可选）。</summary>
        public bool IsDoT { get; init; }

        /// <summary>是否为大招/终结技（可选）。</summary>
        public bool IsUltimate { get; init; }
    }

    /// <summary>
    /// 技能注册表（进程级缓存）：按 ID 查询元数据；在解析数据时可随时补充/更新。
    /// 注意：该缓存为静态字典，默认不加锁；如并发写入，请在调用方控制。
    /// </summary>
    public static class SkillBook
    {
        private static readonly Dictionary<long, SkillMeta> _metas = new();

        /// <summary>
        /// 整条更新/写入一个技能的元数据。
        /// </summary>
        /// <param name="meta">完整的技能元数据对象。</param>
        public static void SetOrUpdate(SkillMeta meta) => _metas[meta.Id] = meta;

        /// <summary>
        /// 仅更新/设置技能名称（快速接口）。
        /// </summary>
        /// <param name="id">技能 ID。</param>
        /// <param name="name">技能名称。</param>
        public static void SetName(long id, string name)
        {
            if (_metas.TryGetValue(id, out var m))
                _metas[id] = new SkillMeta
                {
                    Id = id,
                    Name = name,
                    School = m.School,
                    IconPath = m.IconPath,
                    IsDoT = m.IsDoT,
                    IsUltimate = m.IsUltimate
                };
            else
                _metas[id] = new SkillMeta { Id = id, Name = name };
        }

        /// <summary>
        /// 获取技能元数据；若不存在则返回带占位名的对象（不会写入缓存）。
        /// </summary>
        /// <param name="id">技能 ID。</param>
        /// <returns>若缓存中不存在，返回 Name="技能[id]" 的占位对象。</returns>
        public static SkillMeta Get(long id) =>
            _metas.TryGetValue(id, out var m) ? m : new SkillMeta { Id = id, Name = $"技能[{id}]" };

        /// <summary>
        /// 尝试获取技能元数据。
        /// </summary>
        /// <param name="id">技能 ID。</param>
        /// <param name="meta">输出参数：若命中缓存，则为对应元数据。</param>
        /// <returns>是否命中缓存。</returns>
        public static bool TryGet(long id, out SkillMeta meta) => _metas.TryGetValue(id, out meta);
    }

    // ------------------------------------------------------------
    // # 分类：技能摘要 DTO（给 UI/导出使用）
    // ------------------------------------------------------------

    /// <summary>
    /// 单个玩家的技能摘要（合并统计与元数据）。
    /// 用于列表展示/导出/图表等，不承载写入逻辑。
    /// </summary>
    public sealed class SkillSummary
    {
        /// <summary>技能ID（唯一标识技能，可用于数据库关联）。</summary>
        public long SkillId { get; init; }

        /// <summary>技能名称（默认值为“未知技能”）。</summary>
        public string SkillName { get; init; } = "未知技能";

        /// <summary>技能总伤害或总治疗（取决于来源集合）。</summary>
        public ulong Total { get; init; }

        /// <summary>命中次数（被打次数/治疗次数/伤害次数）。</summary>
        public int HitCount { get; init; }

        /// <summary>每次命中的平均数值。</summary>
        public double AvgPerHit { get; init; }

        /// <summary>暴击率（0~100 或 0~1，取决于生成方的定义；本实现为百分比 0~100）。</summary>
        public double CritRate { get; init; }

        /// <summary>幸运率（0~100）。</summary>
        public double LuckyRate { get; init; }

        /// <summary>单次最高命中。</summary>
        public ulong MaxSingleHit { get; init; }

        /// <summary>单次最低命中（若无记录则可能为 0）。</summary>
        public ulong MinSingleHit { get; init; }

        /// <summary>实时窗口内累计（同一技能当前瞬时值）。</summary>
        public ulong RealtimeValue { get; init; }

        /// <summary>实时窗口内峰值。</summary>
        public ulong RealtimeMax { get; init; }

        /// <summary>该技能自身平均每秒（以本技能的起止时间范围计算）。</summary>
        public double TotalDps { get; init; }

        /// <summary>该技能最后一次命中时间。</summary>
        public DateTime? LastTime { get; init; }

        /// <summary>历史总占比（0~1）。UI 显示百分比时 *100 与四舍五入。</summary>
        public double ShareOfTotal { get; init; }

        public ulong LuckyDamage { get; init; }        // 幸运伤害 = Lucky + CritLucky
        public ulong CritLuckyDamage { get; init; }    // 纯“暴击+幸运”部分
        public ulong CauseLuckyDamage { get; init; }   // 因果幸运的那部分
        public int CountLucky { get; init; }         // 幸运命中次数
    }

    /// <summary>全队聚合的技能摘要（跨玩家）。</summary>
    public sealed class TeamSkillSummary
    {
        /// <summary>技能 ID。</summary>
        public long SkillId { get; init; }

        /// <summary>技能名称。</summary>
        public string SkillName { get; init; } = "未知技能";

        /// <summary>全队该技能的总量（伤害合计）。</summary>
        public ulong Total { get; init; }

        /// <summary>全队命中次数（汇总）。</summary>
        public int HitCount { get; init; }
    }

    // ------------------------------------------------------------
    // # 分类：玩家数据（伤害/治疗/承伤与按技能分组）
    // ------------------------------------------------------------

    /// <summary>
    /// 单个玩家数据：包含伤害、治疗、承伤，以及按技能分组的统计。
    /// 说明：
    /// - <see cref="DamageStats"/>/<see cref="HealingStats"/>/<see cref="TakenStats"/> 为聚合器（不分技能）。
    /// - <see cref="SkillUsage"/>/<see cref="HealingBySkill"/>/<see cref="TakenDamageBySkill"/> 为分技能的细项字典。
    /// - 写入统一通过 AddDamage/AddHealing/AddTakenDamage，保证“聚合”与“分组”一致推进。
    /// </summary>
    public class PlayerData
    {
        #region 基本信息

        /// <summary>玩家唯一 UID。</summary>
        public long Uid { get; }

        /// <summary>玩家昵称。</summary>
        public string Nickname { get; set; } = "未知";

        /// <summary>战力。</summary>
        public int CombatPower { get; set; } = 0;

        /// <summary>职业。</summary>
        public string Profession { get; set; } = "未知";

        public string SubProfession { get; set; } = null;
        public ClassSpec Spec { get; set; } = ClassSpec.Unknown;

        #endregion

        #region 统计对象与索引

        /// <summary>玩家自定义属性（key=value）。可用于外部扩展与联动展示。</summary>
        public Dictionary<string, object> Attributes { get; } = new();

        /// <summary>玩家伤害统计（聚合）。</summary>
        public StatisticData DamageStats { get; } = new();

        /// <summary>玩家治疗统计（聚合）。</summary>
        public StatisticData HealingStats { get; } = new();

        /// <summary>玩家承受总伤害（快速访问）。与 <see cref="TakenStats.Total"/> 不一定完全等价（受 hpLessen 影响）。</summary>
        public ulong TakenDamage { get; private set; }

        /// <summary>按技能分组的伤害/治疗统计（key=技能ID）。</summary>
        public Dictionary<long, StatisticData> SkillUsage { get; } = new();

        /// <summary>
        /// 按技能分组的伤害/治疗统计（key=技能ID）（按元素分组）
        /// </summary>

        public Dictionary<long, Dictionary<string, StatisticData>> SkillUsageByElement = new();

        /// <summary>
        /// 按技能分组的治疗统计（key=技能ID）（按目标分组）
        /// </summary>
        public Dictionary<long, Dictionary<ulong, StatisticData>> HealingBySkillTarget = new();



        /// <summary>按技能分组的承伤统计（key=技能ID）。</summary>
        public Dictionary<long, StatisticData> TakenDamageBySkill { get; } = new();

        /// <summary>按技能分组的治疗统计（key=技能ID）。</summary>
        public Dictionary<long, StatisticData> HealingBySkill { get; } = new();

        /// <summary>玩家/怪物承伤统计（聚合，非按技能）。</summary>
        public StatisticData TakenStats { get; } = new();

        #endregion

        #region 构造

        /// <summary>
        /// 使用玩家 UID 构造实例。
        /// </summary>
        /// <param name="uid">玩家唯一标识。</param>
        public PlayerData(long uid) => Uid = uid;

        #endregion

        #region 添加记录（伤害/治疗/承伤）

        /// <summary>
        /// 添加伤害记录，并同步更新技能分组统计与全程记录。
        /// </summary>
        /// <param name="skillId">技能 ID。</param>
        /// <param name="damage">伤害数值。</param>
        /// <param name="isCrit">是否暴击。</param>
        /// <param name="isLucky">是否幸运。</param>
        /// <param name="hpLessen">扣血值（可选）。通常伤害时与 damage 一致，承伤场景更有意义。</param>
        public void AddDamage(
            long skillId, ulong damage, bool isCrit, bool isLucky, ulong hpLessen = 0,
            string? damageElement = null, bool isCauseLucky = false)
        {
            DamageStats.AddRecord(damage, isCrit, isLucky, hpLessen, isCauseLucky);

            if (!SkillUsage.TryGetValue(skillId, out var stat))
            {
                stat = new StatisticData();
                SkillUsage[skillId] = stat;
            }
            stat.AddRecord(damage, isCrit, isLucky, hpLessen, isCauseLucky);
            if (string.IsNullOrEmpty(SubProfession))
            {
                var sp = skillId.GetSubProfessionBySkillId();
                var spec = skillId.GetClassSpecBySkillId();
                if (!string.IsNullOrEmpty(sp)) SubProfession = sp;
                Spec = spec;
            }

            // 把新增字段写入全程记录（需要你同步扩展 FullRecord.RecordDamage 的签名）
            FullRecord.RecordDamage(
                Uid, skillId, damage, isCrit, isLucky, hpLessen,
                Nickname, CombatPower, Profession,
                damageElement, isCauseLucky, SubProfession);
        }

        /// <summary>
        /// 添加治疗记录（分技能），并同步全程记录。
        /// </summary>
        /// <param name="skillId">技能 ID。</param>
        /// <param name="healing">治疗数值。</param>
        /// <param name="isCrit">是否暴击。</param>
        /// <param name="isLucky">是否幸运。</param>
        /// <param name="damageElement"></param>
        /// <param name="isCauseLucky"></param>
        /// <param name="targetUuid"></param>
        public void AddHealing(
            long skillId, ulong healing, bool isCrit, bool isLucky,
            string? damageElement = null, bool isCauseLucky = false, ulong targetUuid = 0)
        {
            HealingStats.AddRecord(healing, isCrit, isLucky, 0, isCauseLucky);

            if (!HealingBySkill.TryGetValue(skillId, out var stat))
            {
                stat = new StatisticData();
                HealingBySkill[skillId] = stat;
            }
            stat.AddRecord(healing, isCrit, isLucky, 0, isCauseLucky);
            if (string.IsNullOrEmpty(SubProfession))
            {
                var sp = skillId.GetSubProfessionBySkillId();
                var spec = skillId.GetClassSpecBySkillId();
                if (!string.IsNullOrEmpty(sp)) SubProfession = sp;
                Spec = spec;
            }

            FullRecord.RecordHealing(
                Uid, skillId, healing, isCrit, isLucky,
                Nickname, CombatPower, Profession,
                damageElement, isCauseLucky, targetUuid, SubProfession);

        }



        /// <summary>
        /// 添加承伤记录（支持暴击/幸运标记），同时累计到聚合与分技能統計，并寫入全程記錄。
        /// </summary>
        /// <param name="skillId">技能 ID（來源技能）。</param>
        /// <param name="damage">承傷值（一般為命中值）。</param>
        /// <param name="isCrit">是否暴擊（若協議可區分）。</param>
        /// <param name="isLucky">是否幸運（若協議可區分）。</param>
        /// <param name="hpLessen">HP 真实减少值；为 0 时以 <paramref name="damage"/> 作为扣血。</param>
        /// <param name="damageSource">伤害来源（0=玩家，1=怪物，2=法术，3=其他）。</param>
        /// <param name="isMiss">是否未命中。</param>
        /// <param name="isDead">是否死亡。</param>
        public void AddTakenDamage(
            long skillId, ulong damage, bool isCrit, bool isLucky, ulong hpLessen = 0,
            int damageSource = 0, bool isMiss = false, bool isDead = false)
        {
            if (!TakenDamageBySkill.TryGetValue(skillId, out var stat))
            {
                stat = new StatisticData();
                TakenDamageBySkill[skillId] = stat;
            }

            // 1) Miss：只记次数，不进 AddRecord（没有有效数值）
            if (isMiss)
            {
                stat.RegisterMiss();   // ✅ 直接自增
                return;
            }

            // 2) Death：记次数；若有伤害值，仍正常入库
            if (isDead)
            {
                stat.RegisterKill();   // ✅ 直接自增
            }

            var lessen = hpLessen > 0 ? hpLessen : damage;

            // 玩家总承伤累计
            TakenDamage += lessen;

            // 聚合器累计（总体承伤）
            TakenStats.AddRecord(damage, isCrit, isLucky, lessen);

            // 分技能累计
            stat.AddRecord(damage, isCrit, isLucky, lessen /*, isCauseLucky 可按需传 */);

            // 全程记录（若你有扩展 FullRecord.RecordTakenDamage）
            FullRecord.RecordTakenDamage(Uid, skillId, damage, isCrit, isLucky, lessen,
                Nickname, CombatPower, Profession, damageSource, isMiss, isDead);
        }




        /// <summary>
        /// 设置玩家职业。
        /// </summary>
        /// <param name="profession">职业名。</param>
        public void SetProfession(string profession) => Profession = profession;

        #endregion

        #region 添加记录 (属性)

        /// <summary>
        /// 设置玩家自定义属性。
        /// </summary>
        /// <param name="key">属性键。</param>
        /// <param name="value">属性值。</param>
        public void SetAttrKV(string key, object value)
        {
            Attributes[key] = value;
        }

        /// <summary>
        /// 获取玩家自定义属性（不存在则返回 null）。
        /// </summary>
        /// <param name="key">属性键。</param>
        /// <returns>属性值或 null。</returns>
        public object? GetAttrKV(string key)
        {
            return Attributes.TryGetValue(key, out var val) ? val : null;
        }
        #endregion

        #region 实时刷新与聚合输出

        /// <summary>
        /// 检查玩家是否有有效的战斗数据（伤害/治疗/承伤任一有值即视为有战斗数据）。
        /// </summary>
        /// <returns>true 表示有；否则 false。</returns>
        public bool HasCombatData()
        {
            return DamageStats.Total > 0 || HealingStats.Total > 0 || TakenDamage > 0;
        }

        /// <summary>刷新玩家的实时 DPS/HPS（滚动窗口）。</summary>
        public void UpdateRealtimeStats()
        {
            DamageStats.UpdateRealtimeStats();
            HealingStats.UpdateRealtimeStats();
            TakenStats.UpdateRealtimeStats(); // ★ 修复：添加承伤统计的实时更新
        }

        /// <summary>获取总 DPS（总时长平均）。</summary>
        /// <returns>double，单位：每秒。</returns>
        public double GetTotalDps() => DamageStats.GetTotalPerSecond();

        /// <summary>获取总 HPS（总时长平均）。</summary>
        /// <returns>double，单位：每秒。</returns>
        public double GetTotalHps() => HealingStats.GetTotalPerSecond();

        /// <summary>
        /// 获取合并后的命中次数统计（伤害+治疗）。
        /// </summary>
        /// <returns>包含普通/暴击/幸运/总次数的四元组。</returns>
        public (int Normal, int Critical, int Lucky, int Total) GetTotalCount()
            => (
                DamageStats.CountNormal + HealingStats.CountNormal,
                DamageStats.CountCritical + HealingStats.CountCritical,
                DamageStats.CountLucky + HealingStats.CountLucky,
                DamageStats.CountTotal + HealingStats.CountTotal
            );

        /// <summary>
        /// 获取技能统计汇总列表（可选排序和限制数量）。
        /// </summary>
        /// <param name="topN">
        /// 仅返回前 N 条记录（按总伤害/治疗排序后取前 N 条）。传 null 或 &lt;=0 返回全部。
        /// </param>
        /// <param name="orderByTotalDesc">是否按总量降序排序。</param>
        /// <param name="filterType">
        /// 过滤技能类型：<see cref="Core.SkillType.Damage"/> 仅统计伤害；
        /// <see cref="Core.SkillType.Heal"/> 仅统计治疗；null 当前等同 Damage。
        /// </param>
        /// <returns>技能汇总信息列表。</returns>
        public List<SkillSummary> GetSkillSummaries(
            int? topN = null,
            bool orderByTotalDesc = true,
            Core.SkillType? filterType = Core.SkillType.Damage)
        {
            // 1) 选择数据源
            IEnumerable<KeyValuePair<long, StatisticData>> source;
            if (filterType == Core.SkillType.Damage)
                source = SkillUsage;                  // 按技能统计的伤害
            else if (filterType == Core.SkillType.Heal)
                source = HealingBySkill;              // 按技能统计的治疗（你已增加该字典/写入）
            else
                source = SkillUsage;                  // 先用伤害；如需真的“合并伤害+治疗”，我再给你 Merge 版

            // 2) 分母：必须用“source”的 Total 求和（避免用错集合）
            ulong denom = 0;
            foreach (var kv in source) denom += kv.Value.Total;
            if (denom == 0) denom = 1; // 防止除0

            // 3) 生成列表
            var list = new List<SkillSummary>();
            foreach (var kv in source)
            {
                var id = kv.Key;
                var s = kv.Value;
                var meta = SkillBook.Get(id);

                list.Add(new SkillSummary
                {
                    SkillId = id,
                    SkillName = meta.Name,
                    Total = s.Total,
                    HitCount = s.CountTotal,
                    AvgPerHit = s.GetAveragePerHit(),
                    CritRate = s.GetCritRate(),
                    LuckyRate = s.GetLuckyRate(),
                    MaxSingleHit = s.MaxSingleHit,
                    MinSingleHit = s.MinSingleHit == ulong.MaxValue ? 0 : s.MinSingleHit,
                    RealtimeValue = s.RealtimeValue,
                    RealtimeMax = s.RealtimeMax,
                    TotalDps = s.GetTotalPerSecond(),
                    LastTime = s.LastRecordTime,     // 如果你没加这个属性，就先删这一行
                    ShareOfTotal = (double)s.Total / denom,  // 0~1 占比（与 source 对齐）
                    LuckyDamage = s.Lucky + s.CritLucky,   // ★ 合并
                    CritLuckyDamage = s.CritLucky,
                    CauseLuckyDamage = s.CauseLucky,
                    CountLucky = s.CountLucky,

                });
            }

            // 4) 排序/截断
            if (orderByTotalDesc) list = list.OrderByDescending(x => x.Total).ToList();
            if (topN.HasValue && topN.Value > 0 && list.Count > topN.Value)
                list = list.Take(topN.Value).ToList();

            return list;
        }


        /// <summary>
        /// 技能占比（实时窗口）：返回 TopN + 其他 的占比（用于饼图/环图）。
        /// </summary>
        /// <param name="topN">Top N 技能数量。</param>
        /// <param name="includeOthers">是否包含“其他”汇总。</param>
        /// <returns>元组列表：(SkillId, SkillName, Realtime, Percent)。</returns>
        public List<(long SkillId, string SkillName, ulong Realtime, int Percent)> GetSkillDamageShareRealtime(int topN = 10, bool includeOthers = true)
        {
            if (SkillUsage.Count == 0) return new List<(long, string, ulong, int)>();

            // 分母：实时窗口内的伤害
            ulong denom = 0;
            foreach (var kv in SkillUsage) denom += kv.Value.RealtimeValue;
            if (denom == 0) return new List<(long, string, ulong, int)>();

            var top = SkillUsage
                .Select(kv => new { kv.Key, Val = kv.Value.RealtimeValue })
                .OrderByDescending(x => x.Val)
                .ToList();

            var chosen = top.Take(topN).ToList();
            ulong chosenSum = 0;
            foreach (var c in chosen) chosenSum += c.Val;

            var result = new List<(long, string, ulong, int)>(chosen.Count + 1);
            foreach (var c in chosen)
            {
                double r = (double)c.Val / denom;
                int p = (int)Math.Round(r * 100.0);
                var name = SkillBook.Get(c.Key).Name;
                result.Add((c.Key, name, c.Val, p));
            }

            if (includeOthers && top.Count > chosen.Count)
            {
                ulong others = denom - chosenSum;
                int p = (int)Math.Round((double)others / denom * 100.0);
                result.Add((0, "其他", others, p));
            }

            return result;
        }

        /// <summary>重置玩家所有统计与状态（含承伤聚合/分技能）。</summary>
        public void Reset()
        {
            DamageStats.Reset();
            HealingStats.Reset();
            TakenStats.Reset();          // ★ 新增
            TakenDamage = 0;
            Profession = "未知";
            SkillUsage.Clear();
            TakenDamageBySkill.Clear();
            HealingBySkill.Clear();
        }

        #endregion

        #region 技能占比（整场总伤害 / 单玩家）

        /// <summary>
        /// 技能占比（整场总伤害）- 单玩家。
        /// </summary>
        /// <param name="topN">Top N 技能数量。</param>
        /// <param name="includeOthers">是否追加“其他”。</param>
        /// <returns>(SkillId, SkillName, Total, Percent) 列表。</returns>
        public List<(long SkillId, string SkillName, ulong Total, int Percent)>
            GetSkillDamageShareTotal(int topN = 10, bool includeOthers = true)
        {
            if (SkillUsage.Count == 0) return new();

            // 1) 分母：该玩家所有技能的【总伤害】求和
            ulong denom = 0;
            foreach (var kv in SkillUsage) denom += kv.Value.Total;
            if (denom == 0) return new();

            // 2) 按总伤害降序取 TopN
            var top = SkillUsage
                .Select(kv => new { kv.Key, Val = kv.Value.Total })
                .OrderByDescending(x => x.Val)
                .ToList();

            var chosen = top.Take(topN).ToList();
            ulong chosenSum = 0;
            foreach (var c in chosen) chosenSum += c.Val;

            // 3) 组装结果（百分比四舍五入为整数）
            var result = new List<(long SkillId, string SkillName, ulong Total, int Percent)>(chosen.Count + 1);
            foreach (var c in chosen)
            {
                double r = (double)c.Val / denom;
                int p = (int)Math.Round(r * 100.0);
                var name = SkillBook.Get(c.Key).Name;
                result.Add((c.Key, name, c.Val, p));
            }

            // 4) 其余汇总为“其他”
            if (includeOthers && top.Count > chosen.Count)
            {
                ulong others = denom - chosenSum;
                int p = (int)Math.Round((double)others / denom * 100.0);
                result.Add((0, "其他", others, p));
            }

            return result;
        }

        #endregion

        #region 查询承伤（单玩家：聚合/分技能）

        /// <summary>
        /// 获取当前玩家的承伤技能汇总列表（整场）。
        /// </summary>
        /// <param name="topN">仅返回前 N 项（按承伤总量降序）。null 或 &lt;=0 返回全部。</param>
        /// <param name="orderByTotalDesc">是否按承伤总量降序。</param>
        /// <returns><see cref="SkillSummary"/> 列表。</returns>
        public List<SkillSummary> GetTakenDamageSummaries(int? topN = null, bool orderByTotalDesc = true)
        {
            // 如果没有任何承伤记录，直接返回空列表
            if (TakenDamageBySkill.Count == 0) return new();

            // 分母：该玩家的总承伤量（用于计算占比）
            ulong denom = 0;
            foreach (var kv in TakenDamageBySkill) denom += kv.Value.Total;
            if (denom == 0) denom = 1; // 防止除 0

            var list = new List<SkillSummary>(TakenDamageBySkill.Count);
            foreach (var kv in TakenDamageBySkill)
            {
                var id = kv.Key;        // 技能 ID
                var s = kv.Value;       // 该技能的统计数据
                var meta = SkillBook.Get(id); // 技能元数据（名称等）

                list.Add(new SkillSummary
                {
                    SkillId = id,
                    SkillName = meta.Name,
                    Total = s.Total,                 // 承伤总量
                    HitCount = s.CountTotal,         // 被打次数
                    AvgPerHit = s.GetAveragePerHit(),// 平均每次承伤
                    CritRate = 0,                    // 承伤不区分暴击，这里固定为 0
                    LuckyRate = 0,                   // 承伤不区分幸运，这里固定为 0
                    MaxSingleHit = s.MaxSingleHit,   // 单次最大承伤
                    MinSingleHit = s.MinSingleHit == ulong.MaxValue ? 0 : s.MinSingleHit, // 单次最小承伤
                    RealtimeValue = s.RealtimeValue, // 实时窗口内的承伤
                    RealtimeMax = s.RealtimeMax,     // 实时窗口内的最大承伤
                    TotalDps = s.GetTotalPerSecond(),// 严格说是“平均每秒承伤”
                    LastTime = s.LastRecordTime,     // 如果你没加这个属性，就先删这一行
                    ShareOfTotal = (double)s.Total / denom // 0~1 占比（与 source 对齐）
                });
            }

            // 排序
            if (orderByTotalDesc)
                list = list.OrderByDescending(x => x.Total).ToList();

            // 截断
            if (topN.HasValue && topN.Value > 0 && list.Count > topN.Value)
                list = list.Take(topN.Value).ToList();

            return list;
        }

        /// <summary>
        /// 获取该玩家被某个技能打到的详细承伤统计。
        /// </summary>
        /// <param name="skillId">技能ID。</param>
        /// <returns>存在则返回 <see cref="SkillSummary"/>；否则返回 null。</returns>
        public SkillSummary? GetTakenDamageDetail(long skillId)
        {
            // 没有该技能的承伤记录
            if (!TakenDamageBySkill.TryGetValue(skillId, out var stat))
                return null;

            var meta = SkillBook.Get(skillId);
            return new SkillSummary
            {
                SkillId = skillId,
                SkillName = meta.Name,
                Total = stat.Total,
                HitCount = stat.CountTotal,
                AvgPerHit = stat.GetAveragePerHit(),
                CritRate = 0,
                LuckyRate = 0,
                MaxSingleHit = stat.MaxSingleHit,
                MinSingleHit = stat.MinSingleHit == ulong.MaxValue ? 0 : stat.MinSingleHit,
                RealtimeValue = stat.RealtimeValue,
                RealtimeMax = stat.RealtimeMax,
                TotalDps = stat.GetTotalPerSecond(),
                LastTime = stat.LastRecordTime
            };
        }

        #endregion



    }

    // ------------------------------------------------------------
    // # 分类：玩家数据管理器（缓存/定时器/战斗时钟/快照/查询）
    // ------------------------------------------------------------

    /// <summary>
    /// 玩家数据管理器：负责玩家对象创建/缓存、批量实时刷新与外部属性同步、战斗时钟维护、快照持久化以及多种查询接口。
    /// 线程说明：默认与采集/解析线程在同一上下文使用；如多线程访问，请在上层加锁或使用并发集合。
    /// </summary>
    public class PlayerDataManager
    {
        #region 存储

        private readonly object _playersLock = new object(); // ★ 统一锁（2025-08-19 新增）


        /// <summary>
        /// 快照 战斗数据历史列表。
        /// </summary>
        private readonly List<BattleSnapshot> _history = new();

        /// <summary>
        /// 只读访问器：读取玩家数据历史快照列表。
        /// </summary>
        public IReadOnlyList<BattleSnapshot> History => _history;

        /// <summary>UID → 玩家数据。</summary>
        private readonly Dictionary<long, PlayerData> _players = new();

        /// <summary>UID → 昵称（外部同步缓存）。</summary>
        private static readonly ConcurrentDictionary<long, string> _nicknameRequestedUids = new();

        /// <summary>UID → 战力（外部同步缓存）。</summary>
        private static readonly ConcurrentDictionary<long, int> _combatPowerByUid = new();

        /// <summary>UID → 职业（外部同步缓存）。</summary>
        private static readonly ConcurrentDictionary<long, string> _professionByUid = new();


        /// <summary>整场战斗开始时间（第一次出现战斗事件时赋值）。</summary>
        private DateTime? _combatStart;

        /// <summary>整场战斗结束时间（手动结束后赋值；进行中则为 null）。</summary>
        private DateTime? _combatEnd;

        /// <summary>是否处于战斗中。</summary>
        public bool IsInCombat => _combatStart.HasValue && !_combatEnd.HasValue;

        /// <summary>无数据多久后自动清空（秒）。0 表示永不自动结束。</summary>
        private static readonly TimeSpan InactivityTimeout = TimeSpan.FromSeconds(AppConfig.CombatTimeClearDelaySeconds);

        /// <summary>上一次全队有数据的时间。</summary>
        private DateTime _lastCombatActivity = DateTime.MinValue;


        #endregion

        #region 定时器

        /// <summary>用于周期性刷新实时统计的计时器（默认 1 秒）。</summary>
        private readonly System.Timers.Timer _checkTimer;

        /// <summary>最近一次新增玩家时间（用于快速跳过无数据场景）。</summary>
        private DateTime _lastAddTime = DateTime.MinValue;

        /// <summary>标记：已超时，等待下次战斗开始时清空上一场数据。</summary>
        private bool _pendingClearOnNextCombat;

        #endregion

        // 放在 PlayerDataManager 类内部任意位置（方法区）
        private void UpsertCacheProfile(long uid) // ★ 新增
        {
            // 统一从 PlayerData 拿最新三件套，保证写入完整字段（避免把未知覆盖成默认值）
            var p = GetOrCreate(uid);
            _userCache.UpsertIfChanged(new UserProfile
            {
                Uid = uid,
                Nickname = p.Nickname ?? string.Empty,
                Profession = p.Profession ?? string.Empty,
                Power = p.CombatPower
            }, caseInsensitiveName: true, trimName: true);
        }


        // ------------------------------------------------------------
        // # 分类：全局战斗时间（开始/结束/持续/格式化）
        // ------------------------------------------------------------
        #region 全局战斗时间

        /// <summary>
        /// 标记一次战斗活动（任一伤害/治疗/承伤写入时调用）：
        /// - 若尚未开始：设置开始时间为当前
        /// - 若已结束：视为新一场，重置并重新开始
        /// - 若上一场因超时结束但未清空：在此刻清空上一场数据（仅数据与时钟，不清缓存资料）。
        /// </summary>
        private void MarkCombatActivity()
        {
            throw new NotImplementedException("该功能已不在此处实现, 详情请查看 SRDA.Core.Data.DataStorage");

            //var now = DateTime.Now;
            //// —— 新增：如果上一场已超时结束但未清空，则在此刻（新战斗的首个事件）清空上一场 —— 
            //if (_pendingClearOnNextCombat)
            //{

            //    // 只清玩家数据与战斗时钟；缓存（昵称/战力/职业）保留
            //    ClearAll(false);
            //    FormManager.DpsStatistics.HandleClearData(true);
            //    _pendingClearOnNextCombat = false;
            //}

            //// 原逻辑：未开始或已结束 => 开新场
            //if (!_combatStart.HasValue || _combatEnd.HasValue)
            //{
            //    _combatStart = now;
            //    _combatEnd = null;
            //}

            //_lastCombatActivity = now;
        }

        /// <summary>
        /// 手动结束整场战斗（设置结束时间）。
        /// </summary>
        public void EndCombat()
        {
            if (_combatStart.HasValue && !_combatEnd.HasValue)
                _combatEnd = DateTime.Now;
        }

        /// <summary>
        /// 清除战斗时间（仅计时，不清玩家数据）。
        /// </summary>
        public void ResetCombatClock()
        {
            _combatStart = null;
            _combatEnd = null;


        }

        /// <summary>
        /// 获取整场战斗持续时间：
        /// - 未开始：00:00:00
        /// - 进行中：now - start
        /// - 已结束：end - start
        /// </summary>
        /// <returns><see cref="TimeSpan"/> 战斗时长。</returns>
        public TimeSpan GetCombatDuration()
        {
            if (!_combatStart.HasValue) return TimeSpan.Zero;
            if (_combatEnd.HasValue) return _combatEnd.Value - _combatStart.Value;
            return DateTime.Now - _combatStart.Value;
        }

        /// <summary>
        /// 返回整场战斗持续时间的格式化字符串：
        /// &gt;=1 小时 用 hh:mm:ss，否则用 mm:ss。
        /// </summary>
        /// <returns>格式化后的时长字符串。</returns>
        public string GetFormattedCombatDuration()
        {
            var ts = GetCombatDuration();
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero; // 极端情况下兜底

            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss")
                : ts.ToString(@"mm\:ss");
        }


        #endregion

        #region 构造

        // 在 PlayerDataManager 类字段区新增：
        private readonly UserLocalCache _userCache; // ★ 新增

        /// <summary>
        /// 构造函数：启动定时器，按固定频率刷新实时统计。
        /// </summary>
        public PlayerDataManager()
        {
            _userCache = new UserLocalCache(flushDelayMs: 1500); // ★ 新增：本地缓存，默认1.5s合并写盘

            _checkTimer = new System.Timers.Timer(1000)
            {
                AutoReset = true,
                Enabled = false // 先关闭，避免构造期触发回调
            };
            _checkTimer.Elapsed += CheckTimerElapsed;

            // 显式初始化（可选，强调语义）
            _lastCombatActivity = DateTime.MinValue;
            _pendingClearOnNextCombat = false;

            // 所有状态就绪后再启动
            _checkTimer.Start();
        }


        #endregion

        // ------------------------------------------------------------
        // # 分类：玩家实例获取/属性 KV 访问
        // ------------------------------------------------------------
        #region 获取或创建

        /// <summary>
        /// 手动创建一次快照并返回最新快照（如果当前有玩家数据）。
        /// </summary>
        /// <returns>刚保存的那条快照；若无玩家数据返回 null。</returns>
        public BattleSnapshot? TakeSnapshotAndGet()
        {
            if (_players.Count == 0) return null;

            //if (_combatStart.HasValue && !_combatEnd.HasValue)
            //    _combatEnd = _lastCombatActivity != DateTime.MinValue ? _lastCombatActivity : DateTime.Now;

            // 调用内部保存逻辑
            SaveCurrentBattleSnapshot();

            // 返回刚保存的那条快照
            return _history.Count > 0 ? _history[^1] : null; // ^1 是 C# 8.0 的最后一个元素
        }



        /// <summary>
        /// 获取或创建指定 UID 的玩家数据，并在首次创建时套用缓存的昵称/战力/职业信息。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <returns><see cref="PlayerData"/> 实例。</returns>
        public PlayerData GetOrCreate(long uid)
        {
            lock (_playersLock)
            {
                if (!_players.TryGetValue(uid, out var data))
                {
                    data = new PlayerData(uid);
                    _players[uid] = data;
                    _lastAddTime = DateTime.Now;



                    // ★ 先从 UserLocalCache 取一份（若存在）
                    var prof = _userCache.Get(uid.ToString());

                    // ★ 字典 > 缓存 > 默认 值 的优先级合并策略
                    // 昵称
                    if (_nicknameRequestedUids.TryGetValue(uid, out var cachedName) && !string.IsNullOrWhiteSpace(cachedName))
                        data.Nickname = cachedName;
                    else if (prof != null && !string.IsNullOrWhiteSpace(prof.Nickname))
                    {
                        data.Nickname = prof.Nickname;
                        _nicknameRequestedUids[uid] = prof.Nickname; // 同步回字典缓存
                    }

                    // 战力
                    if (_combatPowerByUid.TryGetValue(uid, out var power) && power > 0)
                        data.CombatPower = power;
                    else if (prof != null && prof.Power > 0)
                    {
                        data.CombatPower = (int)prof.Power;
                        _combatPowerByUid[uid] = data.CombatPower;
                    }

                    // 职业
                    if (_professionByUid.TryGetValue(uid, out var profession) && !string.IsNullOrWhiteSpace(profession))
                        data.Profession = profession;
                    else if (prof != null && !string.IsNullOrWhiteSpace(prof.Profession))
                    {
                        data.Profession = prof.Profession;
                        _professionByUid[uid] = data.Profession;
                    }
                }
                return data;
            }
        }



        /// <summary>
        /// 设置指定玩家的自定义属性（KV）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="key">属性键。</param>
        /// <param name="value">属性值。</param>
        public void SetAttrKV(long uid, string key, object value)
        {
            GetOrCreate(uid).SetAttrKV(key, value);
        }

        /// <summary>
        /// 获取指定玩家的自定义属性（不存在返回 null）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="key">属性键。</param>
        /// <returns>属性值或 null。</returns>
        public object? GetAttrKV(long uid, string key)
        {
            return GetOrCreate(uid).GetAttrKV(key);
        }
        #endregion

        // ------------------------------------------------------------
        // # 分类：定时循环/空闲判定与自动结束
        // ------------------------------------------------------------
        #region 定时循环

        /// <summary>
        /// 定时器回调：刷新所有玩家的实时统计，并判断空闲超时逻辑。
        /// </summary>
        private async void CheckTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            bool hasPlayers;
            lock (_playersLock) { hasPlayers = _players.Count > 0; }
            if (_lastAddTime == DateTime.MinValue || !hasPlayers) return;

            UpdateAllRealtimeStats();

            if (AppConfig.CombatTimeClearDelaySeconds != 0) // 0 表示永不自动结束
            {
                if (_lastCombatActivity != DateTime.MinValue &&
                    DateTime.Now - _lastCombatActivity > InactivityTimeout)
                {
                    // —— 不清空 —— 只结束并打标记
                    if (_combatStart.HasValue && !_combatEnd.HasValue)
                        _combatEnd = _lastCombatActivity;

                    _pendingClearOnNextCombat = true;   // 下次战斗开始再清空
                    _lastCombatActivity = DateTime.MinValue;
                }
            }

            // 目前无异步工作，仅占位保持签名一致
            await Task.CompletedTask;
        }

        #endregion

        // ------------------------------------------------------------
        // # 分类：全局写入（统一入口，转发至 PlayerData）
        // ------------------------------------------------------------
        #region 全局写入（转发至 PlayerData）



        /// <summary>
        /// 添加全局伤害记录（会标记战斗活动、写入到玩家聚合与分技能，并同步全程记录）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="skillId">技能 ID。</param>
        /// <param name="damage">伤害值。</param>
        /// <param name="isCrit">是否暴击。</param>
        /// <param name="isLucky">是否幸运。</param>
        ///   /// <param name="damageSource">伤害类型</param>
        /// <param name="hpLessen">HP 扣减值（可选）。</param>
        public void AddDamage(long uid, long skillId, string damageElement, ulong damage, bool isCrit, bool isLucky, bool isCauseLucky, ulong hpLessen = 0)
        {
            MarkCombatActivity();

            // ✅ 直接按实际 skillId 记账，不再做幸运并入上一发
            GetOrCreate(uid).AddDamage(skillId, damage, isCrit, isLucky, hpLessen, damageElement, isCauseLucky);

            // ✅ 日志/技能日记同样使用本次 skillId
            SkillDiaryGate.OnHit(uid, skillId, damage, isCrit, isLucky);

        }





        /// <summary>
        /// 添加全局治疗记录（写入至玩家聚合与分技能，并同步全程记录）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="skillId">技能 ID。</param>
        /// <param name="healing">治疗值。</param>
        /// <param name="isCrit">是否暴击。</param>
        /// <param name="isLucky">是否幸运。</param>
        /// <param name="hpFull">HP 补满值（可选）。</param>
        /// <param name="damageSource">治疗类型</param>
        /// <param name="targetUuid">被治疗的ID（可选）。</param>
        public void AddHealing(long uid, long skillId, string damageElement, ulong healing, bool isCrit, bool isLucky, bool isCauseLucky, ulong targetUuid)
        {
            if (!_combatStart.HasValue || _combatEnd.HasValue)
                return;

            _lastCombatActivity = DateTime.Now;

            // ✅ 直接按实际 skillId 记账
            GetOrCreate(uid).AddHealing(skillId, healing, isCrit, isLucky, damageElement, isCauseLucky, targetUuid);

            // ✅ 记录命中
            SkillDiaryGate.OnHit(uid, skillId, healing, isCrit, isLucky, true);
        }




        /// <summary>
        /// 添加全局承伤记录（完整参数版，推荐）。
        /// </summary>
        /// <param name="uid">玩家 UID（承伤者）。</param>
        /// <param name="skillId">来源技能 ID。</param>
        /// <param name="damage">承伤值。</param>
        /// <param name="isCrit">是否暴击。</param>
        /// <param name="isLucky">是否幸运。</param>
        /// <param name="hpLessen">HP 真实减少值（0 则默认使用 <paramref name="damage"/>）。</param>
        /// <param name="damageSource">伤害类型</param>
        /// <param name="isMiss">是否闪避。</param>
        /// <param name="isDead">是否死亡。</param>
        public void AddTakenDamage(long uid, long skillId, ulong damage, int damageSource, bool isMiss, bool isDead, bool isCrit, bool isLucky, ulong hpLessen = 0)
        {
            MarkCombatActivity();
            GetOrCreate(uid).AddTakenDamage(
      skillId, damage, isCrit, isLucky, hpLessen,
      damageSource, isMiss, isDead);
        }

        /// <summary>设置玩家职业（缓存 + 实例）。</summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="profession">职业名。</param>
        public void SetProfession(long uid, string profession)
        {
            _professionByUid[uid] = profession;
            GetOrCreate(uid).SetProfession(profession);
            UpsertCacheProfile(uid); // ★ 新增：写回本地缓存

        }

        /// <summary>设置玩家战力（缓存 + 实例）。</summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="combatPower">战力值。</param>
        public void SetCombatPower(long uid, int combatPower)
        {
            _combatPowerByUid[uid] = combatPower;
            GetOrCreate(uid).CombatPower = combatPower;
            UpsertCacheProfile(uid); // ★ 新增：写回本地缓存
        }

        /// <summary>设置玩家昵称（缓存 + 实例）。</summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="nickname">昵称。</param>
        public void SetNickname(long uid, string nickname)
        {
            _nicknameRequestedUids[uid] = nickname;
            GetOrCreate(uid).Nickname = nickname;
            UpsertCacheProfile(uid); // ★ 新增：写回本地缓存
        }

        #endregion

        // ------------------------------------------------------------
        // # 分类：批量与查询（玩家集/技能占比/技能详情/全队聚合）
        // ------------------------------------------------------------
        #region 批量与查询


        /// <summary>
        /// 获取有战斗数据的玩家集合（过滤掉没有伤害、治疗与承伤的玩家）。
        /// </summary>
        /// <returns>只返回有战斗数据的玩家列表。</returns>
        public IEnumerable<PlayerData> GetPlayersWithCombatData()
        {
            PlayerData[] snapshot;
            lock (_playersLock)
            {
                if (_players.Count == 0) return Array.Empty<PlayerData>();
                snapshot = _players.Values.ToArray(); // 仅在锁内复制
            }

            // 锁外筛选：只读“标量总值”，避免在 HasCombatData() 里枚举可变集合
            return snapshot.Where(p =>
                p != null &&
                (
                    (p.DamageStats?.Total ?? 0UL) != 0UL ||
                    (p.HealingStats?.Total ?? 0UL) != 0UL ||
                    p.TakenDamage != 0UL
                )
            );
        }

        /// <summary>刷新所有玩家的实时统计（滚动窗口）。</summary>
        public void UpdateAllRealtimeStats()
        {
            PlayerData[] players;
            lock (_playersLock)
            {
                if (_players.Count == 0) return;
                players = _players.Values.ToArray();
            }
            foreach (var p in players) p?.UpdateRealtimeStats();
        }


        /// <summary>获取所有玩家数据对象。</summary>
        /// <returns>玩家数据的枚举。</returns>
        public IEnumerable<PlayerData> GetAllPlayers()
        {
            lock (_playersLock) { return _players.Values.ToArray(); }
        }

        bool InitialStart = false;
        /// <summary>
        /// 清空所有玩家数据（可选是否保留战斗时钟）。清空前会自动保存当前战斗快照。
        /// </summary>
        /// <param name="keepCombatTime">true=保留战斗时钟；false=同时清除战斗时钟。</param>
        public void ClearAll(bool keepCombatTime = true)
        {
            throw new NotImplementedException("该功能已不在此处实现, 详情请查看 SRDA.Core.Data.DataStorage");

            //bool hadPlayers;
            //lock (_playersLock) { hadPlayers = _players.Count > 0; }

            //if (hadPlayers)
            //{
            //    if (_combatStart.HasValue && !_combatEnd.HasValue)
            //        _combatEnd = _lastCombatActivity != DateTime.MinValue ? _lastCombatActivity : DateTime.Now;

            //    // 先保存“当前战斗”的快照（如需把NPC也纳入快照，可在 SaveCurrentBattleSnapshot 内扩展）
            //    SaveCurrentBattleSnapshot();
            //}

            //// 清玩家
            //lock (_playersLock) { _players.Clear();}

            //// ✅ 清“当前战斗”的 NPC 统计（与玩家同一生命周期）
            //// 假设你把 NpcManager 实例挂在同级位置（例如 PlayerDataManager 外的静态单例）
            //StatisticData._npcManager?.ResetAll();

            //// UI 清理与战斗时钟复位（保持你原有逻辑）
            //FormManager.DpsStatistics.ListClear();
            //ResetCombatClock();
        }

        /// <summary>获取所有玩家 UID。</summary>
        public IEnumerable<long> GetAllUids()
        {
            lock (_playersLock) { return _players.Keys.ToArray(); }
        }

        /// <summary>
        /// 获取“全队 Top 技能”（按总伤害聚合）。
        /// </summary>
        /// <param name="topN">Top N 技能数量。</param>
        /// <returns>全队技能汇总列表（降序）。</returns>
        public List<TeamSkillSummary> GetTeamTopSkillsByTotal(int topN = 20)
        {
            PlayerData[] players;
            lock (_playersLock) { players = _players.Values.ToArray(); }

            var agg = new Dictionary<long, (ulong total, int count)>();
            foreach (var p in players)
            {
                // 技能字典也拍快照
                foreach (var kv in p.SkillUsage.ToArray())
                {
                    if (!agg.TryGetValue(kv.Key, out var a))
                        agg[kv.Key] = (kv.Value.Total, kv.Value.CountTotal);
                    else
                        agg[kv.Key] = (a.total + kv.Value.Total, a.count + kv.Value.CountTotal);
                }
            }

            return agg.OrderByDescending(x => x.Value.total)
                      .Take(topN)
                      .Select(x => new TeamSkillSummary
                      {
                          SkillId = x.Key,
                          SkillName = SkillBook.Get(x.Key).Name,
                          Total = x.Value.total,
                          HitCount = x.Value.count
                      })
                      .ToList();
        }

        /// <summary>
        /// 按玩家获取技能明细列表（支持按技能类型过滤）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="topN">Top N 数量（可选）。</param>
        /// <param name="orderByTotalDesc">是否按总量降序。</param>
        /// <param name="filterType">技能类型过滤（默认 Damage）。</param>
        /// <returns><see cref="SkillSummary"/> 列表。</returns>
        public List<SkillSummary> GetPlayerSkillSummaries(
            long uid,
            int? topN = null,
            bool orderByTotalDesc = true,
            Core.SkillType? filterType = Core.SkillType.Damage)
        {
            var p = GetOrCreate(uid);
            return p.GetSkillSummaries(topN, orderByTotalDesc, filterType);
        }


        /// <summary>
        /// 分类：按玩家获取实时技能占比（TopN + 其他）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="topN">Top N 技能。</param>
        /// <param name="includeOthers">是否包含“其他”。</param>
        /// <returns>占比数据 (SkillId, SkillName, Realtime, Percent)。</returns>
        public List<(long SkillId, string SkillName, ulong Realtime, int Percent)>
            GetPlayerSkillShareRealtime(long uid, int topN = 10, bool includeOthers = true)
        {
            var p = GetOrCreate(uid);
            return p.GetSkillDamageShareRealtime(topN, includeOthers);
        }


        /// <summary>
        /// 分类：按玩家 + 技能ID 获取单条技能详情。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="skillId">技能 ID。</param>
        /// <returns>存在则返回 <see cref="SkillSummary"/>；不存在返回 null。</returns>
        public SkillSummary? GetPlayerSkillDetail(long uid, long skillId)
        {
            var p = GetOrCreate(uid);
            if (!p.SkillUsage.TryGetValue(skillId, out var stat))
                return null;

            var meta = SkillBook.Get(skillId);
            return new SkillSummary
            {
                SkillId = skillId,
                SkillName = meta.Name,
                Total = stat.Total,
                HitCount = stat.CountTotal,
                AvgPerHit = stat.GetAveragePerHit(),
                CritRate = stat.GetCritRate(),
                LuckyRate = stat.GetLuckyRate(),
                MaxSingleHit = stat.MaxSingleHit,
                MinSingleHit = stat.MinSingleHit == ulong.MaxValue ? 0 : stat.MinSingleHit,
                RealtimeValue = stat.RealtimeValue,
                RealtimeMax = stat.RealtimeMax,
                TotalDps = stat.GetTotalPerSecond(),
                LastTime = stat.LastRecordTime
            };
        }


        /// <summary>
        /// 技能占比（整场总伤害）- 全队聚合。
        /// </summary>
        /// <param name="topN">Top N 技能数量。</param>
        /// <param name="includeOthers">是否包含“其他”。</param>
        /// <returns>(SkillId, SkillName, Total, Percent) 列表。</returns>
        // 2025-08-19 修改：同理，团队整场占比
        public List<(long SkillId, string SkillName, ulong Total, int Percent)>
            GetTeamSkillDamageShareTotal(int topN = 10, bool includeOthers = true)
        {
            PlayerData[] players;
            lock (_playersLock) { players = _players.Values.ToArray(); }

            var agg = new Dictionary<long, ulong>();
            foreach (var p in players)
            {
                foreach (var kv in p.SkillUsage.ToArray())
                {
                    if (kv.Value.Total == 0) continue;
                    agg[kv.Key] = agg.TryGetValue(kv.Key, out var old) ? old + kv.Value.Total : kv.Value.Total;
                }
            }
            if (agg.Count == 0) return new();

            ulong denom = 0; foreach (var v in agg.Values) denom += v; if (denom == 0) return new();

            var top = agg.Select(kv => new { kv.Key, Val = kv.Value })
                         .OrderByDescending(x => x.Val)
                         .Take(topN)
                         .ToList();

            ulong chosenSum = 0; foreach (var c in top) chosenSum += c.Val;

            var result = new List<(long, string, ulong, int)>(top.Count + 1);
            foreach (var c in top)
            {
                int pcent = (int)Math.Round((double)c.Val / denom * 100.0);
                result.Add((c.Key, SkillBook.Get(c.Key).Name, c.Val, pcent));
            }
            if (includeOthers && agg.Count > top.Count)
            {
                ulong others = denom - chosenSum;
                int pcent = (int)Math.Round((double)others / denom * 100.0);
                result.Add((0, "其他", others, pcent));
            }
            return result;
        }


        /// <summary>
        /// 根据UID获取玩家基础信息：昵称、战力、职业。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <returns>(Nickname, CombatPower, Profession) 三元组。</returns>
        public (string Nickname, int CombatPower, string Profession) GetPlayerBasicInfo(long uid)
        {
            // 先查已创建的 PlayerData
            // 对 _players 的访问统一加锁
            lock (_playersLock)
            {
                if (_players.TryGetValue(uid, out var player))
                {
                    return (player.Nickname, player.CombatPower, player.Profession);
                }
            }

            // 没有 PlayerData，则用缓存字典
            string nickname = _nicknameRequestedUids.TryGetValue(uid, out var name) ? name : "未知";
            int combatPower = _combatPowerByUid.TryGetValue(uid, out var power) ? power : 0;
            string profession = _professionByUid.TryGetValue(uid, out var prof) ? prof : "未知";

            return (nickname, combatPower, profession);
        }

        /// <summary>
        /// 根据玩家 UID 获取完整统计信息（聚合视图）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <returns>包含伤害/治疗/承伤/瞬时/极值等的聚合元组。</returns>
        public (long Uid, string Nickname, int CombatPower, string Profession,
        ulong TotalDamage, double CritRate, double LuckyRate,
        ulong MaxSingleHit, ulong MinSingleHit,
        ulong RealtimeDps, ulong RealtimeDpsMax,
        double TotalDps, ulong TotalHealing, double TotalHps,
        ulong TakenDamage, DateTime? LastRecordTime)
        GetPlayerFullStats(long uid)
        {
            if (!_players.TryGetValue(uid, out var p))
                return (uid, "未知", 0, "未知", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);

            var dmg = p.DamageStats;
            var heal = p.HealingStats;

            return (
                p.Uid,
                p.Nickname,
                p.CombatPower,
                p.Profession,

                dmg.Total,
                dmg.GetCritRate(),
                dmg.GetLuckyRate(),

                dmg.MaxSingleHit,
                dmg.MinSingleHit == ulong.MaxValue ? 0 : dmg.MinSingleHit,

                dmg.RealtimeValue,
                dmg.RealtimeMax,

                p.GetTotalDps(),
                heal.Total,
                p.GetTotalHps(),

                p.TakenDamage,
                dmg.LastRecordTime
            );
        }


        // ------------------------------------------------------------
        // # 分类：承伤查询接口（总量/概览/分技能）
        // ------------------------------------------------------------
        #region 查询承伤
        /// <summary>
        /// 已有：返回玩家总承伤（快速路径）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <returns>玩家总承伤（<see cref="PlayerData.TakenDamage"/>）。</returns>
        public ulong GetPlayerTakenDamageTotal(long uid)
            => GetOrCreate(uid).TakenDamage;

        /// <summary>
        /// 总承伤概览：总承伤 / 平均每秒承伤 / 实时承伤 / 单次最大最小 / 最后一次承伤时间
        /// - 平均每秒承伤 = 玩家总承伤 ÷ 当前整场战斗时长（用全局战斗时钟）
        /// - 实时承伤 = 1秒窗口内（你当前设为1秒）的各技能承伤 RealtimeValue 之和
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <returns>包含六项的概览元组。</returns>
        public (
            ulong Total,
            double AvgTakenPerSec,
            ulong RealtimeTaken,
            ulong MaxSingleHit,
            ulong MinSingleHit,
            DateTime? LastTime
        ) GetPlayerTakenOverview(long uid)
        {
            var p = GetOrCreate(uid);

            ulong total = p.TakenDamage;
            var dur = GetCombatDuration().TotalSeconds;
            double avgPerSec = (dur > 0) ? total / dur : 0.0;

            var s = p.TakenStats;
            ulong realtime = s.RealtimeValue;
            ulong maxHit = s.MaxSingleHit;
            ulong minHit = s.MinSingleHit == ulong.MaxValue ? 0 : s.MinSingleHit;
            DateTime? last = s.LastRecordTime;

            return (total, avgPerSec, realtime, maxHit, minHit, last);
        }



        /// <summary>
        /// 获取指定玩家的承伤技能汇总列表（整场）。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="topN">Top N 技能（可选；null 或 &lt;=0 表示全部）。</param>
        /// <param name="orderByTotalDesc">是否按承伤总量降序。</param>
        /// <returns><see cref="SkillSummary"/> 列表。</returns>
        public List<SkillSummary> GetPlayerTakenDamageSummaries(long uid, int? topN = null, bool orderByTotalDesc = true)
        {
            var p = GetOrCreate(uid);
            return p.GetTakenDamageSummaries(topN, orderByTotalDesc);
        }


        /// <summary>
        /// 获取指定玩家被某个技能打到的详细承伤统计。
        /// </summary>
        /// <param name="uid">玩家 UID。</param>
        /// <param name="skillId">技能 ID。</param>
        /// <returns>存在则返回 <see cref="SkillSummary"/>；否则返回 null。</returns>
        public SkillSummary? GetPlayerTakenDamageDetail(long uid, long skillId)
        {
            var p = GetOrCreate(uid);
            return p.GetTakenDamageDetail(skillId);
        }

        #endregion


        #endregion

        // ------------------------------------------------------------
        // # 分类：快照（生成/保存）
        // ------------------------------------------------------------
        #region 快照类
        /// <summary>
        /// 生成并保存当前战斗快照（在清空前调用）。
        /// 规则：若当前处于进行中且未标记结束，则结束时间取“最后活动时间或 Now”。
        /// </summary>
        // 2025-08-19 修改：锁内抽取 players 快照，锁外遍历
        private void SaveCurrentBattleSnapshot()
        {
            PlayerData[] players;
            lock (_playersLock)
            {
                if (_players.Count == 0) return;
                players = _players.Values.ToArray();
            }

            var endedAt = DateTime.Now;
            var startedAt = _combatStart ?? endedAt;
            var duration = _combatEnd.HasValue ? _combatEnd.Value - startedAt : endedAt - startedAt;
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

            var label = $"结束时间：{endedAt:HH:mm:ss}";

            ulong teamDmg = 0, teamHeal = 0, teamTaken = 0;
            var snapPlayers = new Dictionary<long, SnapshotPlayer>(players.Length);

            foreach (var p in players)
            {
                if (p == null || !p.HasCombatData()) continue;

                var dmg = p.DamageStats;
                var heal = p.HealingStats;

                // 先抓一次快照值（同一时刻的视角）
                var totalDmg = dmg.Total;
                var totalHeal = heal.Total;
                var totalTaken = p.TakenDamage;
                // 用捕获值过滤
                if (totalDmg == 0 && totalHeal == 0 && totalTaken == 0)
                    continue;

                dmg.UpdateRealtimeStats();
                heal.UpdateRealtimeStats();

                teamDmg += dmg.Total;
                teamHeal += heal.Total;
                teamTaken += p.TakenDamage;

                var damageSkills = p.GetSkillSummaries(null, true, Core.SkillType.Damage);
                var healingSkills = p.GetSkillSummaries(null, true, Core.SkillType.Heal);
                var takenSkills = p.GetTakenDamageSummaries(null, true);

                var sp = new SnapshotPlayer
                {
                    Uid = p.Uid,
                    Nickname = p.Nickname,
                    CombatPower = p.CombatPower,
                    Profession = p.Profession,
                    SubProfession = p.SubProfession,
                    TotalDamage = dmg.Total,
                    TotalDps = p.GetTotalDps(),
                    TotalHealing = heal.Total,
                    TotalHps = p.GetTotalHps(),
                    TakenDamage = p.TakenDamage,
                    LastRecordTime = dmg.LastRecordTime,
                    DamageSkills = damageSkills,
                    HealingSkills = healingSkills,
                    TakenSkills = takenSkills,
                    RealtimeDps = dmg.RealtimeValue,
                    CritRate = dmg.GetCritRate(),
                    LuckyRate = dmg.GetLuckyRate(),
                    CriticalDamage = dmg.Critical,
                    LuckyDamage = dmg.Lucky + dmg.CritLucky,   // ★ 合并
                    CritLuckyDamage = dmg.CritLucky,
                    MaxSingleHit = dmg.MaxSingleHit,
                    HealingCritical = heal.Critical,
                    HealingLucky = heal.Lucky + heal.CritLucky,
                    HealingCritLucky = heal.CritLucky,
                    HealingRealtime = heal.RealtimeValue,
                    HealingRealtimeMax = heal.RealtimeMax,
                    RealtimeDpsMax = dmg.RealtimeMax,
                };

                snapPlayers[p.Uid] = sp;
            }
            if (snapPlayers.Count == 0) return;

            var snapshot = new BattleSnapshot
            {
                Label = label,
                StartedAt = startedAt,
                EndedAt = _combatEnd ?? endedAt,
                Duration = duration,
                TeamTotalDamage = teamDmg,
                TeamTotalHealing = teamHeal,
                TeamTotalTakenDamage = teamTaken,
                Players = snapPlayers
            };

            _history.Add(snapshot);
        }


        /// <summary>
        /// 清空所有历史快照（不影响当前战斗数据）
        /// </summary>
        public void ClearSnapshots()
        {
            _history.Clear();
        }
        #endregion

        #region NPC


        /// <summary>
        /// 单个 NPC 的承伤数据（不区分技能，只按“攻击者玩家”聚合）。
        /// </summary>
        public sealed class NpcData
        {
            /// <summary>NPC 唯一ID（可用怪物实体ID或模板ID，按你的采集口径）。</summary>
            public long NpcId { get; }

            /// <summary>NPC 名称（可选）。</summary>
            public string Name { get; private set; } = "未知NPC";

            /// <summary>NPC 承伤聚合（总承伤/实时/峰值/极值等）。</summary>
            public StatisticData TakenStats { get; } = new();

            /// <summary>
            /// 攻击者UID -> 该玩家对该NPC造成的聚合统计（不分技能）。
            /// 仅用于“对NPC伤害排名”。
            /// </summary>
            public Dictionary<long, StatisticData> DamageByPlayer { get; } = new();

            public NpcData(long npcId, string? name = null)
            {
                NpcId = npcId;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    Name = name!;
                }
                else
                {
                    Name = npcId.ToString();
                }
            }

            public void SetName(string name)
            {
                if (!string.IsNullOrWhiteSpace(name)) Name = name;
            }

            /// <summary>
            /// 记录一次“玩家 → NPC”的伤害。
            /// 不区分技能，只累计到攻击者与 NPC 承伤聚合。
            /// </summary>
            public void AddTakenFrom(
                long attackerUid,
                ulong damage,
                bool isCrit,
                bool isLucky,
                ulong hpLessen = 0,
                bool isMiss = false,
                bool isDead = false)
            {
                // 仅排名和承伤需要：Miss不计数值，但可计次数（可选）
                if (isMiss)
                {
                    var s = GetOrCreate(attackerUid);
                    s.RegisterMiss();
                    return;
                }

                var lessen = hpLessen > 0 ? hpLessen : damage;

                // NPC 承伤聚合
                TakenStats.AddRecord(damage, isCrit, isLucky, lessen);

                // 攻击者聚合（对NPC造成的伤害）
                GetOrCreate(attackerUid).AddRecord(damage, isCrit, isLucky, lessen);

                // 击杀次数（可选），仅计数，不影响总量
                if (isDead) TakenStats.RegisterKill();

            }

            private StatisticData GetOrCreate(long uid)
            {
                if (!DamageByPlayer.TryGetValue(uid, out var stat))
                {
                    stat = new StatisticData();
                    DamageByPlayer[uid] = stat;
                }
                return stat;
            }

            /// <summary>刷新该NPC的实时统计（含各攻击者的统计）。</summary>
            public void UpdateRealtime()
            {
                TakenStats.UpdateRealtimeStats();
                if (DamageByPlayer.Count == 0) return;
                foreach (var s in DamageByPlayer.Values)
                    s.UpdateRealtimeStats();
            }

            /// <summary>清空该NPC所有数据。</summary>
            public void Reset()
            {
                TakenStats.Reset();
                DamageByPlayer.Clear();
            }
        }

        /// <summary>
        /// NPC 统计管理器：创建/缓存 NPC，写入承伤事件，查询对NPC的伤害排名。
        /// 说明：
        /// - 只用于 NPC 维度的“承伤与排名”；玩家的总DPS沿用你现有 PlayerDataManager。
        /// - 你可以在解析到“命中目标是NPC”时，同时调用 Players.AddDamage(...)（玩家侧）和本管理器的 AddNpcTakenDamage(...)（NPC侧）。
        /// </summary>
        public sealed class NpcManager
        {
            private readonly object _lock = new();
            private readonly Dictionary<long, NpcData> _npcs = new();

            /// <summary>玩家数据管理器（用于拿昵称/战力/职业/秒伤）。</summary>
            public PlayerDataManager Players { get; }

            public NpcManager(PlayerDataManager players)
            {
                Players = players;
            }

            /// <summary>获取或创建 NPC。</summary>
            public NpcData GetOrCreate(long npcId, string? name = null)
            {
                lock (_lock)
                {
                    if (!_npcs.TryGetValue(npcId, out var npc))
                    {
                        npc = new NpcData(npcId, name);
                        _npcs[npcId] = npc;
                    }
                    else if (!string.IsNullOrWhiteSpace(name))
                    {
                        npc.SetName(name!);
                    }
                    return npc;
                }
            }

            /// <summary>设置 NPC 名称（可选）。</summary>
            public void SetNpcName(long npcId, string name)
            {
                GetOrCreate(npcId).SetName(name);
                FullRecord.SetNpcName(npcId, name);
            }
            // 1) 列出所有出现过的 NPCId（当前战斗）
            public IReadOnlyList<long> GetAllNpcIds()
            {
                lock (_lock)
                {
                    if (_npcs.Count == 0) return Array.Empty<long>();
                    return _npcs.Keys.ToList();
                }
            }

            // 2) 取 NPC 名称（当前战斗）
            public string GetNpcName(long npcId)
            {
                lock (_lock)
                {
                    return _npcs.TryGetValue(npcId, out var n) ? (n.Name ?? $"NPC[{npcId}]") : $"NPC[{npcId}]";
                }
            }

            // 3) 当前战斗里该 NPC 的“承伤PS”= Total / ActiveSeconds（由 StatisticData 维护）
            public double GetNpcTakenPerSecond(long npcId)
            {
                var n = GetOrCreate(npcId);
                return n.TakenStats.GetTotalPerSecond();
            }

            // 4) 当前战斗里“某玩家对该 NPC 的专属DPS”= Total / ActiveSeconds（只看该 NPC 维度）
            public double GetPlayerNpcOnlyDps(long npcId, long uid)
            {
                var n = GetOrCreate(npcId);
                if (!n.DamageByPlayer.TryGetValue(uid, out var s)) return 0;
                return s.GetTotalPerSecond();
            }
            /// <summary>
            /// 记录一次“玩家 → NPC”的伤害（不区分技能）。
            /// 建议：同时仍调用 Players.AddDamage(...) 以维持玩家侧总DPS与技能数据。
            /// </summary>
            public void AddNpcTakenDamage(
                long npcId,
                long attackerUid,
                long skillId,
                ulong damage,
                bool isCrit,
                bool isLucky,
                ulong hpLessen = 0,
                bool isMiss = false,
                bool isDead = false,
                string? npcName = null)
            {
                var npc = GetOrCreate(npcId, npcName);
                npc.AddTakenFrom(attackerUid, damage, isCrit, isLucky, hpLessen, isMiss, isDead);
                FullRecord.RecordNpcTakenDamage(npcId, attackerUid, damage, isCrit, isLucky, hpLessen, isMiss, isDead);


            }

            /// <summary>
            /// 刷新所有 NPC 的实时统计窗口（1s）。
            /// </summary>
            public void UpdateAllRealtime()
            {
                NpcData[] snapshot;
                lock (_lock)
                {
                    if (_npcs.Count == 0) return;
                    snapshot = _npcs.Values.ToArray();
                }
                foreach (var npc in snapshot) npc.UpdateRealtime();
            }

            /// <summary>
            /// 清空指定 NPC 的统计。
            /// </summary>
            public void ResetNpc(long npcId)
            {
                lock (_lock)
                {
                    if (_npcs.TryGetValue(npcId, out var npc))
                        npc.Reset();
                }
            }

            /// <summary>
            /// 清空所有 NPC 的统计。
            /// </summary>
            public void ResetAll()
            {
                lock (_lock)
                {
                    foreach (var npc in _npcs.Values) npc.Reset();
                    _npcs.Clear();
                }
            }

            // =========================
            // 查询接口
            // =========================

            /// <summary>
            /// 获取某个 NPC 的承伤总览：总承伤/实时承伤/峰值/单次最大最小/最后一次时间。
            /// </summary>
            public (
                ulong TotalTaken,
                ulong RealtimeTaken,
                ulong RealtimeTakenMax,
                ulong MaxSingleHit,
                ulong MinSingleHit,
                DateTime? LastTime
            ) GetNpcOverview(long npcId)
            {
                var npc = GetOrCreate(npcId);
                var s = npc.TakenStats;
                return (
                    s.Total,
                    s.RealtimeValue,
                    s.RealtimeMax,
                    s.MaxSingleHit,
                    s.MinSingleHit == ulong.MaxValue ? 0UL : s.MinSingleHit,
                    s.LastRecordTime
                );
            }

            /// <summary>
            /// 对指定 NPC 的伤害排名（按总伤害降序）。
            /// 同时返回该玩家当前总秒伤（沿用 Players 的口径：整场平均）。
            /// </summary>
            /// <param name="npcId">NPC ID。</param>
            /// <param name="topN">前 N 名（默认 20）。</param>
            /// <returns>列表：(Uid, Nickname, CombatPower, Profession, DamageToNpc, TotalDps)</returns>
            public List<(long Uid, string Nickname, int CombatPower, string Profession, ulong DamageToNpc, double TotalDps)>
                GetNpcTopAttackers(long npcId, int topN = 20)
            {
                var npc = GetOrCreate(npcId);

                // 快照一次，避免锁外并发修改
                var items = npc.DamageByPlayer.ToArray();

                var ordered = items
                    .OrderByDescending(kv => kv.Value.Total)
                    .Take(topN)
                    .Select(kv =>
                    {
                        var uid = kv.Key;
                        var totalToNpc = kv.Value.Total;

                        // 从玩家管理器拿基础信息与DPS
                        var (nickname, power, profession) = Players.GetPlayerBasicInfo(uid);
                        var full = Players.GetPlayerFullStats(uid);
                        var totalDps = full.TotalDps;

                        return (Uid: uid,
                                Nickname: nickname,
                                CombatPower: power,
                                Profession: profession,
                                DamageToNpc: totalToNpc,
                                TotalDps: totalDps);
                    })
                    .ToList();

                return ordered;
            }

            /// <summary>
            /// 读取指定 NPC 下，某个玩家对其造成伤害的实时与总量（便于小窗展示）。
            /// </summary>
            /// <returns>(Total, Realtime, RealtimeMax, AvgPerHit, MaxHit, MinHit)</returns>
            public (ulong Total, ulong Realtime, ulong RealtimeMax, double AvgPerHit, ulong MaxHit, ulong MinHit)
                GetPlayerVsNpcStats(long npcId, long uid)
            {
                var npc = GetOrCreate(npcId);
                if (!npc.DamageByPlayer.TryGetValue(uid, out var s))
                    return (0, 0, 0, 0, 0, 0);

                return (
                    s.Total,
                    s.RealtimeValue,
                    s.RealtimeMax,
                    s.GetAveragePerHit(),
                    s.MaxSingleHit,
                    s.MinSingleHit == ulong.MaxValue ? 0UL : s.MinSingleHit
                );
            }
        }
        #endregion
    }
    #endregion

    #region 快照类

    /// <summary>
    /// 一场战斗的完整快照（可用于历史列表、导出或复盘）。
    /// </summary>
    public sealed class BattleSnapshot
    {
        /// <summary>UI 标签（例如：结束时间）。</summary>
        public string Label { get; init; } = "";

        /// <summary>战斗开始时间（若未知则为 EndedAt）。</summary>
        public DateTime StartedAt { get; init; }

        /// <summary>战斗结束/快照时间。</summary>
        public DateTime EndedAt { get; init; }

        /// <summary>战斗时长。</summary>
        public TimeSpan Duration { get; init; }

        /// <summary>全队总伤害。</summary>
        public ulong TeamTotalDamage { get; init; }

        /// <summary>全队总治疗。</summary>
        public ulong TeamTotalHealing { get; init; }

        /// <summary>UID -> 玩家快照字典。</summary>
        public Dictionary<long, SnapshotPlayer> Players { get; init; } = new();

        /// <summary>全队总承伤。</summary>
        public ulong TeamTotalTakenDamage { get; init; }   // ★ 新增

    }

    /// <summary>
    /// 单个玩家在该场战斗的快照（包含聚合与分技能明细）。
    /// </summary>
    public sealed class SnapshotPlayer
    {
        /// <summary>玩家 UID。</summary>
        public long Uid { get; init; }

        /// <summary>昵称。</summary>
        public string Nickname { get; init; } = "未知";

        /// <summary>战力。</summary>
        public int CombatPower { get; init; }

        /// <summary>职业。</summary>
        public string Profession { get; init; } = "未知";

        public string? SubProfession { get; init; }


        /// <summary>实时 DPS（窗口内累计）。</summary>
        public ulong RealtimeDps { get; init; }

        /// <summary>暴击率（百分比 0~100）。</summary>
        public double CritRate { get; init; }

        /// <summary>幸运率（百分比 0~100）。</summary>
        public double LuckyRate { get; init; }

        /// <summary>暴击造成的累计伤害。</summary>
        public ulong CriticalDamage { get; init; }

        /// <summary>幸运造成的累计伤害。</summary>
        public ulong LuckyDamage { get; init; }

        /// <summary>暴击且幸运造成的累计伤害。</summary>
        public ulong CritLuckyDamage { get; init; }

        /// <summary>单次最高伤害。</summary>
        public ulong MaxSingleHit { get; init; }


        // 聚合
        /// <summary>总伤害（整场）。</summary>
        public ulong TotalDamage { get; init; }

        /// <summary>总 DPS（整场平均每秒）。</summary>
        public double TotalDps { get; init; }

        /// <summary>总治疗。</summary>
        public ulong TotalHealing { get; init; }

        /// <summary>总 HPS（整场平均每秒）。</summary>
        public double TotalHps { get; init; }

        /// <summary>总承伤（整场）。</summary>
        public ulong TakenDamage { get; init; }

        /// <summary>最后一次（伤害侧）记录时间。</summary>
        public DateTime? LastRecordTime { get; init; }

        /// <summary>技能明细：伤害。</summary>
        public List<SkillSummary> DamageSkills { get; init; } = new();

        /// <summary>技能明细：治疗。</summary>
        public List<SkillSummary> HealingSkills { get; init; } = new();

        /// <summary>技能明细：承伤。</summary>
        public List<SkillSummary> TakenSkills { get; init; } = new();

        /// <summary>
        /// 伤害统计的“有效作战时长”（秒）。
        /// 只在玩家产生伤害的区间累加；用于计算 TotalDps = TotalDamage / ActiveSecondsDamage。
        /// </summary>
        public double ActiveSecondsDamage { get; init; }

        /// <summary>
        /// 治疗统计的“有效作战时长”（秒）。
        /// 只在玩家产生治疗的区间累加；用于计算 TotalHps = TotalHealing / ActiveSecondsHealing。
        /// </summary>
        public double ActiveSecondsHealing { get; init; }

        /// <summary>
        /// 全程累计的“暴击治疗”总量（单位：点）。
        /// 由治疗事件聚合得到，仅计入暴击标记的治疗。
        /// </summary>
        public ulong HealingCritical { get; init; }

        /// <summary>
        /// 全程累计的“幸运治疗”总量（单位：点）。
        /// 由治疗事件聚合得到，仅计入幸运标记的治疗。
        /// </summary>
        public ulong HealingLucky { get; init; }

        /// <summary>
        /// 全程累计的“暴击且幸运治疗”总量（单位：点）。
        /// 同时满足暴击与幸运标记的治疗量聚合。
        /// </summary>
        public ulong HealingCritLucky { get; init; }

        /// <summary>
        /// 实时治疗量（HPS，单位：点/秒）。
        /// 通常取最近 1 秒或 N 秒滑动窗口的瞬时值；全程快照若不记录实时指标，可为 0。
        /// </summary>
        public ulong HealingRealtime { get; init; }

        /// <summary>
        /// 会话内观测到的治疗实时峰值（HPS，单位：点/秒）。
        /// 取实时窗口的最大值；全程快照若不记录实时指标，可为 0。
        /// </summary>
        public ulong HealingRealtimeMax { get; init; }

        /// <summary>
        /// 会话内观测到的伤害实时峰值（DPS，单位：点/秒）。
        /// 取实时窗口的最大值；全程快照若不记录实时指标，可为 0。
        /// </summary>
        public ulong RealtimeDpsMax { get; init; }


    }



    #endregion



}
