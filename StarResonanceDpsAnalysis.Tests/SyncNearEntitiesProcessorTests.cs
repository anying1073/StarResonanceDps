using System.Text.Json;
using Microsoft.Extensions.Logging;
using StarResonanceDpsAnalysis.Core.Analyze.V2.Processors.WorldNtf;
using StarResonanceDpsAnalysis.Core.Data;
using Xunit.Abstractions;

namespace StarResonanceDpsAnalysis.Tests;

/// <summary>
/// Tests for SyncNearEntitiesProcessor using external test data
/// </summary>
public class SyncNearEntitiesProcessorTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDataDirectory;

    public SyncNearEntitiesProcessorTests(ITestOutputHelper output)
    {
        _output = output;
        _testDataDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "Data"
        );
    }

    [Fact]
    public void ProcessSyntheticPlayerData_ShouldSetAllAttributes()
    {
        // Arrange
        _output.WriteLine("=== Testing with synthetic data ===");
        
        var storage = new DataStorageV2(new DiagnosticLogger<DataStorageV2>(_output, "Storage"));
        var processor = new SyncNearEntitiesProcessor(
            storage,
            new DiagnosticLogger(_output, "Processor")
        );

        var playerUid = 12345678L;
        var playerName = "TestPlayer";
        var level = 50;
        var professionId = 3;
        var combatPower = 75000;

        var payload = TestMessageBuilder.BuildSyncNearEntitiesPayload(
            playerUid,
            playerName,
            level
        );

        // Act
        processor.Process(payload);

        // Assert
        var found = storage.ReadOnlyPlayerInfoDatas.TryGetValue(playerUid, out var player);
        Assert.True(found);
        Assert.NotNull(player);
        Assert.Equal(playerName, player.Name);
        Assert.Equal(level, player.Level);

        _output.WriteLine($"✅ Player found: {player.Name}, Level {player.Level}");
    }

    [Fact]
    public void ProcessMultipleEntities_ShouldHandleAllTypes()
    {
        _output.WriteLine("=== Testing multiple entity types ===");

        var storage = new DataStorageV2(new DiagnosticLogger<DataStorageV2>(_output, "Storage"));
        var processor = new SyncNearEntitiesProcessor(
            storage,
            new DiagnosticLogger(_output, "Processor")
        );

        // Create test data with multiple players
        var players = new[]
        {
            (uid: 11111111L, name: "Fighter", level: 60),
            (uid: 22222222L, name: "Mage", level: 55),
            (uid: 33333333L, name: "Healer", level: 58)
        };

        foreach (var (uid, name, level) in players)
        {
            var payload = TestMessageBuilder.BuildSyncNearEntitiesPayload(uid, name, level);
            processor.Process(payload);
        }

        // Verify all players were processed
        foreach (var (uid, name, level) in players)
        {
            var found = storage.ReadOnlyPlayerInfoDatas.TryGetValue(uid, out var player);
            Assert.True(found, $"Player {uid} should be found");
            Assert.Equal(name, player?.Name);
            Assert.Equal(level, player?.Level);
            _output.WriteLine($"✅ Found player: {name} (UID: {uid})");
        }
    }

    [Fact]
    public void ProcessInvalidPayload_ShouldNotThrow()
    {
        _output.WriteLine("=== Testing error handling ===");

        var storage = new DataStorageV2(new DiagnosticLogger<DataStorageV2>(_output, "Storage"));
        var processor = new SyncNearEntitiesProcessor(
            storage,
            new DiagnosticLogger(_output, "Processor")
        );

        var invalidPayloads = new[]
        {
            Array.Empty<byte>(),
            new byte[] { 0x00 },
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF },
        };

        foreach (var payload in invalidPayloads)
        {
            var exception = Record.Exception(() => processor.Process(payload));
            _output.WriteLine($"Payload length {payload.Length}: {(exception == null ? "No exception" : exception.GetType().Name)}");
        }
    }

    [Theory]
    [InlineData(@"Data\SyncNearEntitiesTestData.bin")]
    public void ProcessBinaryFile_ShouldAnalyzeAndShowResults(string binaryFilePath)
    {
        _output.WriteLine("=== Loading Binary File ===");
        _output.WriteLine($"File path: {binaryFilePath}");

        // Check if file exists
        if (!File.Exists(binaryFilePath))
        {
            _output.WriteLine($"⚠️  File not found: {binaryFilePath}");
            _output.WriteLine("Please update the file path in the test to point to your binary file.");
            return;
        }

        // Load the binary data
        var payloadBytes = File.ReadAllBytes(binaryFilePath);
        _output.WriteLine($"Loaded {payloadBytes.Length} bytes");
        _output.WriteLine($"First 64 bytes (hex): {BitConverter.ToString(payloadBytes.Take(64).ToArray())}");
        _output.WriteLine($"Base64: {Convert.ToBase64String(payloadBytes)}");
        _output.WriteLine("");

        var storage = new DataStorageV2(new DiagnosticLogger<DataStorageV2>(_output, "Storage"));
        var processor = new SyncNearEntitiesProcessor(
            storage,
            new DiagnosticLogger(_output, "Processor")
        );

        // Act
        _output.WriteLine("=== Processing Payload ===");
        try
        {
            processor.Process(payloadBytes);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error processing payload: {ex.Message}");
            _output.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }

        // Show what was found
        _output.WriteLine("");
        _output.WriteLine("=== Parsed Results ===");
        
        if (storage.ReadOnlyPlayerInfoDatas.Count == 0)
        {
            _output.WriteLine("⚠️  No entities found in the payload");
        }
        else
        {
            _output.WriteLine($"Found {storage.ReadOnlyPlayerInfoDatas.Count} entities:");
            foreach (var (uid, player) in storage.ReadOnlyPlayerInfoDatas)
            {
                _output.WriteLine($"\n📋 Entity UID: {uid}");
                _output.WriteLine($"   Name: {player.Name ?? "(none)"}");
                _output.WriteLine($"   Level: {player.Level}");
                _output.WriteLine($"   Profession: {player.ProfessionID}");
                _output.WriteLine($"   Combat Power: {player.CombatPower}");
                _output.WriteLine($"   HP: {player.HP}/{player.MaxHP}");
                _output.WriteLine($"   Critical: {player.Critical}");
                _output.WriteLine($"   Lucky: {player.Lucky}");
                _output.WriteLine($"   Season: Lv{player.SeasonLevel} (Strength: {player.SeasonStrength})");
                _output.WriteLine($"   Rank Level: {player.RankLevel}");
                _output.WriteLine($"   NPC Template ID: {player.NpcTemplateId}");
            }
        }

        // Suggest creating a test data file
        _output.WriteLine("");
        _output.WriteLine("=== Generate Test Data File ===");
        _output.WriteLine("To create a reusable test case, use this JSON:");
        _output.WriteLine("");

        var testDataJson = SyncNearEntitiesTestDataBuilder.CreateTestDataJsonFromResults(
            $"Captured from {Path.GetFileName(binaryFilePath)}",
            Convert.ToBase64String(payloadBytes),
            storage.ReadOnlyPlayerInfoDatas
        );
        _output.WriteLine(testDataJson);
        _output.WriteLine("");
        _output.WriteLine($"Save this to: Data/{Path.GetFileNameWithoutExtension(binaryFilePath)}.json");
    }

    [Fact(Skip = "Remove Skip and set your file path to run this test")]
    public void LoadMyBinaryFile_QuickTest()
    {
        // TODO: Replace this with your actual file path
        var myFilePath = @"C:\Users\YourName\Desktop\packet_data.bin";

        ProcessBinaryFile_ShouldAnalyzeAndShowResults(myFilePath);
    }

    private TestDataModel LoadTestData(string path)
    {
        var json = File.ReadAllText(path);
        var model = JsonSerializer.Deserialize<TestDataModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (model == null)
        {
            throw new InvalidOperationException($"Failed to deserialize test data from {path}");
        }

        return model;
    }

    private class TestDataModel
    {
        public string Description { get; set; } = "";
        public string PayloadBase64 { get; set; } = "";
        public List<ExpectedPlayerData> ExpectedPlayers { get; set; } = new();
        public List<ExpectedEnemyData> ExpectedEnemies { get; set; } = new();

        public byte[] PayloadBytes => Convert.FromBase64String(PayloadBase64);
    }

    private class ExpectedPlayerData
    {
        public long Uid { get; set; }
        public string? Name { get; set; }
        public int? Level { get; set; }
        public int? ProfessionId { get; set; }
        public int? CombatPower { get; set; }
        public int? HP { get; set; }
        public int? MaxHP { get; set; }
        public int? Critical { get; set; }
        public int? Lucky { get; set; }
        public int? SeasonStrength { get; set; }
        public int? SeasonLevel { get; set; }
        public int? RankLevel { get; set; }
    }

    private class ExpectedEnemyData
    {
        public long Uid { get; set; }
        public int? TemplateId { get; set; }
        public string? Name { get; set; }
        public int? HP { get; set; }
        public int? MaxHP { get; set; }
        public int? SeasonLevel { get; set; }
    }
}

