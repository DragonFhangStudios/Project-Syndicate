using DFS.ProjectSyndicate.Characters;
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
        player.Tier = 2;
        player.JobData.AssignedJobId = "trash_cleanup";
        player.JobData.CurrentTaskIndex = 2;
        player.JobData.TotalEarned = 15;

        await _repo.SaveAsync(player);
        var result = await _repo.LoadAsync("carlo-valentini");

        Assert.Equal(PlayerLoadStatus.Loaded, result.Status);
        var loaded = result.Player!;
        Assert.Equal("carlo-valentini", loaded.Id);
        Assert.Equal("Carlo Valentini", loaded.Name);
        Assert.Equal(1234.5f, loaded.Cash);
        Assert.Equal(3, loaded.Level);
        Assert.Equal(42, loaded.XP);
        Assert.Equal(2, loaded.Tier);
        Assert.Equal(6, loaded.Strength);
        Assert.Equal("trash_cleanup", loaded.JobData.AssignedJobId);
        Assert.Equal(2, loaded.JobData.CurrentTaskIndex);
        Assert.Equal(15, loaded.JobData.TotalEarned);
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
