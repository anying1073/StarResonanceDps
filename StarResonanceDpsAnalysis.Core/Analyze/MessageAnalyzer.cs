using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Extends.BlueProto;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Tools;
using Zproto;
using ZstdNet;

// 0472 -> 针对可控类型与 null 的比较, 由于使用了 Protobuf 的固定类型, 本页禁用该警告
#pragma warning disable 0472

namespace StarResonanceDpsAnalysis.Core.Analyze
{
    /// <summary>
    /// 消息解析器
    /// 负责处理从游戏抓包获得的TCP数据，包括解压缩、Protobuf解析、数据同步、伤害统计等。
    /// </summary>
    internal class MessageAnalyzer
    {
        /// <summary>
        /// 顶层消息类型处理器
        /// Key = 消息类型ID (低15位)
        /// Value = 对应的解析方法
        /// </summary>
        private static readonly Dictionary<MessageType, Action<ByteReader, bool, ILogger?>> MessageHandlerMap = new()
        {
            { MessageType.Notify, ProcessNotifyMsg },
            { MessageType.FrameDown, ProcessFrameDown }
        };

        /// <summary>
        /// 主入口：处理一批TCP数据包
        /// </summary>
        public static void Process(byte[] packets, ILogger? logger = null)
        {
            var packetsReader = new ByteReader(packets);
            while (packetsReader.Remaining > 0)
            {
                // 包头长度检查
                if (!packetsReader.TryPeekUInt32BE(out uint packetSize)) break;
                if (packetSize < 6) break;                           // 小于最小长度，不合法
                if (packetSize > packetsReader.Remaining) break;     // 不完整，等待下个包

                // 按长度截取出一个完整包
                var packetReader = new ByteReader(packetsReader.ReadBytes((int)packetSize));
                uint sizeAgain = packetReader.ReadUInt32BE();
                if (sizeAgain != packetSize) continue; // 长度不一致，丢弃

                // 读取消息类型
                var packetType = packetReader.ReadUInt16BE();
                var isZstdCompressed = (packetType & 0x8000) != 0; // 高位bit15表示是否压缩
                var msgTypeId = packetType & 0x7FFF;                // 低15位是真实类型

                // 分发到对应处理方法
                logger?.LogTrace("MessageTypeId:{id}", msgTypeId);
                if (!MessageHandlerMap.TryGetValue((MessageType)msgTypeId, out var handler)) continue;
                handler?.Invoke(packetReader, isZstdCompressed, logger);
            }

        }

        /// <summary>
        /// Notify 消息内部方法表
        /// Key = methodId
        /// Value = 对应的处理方法
        /// </summary>
        private static readonly Dictionary<uint, Action<byte[], bool>> ProcessMethods = new()
        {
            { 0x00000006U, ProcessSyncNearEntities },        // 同步周边玩家实体
            { 0x00000015U, ProcessSyncContainerData },       // 同步自身完整容器数据
            { 0x00000016U, ProcessSyncContainerDirtyData },  // 同步自身部分更新（脏数据）
            { 0x0000002EU, ProcessSyncToMeDeltaInfo },       // 同步自己受到的增量伤害
            { 0x0000002DU, ProcessSyncNearDeltaInfo }        // 同步周边增量伤害
        };

        /// <summary>
        /// 处理 Notify 消息（带 serviceUuid 和 methodId 的 RPC）
        /// </summary>
        public static void ProcessNotifyMsg(ByteReader packet, bool isZstdCompressed, ILogger? logger = null)
        {
            var serviceUuid = packet.ReadUInt64BE(); // 服务UUID
            _ = packet.ReadUInt32BE(); // stubId (暂时不用)
            var methodId = packet.ReadUInt32BE(); // 方法ID

            if (serviceUuid != 0x0000000063335342UL) return; // 非战斗相关，忽略

            byte[] msgPayload = packet.ReadRemaining();
            byte[] origPayload = msgPayload;
            if (isZstdCompressed) msgPayload = DecompressZstdIfNeeded(msgPayload);

            logger?.LogTrace("MethodId: {methodId}", methodId);
            if (!ProcessMethods.TryGetValue(methodId, out var processMethod)) return;
            processMethod(msgPayload, isZstdCompressed);
        }

