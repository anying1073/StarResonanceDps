using System.Diagnostics;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;
using StarResonanceDpsAnalysis.Core.Logging;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

/// <summary>
/// Processes the SyncContainerData message to update the current player's core information.
/// </summary>
public sealed class SyncContainerDataProcessor(IDataStorage storage, ILogger? logger) : WorldNtfBaseProcessor(WorldNtfMessageId.SyncContainerData)
{
    public override void Process(byte[] payload)
    {
        logger?.LogDebug(CoreLogEvents.SyncContainerData, "SyncContainerData received: {Bytes} bytes", payload.Length);

        var syncContainerData = Zproto.WorldNtf.Types.SyncContainerData.Parser.ParseFrom(payload);
        //var syncContainerData = SyncContainerData.Parser.ParseFrom(payload);
        if (syncContainerData?.VData == null) return;
        var vData = syncContainerData.VData;
        Debug.Assert(vData != null);
        if (vData.CharId == 0) return;

        var playerUid = vData.CharId;

        // Capture previous snapshot for concise diff logging
        var prev = storage.CurrentPlayerInfo;
        var prevName = prev.Name;
        var prevLevel = prev.Level;
        var prevHp = prev.HP;
        var prevMaxHp = prev.MaxHP;
        var prevPower = prev.CombatPower;
        var prevProfId = prev.ProfessionID;

        storage.CurrentPlayerUUID = playerUid;
        storage.CurrentPlayerInfo.UID = playerUid;
        storage.EnsurePlayer(playerUid);

        var updates = new List<string>(6);

        if (vData.RoleLevel?.Level is { } level && level != 0)
        {
            storage.CurrentPlayerInfo.Level = level;
            storage.SetPlayerLevel(playerUid, level);
            if (prevLevel != level) updates.Add($"level={level}");
        }

        if (vData.Attr?.CurHp is { } curHp && curHp != 0)
        {
            storage.CurrentPlayerInfo.HP = curHp;
            storage.SetPlayerHP(playerUid, curHp);
            if (prevHp != curHp) updates.Add($"hp={curHp}");
        }

        if (vData.Attr?.MaxHp is { } maxHp && maxHp != 0)
        {
            storage.CurrentPlayerInfo.MaxHP = maxHp;
            storage.SetPlayerMaxHP(playerUid, maxHp);
            if (prevMaxHp != maxHp) updates.Add($"maxHp={maxHp}");
        }

        if (vData.CharBase != null)
        {
            if (!string.IsNullOrEmpty(vData.CharBase.Name))
            {
                storage.CurrentPlayerInfo.Name = vData.CharBase.Name;
                storage.SetPlayerName(playerUid, vData.CharBase.Name);
                if (!string.Equals(prevName, vData.CharBase.Name, StringComparison.Ordinal))
                    updates.Add($"name='{vData.CharBase.Name}'");
            }

            if (vData.CharBase.FightPoint != 0)
            {
                storage.CurrentPlayerInfo.CombatPower = vData.CharBase.FightPoint;
                storage.SetPlayerCombatPower(playerUid, vData.CharBase.FightPoint);
                if (prevPower != vData.CharBase.FightPoint)
                    updates.Add($"power={vData.CharBase.FightPoint}");
            }
        }

        if (vData.ProfessionList?.CurProfessionId is { } profId && profId != 0)
        {
            storage.CurrentPlayerInfo.ProfessionID = profId;
            storage.SetPlayerProfessionID(playerUid, profId);
            if (prevProfId != profId) updates.Add($"professionId={profId}");
        }

        if (updates.Count > 0)
        {
            logger?.LogDebug(CoreLogEvents.SyncContainerData,
                "Player {UID} updated: {Updates}", playerUid, string.Join(", ", updates));
        }
        else
        {
            logger?.LogTrace(CoreLogEvents.SyncContainerData, "Player {UID} no effective field updates", playerUid);
        }
    }
}
