using BlueProto;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze.Models;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;

/// <summary>
/// Processes the SyncNearEntities message to update player and enemy attributes.
/// </summary>
internal sealed class SyncNearEntitiesProcessor : IMessageProcessor
{
    private readonly IDataStorage _storage;
    private readonly ILogger? _logger;
    private readonly List<Action<long, RepeatedField<Attr>>?> _entitySyncHandlers;

    public SyncNearEntitiesProcessor(IDataStorage storage, ILogger? logger)
    {
        _storage = storage;
        _logger = logger;
        _entitySyncHandlers =
        [
            null,
            ProcessEnemyAttrs, // EEntityType.EntMonster(1)
            null, null, null, null, null, null, null, null,
            ProcessPlayerAttrs // EEntityType.EntChar(10)
        ];
    }

    public void Process(byte[] payload)
    {
        _logger?.LogDebug("Sync Near entities");
        var syncNearEntities = SyncNearEntities.Parser.ParseFrom(payload);
        if (syncNearEntities.Appear == null || syncNearEntities.Appear.Count == 0) return;

        foreach (var entity in syncNearEntities.Appear)
        {
            if (entity.EntType != EEntityType.EntChar) continue;

            var playerUid = entity.Uuid.ShiftRight16();
            if (playerUid == 0) continue;

            var attrCollection = entity.Attrs;
            if (attrCollection?.Attrs == null) continue;

            if ((int)entity.EntType < 0 || (int)entity.EntType >= _entitySyncHandlers.Count) continue;
            var handler = _entitySyncHandlers[(int)entity.EntType];
            handler?.Invoke(playerUid, attrCollection.Attrs);
        }
    }

    private void ProcessPlayerAttrs(long playerUid, RepeatedField<Attr> attrs)
    {
        _storage.EnsurePlayer(playerUid);

        foreach (var attr in attrs)
        {
            if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;
            var reader = new CodedInputStream(attr.RawData.ToByteArray());

            var attrType = (AttrType)attr.Id;
            if (!Enum.IsDefined(attrType))
            {
                _logger?.LogWarning("Unknown attribute type: {AttrType}", attrType);
                continue;
            }
            switch (attrType)
            {
                case AttrType.AttrName:
                    _storage.SetPlayerName(playerUid, reader.ReadString());
                    break;
                case AttrType.AttrProfessionId:
                    _storage.SetPlayerProfessionID(playerUid, reader.ReadInt32());
                    break;
                case AttrType.AttrFightPoint:
                    _storage.SetPlayerCombatPower(playerUid, reader.ReadInt32());
                    break;
                case AttrType.AttrLevel:
                    _storage.SetPlayerLevel(playerUid, reader.ReadInt32());
                    break;
                case AttrType.AttrRankLevel:
                    _storage.SetPlayerRankLevel(playerUid, reader.ReadInt32());
                    break;
                case AttrType.AttrCri:
                    _storage.SetPlayerCritical(playerUid, reader.ReadInt32());
                    break;
                case AttrType.AttrLucky:
                    _storage.SetPlayerLucky(playerUid, reader.ReadInt32());
                    break;
                case AttrType.AttrHp:
                    _storage.SetPlayerHP(playerUid, reader.ReadInt32());
                    break;
                case AttrType.AttrMaxHp:
                    _storage.SetPlayerMaxHP(playerUid, reader.ReadInt32());
                    break;
                case AttrType.AttrId:
                case AttrType.AttrElementFlag:
                case AttrType.AttrReductionLevel:
                case AttrType.AttrReduntionId:
                case AttrType.AttrEnergyFlag:
                    break;
                // default:
                //     throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void ProcessEnemyAttrs(long enemyUid, RepeatedField<Attr> attrs)
    {
        // Placeholder for enemy attribute processing logic
        // In a more advanced implementation, this would update an enemy data store.
        _logger?.LogTrace("Processing attributes for enemy {EnemyUid}", enemyUid);
    }
}
