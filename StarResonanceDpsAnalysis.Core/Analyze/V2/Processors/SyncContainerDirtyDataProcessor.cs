using System.Text;
using BlueProto;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Extends.BlueProto;
using StarResonanceDpsAnalysis.Core.Extends.System;
using StarResonanceDpsAnalysis.WPF.Data;

namespace StarResonanceDpsAnalysis.Core.Analyze.V2.Processors;

/// <summary>
/// Processes the SyncContainerDirtyData message for incremental player data updates.
/// </summary>
internal sealed class SyncContainerDirtyDataProcessor(IDataStorage storage, ILogger? logger) : IMessageProcessor
{
    private readonly IDataStorage _storage = storage;
    private readonly ILogger? _logger = logger;

    public void Process(byte[] payload)
    {
        _logger?.LogDebug(nameof(SyncContainerDirtyDataProcessor));
        try
        {
            if (_storage.CurrentPlayerUUID == 0) return;
            var dirty = SyncContainerDirtyData.Parser.ParseFrom(payload);
            if (dirty?.VData?.BufferS == null || dirty.VData.BufferS.Length == 0) return;

            var buf = dirty.VData.BufferS.ToByteArray();
            using var ms = new MemoryStream(buf, false);
            using var br = new BinaryReader(ms);

            if (!DoesStreamHaveIdentifier(br)) return;

            var fieldIndex = br.ReadUInt32();
            _ = br.ReadInt32();

            var playerUid = _storage.CurrentPlayerUUID.ShiftRight16();
            _storage.EnsurePlayer(playerUid);

            switch (fieldIndex)
            {
                case 2: ProcessNameAndCombatPower(br, playerUid); break;
                case 16: ProcessHp(br, playerUid); break;
                case 61: ProcessProfession(br, playerUid); break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process dirty container data.");
        }
    }

    private void ProcessNameAndCombatPower(BinaryReader br, long playerUid)
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
                    _storage.CurrentPlayerInfo.Name = playerName;
                    _storage.SetPlayerName(playerUid, playerName);
                }
                break;
            case 35:
                var fightPoint = (int)br.ReadUInt32();
                _ = br.ReadInt32();
                if (fightPoint != 0)
                {
                    _storage.CurrentPlayerInfo.CombatPower = fightPoint;
                    _storage.SetPlayerCombatPower(playerUid, fightPoint);
                }
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
                var curHp = (int)br.ReadUInt32();
                _storage.CurrentPlayerInfo.HP = curHp;
                _storage.SetPlayerHP(playerUid, curHp);
                break;
            case 2:
                var maxHp = (int)br.ReadUInt32();
                _storage.CurrentPlayerInfo.MaxHP = maxHp;
                _storage.SetPlayerMaxHP(playerUid, maxHp);
                break;
        }
    }

    private void ProcessProfession(BinaryReader br, long playerUid)
    {
        if (!DoesStreamHaveIdentifier(br)) return;
        var fieldIndex = br.ReadUInt32();
        _ = br.ReadInt32();
        if (fieldIndex == 1)
        {
            var curProfessionId = (int)br.ReadUInt32();
            _ = br.ReadInt32();
            if (curProfessionId != 0)
            {
                _storage.CurrentPlayerInfo.ProfessionID = curProfessionId;
                _storage.SetPlayerProfessionID(playerUid, curProfessionId);
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
