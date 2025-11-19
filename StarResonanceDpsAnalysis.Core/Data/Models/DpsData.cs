using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.Core.Data.Models
{
    public class DpsData
    {
        private long _uid;
        private long _startLoggedTick; // 使用0表示null
        private long _lastLoggedTick;
        private long _totalAttackDamage;
        private long _totalTakenDamage;
        private long _totalHeal;

        /// <summary>
        /// 玩家UID
        /// </summary>
        public long UID
        {
            get => Interlocked.Read(ref _uid);
            internal set => Interlocked.Exchange(ref _uid, value);
        }

        /// <summary>
        /// 统计开始的时间戳 (Ticks)
        /// </summary>
        public long? StartLoggedTick
        {
            get
            {
                var value = Interlocked.Read(ref _startLoggedTick);
                return value == 0 ? null : value;
            }
            internal set => Interlocked.Exchange(ref _startLoggedTick, value ?? 0);
        }

        /// <summary>
        /// 最后一次统计的时间戳 (Ticks)
        /// </summary>
        public long LastLoggedTick
        {
            get => Interlocked.Read(ref _lastLoggedTick);
            internal set => Interlocked.Exchange(ref _lastLoggedTick, value);
        }

        /// <summary>
        /// 统计的总伤害
        /// </summary>
        public long TotalAttackDamage
        {
            get => Interlocked.Read(ref _totalAttackDamage);
            internal set => Interlocked.Exchange(ref _totalAttackDamage, value);
        }

        /// <summary>
        /// 统计的总承受伤害
        /// </summary>
        public long TotalTakenDamage
        {
            get => Interlocked.Read(ref _totalTakenDamage);
            internal set => Interlocked.Exchange(ref _totalTakenDamage, value);
        }

        /// <summary>
        /// 统计的总治疗量
        /// </summary>
        public long TotalHeal
        {
            get => Interlocked.Read(ref _totalHeal);
            internal set => Interlocked.Exchange(ref _totalHeal, value);
        }

        /// <summary>
        /// 是否为NPC数据
        /// </summary>
        public bool IsNpcData { get; internal set; } = false;

        // Lock-free immutable battle logs
        private ImmutableList<BattleLog> _battleLogs = ImmutableList<BattleLog>.Empty;

        /// <summary>
        /// 战斗日志列表 - 内部使用的可变构建器
        /// </summary>
        internal List<BattleLog> BattleLogs { get; } = new(16384);

        /// <summary>
        /// 只读战斗日志列表 - 返回快照，无需锁
        /// </summary>
        public IReadOnlyList<BattleLog> ReadOnlyBattleLogs => Volatile.Read(ref _battleLogs);

        /// <summary>
        /// 添加战斗日志（线程安全，无锁）
        /// </summary>
        internal void AddBattleLog(BattleLog log)
        {
            // Add to mutable list for internal processing
            BattleLogs.Add(log);

            // Update immutable snapshot atomically
            ImmutableList<BattleLog> current, updated;
            do
            {
                current = Volatile.Read(ref _battleLogs);
                updated = current.Add(log);
            } while (Interlocked.CompareExchange(ref _battleLogs, updated, current) != current);
        }

        /// <summary>
        /// 批量添加战斗日志（性能优化，线程安全）
        /// </summary>
        internal void AddBattleLogRange(IEnumerable<BattleLog> logs)
        {
            var logsArray = logs as BattleLog[] ?? logs.ToArray();
            
            // Add to mutable list
            BattleLogs.AddRange(logsArray);

            // Update immutable snapshot atomically
            ImmutableList<BattleLog> current, updated;
            do
            {
                current = Volatile.Read(ref _battleLogs);
                updated = current.AddRange(logsArray);
            } while (Interlocked.CompareExchange(ref _battleLogs, updated, current) != current);
        }

        /// <summary>
        /// 清空战斗日志（线程安全）
        /// </summary>
        internal void ClearBattleLogs()
        {
            BattleLogs.Clear();
            Volatile.Write(ref _battleLogs, ImmutableList<BattleLog>.Empty);
        }

        /// <summary>
        /// 技能统计数据字典
        /// </summary>
        internal Dictionary<long, SkillData> SkillDic { get; } = [];
        private readonly object _skillLock = new();

        /// <summary>
        /// 只读技能统计数据字典
        /// </summary>
        public ReadOnlyDictionary<long, SkillData> ReadOnlySkillDatas
        {
            get
            {
                lock (_skillLock)
                {
                    var copy = SkillDic.ToDictionary(static pair => pair.Key,
                        static pair => CloneSkillData(pair.Value));
                    return new ReadOnlyDictionary<long, SkillData>(copy);
                }
            }
        }

        /// <summary>
        /// 只读技能统计数据列表
        /// </summary>
        public IReadOnlyList<SkillData> ReadOnlySkillDataList
        {
            get
            {
                lock (_skillLock)
                {
                    return SkillDic.Values.Select(CloneSkillData).ToList();
                }
            }
        }

        /// <summary>
        /// 获取或创建技能统计数据
        /// </summary>
        /// <param name="skillId">技能UID</param>
        /// <param name="updater"></param>
        /// <returns></returns>
        public void UpdateSkillData(long skillId, Action<SkillData> updater)
        {
            if (updater is null) throw new ArgumentNullException(nameof(updater));

            lock (_skillLock)
            {
                if (!SkillDic.TryGetValue(skillId, out var skillData))
                {
                    skillData = new SkillData(skillId);
                    SkillDic[skillId] = skillData;
                }

                updater(skillData);
            }
        }

        // Thread-safe increment methods
        internal long AddTotalAttackDamage(long value)
        {
            return Interlocked.Add(ref _totalAttackDamage, value);
        }

        internal long AddTotalTakenDamage(long value)
        {
            return Interlocked.Add(ref _totalTakenDamage, value);
        }

        internal long AddTotalHeal(long value)
        {
            return Interlocked.Add(ref _totalHeal, value);
        }

        private static SkillData CloneSkillData(SkillData source)
        {
            return new SkillData(source.SkillId)
            {
                TotalValue = source.TotalValue,
                UseTimes = source.UseTimes,
                CritTimes = source.CritTimes,
                LuckyTimes = source.LuckyTimes
            };
        }
    }
}
