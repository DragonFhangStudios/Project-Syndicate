using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

// A saved tier is a rank in the character's career (Phase 6B). A tier the career doesn't have is reported through
// the corrupt-save path and the file is never changed; legitimate tiers keep loading in every schema version.
public class SaveTierValidationTests : IDisposable
{
    private const string Id = "char-00000000ee01";
    private readonly TempSaveDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private string SavePath => _dir.File(Id + ".json");

    private GameService NewGame() => TestJobs.CreateGame(new JsonFilePlayerRepository(_dir.Path));

    private static string SaveJson(int schemaVersion, int tier, string id = Id)
    {
        string lifetimeXp = schemaVersion >= 2 ? "\"TotalXP\": 1600, \"JobsCompleted\": 40," : "";
        string career = schemaVersion >= 3 ? "\"CareerPath\": \"Civilian\"," : "";
        return $$"""
            { "SchemaVersion": {{schemaVersion}}, "CharacterId": "{{id}}", "Name": "Marisa Lowe",
              {{career}} "Level": 5, "XP": 100, {{lifetimeXp}} "Tier": {{tier}}, "Cash": 2500,
              "Strength": 5, "Intellect": 5, "Endurance": 5 }
            """;
    }

    [Theory]
    [InlineData(3, 6)]
    [InlineData(3, 99)]
    [InlineData(2, 6)]
    [InlineData(1, 6)]
    public async Task TierAboveTheCareersTopRank_IsReportedAsCorrupt_InEverySchemaVersion(int schemaVersion, int tier)
    {
        await File.WriteAllTextAsync(SavePath, SaveJson(schemaVersion, tier));

        var loaded = await new JsonFilePlayerRepository(_dir.Path).LoadAsync(Id);

        Assert.Equal(PlayerLoadStatus.Corrupt, loaded.Status);
        Assert.Equal($"tier {tier} is outside the Civilian career's ranks (1-5)", loaded.Problem);
        Assert.Null(loaded.Player);
    }

    [Fact]
    public async Task TierBelowOne_KeepsItsExistingMessage()
    {
        await File.WriteAllTextAsync(SavePath, SaveJson(3, 0));

        var loaded = await new JsonFilePlayerRepository(_dir.Path).LoadAsync(Id);

        Assert.Equal((PlayerLoadStatus.Corrupt, "level, XP or tier out of range"), (loaded.Status, loaded.Problem));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task TheLowestAndHighestRealTiers_StillLoad(int tier)
    {
        await File.WriteAllTextAsync(SavePath, SaveJson(3, tier));

        var loaded = await new JsonFilePlayerRepository(_dir.Path).LoadAsync(Id);

        Assert.Equal((PlayerLoadStatus.Loaded, tier), (loaded.Status, loaded.Player?.Tier));
    }

    [Fact]
    public async Task ImpossibleTierSave_BlocksEveryAction_AndIsNeverChanged()
    {
        string json = SaveJson(3, 6);
        await File.WriteAllTextAsync(SavePath, json);
        var game = NewGame();

        var outcomes = new[]
        {
            await game.AcceptJobAsync(Id, TestJobs.TrashCleanup),
            await game.CompleteTaskAsync(Id),
            await game.PromoteAsync(Id)
        };

        Assert.All(outcomes, o =>
        {
            Assert.Equal(PlayerLoadStatus.Corrupt, o.State.Status);
            Assert.Null(o.Result);
        });
        Assert.Equal(json, await File.ReadAllTextAsync(SavePath));
    }

    [Fact]
    public async Task ImpossibleTierSave_IsNeverOverwritten()
    {
        string json = SaveJson(3, 99);
        await File.WriteAllTextAsync(SavePath, json);
        var replacement = NewCharacters.Create(Id, "Marisa Lowe", DateTime.UtcNow);

        var error = await Assert.ThrowsAsync<CorruptSaveException>(() => new JsonFilePlayerRepository(_dir.Path).SaveAsync(replacement));

        Assert.Contains("tier 99 is outside the Civilian career's ranks", error.Message);
        Assert.Equal(json, await File.ReadAllTextAsync(SavePath));
    }

    [Fact]
    public async Task ImpossibleTierSave_IsListedAsUnreadable()
    {
        await File.WriteAllTextAsync(SavePath, SaveJson(3, 6));

        var (created, _) = await NewGame().ListCharactersAsync();

        var entry = Assert.Single(created);
        Assert.Equal((Id, PlayerLoadStatus.Corrupt), (entry.Id, entry.State.Status));
    }

    [Fact]
    public async Task DemoCharacterSave_WithAnImpossibleTier_IsNotRecreated()
    {
        // Demo characters are created on first use; a damaged existing save must not be replaced by a fresh one.
        string path = _dir.File("sonny-perelli.json");
        string json = SaveJson(3, 7, "sonny-perelli");
        await File.WriteAllTextAsync(path, json);

        var state = await NewGame().GetOrCreatePlayerAsync("sonny-perelli");

        Assert.Equal(PlayerLoadStatus.Corrupt, state.Status);
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }
}
