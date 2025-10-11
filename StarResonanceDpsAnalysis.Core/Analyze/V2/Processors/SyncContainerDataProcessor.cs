using System.Diagnostics;
using BlueProto;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;

/// <summary>
/// Processes the SyncContainerData message to update the current player's core information.
/// </summary>
public sealed class SyncContainerDataProcessor(IDataStorage storage, ILogger? logger) : IMessageProcessor
{
    private readonly IDataStorage _storage = storage;

    public void Process(byte[] payload)
    {
        logger?.LogDebug(nameof(SyncContainerDataProcessor));
        var syncContainerData = SyncContainerData.Parser.ParseFrom(payload);
        if (syncContainerData?.VData == null) return;
        var vData = syncContainerData.VData;
        Debug.Assert(vData != null);
        if (vData.CharId == 0) return;

        var playerUid = vData.CharId;
        _storage.CurrentPlayerInfo.UID = playerUid;
        _storage.EnsurePlayer(playerUid);

        if (vData.RoleLevel?.Level is { } level && level != 0)
        {
            _storage.CurrentPlayerInfo.Level = level;
            _storage.SetPlayerLevel(playerUid, level);
        }

        if (vData.Attr?.CurHp is { } curHp && curHp != 0)
        {
            _storage.CurrentPlayerInfo.HP = curHp;
            _storage.SetPlayerHP(playerUid, curHp);
        }

        if (vData.Attr?.MaxHp is { } maxHp && maxHp != 0)
        {
            _storage.CurrentPlayerInfo.MaxHP = maxHp;
            _storage.SetPlayerMaxHP(playerUid, maxHp);
        }

        if (vData.CharBase != null)
        {
            if (!string.IsNullOrEmpty(vData.CharBase.Name))
            {
                _storage.CurrentPlayerInfo.Name = vData.CharBase.Name;
                _storage.SetPlayerName(playerUid, vData.CharBase.Name);
            }

            if (vData.CharBase.FightPoint != 0)
            {
                _storage.CurrentPlayerInfo.CombatPower = vData.CharBase.FightPoint;
                _storage.SetPlayerCombatPower(playerUid, vData.CharBase.FightPoint);
            }
        }

        if (vData.ProfessionList?.CurProfessionId is { } profId && profId != 0)
        {
            _storage.CurrentPlayerInfo.ProfessionID = profId;
            _storage.SetPlayerProfessionID(playerUid, profId);
        }
    }
}
