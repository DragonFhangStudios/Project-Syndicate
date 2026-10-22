using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

public class JsonFilePlayerRepositoryTests : IDisposable
{
    private readonly TempSaveDirectory _dir = new();
    private readonly JsonFilePlayerRepository _repo;

    public JsonFilePlayerRepositoryTests() => _repo = new JsonFilePlayerRepository(_dir.Path);

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task Load_MissingSave_ReturnsNotFound_AndWritesNothing()
    {
        var result = await _repo.LoadAsync("sonny-perelli");

        Assert.Equal(PlayerLoadStatus.NotFound, result.Status);
        Assert.Empty(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsPlayerState()
    {
        var player = DemoCharacters.Find("carlo-valentini")!.CreatePlayer();
        player.Cash = 1234.5f;
        player.Level = 3;
        player.XP = 42;
        player.TotalXP = 342;
        player.Tier = 2;
        player.JobData.AssignedJobId = "trash_cleanup";
        player.JobData.CurrentTaskIndex = 2;
        player.JobData.TotalEarned = 15;
        player.JobData.JobsCompleted = 3;

        await _repo.SaveAsync(player);
        var result = await _repo.LoadAsync("carlo-valentini");

        Assert.Equal(PlayerLoadStatus.Loaded, result.Status);
        var loaded = result.Player!;
        Assert.Equal("carlo-valentini", loaded.Id);
        Assert.Equal("Carlo Valentini", loaded.Name);
        Assert.Equal(1234.5f, loaded.Cash);
        Assert.Equal(3, loaded.Level);
        Assert.Equal(42, loaded.XP);
        Assert.Equal(342, loaded.TotalXP);
        Assert.Equal(2, loaded.Tier);
        Assert.Equal(6, loaded.Strength);
        Assert.Equal("trash_cleanup", loaded.JobData.AssignedJobId);
        Assert.Equal(2, loaded.JobData.CurrentTaskIndex);
        Assert.Equal(15, loaded.JobData.TotalEarned);
        Assert.Equal(3, loaded.JobData.JobsCompleted);
    }

    [Fact]
    public async Task Save_WritesCurrentSchemaVersion()
    {
        await _repo.SaveAsync(DemoCharacters.Find("sonny-perelli")!.CreatePlayer());

        string json = await File.ReadAllTextAsync(_dir.File("sonny-perelli.json"));

        Assert.Contains($"\"SchemaVersion\": {PlayerSaveData.CurrentSchemaVersion}", json);
        Assert.Equal(3, PlayerSaveData.CurrentSchemaVersion);
        Assert.Contains("\"CareerPath\": \"Civilian\"", json);
    }

    [Fact]
    public async Task Load_Version2Save_BecomesCivilian_WithProgressIntact()
    {
        // Phase 2C wrote v2 saves: no CareerPath or CreatedAtUtc.
        await File.WriteAllTextAsync(_dir.File("sonny-perelli.json"), """
            { "SchemaVersion": 2, "CharacterId": "sonny-perelli", "Name": "Sonny Perelli", "Level": 2, "XP": 60,
              "TotalXP": 160, "Tier": 2, "Cash": 1020, "Strength": 8, "Intellect": 5, "Endurance": 7,
              "AssignedJobId": "trash_cleanup", "CurrentTaskIndex": 1, "TotalEarned": 20, "JobsCompleted": 4 }
            """);

        var player = (await _repo.LoadAsync("sonny-perelli")).Player!;

        Assert.Equal(CareerPath.Civilian, player.CareerPath);
        Assert.Null(player.CreatedAtUtc);
        Assert.Equal(160, player.TotalXP);
        Assert.Equal(2, player.Tier);
        Assert.Equal(1020, player.Cash);
        Assert.Equal(4, player.JobData.JobsCompleted);
        Assert.Equal("trash_cleanup", player.JobData.AssignedJobId);
        Assert.Equal(1, player.JobData.CurrentTaskIndex);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsCareerPathAndCreationTime()
    {
        var created = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        await _repo.SaveAsync(NewCharacters.Create("char-00000000abcd", "Vinnie", created));

        var player = (await _repo.LoadAsync("char-00000000abcd")).Player!;

        Assert.Equal(CareerPath.Civilian, player.CareerPath);
        Assert.Equal(created, player.CreatedAtUtc);
    }

    [Theory]
    [InlineData("\"Criminal\"")]
    [InlineData("7")]
    public async Task Load_UnknownCareerPath_IsCorrupt(string careerPath)
    {
        await File.WriteAllTextAsync(_dir.File("sonny-perelli.json"),
            "{ \"SchemaVersion\": 3, \"CharacterId\": \"sonny-perelli\", \"Name\": \"Sonny\", \"Level\": 1, \"Tier\": 1, \"CareerPath\": "
            + careerPath + " }");

        Assert.Equal(PlayerLoadStatus.Corrupt, (await _repo.LoadAsync("sonny-perelli")).Status);
    }

    [Fact]
    public async Task Create_RefusesExistingSave_AndLeavesItUntouched()
    {
        string path = _dir.File("char-00000000abcd.json");
        await File.WriteAllTextAsync(path, "existing");

        bool created = await _repo.CreateAsync(NewCharacters.Create("char-00000000abcd", "Vinnie", DateTime.UtcNow));

        Assert.False(created);
        Assert.Equal("existing", await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task ListCharacterIds_ReturnsSavesOnly()
    {
        await _repo.SaveAsync(DemoCharacters.Find("sonny-perelli")!.CreatePlayer());
        await _repo.CreateAsync(NewCharacters.Create("char-00000000abcd", "Vinnie", DateTime.UtcNow));
        await File.WriteAllTextAsync(_dir.File("notes.txt"), "x");
        await File.WriteAllTextAsync(_dir.File("Bad Name.json"), "{}");

        var ids = await _repo.ListCharacterIdsAsync();

        Assert.Equal(["char-00000000abcd", "sonny-perelli"], ids);
    }

    [Fact]
    public async Task ListCharacterIds_MissingDirectory_IsEmpty()
    {
        var repo = new JsonFilePlayerRepository(Path.Combine(_dir.Path, "does-not-exist"));

        Assert.Empty(await repo.ListCharacterIdsAsync());
    }

    [Fact]
    public async Task Load_Version1Save_RebuildsLifetimeXp()
    {
        // Phase 2A wrote v1 saves without TotalXP or JobsCompleted.
        await File.WriteAllTextAsync(_dir.File("sonny-perelli.json"), """
            {
              "SchemaVersion": 1, "CharacterId": "sonny-perelli", "Name": "Sonny Perelli",
              "Level": 3, "XP": 50, "Tier": 1, "Cash": 1500, "Strength": 8, "Intellect": 5, "Endurance": 7,
              "AssignedJobId": "", "CurrentTaskIndex": 0, "TotalEarned": 10
            }
            """);

        var result = await _repo.LoadAsync("sonny-perelli");

        Assert.Equal(PlayerLoadStatus.Loaded, result.Status);
        Assert.Equal(350, result.Player!.TotalXP); // 100 (level 2) + 200 (level 3) + 50
        Assert.Equal(0, result.Player.JobData.JobsCompleted);
        Assert.Equal(10, result.Player.JobData.TotalEarned);
    }

    [Fact]
    public async Task Load_LifetimeXpBelowLevelProgress_IsCorrupt()
    {
        await File.WriteAllTextAsync(_dir.File("sonny-perelli.json"), """
            { "SchemaVersion": 2, "CharacterId": "sonny-perelli", "Name": "Sonny", "Level": 1, "XP": 80, "TotalXP": 10, "Tier": 1 }
            """);

        Assert.Equal(PlayerLoadStatus.Corrupt, (await _repo.LoadAsync("sonny-perelli")).Status);
    }

    [Fact]
    public async Task Save_LeavesNoTemporaryFile()
    {
        await _repo.SaveAsync(DemoCharacters.Find("sonny-perelli")!.CreatePlayer());

        Assert.Equal(["sonny-perelli.json"], Directory.GetFiles(_dir.Path).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{\"SchemaVersion\": 99, \"CharacterId\": \"sonny-perelli\", \"Name\": \"Sonny\", \"Level\": 1, \"Tier\": 1}")]
    [InlineData("{\"SchemaVersion\": 1, \"CharacterId\": \"someone-else\", \"Name\": \"Sonny\", \"Level\": 1, \"Tier\": 1}")]
    public async Task Load_CorruptSave_ReturnsCorrupt_AndLeavesFileUntouched(string contents)
    {
        string path = _dir.File("sonny-perelli.json");
        await File.WriteAllTextAsync(path, contents);

        var result = await _repo.LoadAsync("sonny-perelli");

        Assert.Equal(PlayerLoadStatus.Corrupt, result.Status);
        Assert.Null(result.Player);
        Assert.Equal(contents, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Save_OverCorruptSave_Throws_AndLeavesFileUntouched()
    {
        string path = _dir.File("sonny-perelli.json");
        await File.WriteAllTextAsync(path, "{ damaged");

        await Assert.ThrowsAsync<CorruptSaveException>(
            () => _repo.SaveAsync(DemoCharacters.Find("sonny-perelli")!.CreatePlayer()));
        Assert.Equal("{ damaged", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("Sonny Perelli")]
    [InlineData("")]
    [InlineData("c:/temp/x")]
    public async Task InvalidCharacterId_IsRejected(string id)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _repo.LoadAsync(id));
    }
}