/// <summary>
/// Helper class to generate more complex test data
/// </summary>
public static class SyncNearEntitiesTestDataBuilder
{
    /// <summary>
    /// Creates a test data JSON file from a base64-encoded payload and expected values
    /// </summary>
    public static string CreateTestDataJson(
        string description,
        string payloadBase64,
        List<(long uid, string? name, int? level)>? players = null,
        List<(long uid, int? templateId, string? name)>? enemies = null)
    {
        var expectedPlayers = players?.Select(p => new
        {
            uid = p.uid,
            name = p.name,
            level = p.level
        }).ToList();

        var expectedEnemies = enemies?.Select(e => new
        {
            uid = e.uid,
            templateId = e.templateId,
            name = e.name
        }).ToList();

        var testData = new
        {
            description,
            payloadBase64,
            expectedPlayers = expectedPlayers != null ? (object)expectedPlayers : new List<object>(),
            expectedEnemies = expectedEnemies != null ? (object)expectedEnemies : new List<object>()
        };

        return JsonSerializer.Serialize(testData, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Creates test data JSON from parsed results
    /// </summary>
    public static string CreateTestDataJsonFromResults(
        string description,
        string payloadBase64,
        System.Collections.ObjectModel.ReadOnlyDictionary<long, Core.Data.Models.PlayerInfo> parsedEntities)
    {
        var players = new List<object>();
        var enemies = new List<object>();

        foreach (var (uid, info) in parsedEntities)
        {
            var entity = new Dictionary<string, object?>
            {
                ["uid"] = uid
            };

            // Add non-default values
            if (!string.IsNullOrEmpty(info.Name))
                entity["name"] = info.Name;
            if (info.Level != 0)
                entity["level"] = info.Level;
            if (info.ProfessionID != 0)
                entity["professionId"] = info.ProfessionID;
            if (info.CombatPower != 0)
                entity["combatPower"] = info.CombatPower;
            if (info.HP != 0)
                entity["hp"] = info.HP;
            if (info.MaxHP != 0)
                entity["maxHP"] = info.MaxHP;
            if (info.Critical != 0)
                entity["critical"] = info.Critical;
            if (info.Lucky != 0)
                entity["lucky"] = info.Lucky;
            if (info.SeasonStrength != 0)
                entity["seasonStrength"] = info.SeasonStrength;
            if (info.SeasonLevel != 0)
                entity["seasonLevel"] = info.SeasonLevel;
            if (info.RankLevel != 0)
                entity["rankLevel"] = info.RankLevel;

            // Determine if it's an enemy (NPC) or player
            if (info.NpcTemplateId != 0)
            {
                entity["templateId"] = info.NpcTemplateId;
                enemies.Add(entity);
            }
            else
            {
                players.Add(entity);
            }
        }

        var testData = new
        {
            description,
            payloadBase64,
            expectedPlayers = players,
            expectedEnemies = enemies
        };

        return JsonSerializer.Serialize(testData, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Example usage for capturing real data:
    /// 
    /// // When you capture a real packet, get the base64 payload
    /// var payload = "CiEIgICMr/tpEAoaFhIMCAESCAcQbGF5ZXIxEgYIkE4SAQo=";
    /// 
    /// // Create test data
    /// var json = SyncNearEntitiesTestDataBuilder.CreateTestDataJson(
    ///     "Real player data from game",
    ///     payload,
    ///     players: new() { (55502962, "Player1", 10) }
    /// );
    /// 
    /// // Save to file
    /// File.WriteAllText("Data/my_test_case.json", json);
    /// </summary>
    public static void ExampleUsage()
    {
        // This is just documentation, not meant to run
    }
}
