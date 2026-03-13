#define NEW_MESSAGE_HANDLER

using System.Diagnostics;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.BlueProto;
using StarResonanceDpsAnalysis.Core.Extends.Data;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Tools;
using Zproto;

namespace StarResonanceDpsAnalysis.Core.Analyze.V1
{
    /// <summary>
    /// 消息解析器
    /// 负责处理从游戏抓包获得的TCP数据，包括解压缩、Protobuf解析、数据同步、伤害统计等。
    /// </summary>
    public class MessageAnalyzer : IMessageAnalyzer
    {
        private readonly ILogger<MessageAnalyzer>? _logger;

        /// <summary>
        /// 消息解析器
        /// 负责处理从游戏抓包获得的TCP数据，包括解压缩、Protobuf解析、数据同步、伤害统计等。
        /// </summary>
        public MessageAnalyzer(IDataStorage dataStorage, ILogger<MessageAnalyzer>? logger = null)
        {
            _logger = logger;
            _processMethods = new Dictionary<WorldNtfMessageId, Action<byte[]>>
            {
                { WorldNtfMessageId.SyncNearEntities, ProcessSyncNearEntities },        // 同步周边玩家实体
                { WorldNtfMessageId.SyncContainerData, ProcessSyncContainerData },       // 同步自身完整容器数据
                { WorldNtfMessageId.SyncContainerDirtyData, ProcessSyncContainerDirtyData },  // 同步自身部分更新（脏数据）
                { WorldNtfMessageId.SyncToMeDeltaInfo, ProcessSyncToMeDeltaInfo },       // 同步自己受到的增量伤害
                { WorldNtfMessageId.SyncNearDeltaInfo, ProcessSyncNearDeltaInfo }        // 同步周边增量伤害
            };
            _processSyncNearEntitiesMethods = new Dictionary<EEntityType, Action<long, RepeatedField<Attr>, byte[]>?>
            {
                { EEntityType.EntMonster,ProcessEnemyAttrs },// EEntityType.EntMonster(1)
                { EEntityType.EntChar,ProcessPlayerAttrs } // EEntityType.EntChar(10)
            };
            _messageHandlerMap = new Dictionary<MessageType, Action<ByteReader, bool>>
            {
                { MessageType.Notify, ProcessNotifyMsg },
                { MessageType.FrameDown, ProcessFrameDown }
            };
            _messageHandlerReg = new WorldNtfMessageHandlerRegistry(dataStorage, logger);
        }

        /// <summary>
        /// 顶层消息类型处理器
        /// Key = 消息类型ID (低15位)
        /// Value = 对应的解析方法
        /// </summary>
        private readonly Dictionary<MessageType, Action<ByteReader, bool>> _messageHandlerMap;
        private readonly Dictionary<WorldNtfMessageId, Action<byte[]>> _processMethods;
        private readonly WorldNtfMessageHandlerRegistry _messageHandlerReg;

        /// <summary>
        /// 主入口：处理一批TCP数据包
        /// </summary>
        public void Process(byte[] packets)
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