        #region Zstd 解压逻辑
        private static readonly uint ZSTD_MAGIC = 0xFD2FB528;
        private static readonly uint SKIPPABLE_MAGIC_MIN = 0x184D2A50;
        private static readonly uint SKIPPABLE_MAGIC_MAX = 0x184D2A5F;

        /// <summary>
        /// 如果数据包含Zstd帧则解压缩，否则原样返回
        /// </summary>
        private static byte[] DecompressZstdIfNeeded(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 4) return [];

            int off = 0;
            while (off + 4 <= buffer.Length)
            {
                uint magic = BitConverter.ToUInt32(buffer, off);
                if (magic == ZSTD_MAGIC) break;
                if (magic >= SKIPPABLE_MAGIC_MIN && magic <= SKIPPABLE_MAGIC_MAX)
                {
                    if (off + 8 > buffer.Length) throw new InvalidDataException("不完整的skippable帧头");

                    uint size = BitConverter.ToUInt32(buffer, off + 4);
                    if (off + 8 + size > buffer.Length) throw new InvalidDataException("不完整的skippable帧数据");

                    off += 8 + (int)size;

                    continue;
                }

                off++;
            }
            if (off + 4 > buffer.Length) return buffer;

            using var input = new MemoryStream(buffer, off, buffer.Length - off, writable: false);
            using var decoder = new DecompressionStream(input);
            using var output = new MemoryStream();

            const long MAX_OUT = 32L * 1024 * 1024; // 最大解压32MB
            var temp = new byte[8192];
            long total = 0;
            int read;
            while ((read = decoder.Read(temp, 0, temp.Length)) > 0)
            {
                total += read;

                if (total > MAX_OUT) throw new InvalidDataException("解压结果超过32MB限制");

                output.Write(temp, 0, read);
            }

            return output.ToArray();
        }
        #endregion

        private static readonly List<Action<long, RepeatedField<Attr>, byte[]>?> ProcessSyncNearEntitiesMethods = new()
        {
            null,
            ProcessEnemyAttrs, // EEntityType.EntMonster(1)
            null, null, null, null, null, null, null, null,
            ProcessPlayerAttrs // EEntityType.EntChar(10)
        };

        /// <summary>
        /// 同步周边实体 怪物和玩家
        /// </summary>
        public static void ProcessSyncNearEntities(byte[] payloadBuffer, bool b)
        {
            var syncNearEntities = WorldNtf.Types.SyncNearEntities.Parser.ParseFrom(payloadBuffer);
            if (syncNearEntities.Appear == null || syncNearEntities.Appear.Count == 0) return;

            foreach (var entity in syncNearEntities.Appear)
            {
                if (entity.EntType != EEntityType.EntChar) continue;

                // 提取UID
                var playerUid = entity.Uuid.ShiftRight16();
                if (playerUid == 0) continue;

                var attrCollection = entity.Attrs;
                if (attrCollection?.Attrs == null) continue;

                if ((int)entity.EntType < 0 || (int)entity.EntType >= ProcessSyncNearEntitiesMethods.Count) continue;
                var processSyncNearEntitiesMethod = ProcessSyncNearEntitiesMethods[(int)entity.EntType];
                processSyncNearEntitiesMethod?.Invoke(playerUid, attrCollection.Attrs, payloadBuffer);
            }
        }


        public static byte[] ModulePayloadBuffer { get; set; } = [];
        /// <summary>
        /// 同步自身完整容器数据（基础属性、昵称、职业、战力）
        /// </summary>
        public static void ProcessSyncContainerData(byte[] payloadBuffer, bool b)
        {
            // TODO: 模组分析模块待分离
            ModulePayloadBuffer = payloadBuffer;

            //Console.WriteLine("Head (前64字节): " + ToHex(payloadBuffer));
            var syncContainerData = Zproto.WorldNtf.Types.SyncContainerData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerData?.VData == null) return;
            var vData = syncContainerData.VData;
            if (vData.CharId == null || vData.CharId == 0) return;

            var playerUid = vData.CharId;
            DataStorage.CurrentPlayerInfo.UID = playerUid;
            DataStorage.TestCreatePlayerInfoByUID(playerUid);

