using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

public class GameServiceTests : IDisposable
{
    private readonly TempSaveDirectory _dir = new();
    private readonly JsonFilePlayerRepository _repo;
    private readonly GameService _game;

    public GameServiceTests()
    {
        _repo = new JsonFilePlayerRepository(_dir.Path);
        _game = TestJobs.CreateGame(_repo);
    }

    public void Dispose() => _dir.Dispose();

    [Fact]
    public async Task FirstLoad_CreatesSaveFromDemoProfile()
    {
        var result = await _game.GetOrCreatePlayerAsync("giovanni-ragoli");

        Assert.Equal(PlayerLoadStatus.Loaded, result.Status);
        Assert.Equal(3000, result.Player!.Cash);
        Assert.Equal(1, result.Player.Level);
        Assert.True(File.Exists(_dir.File("giovanni-ragoli.json")));
    }

    [Fact]
    public async Task UnknownCharacter_IsReported_AndNothingIsWritten()
    {
        var result = await _game.GetOrCreatePlayerAsync("not-a-character");

        Assert.Equal(PlayerLoadStatus.UnknownCharacter, result.Status);
        Assert.Empty(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task Preview_DoesNotCreateSave()
    {
        var result = await _game.PreviewPlayerAsync("sonny-perelli");

        Assert.Equal(PlayerLoadStatus.Loaded, result.Status);
        Assert.Empty(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task CorruptSave_IsReported_NotReplaced()
    {
        string path = _dir.File("sonny-perelli.json");
        await File.WriteAllTextAsync(path, "garbage");

        var result = await _game.GetOrCreatePlayerAsync("sonny-perelli");

        Assert.Equal(PlayerLoadStatus.Corrupt, result.Status);
        Assert.Equal("garbage", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task EachLoad_ReturnsItsOwnInstance()
    {
        // Two sessions viewing the same character must not share a mutable object.
        var a = (await _game.GetOrCreatePlayerAsync("sonny-perelli")).Player!;
        var b = (await _game.GetOrCreatePlayerAsync("sonny-perelli")).Player!;

        Assert.NotSame(a, b);
        a.Cash = 1;
        Assert.Equal(1500, b.Cash);
    }

    [Fact]
    public async Task Characters_AreStoredIndependently()
    {
        var sonny = (await _game.GetOrCreatePlayerAsync("sonny-perelli")).Player!;
        sonny.Cash = 99;
        await _repo.SaveAsync(sonny);

        var carlo = (await _game.GetOrCreatePlayerAsync("carlo-valentini")).Player!;

        Assert.Equal(1200, carlo.Cash);
    }

    [Fact]
    public void DemoProfiles_CreateFreshInstances()
    {
        var profile = DemoCharacters.Find("sonny-perelli")!;
        Assert.NotSame(profile.CreatePlayer(), profile.CreatePlayer());
    }
}
