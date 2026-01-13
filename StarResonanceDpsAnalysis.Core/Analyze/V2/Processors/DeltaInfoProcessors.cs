using BlueProto;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.BlueProto;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Logging;
using StarResonanceDpsAnalysis.Core.Tools;
using Google.Protobuf;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;

/// <summary>
/// Processes delta info messages for damage and healing events.
/// </summary>
public abstract class BaseDeltaInfoProcessor(IDataStorage storage, ILogger? logger) : IMessageProcessor
{
    protected readonly IDataStorage _storage = storage;
    protected readonly ILogger? _logger = logger;

    public abstract void Process(byte[] payload);

    protected void ProcessAoiSyncDelta(AoiSyncDelta? delta)
    {
        if (delta == null) return;

        var targetUuidRaw = delta.Uuid;
        if (targetUuidRaw == 0) return;

        var isTargetPlayer = targetUuidRaw.IsUuidPlayerRaw();
        var targetUuid = targetUuidRaw.ShiftRight16();

        var attrCollection = delta.Attrs;
        if (attrCollection?.Attrs != null && isTargetPlayer)
        {
            _storage.EnsurePlayer(targetUuid);

            foreach (var attr in attrCollection.Attrs)
            {
                if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;
                var reader = new CodedInputStream(attr.RawData.ToByteArray());

                var attrType = (AttrType)attr.Id;
                switch (attrType)
                {
                    case AttrType.AttrName:
                        _storage.SetPlayerName(targetUuid, reader.ReadString());
                        break;
                    case AttrType.AttrProfessionId:
                        _storage.SetPlayerProfessionID(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrFightPoint:
                        _storage.SetPlayerCombatPower(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrLevel:
                        _storage.SetPlayerLevel(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrRankLevel:
                        _storage.SetPlayerRankLevel(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrCri:
                        _storage.SetPlayerCritical(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrLucky:
                        _storage.SetPlayerLucky(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrHp:
                        _storage.SetPlayerHP(targetUuid, reader.ReadInt32());
                        break;

                    case (AttrType)0x2CB0: // AttrDreamIntensity
                        _logger?.LogWarning("[BaseDeltaInfoProcessor] Test for get AttrDreamIntensity: targetUuid[{targetUuid}], intensity[{value}]", targetUuid, reader.ReadInt32());
                        break;

                    case AttrType.AttrElementFlag:
                        _storage.SetPlayerElementFlag(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrReductionLevel:
                        _storage.SetPlayerReductionLevel(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrEnergyFlag:
                        _storage.SetPlayerEnergyFlag(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrMaxHp:
                        _storage.SetPlayerMaxHP(targetUuid, reader.ReadInt32());
                        break;
                    case AttrType.AttrId:
                    case AttrType.AttrReduntionId:
                        _ = reader.ReadInt32();
                        break;
                    default:
                        break;
                }
            }
        }

        var skillEffect = delta.SkillEffects;
        if (skillEffect?.Damages == null || skillEffect.Damages.Count == 0) return;

        var count = 0;
        var heals = 0;
        var crits = 0;

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

            var (id, ticks) = IDGenerator.Next();
            _storage.AddBattleLog(new BattleLog
            {
                PacketID = id,
                TimeTicks = ticks,
                SkillID = skillId,
                AttackerUuid = attackerUuid,
                TargetUuid = targetUuid,
                Value = damageSigned,
                ValueElementType = (int)d.Property,
                DamageSourceType = (int)(d.HasDamageSource ? d.DamageSource : 0),
                IsAttackerPlayer = isAttackerPlayer,
                IsTargetPlayer = isTargetPlayer,
                IsLucky = d.LuckyValue != 0,
                IsCritical = (d.TypeFlag & 1) == 1,
                IsHeal = d.Type == EDamageType.Heal,
                IsMiss = d.HasIsMiss && d.IsMiss,
                IsDead = d.HasIsDead && d.IsDead
            });
            count++;
            if ((d.TypeFlag & 1) == 1) crits++;
            if (d.Type == EDamageType.Heal) heals++;
        }

        if (count > 0)
        {
            _logger?.LogTrace(CoreLogEvents.DeltaProcessed,
                "Delta processed: {Count} events (crit={Crit}, heal={Heal}) TargetPlayer={IsTargetPlayer}",
                count, crits, heals, isTargetPlayer);
        }
    }
}

public sealed class SyncToMeDeltaInfoProcessor(IDataStorage storage, ILogger? logger)
    : BaseDeltaInfoProcessor(storage, logger)
{
    public override void Process(byte[] payload)
    {
        _logger?.LogDebug(CoreLogEvents.SyncToMeDelta, nameof(SyncToMeDeltaInfoProcessor));
        var syncToMeDeltaInfo = SyncToMeDeltaInfo.Parser.ParseFrom(payload);
        var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
        var uuid = aoiSyncToMeDelta.Uuid.ShiftRight16();
        if (uuid != 0 && _storage.CurrentPlayerUUID != uuid)
        {
            _storage.CurrentPlayerUUID = uuid;
        }

        var aoiSyncDelta = aoiSyncToMeDelta.BaseDelta;
        if (aoiSyncDelta == null) return;

        ProcessAoiSyncDelta(aoiSyncDelta);
    }
}

public sealed class SyncNearDeltaInfoProcessor(IDataStorage storage, ILogger? logger)
    : BaseDeltaInfoProcessor(storage, logger)
{
    public override void Process(byte[] payload)
    {
        _logger?.LogDebug(CoreLogEvents.SyncNearDelta, nameof(SyncNearDeltaInfoProcessor));
        var syncNearDeltaInfo = SyncNearDeltaInfo.Parser.ParseFrom(payload);
        if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0) return;

        foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos)
        {
            ProcessAoiSyncDelta(aoiSyncDelta);
        }
    }
}