                if (!_messageHandlerMap.TryGetValue((MessageType)msgTypeId, out var handler)) continue;
                handler.Invoke(packetReader, isZstdCompressed);
            }
        }

        /// <summary>
        /// 处理 Notify 消息（带 serviceUuid 和 methodId 的 RPC）
        /// </summary>
        private void ProcessNotifyMsg(ByteReader packet, bool isZstdCompressed)
        {
            var serviceUuid = (ServiceIds)packet.ReadUInt64BE();
            //Debug.Assert(Enum.IsDefined(serviceUuid));
            _ = packet.ReadUInt32BE();
            var methodId = (WorldNtfMessageId)packet.ReadUInt32BE();
            //Debug.Assert(Enum.IsDefined(methodId));

            if (serviceUuid != ServiceIds.WorldNtf) return;

            byte[] msgPayload = packet.ReadRemaining();
            if (isZstdCompressed) msgPayload = msgPayload.DecompressZstdIfNeeded();

#if NEW_MESSAGE_HANDLER
            if (!_messageHandlerReg.TryGetProcessor(methodId, out var processor)) return;
            processor.Process(msgPayload);
#else
            if (!_processMethods.TryGetValue(methodId, out var processMethod)) return;
            processMethod(msgPayload);
#endif
        }

        /// <summary>
        /// 处理 FrameDown 消息（嵌套内部数据包）
        /// </summary>
        private void ProcessFrameDown(ByteReader reader, bool isZstdCompressed)
        {
            _ = reader.ReadUInt32BE();
            if (reader.Remaining == 0) return;

            var nestedPacket = reader.ReadRemaining();
            if (isZstdCompressed) nestedPacket = nestedPacket.DecompressZstdIfNeeded();
            Process(nestedPacket);
        }

        private readonly Dictionary<EEntityType, Action<long, RepeatedField<Attr>, byte[]>?> _processSyncNearEntitiesMethods;

        /// <summary>
        /// 同步周边实体 怪物和玩家
        /// </summary>
        private void ProcessSyncNearEntities(byte[] payloadBuffer)
        {
            var syncNearEntities = WorldNtf.Types.SyncNearEntities.Parser.ParseFrom(payloadBuffer);
            if (syncNearEntities.Appear == null || syncNearEntities.Appear.Count == 0) return;

            foreach (var entity in syncNearEntities.Appear)
            {
                // プレイヤーもモンスターも、対応ハンドラがあるなら処理する
                var ret = _processSyncNearEntitiesMethods.TryGetValue(entity.EntType, out var processMethod);
                if (!ret) continue;
                if (processMethod == null) continue;

                // 提取UID
                var entityUid = entity.Uuid.ShiftRight16();
                if (entityUid == 0) continue;

                var attrCollection = entity.Attrs;
                if (attrCollection?.Attrs == null) continue;

                processMethod.Invoke(entityUid, attrCollection.Attrs, payloadBuffer);
            }
        }

        /// <summary>
        /// 同步自身完整容器数据（基础属性、昵称、职业、战力）
        /// </summary>
        private void ProcessSyncContainerData(byte[] payloadBuffer)
        {
            var syncContainerData = Zproto.WorldNtf.Types.SyncContainerData.Parser.ParseFrom(payloadBuffer);
            if (syncContainerData?.VData == null) return;
            var vData = syncContainerData.VData;
            if (vData.CharId == null || vData.CharId == 0) return;

            var playerUid = vData.CharId;

            // ✅ 统一通过 setter 设置当前玩家UID，而不是直接赋值
            DataStorage.Instance.SetCurrentPlayerUid(playerUid);
            DataStorage.Instance.EnsurePlayer(playerUid);

            var tmpLevel = vData.RoleLevel?.Level ?? 0;
            if (tmpLevel != 0)
            {
                DataStorage.Instance.CurrentPlayerInfo.Level = tmpLevel;
                DataStorage.Instance.SetPlayerLevel(playerUid, tmpLevel);
            }

            var tmpHP = vData.Attr?.CurHp ?? 0;
            if (tmpHP != 0)
            {
                DataStorage.Instance.CurrentPlayerInfo.HP = tmpHP;
                DataStorage.Instance.SetPlayerHP(playerUid, tmpHP);
            }

            var tmpMaxHP = vData.Attr?.MaxHp ?? 0;
            if (tmpMaxHP != 0)
            {
                DataStorage.Instance.CurrentPlayerInfo.MaxHP = tmpMaxHP;
                DataStorage.Instance.SetPlayerMaxHP(playerUid, tmpMaxHP);
            }

            if (vData.CharBase != null)
            {
                if (!string.IsNullOrEmpty(vData.CharBase.Name))
                {
                    DataStorage.Instance.CurrentPlayerInfo.Name = vData.CharBase.Name;
                    DataStorage.Instance.SetPlayerName(playerUid, vData.CharBase.Name);
                }

                if (vData.CharBase.FightPoint != 0)
                {
                    DataStorage.Instance.CurrentPlayerInfo.CombatPower = vData.CharBase.FightPoint;
                    DataStorage.Instance.SetPlayerCombatPower(playerUid, vData.CharBase.FightPoint);
                }
            }

            var professionList = vData.ProfessionList;
            if (professionList != null && professionList.CurProfessionId != 0)
            {
                DataStorage.Instance.CurrentPlayerInfo.ProfessionID = professionList.CurProfessionId;
                DataStorage.Instance.SetPlayerProfessionID(playerUid, professionList.CurProfessionId);
            }
        }

        /// <summary>
        /// 同步自身部分更新（脏数据） //增量更新，有数据就更新
        /// </summary>
        private void ProcessSyncContainerDirtyData(byte[] payloadBuffer)
        {
            try
            {
                var playerUid = DataStorage.Instance.CurrentPlayerInfo.UID;
                if (playerUid == 0) return;
                DataStorage.Instance.EnsurePlayer(playerUid);

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
                                    DataStorage.Instance.CurrentPlayerInfo.Name = playerName;
                                    DataStorage.Instance.SetPlayerName(playerUid, playerName);
                                }
                                break;

                            case 35:
                                var fightPoint = (int)br.ReadUInt32();
                                _ = br.ReadInt32();
                                if (fightPoint != 0)
                                {
                                    DataStorage.Instance.CurrentPlayerInfo.CombatPower = fightPoint;
                                    DataStorage.Instance.SetPlayerCombatPower(playerUid, fightPoint);
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
                                DataStorage.Instance.CurrentPlayerInfo.HP = curHp;
                                DataStorage.Instance.SetPlayerHP(playerUid, curHp);
                                break;

                            case 2:
                                var maxHp = (int)br.ReadUInt32();
                                DataStorage.Instance.CurrentPlayerInfo.MaxHP = maxHp;
                                DataStorage.Instance.SetPlayerMaxHP(playerUid, maxHp);
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
                                DataStorage.Instance.CurrentPlayerInfo.ProfessionID = curProfessionId;
                                DataStorage.Instance.SetPlayerProfessionID(playerUid, curProfessionId);
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
        private bool DoesStreamHaveIdentifier(BinaryReader br)
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
        public void ProcessSyncToMeDeltaInfo(byte[] payloadBuffer)
        {
            var syncToMeDeltaInfo = WorldNtf.Types.SyncToMeDeltaInfo.Parser.ParseFrom(payloadBuffer);
            var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
            var uuid = aoiSyncToMeDelta.Uuid.ShiftRight16();
            if (uuid != 0)
            {
                // ✅ 统一通过 setter 设置当前玩家UID，而不是直接赋值
                DataStorage.Instance.SetCurrentPlayerUid(uuid);
            }

            var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;
            if (aoiSyncDelta == null) return;

            ProcessAoiSyncDelta(aoiSyncDelta);
        }

        /// <summary>
        /// 同步周边增量伤害（范围内其他角色的技能/伤害）
        /// </summary>
        public void ProcessSyncNearDeltaInfo(byte[] payloadBuffer)
        {
            try
            {
                var syncNearDeltaInfo = WorldNtf.Types.SyncNearDeltaInfo.Parser.ParseFrom(payloadBuffer);
                if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0) return;

                foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos) ProcessAoiSyncDelta(aoiSyncDelta);
            }
            catch (InvalidProtocolBufferException ex)
            {
                _logger?.LogWarning(ex, "Failed to parse SyncNearDeltaInfo payload");
            }
        }

        /// <summary>
        /// 处理一条技能伤害/治疗记录
        /// </summary>
        private void ProcessAoiSyncDelta(WorldNtf.Types.AoiSyncDelta? delta)
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
                Debug.Assert(d != null);
                var skillId = d.OwnerId;
                if (skillId == 0) continue;

                var attackerRaw = d.TopSummonerId != 0 ? d.TopSummonerId : d.AttackerUuid;
                if (attackerRaw == 0) continue;
                var isAttackerPlayer = attackerRaw.IsUuidPlayerRaw();
                var attackerUuid = attackerRaw.ShiftRight16();

                var damageSigned = d.HasValue ? d.Value : (d.HasLuckyValue ? d.LuckyValue : 0L);
                if (damageSigned == 0) continue;

                var isCrit = (d.TypeFlag & 1) == 1;
                var isHeal = d.Type == EDamageType.Heal;
                var isLucky = d.LuckyValue != 0;

                var isMiss = d is { HasIsMiss: true, IsMiss: true };
                var isDead = d is { HasIsDead: true, IsDead: true };

                var damageEleType = (int)d.Property;
                var damageSource = (int)(d.HasDamageSource ? d.DamageSource : 0);

                var (id, ticks) = IDGenerator.Next();
                DataStorage.Instance.AddBattleLog(new BattleLog
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
        /// 同步周边实体，玩家数据
        /// </summary>
        public void ProcessPlayerAttrs(long playerUid, RepeatedField<Attr> attrs, byte[] payload)
        {
            DataStorage.Instance.EnsurePlayer(playerUid);

            foreach (var attr in attrs)
            {
                if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;
                var data = attr.RawData.ToByteArray();
                var reader = new CodedInputStream(data);

                switch ((EAttrType)attr.Id)
                {
                    case EAttrType.AttrName:
                        var playerName = reader.ReadString();
                        DataStorage.Instance.SetPlayerName(playerUid, playerName);
                        Debug.WriteLine($"SyncNearEntitiesV1: SetPlayerName:{playerUid}@{playerName}");
                        break;
                    case EAttrType.AttrProfessionId:
                        DataStorage.Instance.SetPlayerProfessionID(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrFightPoint:
                        DataStorage.Instance.SetPlayerCombatPower(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrLevel:
                        DataStorage.Instance.SetPlayerLevel(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrRankLevel:
                        DataStorage.Instance.SetPlayerRankLevel(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrCri:
                        DataStorage.Instance.SetPlayerCritical(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrLuck:
                        DataStorage.Instance.SetPlayerLucky(playerUid, reader.ReadInt32());
                        break;
                    case EAttrType.AttrHp:
                        DataStorage.Instance.SetPlayerHP(playerUid, reader.ReadInt32());
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
                        DataStorage.Instance.SetPlayerSeasonStrength(playerUid, strength);
                        break;
                    case EAttrType.AttrSeasonLevel:
                        var level = reader.ReadInt32();
                        DataStorage.Instance.SetPlayerSeasonLevel(playerUid, level);
                        break;
                    case EAttrType.AttrCombatState:
                        var state = reader.ReadBool();
                        Debug.WriteLine($"CombatState:[{BitConverter.ToString(data)}]");
                        DataStorage.Instance.SetPlayerCombatState(playerUid, state);
                        DataStorage.Instance.SetPlayerCombatStateTime(playerUid, DateTime.UtcNow.Ticks);
                        break;
                    case EAttrType.AttrCombatStateTime:
                        var time = reader.ReadInt64();
                        Debug.WriteLine($"CombatStateTime:[{BitConverter.ToString(data)}].[{time}].[{TimeSpan.FromTicks(time):c}");
                        if (DataStorage.Instance.ReadOnlyPlayerInfoDatas.TryGetValue(playerUid, out var info))
                        {
                            var tick = info.CombatStateTime;
                            var tickDiff = DateTime.UtcNow.Ticks - tick;
                            var ts = TimeSpan.FromTicks(tickDiff);
                            if (ts > TimeSpan.FromSeconds(1))
                            {
                                DataStorage.Instance.SetPlayerCombatState(playerUid, false);
                            }
                        }

                        DataStorage.Instance.SetPlayerCombatStateTime(playerUid, DateTime.UtcNow.Ticks);
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

        public void ProcessEnemyAttrs(long enemyUid, RepeatedField<Attr> attrs, byte[] arg3)
        {
            if (attrs.Count == 0) return;

            DataStorage.Instance.EnsurePlayer(enemyUid);

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
                            DataStorage.Instance.SetNpcTemplateId(enemyUid, templateId);
                            break;

                        case EAttrType.AttrHp:
                            var enemyHp = reader.ReadInt32();
                            DataStorage.Instance.SetPlayerHP(enemyUid, enemyHp);
                            break;

                        case EAttrType.AttrMaxHp:
                            var enemyMaxHp = reader.ReadInt32();
                            DataStorage.Instance.SetPlayerMaxHP(enemyUid, enemyMaxHp);
                            break;

                        case EAttrType.AttrMonsterSeasonLevel:
                            var seasonLv = reader.ReadInt32();
                            DataStorage.Instance.SetPlayerSeasonLevel(enemyUid, seasonLv);
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
            var length = br.ReadUInt32();
            _ = br.ReadInt32();

            var bytes = length > 0 ? br.ReadBytes((int)length) : null;

            _ = br.ReadInt32();

            return bytes?.Length != 0 ? Encoding.UTF8.GetString(bytes!) : string.Empty;
        }
    }
}