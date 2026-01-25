using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Data;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;

/// <summary>
/// Processes the SyncContainerDirtyData message for incremental player data updates.
/// </summary>
internal sealed class SyncContainerDirtyDataProcessor(IDataStorage storage, ILogger? logger) : WorldNtfBaseProcessor(WorldNtfMessageId.SyncContainerDirtyData)
{
    public override void Process(byte[] payload)
    {
        logger?.LogDebug(nameof(SyncContainerDirtyDataProcessor));
        try
        {
            if (storage.CurrentPlayerUUID == 0) return;
            var dirty = Zproto.WorldNtf.Types.SyncContainerDirtyData.Parser.ParseFrom(payload);
            if (dirty?.VData?.Buffer == null || dirty.VData.Buffer.Length == 0) return;

            var buf = dirty.VData.Buffer.ToByteArray();
            using var ms = new MemoryStream(buf, false);
            using var br = new BinaryReader(ms);

            if (!DoesStreamHaveIdentifier(br)) return;

            var fieldIndex = br.ReadUInt32();
            _ = br.ReadInt32();

            var playerUid = storage.CurrentPlayerUUID;
            storage.EnsurePlayer(playerUid);

            switch (fieldIndex)
            {
                case 2: ProcessNameAndPowerLevel(br, playerUid); break;
                case 16: ProcessHp(br, playerUid); break;
                case 61: ProcessClass(br, playerUid); break;
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to process dirty container data.");
        }
    }

    private void ProcessNameAndPowerLevel(BinaryReader br, long playerUid)
    {
        if (!DoesStreamHaveIdentifier(br)) return;
        var fieldIndex = br.ReadUInt32();
        _ = br.ReadInt32();
        switch (fieldIndex)
        {
            case 5:
                var playerName = StreamReadString(br);
                if (!string.IsNullOrEmpty(playerName))
                {
                    storage.CurrentPlayerInfo.Name = playerName;
                    storage.SetPlayerName(playerUid, playerName);
                }
                break;
            case 35:
                var fightPoint = br.ReadInt32();
                _ = br.ReadInt32();
                if (fightPoint != 0)
                {
                    storage.CurrentPlayerInfo.CombatPower = fightPoint;
                    storage.SetPlayerCombatPower(playerUid, fightPoint);
                }
                break;
            default:
                var tt = br.ReadInt32();
                Debug.WriteLine("tt:" + tt);
                break;
        }
    }

    private void ProcessHp(BinaryReader br, long playerUid)
    {
        if (!DoesStreamHaveIdentifier(br)) return;
        var fieldIndex = br.ReadUInt32();
        _ = br.ReadInt32();
        switch (fieldIndex)
        {
            case 1:
                var curHp = br.ReadInt32();
                storage.CurrentPlayerInfo.HP = curHp;
                storage.SetPlayerHP(playerUid, curHp);
                break;
            case 2:
                //var maxHp = (int)br.ReadUInt32();
                var maxHp = br.ReadInt32();
                storage.CurrentPlayerInfo.MaxHP = maxHp;
                storage.SetPlayerMaxHP(playerUid, maxHp);
                break;
        }
    }

    private void ProcessClass(BinaryReader br, long playerUid)
    {
        if (!DoesStreamHaveIdentifier(br)) return;
        var fieldIndex = br.ReadUInt32();
        _ = br.ReadInt32();
        if (fieldIndex == 1)
        {
            var curClassId = br.ReadInt32();
            _ = br.ReadInt32();
            if (curClassId != 0)
            {
                storage.CurrentPlayerInfo.ProfessionID = curClassId;
                storage.SetPlayerProfessionID(playerUid, curClassId);
            }
        }
    }

    private static bool DoesStreamHaveIdentifier(BinaryReader br)
    {
        var s = br.BaseStream;
        if (s.Position + 8 > s.Length) return false;
        var id1 = br.ReadUInt32();
        _ = br.ReadInt32();
        if (id1 != 0xFFFFFFFE) return false;
        if (s.Position + 8 > s.Length) return false;
        _ = br.ReadInt32();
        _ = br.ReadInt32();
        return true;
    }

    private static string StreamReadString(BinaryReader br)
    {
        var length = br.ReadUInt32();
        _ = br.ReadInt32();
        var bytes = length > 0 ? br.ReadBytes((int)length) : Array.Empty<byte>();
        _ = br.ReadInt32();
        return bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
    }
}
