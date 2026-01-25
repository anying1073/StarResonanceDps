//using BlueProto;

using System.Diagnostics;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Extends.System;
using Zproto;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

/// <summary>
/// Processes the SyncNearEntities message to update player and enemy attributes.
/// </summary>
internal sealed class SyncNearEntitiesProcessor : WorldNtfBaseProcessor
{
    private readonly IDataStorage _storage;
    private readonly ILogger? _logger;
    private readonly Dictionary<EEntityType, Action<long, RepeatedField<Attr>>> _entityHandlerDict;

    public SyncNearEntitiesProcessor(IDataStorage storage, ILogger? logger) : base(WorldNtfMessageId.SyncNearEntities)
    {
        _storage = storage;
        _logger = logger;
        _entityHandlerDict = new()
        {
            { EEntityType.EntMonster, ProcessEnemyAttrs },
            { EEntityType.EntChar , ProcessPlayerAttrs},
            { EEntityType.EntCollection , ProcessCollection}
        };
    }

    private void ProcessCollection(long arg1, RepeatedField<Attr> arg2)
    {
        Debug.WriteLine("ProcessCollection");
    }

    public override void Process(byte[] payload)
    {
        _logger?.LogDebug("Sync Near entities");
        var syncNearEntities = Zproto.WorldNtf.Types.SyncNearEntities.Parser.ParseFrom(payload);
        var entities = syncNearEntities.Appear;
        if (!(entities?.Count > 0)) return;
        foreach (var entity in entities)
        {
            if (!_entityHandlerDict.TryGetValue(entity.EntType, out var handler))
            {
                continue;
            }
            var attrCollection = entity.Attrs;
            if (!(attrCollection?.Attrs.Count > 0)) continue;

            var entityUid = entity.Uuid.ShiftRight16();
            if (entityUid == 0) continue;

            handler(entityUid, attrCollection.Attrs);
        }
    }

    private void ProcessPlayerAttrs(long playerUid, RepeatedField<Attr> attrs)
    {
        _storage.EnsurePlayer(playerUid);

        foreach (var attr in attrs)
        {
            if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;
            var reader = new CodedInputStream(attr.RawData.ToByteArray());

            var attrType = (EAttrType)attr.Id;
//            if (!Enum.IsDefined(attrType))
//            {
//#if DEBUG
//                _logger?.LogWarning("Unknown attribute type: {AttrType}", attrType);
//#else
//                _logger?.LogTrace("Unknown attribute type: {AttrType}", attrType);
//#endif
//                continue;
//            }
            switch (attrType)
            {
                case EAttrType.AttrName:
                    var playerName = reader.ReadString();
                    _storage.SetPlayerName(playerUid, playerName);
                    _logger?.LogDebug("Set PlayerName: {playerUid}@{playerName}", playerUid, playerName);
                    Debug.WriteLine($"SyncNearEntitiesV2: SetPlayerName:{playerUid}@{playerName}");
                    break;
                case EAttrType.AttrProfessionId:
                    _storage.SetPlayerProfessionID(playerUid, reader.ReadInt32());
                    break;
                case EAttrType.AttrFightPoint:
                    _storage.SetPlayerCombatPower(playerUid, reader.ReadInt32());
                    break;
                case EAttrType.AttrLevel:
                    _storage.SetPlayerLevel(playerUid, reader.ReadInt32());
                    break;
                case EAttrType.AttrRankLevel:
                    _storage.SetPlayerRankLevel(playerUid, reader.ReadInt32());
                    break;
                case EAttrType.AttrCri:
                    _storage.SetPlayerCritical(playerUid, reader.ReadInt32());
                    break;
                case EAttrType.AttrLuck:
                    _storage.SetPlayerLucky(playerUid, reader.ReadInt32());
                    break;
                case EAttrType.AttrHp:
                    _storage.SetPlayerHP(playerUid, reader.ReadInt32());
                    break;
                case EAttrType.AttrMaxHp:
                case EAttrType.AttrMaxHpAdd:
                case EAttrType.AttrMaxHpExAdd:
                case EAttrType.AttrMaxHpExPer:
                case EAttrType.AttrMaxHpTotal:
                case EAttrType.AttrMaxHpPer:
                    _storage.SetPlayerMaxHP(playerUid, reader.ReadInt32());
                    break;

                case EAttrType.AttrSeasonStrength:
                case EAttrType.AttrSeasonStrengthTotal:
                case EAttrType.AttrSeasonStrengthAdd:
                case EAttrType.AttrSeasonStrengthExAdd:
                case EAttrType.AttrSeasonStrengthPer:
                case EAttrType.AttrSeasonStrengthExPer:
                    _storage.SetPlayerSeasonStrength(playerUid, reader.ReadInt32());
                    break;
                case EAttrType.AttrSeasonLevel:
                case EAttrType.AttrSeasonLv:
                    _storage.SetPlayerSeasonLevel(playerUid, reader.ReadInt32());
                    break;
                //case EAttrType.AttrElementFlag:
                //    _storage.SetPlayerElementFlag(playerUid, reader.ReadInt32());
                //    break;
                //case EAttrType.AttrReductionLevel:
                //_storage.SetPlayerReductionLevel(playerUid, reader.ReadInt32());
                //break;
                //case EAttrType.AttrEnergyFlag:
                //    _storage.SetPlayerEnergyFlag(playerUid, reader.ReadInt32());
                //    break;
                case EAttrType.AttrId:
                    //case EAttrType.AttrReduntionId:
                    break;
                    // default:
                    //     throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void ProcessEnemyAttrs(long enemyUid, RepeatedField<Attr> attrs)
    {
        if (attrs.Count == 0) return;

        _storage.EnsurePlayer(enemyUid);

        foreach (var attr in attrs)
        {
            if (attr.Id == 0 || attr.RawData == null || attr.RawData.Length == 0) continue;

            var rawBytes = attr.RawData.ToByteArray();
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                var rawBase64 = Convert.ToBase64String(rawBytes);
                _logger.LogDebug("Found attrId {AttrId} for enemy {EnemyUid} {AttrRaw}", attr.Id, enemyUid, rawBase64);
            }

            var reader = new CodedInputStream(rawBytes);
            try
            {
                switch ((EAttrType)attr.Id)
                {
                    case EAttrType.AttrName:
                        var enemyName = reader.ReadString();
                        _storage.SetPlayerName(enemyUid, enemyName);
                        _logger?.LogInformation("Found monster name {EnemyName} for uuid {EnemyUid}", enemyName, enemyUid);
                        break;
                    case EAttrType.AttrId:
                        var templateId = reader.ReadInt32();
                        _storage.SetNpcTemplateId(enemyUid, templateId);
                        _logger?.LogInformation("Set enemy {Uid} template id{templateId}", enemyUid, templateId);
                        break;
                    case EAttrType.AttrHp:
                        var enemyHp = reader.ReadInt32();
                        _storage.SetPlayerHP(enemyUid, enemyHp);
                        break;
                    case EAttrType.AttrMaxHp:
                        var enemyMaxHp = reader.ReadInt32();
                        _storage.SetPlayerMaxHP(enemyUid, enemyMaxHp);
                        break;

                    case EAttrType.AttrMonsterSeasonLevel:
                        var seasonLv = reader.ReadInt32();
                        _storage.SetPlayerSeasonLevel(enemyUid, seasonLv);
                        break;
                }
            }
            catch (InvalidProtocolBufferException ex)
            {
                _logger?.LogWarning(ex, "Failed to decode attr {AttrId} for enemy {EnemyUid}", attr.Id, enemyUid);
            }
        }
    }
}
