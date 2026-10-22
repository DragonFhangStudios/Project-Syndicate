using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

public class CharacterCreationTests : IDisposable
{
    private readonly TempSaveDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private JsonFilePlayerRepository Repo() => new(_dir.Path);

    private GameService NewGame(Func<string>? ids = null) =>
        new(Repo(), new Careers.CareerService(TestJobs.RealCatalog()), ids);

    private async Task<string> CreateAsync(GameService game, string name)
    {
        var result = await game.CreateCharacterAsync(name, Guid.NewGuid());
        Assert.True(result.Success, result.Error);
        return result.CharacterId!;
    }

    [Fact]
    public async Task Create_MakesCivilianWithStartingProfile()
    {
        var before = DateTime.UtcNow;
        string id = await CreateAsync(NewGame(), "  Rosa   Marquez ");

        var player = (await Repo().LoadAsync(id)).Player!;

        Assert.True(CharacterIds.IsCustom(id));
        Assert.Equal(id, player.Id);
        Assert.Equal("Rosa Marquez", player.Name);
        Assert.Equal(CareerPath.Civilian, player.CareerPath);
        Assert.Equal(1, player.Level);
        Assert.Equal(0, player.XP);
        Assert.Equal(0, player.TotalXP);
        Assert.Equal(1, player.Tier);
        Assert.Equal(NewCharacters.StartingCash, player.Cash);
        Assert.Equal(NewCharacters.StartingAttribute, player.Strength);
        Assert.Equal(NewCharacters.StartingAttribute, player.Intellect);
        Assert.Equal(NewCharacters.StartingAttribute, player.Endurance);
        Assert.Equal(string.Empty, player.JobData.AssignedJobId);
        Assert.InRange(player.CreatedAtUtc!.Value, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task Ids_AreUnique_AndIndependentOfNames()
    {
        var game = NewGame();
        string a = await CreateAsync(game, "Vinnie");
        string b = await CreateAsync(game, "Rosa");

        Assert.NotEqual(a, b);
        Assert.DoesNotContain("vinnie", a);
        Assert.DoesNotContain("rosa", b);
    }

    [Fact]
    public async Task InvalidName_IsRejected_AndNothingIsWritten()
    {
        var result = await NewGame().CreateCharacterAsync("../../evil", Guid.NewGuid());

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Empty(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task DuplicateName_IsRejected_CaseInsensitively()
    {
        var game = NewGame();
        await CreateAsync(game, "Vinnie Russo");

        var second = await game.CreateCharacterAsync("vinnie russo", Guid.NewGuid());

        Assert.False(second.Success);
        Assert.Single(Directory.GetFiles(_dir.Path));
    }

    [Theory]
    [InlineData("Sonny Perelli")]
    [InlineData("carlo valentini")]
    public async Task DemoCharacterNames_AreReserved(string name)
    {
        var result = await NewGame().CreateCharacterAsync(name, Guid.NewGuid());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task SameRequestSentTwice_CreatesOneCharacter()
    {
        var game = NewGame();
        var request = Guid.NewGuid();

        var first = await game.CreateCharacterAsync("Vinnie", request);
        var second = await game.CreateCharacterAsync("Vinnie", request);

        Assert.True(first.Success && second.Success);
        Assert.Equal(first.CharacterId, second.CharacterId);
        Assert.Single(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task RapidConcurrentSubmissions_CreateOneCharacter()
    {
        var game = NewGame();
        var request = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => game.CreateCharacterAsync("Vinnie", request)));

        Assert.All(results, r => Assert.True(r.Success));
        Assert.Single(results.Select(r => r.CharacterId).Distinct());
        Assert.Single(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task IdCollision_RetriesWithANewId_AndNeverOverwrites()
    {
        // An existing save (deliberately unreadable) sits at the first id the generator will offer.
        string taken = "char-000000000001";
        await File.WriteAllTextAsync(_dir.File(taken + ".json"), "{ existing data");
        var offered = new Queue<string>([taken, "char-000000000002"]);

        var result = await NewGame(() => offered.Dequeue()).CreateCharacterAsync("Vinnie", Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Equal("char-000000000002", result.CharacterId);
        Assert.Equal("{ existing data", await File.ReadAllTextAsync(_dir.File(taken + ".json")));
    }

    [Fact]
    public async Task CreatedCharacter_SurvivesRestart()
    {
        string id = await CreateAsync(NewGame(), "Vinnie");

        var restored = await NewGame().GetOrCreatePlayerAsync(id);

        Assert.Equal(PlayerLoadStatus.Loaded, restored.Status);
        Assert.Equal("Vinnie", restored.Player!.Name);
    }

    [Fact]
    public async Task CreatedCharacter_CanPlayTheJobLoop_AndKeepsProgress()
    {
        var game = NewGame();
        string id = await CreateAsync(game, "Vinnie");

        await game.AcceptJobAsync(id, TestJobs.TrashCleanup);
        GameActionOutcome last = null!;
        for (int i = 0; i < 4; i++) last = await game.CompleteTaskAsync(id);

        Assert.NotNull(last.Result!.Reward);
        var restored = (await NewGame().GetOrCreatePlayerAsync(id)).Player!;
        Assert.Equal(NewCharacters.StartingCash + 5, restored.Cash);
        Assert.Equal(40, restored.TotalXP);
    }

    [Fact]
    public async Task Listing_ShowsCreatedCharacters_NewestFirst_AndAllDemoCharacters()
    {
        var game = NewGame();
        string older = await CreateAsync(game, "Older");
        await Task.Delay(20);
        string newer = await CreateAsync(game, "Newer");

        var (created, demo) = await game.ListCharactersAsync();

        Assert.Equal([newer, older], created.Select(c => c.Id));
        Assert.All(created, c => Assert.False(c.IsDemo));
        Assert.Equal(DemoCharacters.All.Select(d => d.Id), demo.Select(d => d.Id));
        Assert.All(demo, d => Assert.True(d.IsDemo));
    }

    [Fact]
    public async Task ListingAndCreating_LeaveExistingDemoSavesByteForByte()
    {
        // A demo save from an earlier version, with progress.
        string path = _dir.File("sonny-perelli.json");
        string v2 = """
            { "SchemaVersion": 2, "CharacterId": "sonny-perelli", "Name": "Sonny Perelli", "Level": 2, "XP": 60,
              "TotalXP": 160, "Tier": 2, "Cash": 1020, "Strength": 8, "Intellect": 5, "Endurance": 7,
              "AssignedJobId": "", "CurrentTaskIndex": 0, "TotalEarned": 20, "JobsCompleted": 4 }
            """;
        await File.WriteAllTextAsync(path, v2);
        var game = NewGame();

        await CreateAsync(game, "Vinnie");
        var (_, demo) = await game.ListCharactersAsync();

        Assert.Equal(v2, await File.ReadAllTextAsync(path));
        var sonny = demo.Single(d => d.Id == "sonny-perelli").State.Player!;
        Assert.Equal(2, sonny.Tier);
        Assert.Equal(1020, sonny.Cash);
        Assert.False(File.Exists(_dir.File("carlo-valentini.json"))); // listing creates nothing
    }

    [Fact]
    public async Task CorruptCreatedSave_IsListedAsUnreadable_AndLeftUntouched()
    {
        string path = _dir.File("char-0000000000aa.json");
        await File.WriteAllTextAsync(path, "garbage");

        var (created, _) = await NewGame().ListCharactersAsync();
        var outcome = await NewGame().AcceptJobAsync("char-0000000000aa", TestJobs.TrashCleanup);

        Assert.Equal(PlayerLoadStatus.Corrupt, created.Single().State.Status);
        Assert.Equal(PlayerLoadStatus.Corrupt, outcome.State.Status);
        Assert.Equal("garbage", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("char-000000000000")]  // well-formed but never created
    [InlineData("char-XYZ")]
    [InlineData("../sonny-perelli")]
    [InlineData("..\\char-000000000000")]
    [InlineData("someone-else")]
    public async Task UnknownOrMalformedIds_AreUnknown_AndNothingIsCreated(string id)
    {
        var game = NewGame();

        Assert.Equal(PlayerLoadStatus.UnknownCharacter, (await game.GetOrCreatePlayerAsync(id)).Status);
        Assert.Equal(PlayerLoadStatus.UnknownCharacter, (await game.CompleteTaskAsync(id)).State.Status);
        Assert.Empty(Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task CreatedCharacters_DoNotShareState()
    {
        var game = NewGame();
        string vinnie = await CreateAsync(game, "Vinnie");
        string rosa = await CreateAsync(game, "Rosa");

        await game.AcceptJobAsync(vinnie, TestJobs.TrashCleanup);
        var rosaState = (await game.GetOrCreatePlayerAsync(rosa)).Player!;
        var vinnieA = (await game.GetOrCreatePlayerAsync(vinnie)).Player!;
        var vinnieB = (await game.GetOrCreatePlayerAsync(vinnie)).Player!;

        Assert.Equal(string.Empty, rosaState.JobData.AssignedJobId);
        Assert.NotSame(vinnieA, vinnieB);
        vinnieA.Cash = 1;
        Assert.Equal(NewCharacters.StartingCash, vinnieB.Cash);
    }
}
