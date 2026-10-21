using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

// Actions through GameService: persistence, restart, duplicate-reward protection and isolation.
public class GameActionTests : IDisposable
{
    private const string Sonny = "sonny-perelli";

    private readonly TempSaveDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private Gameplay.GameService NewGame() => TestJobs.CreateGame(new JsonFilePlayerRepository(_dir.Path));

    private static async Task CompleteWholeJobAsync(Gameplay.GameService game, string id)
    {
        Assert.True((await game.AcceptJobAsync(id, TestJobs.TrashCleanup)).Result!.Success);
        for (int i = 0; i < 4; i++) await game.CompleteTaskAsync(id);
    }

    [Fact]
    public async Task Progress_SurvivesRestart()
    {
        await CompleteWholeJobAsync(NewGame(), Sonny);
        var game = NewGame();
        await game.AcceptJobAsync(Sonny, TestJobs.TrashCleanup);
        await game.CompleteTaskAsync(Sonny);

        // A new service and repository stand in for an application restart.
        var restored = (await NewGame().GetOrCreatePlayerAsync(Sonny)).Player!;

        Assert.Equal(1505, restored.Cash);
        Assert.Equal(40, restored.TotalXP);
        Assert.Equal(1, restored.JobData.JobsCompleted);
        Assert.Equal(TestJobs.TrashCleanup, restored.JobData.AssignedJobId);
        Assert.Equal(1, restored.JobData.CurrentTaskIndex);
    }

    [Fact]
    public async Task EveryStep_IsSavedImmediately()
    {
        var game = NewGame();
        await game.AcceptJobAsync(Sonny, TestJobs.TrashCleanup);

        var onDisk = await new JsonFilePlayerRepository(_dir.Path).LoadAsync(Sonny);

        Assert.Equal(TestJobs.TrashCleanup, onDisk.Player!.JobData.AssignedJobId);
    }

    [Fact]
    public async Task ConcurrentFinalTaskClicks_PayExactlyOnce()
    {
        var game = NewGame();
        await game.AcceptJobAsync(Sonny, TestJobs.TrashCleanup);
        for (int i = 0; i < 3; i++) await game.CompleteTaskAsync(Sonny);

        // Ten simultaneous "finish" requests, as from several tabs or rapid clicks.
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => game.CompleteTaskAsync(Sonny)));

        Assert.Single(outcomes, o => o.Result!.Reward is not null);
        var player = (await game.GetOrCreatePlayerAsync(Sonny)).Player!;
        Assert.Equal(1505, player.Cash);
        Assert.Equal(40, player.TotalXP);
        Assert.Equal(1, player.JobData.JobsCompleted);
    }

    [Fact]
    public async Task StaleSession_CannotRepeatAReward()
    {
        // Two sessions load the same character on the last task; the first finishes the job.
        var game = NewGame();
        await game.AcceptJobAsync(Sonny, TestJobs.TrashCleanup);
        for (int i = 0; i < 3; i++) await game.CompleteTaskAsync(Sonny);

        var first = await game.CompleteTaskAsync(Sonny);
        var second = await NewGame().CompleteTaskAsync(Sonny); // separate service, same save

        Assert.NotNull(first.Result!.Reward);
        Assert.False(second.Result!.Success);
        Assert.Null(second.Result.Reward);
        Assert.Equal(1505, second.Player!.Cash);
    }

    [Fact]
    public async Task FailedAction_DoesNotWriteTheSave()
    {
        var game = NewGame();
        await game.GetOrCreatePlayerAsync(Sonny);
        string path = _dir.File("sonny-perelli.json");
        string before = await File.ReadAllTextAsync(path);

        var outcome = await game.CompleteTaskAsync(Sonny); // no active job

        Assert.False(outcome.Result!.Success);
        Assert.Equal(before, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task CorruptSave_BlocksActions_AndIsNotOverwritten()
    {
        string path = _dir.File("sonny-perelli.json");
        await File.WriteAllTextAsync(path, "{ broken");

        var outcome = await NewGame().AcceptJobAsync(Sonny, TestJobs.TrashCleanup);

        Assert.Equal(PlayerLoadStatus.Corrupt, outcome.State.Status);
        Assert.Null(outcome.Result);
        Assert.Equal("{ broken", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task OneCharactersProgress_DoesNotAffectAnother()
    {
        var game = NewGame();
        await CompleteWholeJobAsync(game, Sonny);

        var carlo = (await game.GetOrCreatePlayerAsync("carlo-valentini")).Player!;

        Assert.Equal(1200, carlo.Cash);
        Assert.Equal(0, carlo.TotalXP);
        Assert.Equal(0, carlo.JobData.JobsCompleted);
    }

    [Fact]
    public async Task Promotion_IsSaved()
    {
        var game = NewGame();
        for (int i = 0; i < 4; i++) await CompleteWholeJobAsync(game, Sonny);

        var outcome = await game.PromoteAsync(Sonny);
        var restored = (await NewGame().GetOrCreatePlayerAsync(Sonny)).Player!;

        Assert.True(outcome.Result!.Success);
        Assert.Equal(2, restored.Tier);
        Assert.Equal(1020, restored.Cash);
    }
}