            var tmpLevel = vData.RoleLevel?.Level ?? 0;
            if (tmpLevel != 0)
            {
                DataStorage.CurrentPlayerInfo.Level = tmpLevel;
                DataStorage.SetPlayerLevel(playerUid, tmpLevel);
            }

            var tmpHP = vData.Attr?.CurHp ?? 0;
            if (tmpHP != 0)
            {
                DataStorage.CurrentPlayerInfo.HP = tmpHP;
                DataStorage.SetPlayerHP(playerUid, tmpHP);
            }

            var tmpMaxHP = vData.Attr?.MaxHp ?? 0;
            if (tmpMaxHP != 0)
            {
                DataStorage.CurrentPlayerInfo.MaxHP = tmpMaxHP;
                DataStorage.SetPlayerMaxHP(playerUid, tmpMaxHP);
            }

            if (vData.CharBase != null)
            {
                if (!string.IsNullOrEmpty(vData.CharBase.Name))
                {
                    DataStorage.CurrentPlayerInfo.Name = vData.CharBase.Name;
                    DataStorage.SetPlayerName(playerUid, vData.CharBase.Name);
                }

                if (vData.CharBase.FightPoint != 0)
                {
                    DataStorage.CurrentPlayerInfo.CombatPower = vData.CharBase.FightPoint;
                    DataStorage.SetPlayerCombatPower(playerUid, vData.CharBase.FightPoint);
                }
            }

