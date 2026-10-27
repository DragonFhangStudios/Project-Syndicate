using DFS.JobSystem.Core;
using DFS.JobSystem.Data;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

// The whole civilian employment loop across all nine jobs: rules, persistence and stale or concurrent sessions.
public class CivilianLoopTests : IDisposable
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();
    private readonly CareerService _careers = new(Catalog);
    private readonly TempSaveDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private GameService NewGame() => TestJobs.CreateGame(new JsonFilePlayerRepository(_dir.Path));

    public static TheoryData<string> AllJobIds() => new(TestJobs.RealCatalog().All.Select(j => j.Id));

    // Level 4 Worker: every job up to and including the Worker contracts is open to them.
    private static SyndicatePlayer SeasonedWorker()
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(650);
        player.Tier = 2;
        player.Cash = 2000;
        return player;
    }

    // Writes a save for a level 4 Worker, as the current game would, and returns its id.
    private async Task<string> SaveSeasonedWorkerAsync(string jobId = "", int taskIndex = 0)
    {
        var player = SeasonedWorker();
        player.Id = "char-00000000bb01";
        player.JobData.AssignedJobId = jobId;
        player.JobData.CurrentTaskIndex = taskIndex;
        await new JsonFilePlayerRepository(_dir.Path).SaveAsync(player);
        return player.Id;
    }

    [Theory]
    [MemberData(nameof(AllJobIds))]
    public void EveryJob_RunsItsTasksInOrder_AndPaysItsTermsExactlyOnce(string jobId)
    {
        var job = Catalog.Find(jobId)!;
        // A level 6 Professional, so every job on the board is open.
        var player = SeasonedWorker();
        player.AddXP(1500 - player.TotalXP);
        player.Tier = 3;
        var before = (player.Cash, player.TotalXP, player.JobData.JobsCompleted, player.JobData.TotalEarned);

        var accepted = _careers.AcceptJob(player, jobId);
        Assert.True(accepted.Success, accepted.Message);
        Assert.Equal($"You took the {job.Title} job. First task: {job.Tasks[0]}.", accepted.Message);

        for (int i = 0; i < job.Tasks.Count - 1; i++)
        {
            var step = _careers.CompleteCurrentTask(player);
            Assert.Equal($"Done: {job.Tasks[i]}. Next: {job.Tasks[i + 1]}.", step.Message);
            Assert.Null(step.Reward);
            Assert.Equal(i + 1, player.JobData.CurrentTaskIndex);
            Assert.Equal(before.Cash, player.Cash);
        }

        var last = _careers.CompleteCurrentTask(player);
        Assert.Equal(new JobReward(job.Id, job.Title, job.Payout, job.XPReward, last.Reward!.LevelsGained, player.Level), last.Reward);
        Assert.Equal(
            (before.Cash + job.Payout, before.TotalXP + job.XPReward, before.JobsCompleted + 1, before.TotalEarned + job.Payout),
            (player.Cash, player.TotalXP, player.JobData.JobsCompleted, player.JobData.TotalEarned));
        Assert.Equal(string.Empty, player.JobData.AssignedJobId);

        // Further clicks after the job closed pay nothing.
        Assert.False(_careers.CompleteCurrentTask(player).Success);
        Assert.Equal(before.Cash + job.Payout, player.Cash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TRASH_CLEANUP")]
    [InlineData("trash_cleanup ")]
    [InlineData("../trash_cleanup")]
    [InlineData("no_such_job")]
    public async Task UnknownJobIds_AreRefused_AndNothingIsSaved(string jobId)
    {
        string id = await SaveSeasonedWorkerAsync();
        string before = await File.ReadAllTextAsync(_dir.File(id + ".json"));

        var outcome = await NewGame().AcceptJobAsync(id, jobId);

        Assert.False(outcome.Result!.Success);
        Assert.Equal("That job is not available.", outcome.Result.Message);
        Assert.Equal(before, await File.ReadAllTextAsync(_dir.File(id + ".json")));
    }

    [Fact]
    public void LevelUpsDuringWorkerContracts_KeepLifetimeXpExact()
    {
        var player = SeasonedWorker(); // 650 lifetime XP, level 4 (50 of 400)
        for (int i = 0; i < 7; i++) BalanceSearch.PlayJob(_careers, player, "logistics_dispatch_coordinator");

        // 650 + 7 × 55 = 1,035: level 5 starts at 1,000.
        Assert.Equal((1035, 5, 35), (player.TotalXP, player.Level, player.XP));
    }

    [Fact]
    public async Task TwoSessions_AcceptingDifferentContractsAtOnce_EndWithOneActiveContract()
    {
        string id = await SaveSeasonedWorkerAsync();
        var game = NewGame();

        var outcomes = await Task.WhenAll(
            game.AcceptJobAsync(id, "fleet_maintenance_crew"),
            game.AcceptJobAsync(id, "municipal_records_contract"));

        var winner = Assert.Single(outcomes, o => o.Result!.Success);
        var loser = Assert.Single(outcomes, o => !o.Result!.Success);
        Assert.Equal("Finish your current job before taking another.", loser.Result!.Message);
        var saved = (await NewGame().GetOrCreatePlayerAsync(id)).Player!;
        Assert.Equal(winner.Player!.JobData.AssignedJobId, saved.JobData.AssignedJobId);
        Assert.Equal(0, saved.JobData.CurrentTaskIndex);
    }

    [Fact]
    public async Task StaleSession_CannotReplaceAnActiveContract()
    {
        // Session A takes a contract and starts it; session B, opened earlier, still shows no job and tries another.
        string id = await SaveSeasonedWorkerAsync();
        var sessionA = NewGame();
        Assert.True((await sessionA.AcceptJobAsync(id, "logistics_dispatch_coordinator")).Result!.Success);
        await sessionA.CompleteTaskAsync(id);

        var fromB = await NewGame().AcceptJobAsync(id, "night_freight_sorting");

        Assert.False(fromB.Result!.Success);
        Assert.Equal(("logistics_dispatch_coordinator", 1), (fromB.Player!.JobData.AssignedJobId, fromB.Player.JobData.CurrentTaskIndex));
    }

    [Fact]
    public async Task ConcurrentFinalClicks_OnAWorkerContract_PayOnce()
    {
        var job = Catalog.Find("logistics_dispatch_coordinator")!;
        string id = await SaveSeasonedWorkerAsync(job.Id, job.Tasks.Count - 1);
        var game = NewGame();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => game.CompleteTaskAsync(id)));

        Assert.Single(outcomes, o => o.Result!.Reward is not null);
        var saved = (await NewGame().GetOrCreatePlayerAsync(id)).Player!;
        Assert.Equal((2000f + 575, 650 + 55, 1), (saved.Cash, saved.TotalXP, saved.JobData.JobsCompleted));
    }

    [Fact]
    public async Task WorkerContractInProgress_SurvivesARestart_AndFinishesCorrectly()
    {
        string id = await SaveSeasonedWorkerAsync();
        var first = NewGame();
        await first.AcceptJobAsync(id, "fleet_maintenance_crew");
        for (int i = 0; i < 3; i++) await first.CompleteTaskAsync(id);

        var restarted = NewGame();
        var resumed = (await restarted.GetOrCreatePlayerAsync(id)).Player!;
        Assert.Equal(("fleet_maintenance_crew", 3), (resumed.JobData.AssignedJobId, resumed.JobData.CurrentTaskIndex));

        GameActionOutcome last;
        do last = await restarted.CompleteTaskAsync(id); while (last.Result!.Reward is null);
        Assert.Equal((2340f, 700, 2), (last.Player!.Cash, last.Player.TotalXP, last.Player.Tier));
    }

    [Fact]
    public async Task Phase3Save_WithAnActiveJob_LoadsAndFinishes_AndStaysVersion3()
    {
        // A version 3 save as Phase 3 wrote it: a Worker halfway through Night Security Patrol.
        string id = "char-00000000bb02";
        string path = _dir.File(id + ".json");
        await File.WriteAllTextAsync(path, $$"""
            {
              "SchemaVersion": 3,
              "CharacterId": "{{id}}",
              "Name": "Marisa Lowe",
              "CareerPath": "Civilian",
              "CreatedAtUtc": "2026-10-21T22:10:00Z",
              "Level": 3,
              "XP": 40,
              "TotalXP": 340,
              "Tier": 2,
              "Cash": 1060,
              "Strength": 5,
              "Intellect": 5,
              "Endurance": 5,
              "AssignedJobId": "night_security",
              "CurrentTaskIndex": 3,
              "TotalEarned": 1000,
              "JobsCompleted": 12,
              "SavedAtUtc": "2026-10-21T23:00:00Z"
            }
            """);
        var game = NewGame();

        GameActionOutcome last;
        do last = await game.CompleteTaskAsync(id); while (last.Result!.Reward is null);

        Assert.Equal((1280f, 385, 13, 1220), (last.Player!.Cash, last.Player.TotalXP, last.Player.JobData.JobsCompleted, last.Player.JobData.TotalEarned));
        Assert.Contains("\"SchemaVersion\": 3", await File.ReadAllTextAsync(path));
        // The midgame contracts open to this level 3 Worker.
        Assert.True((await game.AcceptJobAsync(id, "municipal_records_contract")).Result!.Success);
    }

    [Fact]
    public async Task ActiveJobRemovedFromTheCatalog_PaysNothing_AndDoesNotBlockNewWork()
    {
        string id = await SaveSeasonedWorkerAsync("retired_courier_route", 2);
        string path = _dir.File(id + ".json");
        string before = await File.ReadAllTextAsync(path);
        var game = NewGame();

        var complete = await game.CompleteTaskAsync(id);
        Assert.False(complete.Result!.Success);
        Assert.Null(complete.Result.Reward);
        Assert.Equal(before, await File.ReadAllTextAsync(path));

        var accepted = await game.AcceptJobAsync(id, "night_freight_sorting");
        Assert.True(accepted.Result!.Success, accepted.Result.Message);
        Assert.Equal(("night_freight_sorting", 0, 2000f), (accepted.Player!.JobData.AssignedJobId, accepted.Player.JobData.CurrentTaskIndex, accepted.Player.Cash));
    }

    [Theory]
    [InlineData(7)]    // the job has 7 tasks: indexes 0–6 are valid
    [InlineData(40)]
    public async Task ActiveJobWithProgressOutOfRange_PaysNothing_AndDoesNotBlockNewWork(int taskIndex)
    {
        // E.g. a job's task list was shortened by a data update while a character was part-way through it.
        string id = await SaveSeasonedWorkerAsync("logistics_dispatch_coordinator", taskIndex);
        var game = NewGame();

        var complete = await game.CompleteTaskAsync(id);
        Assert.False(complete.Result!.Success);
        Assert.Null(complete.Result.Reward);
        Assert.Null(_careers.ActiveJob(complete.Player!));

        var accepted = await game.AcceptJobAsync(id, "municipal_records_contract");
        Assert.True(accepted.Result!.Success, accepted.Result.Message);
        Assert.Equal((2000f, 650), (accepted.Player!.Cash, accepted.Player.TotalXP));
    }

    [Fact]
    public async Task NegativeJobProgress_IsACorruptSave_AndIsLeftUntouched()
    {
        // Persistence rejects it before the rules see it, so a damaged save is reported rather than guessed at.
        string id = await SaveSeasonedWorkerAsync("logistics_dispatch_coordinator");
        string path = _dir.File(id + ".json");
        string damaged = (await File.ReadAllTextAsync(path)).Replace("\"CurrentTaskIndex\": 0", "\"CurrentTaskIndex\": -1");
        await File.WriteAllTextAsync(path, damaged);

        var outcome = await NewGame().AcceptJobAsync(id, "municipal_records_contract");

        Assert.Equal(PlayerLoadStatus.Corrupt, outcome.State.Status);
        Assert.Equal(damaged, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void ActiveJob_IsTheAssignedJob_OnlyWhileItCanBeWorked()
    {
        var player = SeasonedWorker();
        Assert.Null(_careers.ActiveJob(player));

        _careers.AcceptJob(player, "night_freight_sorting");
        Assert.Equal("night_freight_sorting", _careers.ActiveJob(player)!.Id);

        for (int i = 0; i < 5; i++) _careers.CompleteCurrentTask(player);
        Assert.Null(_careers.ActiveJob(player));
    }

    [Fact]
    public async Task CorruptSave_RefusesWorkerContracts_AndIsLeftUntouched()
    {
        string id = "char-00000000bb03";
        string path = _dir.File(id + ".json");
        await File.WriteAllTextAsync(path, "{ \"SchemaVersion\": 3, \"Name\": ");

        var outcome = await NewGame().AcceptJobAsync(id, "night_freight_sorting");

        Assert.Equal(PlayerLoadStatus.Corrupt, outcome.State.Status);
        Assert.Null(outcome.Result);
        Assert.Equal("{ \"SchemaVersion\": 3, \"Name\": ", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void WpfLoadingPath_ReadsAllThirteenJobs_WithTheSameTerms()
    {
        // The WPF app loads jobs through JobLoader into its own JobManager, without JobCatalog's checks.
        var manager = new JobManager();
        JobLoader.LoadAndRegisterJobs(manager, JobCatalog.DefaultPath);

        var wpf = manager.GetAllJobs().ToList();
        Assert.Equal(
            ["trash_cleanup", "grocery_delivery", "diner_night_shift", "warehouse_inventory", "night_security",
             "night_freight_sorting", "fleet_maintenance_crew", "municipal_records_contract", "logistics_dispatch_coordinator",
             "commercial_property_inspector", "hospital_supply_coordinator", "port_customs_broker", "city_contracts_manager"],
            wpf.Select(j => j.Id));
        Assert.Equal(Catalog.All.Select(Terms), wpf.Select(Terms));
    }

    private static (string, string, int, int, int, int, string, string?, string?) Terms(Job job) =>
        (job.Id, job.Title, job.Payout, job.XPReward, job.MinLevel, job.MinTier, string.Join("|", job.Tasks), job.Career, job.Access);
}
