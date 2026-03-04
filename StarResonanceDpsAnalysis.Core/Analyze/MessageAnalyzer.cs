using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
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
                if (packetSize < 6) break;
                if (packetSize > packetsReader.Remaining) break;

                // 按长度截取出一个完整包
                var packetReader = new ByteReader(packetsReader.ReadBytes((int)packetSize));
                uint sizeAgain = packetReader.ReadUInt32BE();
                if (sizeAgain != packetSize) continue;

                // 读取消息类型
                var packetType = packetReader.ReadUInt16BE();
                var isZstdCompressed = (packetType & 0x8000) != 0;
                var msgTypeId = packetType & 0x7FFF;

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
            var serviceUuid = packet.ReadUInt64BE();
            _ = packet.ReadUInt32BE();
            var methodId = packet.ReadUInt32BE();

            if (serviceUuid != 0x0000000063335342UL) return;

            byte[] msgPayload = packet.ReadRemaining();
            if (isZstdCompressed) msgPayload = DecompressZstdIfNeeded(msgPayload);

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

            const long MAX_OUT = 32L * 1024 * 1024;
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
                // プレイヤーもモンスターも、対応ハンドラがあるなら処理する
                var entTypeIndex = (int)entity.EntType;
                if (entTypeIndex < 0 || entTypeIndex >= ProcessSyncNearEntitiesMethods.Count)
                    continue;

                var processMethod = ProcessSyncNearEntitiesMethods[entTypeIndex];
                if (processMethod == null)
                    continue;

                // 提取UID
                var entityUid = entity.Uuid.ShiftRight16();
                if (entityUid == 0) continue;

                var attrCollection = entity.Attrs;
                if (attrCollection?.Attrs == null) continue;

                processMethod.Invoke(entityUid, attrCollection.Attrs, payloadBuffer);
            }
        }

        public static byte[] ModulePayloadBuffer { get; set; } = [];

        /// <summary>
        /// 同步自身完整容器数据（基础属性、昵称、职业、战力）
        /// </summary>
        public static void ProcessSyncContainerData(byte[] payloadBuffer, bool b)
        {
            ModulePayloadBuffer = payloadBuffer;

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
                var playerUid = DataStorage.CurrentPlayerInfo.UID;
                if (playerUid == 0) return;
                DataStorage.TestCreatePlayerInfoByUID(playerUid);

                var dirty = Zproto.WorldNtf.Types.SyncContainerDirtyData.Parser.ParseFrom(payloadBuffer);
                if (dirty?.VData?.Buffer == null || dirty.VData.Buffer.Length == 0) return;

                var buf = dirty.VData.Buffer.ToByteArray();
                using var ms = new MemoryStream(buf, writable: false);
                using var br = new BinaryReader(ms);

                if (!DoesStreamHaveIdentifier(br)) return;

                var fieldIndex = br.ReadUInt32();
                _ = br.ReadInt32();

                switch (fieldIndex)
                {
                    case 2:
                        if (!DoesStreamHaveIdentifier(br)) break;
                        fieldIndex = br.ReadUInt32();
                        _ = br.ReadInt32();
                        switch (fieldIndex)
                        {
                            case 5:
                                var playerName = StreamReadString(br);
                                if (!string.IsNullOrEmpty(playerName))
                                {
                                    DataStorage.CurrentPlayerInfo.Name = playerName;
                                    DataStorage.SetPlayerName(playerUid, playerName);
                                }
                                break;

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

                    case 16:
                        if (!DoesStreamHaveIdentifier(br)) break;
                        fieldIndex = br.ReadUInt32();
                        _ = br.ReadInt32();
                        switch (fieldIndex)
                        {
                            case 1:
                                var curHp = (int)br.ReadUInt32();
                                DataStorage.CurrentPlayerInfo.HP = curHp;
                                DataStorage.SetPlayerHP(playerUid, curHp);
                                break;

                            case 2:
                                var maxHp = (int)br.ReadUInt32();
                                DataStorage.CurrentPlayerInfo.MaxHP = maxHp;
                                DataStorage.SetPlayerMaxHP(playerUid, maxHp);
                                break;
                        }
                        break;

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

            if (s.Position + 8 > s.Length) return false;

            uint id1 = br.ReadUInt32();
            _ = br.ReadInt32();

            if (id1 != 0xFFFFFFFE)
            {
                return false;
            }

            if (s.Position + 8 > s.Length) return false;

            _ = br.ReadInt32();
            _ = br.ReadInt32();

            return true;
        }

        /// <summary>
        /// 同步自身增量伤害
        /// </summary>
        public static void ProcessSyncToMeDeltaInfo(byte[] payloadBuffer, bool b)
        {
            var syncToMeDeltaInfo = WorldNtf.Types.SyncToMeDeltaInfo.Parser.ParseFrom(payloadBuffer);
            var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
            var uuid = aoiSyncToMeDelta.Uuid.ShiftRight16();
            if (uuid != 0 && DataStorage.CurrentPlayerInfo.UID != uuid)
            {
                DataStorage.CurrentPlayerInfo.UID = uuid;
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
            try
            {
                var syncNearDeltaInfo = WorldNtf.Types.SyncNearDeltaInfo.Parser.ParseFrom(payloadBuffer);
                if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0) return;

                foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos) ProcessAoiSyncDelta(aoiSyncDelta);
            }
            catch (InvalidProtocolBufferException)
            {
                // Ignore temporarily
            }
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
                    ProcessPlayerAttrs(targetUuid, attrCollection.Attrs, []);
                }
                else
                {
                    ProcessEnemyAttrs(targetUuid, attrCollection.Attrs, []);
                }
            }

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

                var damageSigned = d.HasValue ? d.Value : (d.HasLuckyValue ? d.LuckyValue : 0L);
                if (damageSigned == 0) continue;

                var isCrit = d.TypeFlag != null && ((d.TypeFlag & 1) == 1);
                var isHeal = d.Type == EDamageType.Heal;
                var luckyValue = d.LuckyValue;
                var isLucky = luckyValue != null && luckyValue != 0;

                var isMiss = d.HasIsMiss && d.IsMiss;
                var isDead = d.HasIsDead && d.IsDead;

                var damageEleType = (int)d.Property;
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
            _ = reader.ReadUInt32BE();
            if (reader.Remaining == 0) return;

            var nestedPacket = reader.ReadRemaining();
            if (isZstdCompressed) nestedPacket = DecompressZstdIfNeeded(nestedPacket);
            Process(nestedPacket, logger);
        }

        public static ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>
        /// 同步周边实体，玩家数据
        /// </summary>
        public static void ProcessPlayerAttrs(long playerUid, RepeatedField<Attr> attrs, byte[] payload)
        {
            DataStorage.TestCreatePlayerInfoByUID(playerUid);

            foreach (var attr in attrs)
            {
                if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;
                var data = attr.RawData.ToByteArray();
                var reader = new CodedInputStream(data);

                switch ((EAttrType)attr.Id)
                {
                    case EAttrType.AttrName:
                        var playerName = reader.ReadString();
                        DataStorage.SetPlayerName(playerUid, playerName);
                        Debug.WriteLine($"SyncNearEntitiesV1: SetPlayerName:{playerUid}@{playerName}");
                        break;
                    case EAttrType.AttrProfessionId:
                        DataStorage.SetPlayerProfessionID(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrFightPoint:
                        DataStorage.SetPlayerCombatPower(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrLevel:
                        DataStorage.SetPlayerLevel(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrRankLevel:
                        DataStorage.SetPlayerRankLevel(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrCri:
                        DataStorage.SetPlayerCritical(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrLuck:
                        DataStorage.SetPlayerLucky(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrHp:
                        DataStorage.SetPlayerHP(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrMaxHp:
                        _ = reader.ReadInt32();
                        break;
                    case EAttrType.AttrSeasonStrength:
                    case EAttrType.AttrSeasonStrengthTotal:
                    case EAttrType.AttrSeasonStrengthAdd:
                    case EAttrType.AttrSeasonStrengthExAdd:
                    case EAttrType.AttrSeasonStrengthPer:
                    case EAttrType.AttrSeasonStrengthExPer:
                        var strength = reader.ReadInt32();
                        DataStorage.SetPlayerSeasonStrength(playerUid, strength);
                        break;
                    case EAttrType.AttrSeasonLevel:
                        var level = reader.ReadInt32();
                        DataStorage.SetPlayerSeasonLevel(playerUid, level);
                        break;
                    case EAttrType.AttrCombatState:
                        var state = reader.ReadBool();
                        Debug.WriteLine($"CombatState:[{BitConverter.ToString(data)}]");
                        DataStorage.SetPlayerCombatState(playerUid, state);
                        DataStorage.SetPlayerCombatStateTime(playerUid, DateTime.UtcNow.Ticks);
                        break;
                    case EAttrType.AttrCombatStateTime:
                        var time = reader.ReadInt64();
                        Debug.WriteLine($"CombatStateTime:[{BitConverter.ToString(data)}].[{time}].[{TimeSpan.FromTicks(time):c}");
                        if (DataStorage.ReadOnlyPlayerInfoDatas.TryGetValue(playerUid, out var info))
                        {
                            var tick = info.CombatStateTime;
                            var tickDiff = DateTime.UtcNow.Ticks - tick;
                            var ts = TimeSpan.FromTicks(tickDiff);
                            if (ts > TimeSpan.FromSeconds(1))
                            {
                                DataStorage.SetPlayerCombatState(playerUid, false);
                            }
                        }

                        DataStorage.SetPlayerCombatStateTime(playerUid, DateTime.UtcNow.Ticks);
                        break;
                    case EAttrType.AttrCanIntoCombat:
                        Debug.WriteLine($"CanIntoCombat:{BitConverter.ToString(data)}");
                        break;
                    case EAttrType.AttrInBattleShow:
                        Debug.WriteLine($"InBattleShow:[{BitConverter.ToString(data)}]");
                        break;
                    default:
                        break;
                }
            }
        }

        public static void ProcessEnemyAttrs(long enemyUid, RepeatedField<Attr> attrs, byte[] arg3)
        {
            if (attrs.Count == 0) return;

            DataStorage.TestCreatePlayerInfoByUID(enemyUid);

            foreach (var attr in attrs)
            {
                if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;

                var rawBytes = attr.RawData.ToByteArray();
                var reader = new CodedInputStream(rawBytes);
                try
                {
                    switch ((EAttrType)attr.Id)
                    {
                        case EAttrType.AttrName:
                            // 受信された怪物名は採用しない。
                            // 表示名は WPF 側で JsonLocalizationProvider + NpcTemplateId により補正する。
                            // _ = reader.ReadString();
                            break;

                        case EAttrType.AttrId:
                            var templateId = reader.ReadInt32();
                            DataStorage.SetNpcTemplateId(enemyUid, templateId);
                            break;

                        case EAttrType.AttrHp:
                            var enemyHp = reader.ReadInt32();
                            DataStorage.SetPlayerHP(enemyUid, enemyHp);
                            break;

                        case EAttrType.AttrMaxHp:
                            var enemyMaxHp = reader.ReadInt32();
                            DataStorage.SetPlayerMaxHP(enemyUid, enemyMaxHp);
                            break;

                        case EAttrType.AttrMonsterSeasonLevel:
                            var seasonLv = reader.ReadInt32();
                            DataStorage.SetPlayerSeasonLevel(enemyUid, seasonLv);
                            break;
                    }
                }
                catch (InvalidProtocolBufferException)
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// 从流中读取字符串（带4字节对齐）
        /// </summary>
        private static string StreamReadString(BinaryReader br)
        {
            uint length = br.ReadUInt32();
            _ = br.ReadInt32();

            byte[] bytes = length > 0 ? br.ReadBytes((int)length) : Array.Empty<byte>();

            _ = br.ReadInt32();

            return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }
    }
}