            var professionList = vData.ProfessionList;
            if (professionList != null && professionList.CurProfessionId != 0)
            {
                DataStorage.CurrentPlayerInfo.ProfessionID = professionList.CurProfessionId;
                DataStorage.SetPlayerProfessionID(playerUid, professionList.CurProfessionId);
            }
        }


        /// <summary>
        /// 同步自身部分更新（脏数据） //增量更新，有数据就更新
        /// </summary>
        public static void ProcessSyncContainerDirtyData(byte[] payloadBuffer, bool b)
        {
            try
            {
                if (DataStorage.CurrentPlayerUUID == 0) return;
                //var dirty = Zproto.WorldNtf.Types.SyncContainerDirtyData.Parser.ParseFrom(payloadBuffer);
                var dirty = Zproto.WorldNtf.Types.SyncContainerDirtyData.Parser.ParseFrom(payloadBuffer);
                if (dirty?.VData?.Buffer == null || dirty.VData.Buffer.Length == 0) return;

                var buf = dirty.VData.Buffer.ToByteArray();
                using var ms = new MemoryStream(buf, writable: false);
                using var br = new BinaryReader(ms);

                if (!DoesStreamHaveIdentifier(br)) return;

                var fieldIndex = br.ReadUInt32();
                _ = br.ReadInt32();

                var playerUid = DataStorage.CurrentPlayerUUID.ShiftRight16();

                DataStorage.TestCreatePlayerInfoByUID(playerUid);

                switch (fieldIndex)
                {
                    // 名字和战力
                    case 2:
                        if (!DoesStreamHaveIdentifier(br)) break;
                        fieldIndex = br.ReadUInt32();
                        _ = br.ReadInt32();
                        switch (fieldIndex)
                        {
                            // 名字
                            case 5:
                                var playerName = StreamReadString(br);
                                if (!string.IsNullOrEmpty(playerName))
                                {
                                    DataStorage.CurrentPlayerInfo.Name = playerName;
                                    DataStorage.SetPlayerName(playerUid, playerName);
                                }

                                break;

                            // 战力
                            case 35:
                                var fightPoint = (int)br.ReadUInt32();
                                _ = br.ReadInt32();
                                if (fightPoint != 0)
                                {
                                    DataStorage.CurrentPlayerInfo.CombatPower = fightPoint;
                                    DataStorage.SetPlayerCombatPower(playerUid, fightPoint);
                                }

                                break;

                        }

                        break;

                    // HP
                    case 16:
                        if (!DoesStreamHaveIdentifier(br)) break;
                        fieldIndex = br.ReadUInt32();
                        _ = br.ReadInt32();
                        switch (fieldIndex)
                        {
                            // 当前血量
                            case 1:
                                var curHp = (int)br.ReadUInt32();
                                DataStorage.CurrentPlayerInfo.HP = curHp;
                                DataStorage.SetPlayerHP(playerUid, curHp);
                                break;

                            // 最大血量
                            case 2:
                                var maxHp = (int)br.ReadUInt32();
                                DataStorage.CurrentPlayerInfo.MaxHP = maxHp;
                                DataStorage.SetPlayerMaxHP(playerUid, maxHp);
                                break;

                        }

                        break;

                    // 职业
                    case 61:
                        if (!DoesStreamHaveIdentifier(br)) break;
                        fieldIndex = br.ReadUInt32();
                        _ = br.ReadInt32();
                        if (fieldIndex == 1)
                        {
                            var curProfessionId = (int)br.ReadUInt32();
                            _ = br.ReadInt32();
                            if (curProfessionId != 0)
                            {
                                DataStorage.CurrentPlayerInfo.ProfessionID = curProfessionId;
                                DataStorage.SetPlayerProfessionID(playerUid, curProfessionId);
                            }
                        }

                        break;

                }


            }
            catch
            {
                // ignore
            }
        }


        /// <summary>
        /// 判断数据流是否还有标识符
        /// </summary>
        private static bool DoesStreamHaveIdentifier(BinaryReader br)
        {
            var s = br.BaseStream;

            // 先保证至少能读前 8 字节（uint32 + int32）
            if (s.Position + 8 > s.Length) return false;

            uint id1 = br.ReadUInt32();  // 期望 0xFFFFFFFE
            _ = br.ReadInt32(); // 跟随占位/长度（无论如何都消耗）

            if (id1 != 0xFFFFFFFE)
            {
                // 与 JS 一样：首段不对就返回 false（此时已前进 8 字节）
                return false;
            }

            // 通过第一段校验后，再读后续 8 字节
            if (s.Position + 8 > s.Length) return false;

            _ = br.ReadInt32();    // 理想情况下是 0xFFFFFFFD（即 -3）
            _ = br.ReadInt32(); // 占位/保留

            // JS 代码并未强制校验 id2，所以这里直接返回 true
            return true;
        }

        /// <summary>
        /// 同步自身增量伤害
        /// </summary>
        public static void ProcessSyncToMeDeltaInfo(byte[] payloadBuffer, bool b)
        {
            var syncToMeDeltaInfo = WorldNtf.Types.SyncToMeDeltaInfo.Parser.ParseFrom(payloadBuffer);
            var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
            var uuid = aoiSyncToMeDelta.Uuid;
            if (uuid != 0 && DataStorage.CurrentPlayerUUID != uuid)
            {
                DataStorage.CurrentPlayerUUID = uuid;
            }

            var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;
            if (aoiSyncDelta == null) return;

            ProcessAoiSyncDelta(aoiSyncDelta);
        }


        /// <summary>
        /// 同步周边增量伤害（范围内其他角色的技能/伤害）
        /// </summary>
        public static void ProcessSyncNearDeltaInfo(byte[] payloadBuffer, bool b)
        {
            var syncNearDeltaInfo = WorldNtf.Types.SyncNearDeltaInfo.Parser.ParseFrom(payloadBuffer);
            if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0) return;

            foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos) ProcessAoiSyncDelta(aoiSyncDelta);
        }


        /// <summary>
        /// 处理一条技能伤害/治疗记录
        /// </summary>
        public static void ProcessAoiSyncDelta(WorldNtf.Types.AoiSyncDelta delta)
        {
            if (delta == null) return;

            var targetUuidRaw = delta.Uuid;
            if (targetUuidRaw == 0) return;

            var isTargetPlayer = targetUuidRaw.IsUuidPlayerRaw();
            var targetUuid = targetUuidRaw.ShiftRight16();
            var attrCollection = delta.Attrs;
            if (attrCollection?.Attrs != null)
            {
                if (isTargetPlayer)
                {
                    //玩家
                    ProcessPlayerAttrs(targetUuidRaw, attrCollection.Attrs, []);
                }
                else
                {
                    //怪物
                    ProcessEnemyAttrs(targetUuid, attrCollection.Attrs, []);
                }
            }

            // SkillEffects：本次增量包含的技能相关效果（伤害/治疗等）
            var skillEffect = delta.SkillEffects;
            if (skillEffect?.Damages == null || skillEffect.Damages.Count == 0) return;

            foreach (var d in skillEffect.Damages)
            {
                var skillId = d.OwnerId;
                if (skillId == 0) continue;

                var attackerRaw = d.TopSummonerId != 0 ? d.TopSummonerId : d.AttackerUuid;
                if (attackerRaw == 0) continue;
                var isAttackerPlayer = attackerRaw.IsUuidPlayerRaw();
                var attackerUuid = attackerRaw.ShiftRight16();

                //// 这个判断里的 info 也没用到啊?
                //// 检查是否缺少基本信息，如果缺少则尝试补充
                //if (isAttackerPlayer && attackerUuid != 0)
                //{
                //    var info = StatisticData._manager.GetPlayerBasicInfo(attackerUuid);
                //}

                // 伤害数值
                var damageSigned = d.HasValue ? d.Value : (d.HasLuckyValue ? d.LuckyValue : 0L);
                if (damageSigned == 0) continue;

                var damage = Math.Abs(damageSigned);

                // 标志位
                var isCrit = d.TypeFlag != null && ((d.TypeFlag & 1) == 1);
                var isHeal = d.Type == EDamageType.Heal;
                var luckyValue = d.LuckyValue;
                var isLucky = luckyValue != null && luckyValue != 0;
                var hpLessen = d.HasHpLessenValue ? d.HpLessenValue : 0L;

                // 1) 是否“造成”幸运（CauseLucky）：TypeFlag 的 bit2
                var isCauseLucky = d.TypeFlag != null && ((d.TypeFlag & 0b100) == 0b100);

                // 2) 是否 Miss
                var isMiss = d.HasIsMiss && d.IsMiss;

                // 3) 是否打死/目标死亡
                var isDead = d.HasIsDead && d.IsDead;

                // 4) 元素标签（把 d.Property 转你现有的标签字符串）
                var damageEleType = (int)d.Property;
                var damageElementStr = EDamagePropertyExtends.GetDamageElement((int)d.Property);

                // 5) 伤害来源（EDamageSource）
                int damageSource = (int)(d.HasDamageSource ? d.DamageSource : 0);

                (var id, var ticks) = IDGenerator.Next();
                DataStorage.AddBattleLog(new()
                {
                    PacketID = id,
                    TimeTicks = ticks,
                    SkillID = skillId,
                    AttackerUuid = attackerUuid,
                    TargetUuid = targetUuid,
                    Value = damageSigned,
                    ValueElementType = damageEleType,
                    DamageSourceType = damageSource,
                    IsAttackerPlayer = isAttackerPlayer,
                    IsTargetPlayer = isTargetPlayer,
                    IsLucky = isLucky,
                    IsCritical = isCrit,
                    IsHeal = isHeal,
                    IsMiss = isMiss,
                    IsDead = isDead,
                });

            }
        }

        /// <summary>
        /// 处理 FrameDown 消息（嵌套内部数据包）
        /// </summary>
        public static void ProcessFrameDown(ByteReader reader, bool isZstdCompressed, ILogger? logger = null)
        {
            _ = reader.ReadUInt32BE(); // serverSequenceId
            if (reader.Remaining == 0) return;

            var nestedPacket = reader.ReadRemaining();
            if (isZstdCompressed) nestedPacket = DecompressZstdIfNeeded(nestedPacket);
            Process(nestedPacket, logger); // 递归解析内部消息
        }
        /// <summary>
        /// 同步周边实体，玩家数据
        /// </summary>
        /// <param name="playerUid"></param>
        /// <param name="attrs"></param>
        public static void ProcessPlayerAttrs(long playerUid, RepeatedField<Attr> attrs, byte[] payload)
        {
            DataStorage.TestCreatePlayerInfoByUID(playerUid);

            foreach (var attr in attrs)
            {
                if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;
                var reader = new CodedInputStream(attr.RawData.ToByteArray());

                switch (attr.Id)
                {
                    case (int)AttrType.AttrName:
                        var playerName = reader.ReadString();
                        DataStorage.SetPlayerName(playerUid, playerName);
                        Debug.WriteLine($"SyncNearEntitiesV1: SetPlayerName:{playerUid}@{playerName}");
                        break;
                    case (int)AttrType.AttrProfessionId:
                        DataStorage.SetPlayerProfessionID(playerUid, reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrFightPoint:
                        DataStorage.SetPlayerCombatPower(playerUid, reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrLevel:
                        DataStorage.SetPlayerLevel(playerUid, reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrRankLevel:
                        DataStorage.SetPlayerRankLevel(playerUid, reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrCri:
                        DataStorage.SetPlayerCritical(playerUid, reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrLucky:
                        DataStorage.SetPlayerLucky(playerUid, reader.ReadInt32());
                        break;
                    case (int)AttrType.AttrHp:
                        DataStorage.SetPlayerHP(playerUid, reader.ReadInt32());
                        break;

                    case (int)AttrType.AttrMaxHp:
                        _ = reader.ReadInt32();
                        break;
                    case (int)AttrType.AttrElementFlag:
                        _ = reader.ReadInt32();
                        break;
                    case (int)AttrType.AttrEnergyFlag:
                        _ = reader.ReadInt32();
                        break;
                    case (int)AttrType.AttrReductionLevel:
                        _ = reader.ReadInt32();
                        break;
                    default:
                        break;
                }
            }


        }

        public static void ProcessEnemyAttrs(long enemyUid, RepeatedField<Attr> attrs, byte[] arg3)
        {
            #region
            //foreach (var attr in attrs)
            //{
            //    if (attr.Id == 0 || attr.RawData == null)
            //        continue;
            //    var reader = new Google.Protobuf.CodedInputStream(attr.RawData.ToByteArray());

            //    // Console.WriteLine(@$"发现属性ID {attr.Id} 对应敌人E{enemyUid} 原始数据={Convert.ToBase64String(attr.RawData.ToByteArray())}");
            //    switch (attr.Id)
            //    {
            //        case (int)AttrType.AttrName:

            //            // 怪物名直接是 string
            //            string enemyName = reader.ReadString();

            //            Console.WriteLine($"发现怪物名 {enemyName}，对应ID {enemyUid}");
            //            break;

            //        case (int)AttrType.AttrId:

            //            // 怪物模板 ID
            //            int templateId = reader.ReadInt32();
            //            string name = MonsterNameResolver.Instance.GetName(templateId);
            //            if (!string.IsNullOrEmpty(name))
            //            {
            //                Console.WriteLine($"怪物名：{name}，对应模板ID {templateId}");
            //            }

            //            break;

            //        case (int)AttrType.AttrHp:

            //            var data = attr.RawData.ToByteArray();
            //            if (data.Length == 0)
            //            {
            //                //Console.WriteLine($"怪物 {enemyUid} 的血量数据为空，跳过");
            //                break;
            //            }
            //            int enemyHp = reader.ReadInt32();

            //            //Console.WriteLine($"发现怪物当前血量 {enemyHp}，对应敌人ID {enemyUid}"); 
            //            break;

            //        case (int)AttrType.AttrMaxHp:

            //            int enemyMaxHp = reader.ReadInt32();

            //            Console.WriteLine($"发现怪物最大血量 {enemyMaxHp}，对应敌人ID {enemyUid}");
            //            break;

            //        default:

            //            // 未知属性静默，可选 debug
            //            // this.logger.Debug($"Found unknown attrId {attr.Id} for E{enemyUid} {Convert.ToBase64String(attr.RawData)}");
            //            break;

            //    }
            //}
            #endregion
        }






        /// <summary>
        /// 从流中读取字符串（带4字节对齐）
        /// </summary>
        private static string StreamReadString(BinaryReader br)
        {
            uint length = br.ReadUInt32();  // uint32LE
            _ = br.ReadInt32();             // guard（占位/长度，无论如何都消耗）

            // 即使 length 为 0，也要读后置 guard，和 JS 行为保持一致
            byte[] bytes = length > 0 ? br.ReadBytes((int)length) : Array.Empty<byte>();

            _ = br.ReadInt32();             // guard（占位/保留）

            return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }

    }
}
