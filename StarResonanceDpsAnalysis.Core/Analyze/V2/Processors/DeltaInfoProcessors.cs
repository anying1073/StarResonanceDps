using BlueProto;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Data.Models;
using StarResonanceDpsAnalysis.Core.Extends.BlueProto;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.Core.Tools;
using StarResonanceDpsAnalysis.WPF.Data;

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

        // Attribute processing logic can be further refactored if needed
        if (delta.Attrs?.Attrs != null)
        {
            // This part might need access to the entity sync handlers or be refactored
            // For now, we assume attribute updates are handled by SyncNearEntitiesProcessor
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

            var damageSigned = d.HasValue ? d.Value : d.HasLuckyValue ? d.LuckyValue : 0L;
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
        }
    }
}

public sealed class SyncToMeDeltaInfoProcessor(IDataStorage storage, ILogger? logger)
    : BaseDeltaInfoProcessor(storage, logger)
{
    public override void Process(byte[] payload)
    {
        _logger?.LogDebug(nameof(SyncToMeDeltaInfoProcessor));
        var syncToMeDeltaInfo = SyncToMeDeltaInfo.Parser.ParseFrom(payload);
        var aoiSyncToMeDelta = syncToMeDeltaInfo.DeltaInfo;
        var uuid = aoiSyncToMeDelta.Uuid;
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
        _logger?.LogDebug(nameof(SyncNearDeltaInfoProcessor));
        var syncNearDeltaInfo = SyncNearDeltaInfo.Parser.ParseFrom(payload);
        if (syncNearDeltaInfo.DeltaInfos == null || syncNearDeltaInfo.DeltaInfos.Count == 0) return;

        foreach (var aoiSyncDelta in syncNearDeltaInfo.DeltaInfos)
        {
            ProcessAoiSyncDelta(aoiSyncDelta);
        }
    }
}
