using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

public class JobCatalogDataTests
{
    private readonly JobCatalog _catalog = TestJobs.RealCatalog();

    [Fact]
    public void ShipsFiveJobs_IncludingTheOriginalTrashCleanup()
    {
        Assert.Equal(5, _catalog.All.Count);

        var trash = _catalog.Find(TestJobs.TrashCleanup)!;
        Assert.Equal(["Go to warehouse", "Grab supplies", "Clean the streets", "Empty trash at warehouse"], trash.Tasks);
        Assert.Equal(5, trash.Payout);
        Assert.Equal(40, trash.XPReward);
        Assert.Equal(0, trash.MinLevel);
        Assert.Equal(0, trash.MinTier);
    }

    [Fact]
    public void Jobs_AreDistinct_AndValid()
    {
        Assert.All(_catalog.All, job => Assert.Null(JobCatalog.Validate(job)));
        Assert.Equal(_catalog.All.Count, _catalog.All.Select(j => j.Id).Distinct().Count());
        Assert.Equal(_catalog.All.Count, _catalog.All.Select(j => j.Description).Distinct().Count());
        Assert.Equal(_catalog.All.Count, _catalog.All.Select(j => (j.Payout, j.XPReward)).Distinct().Count());
    }

    [Fact]
    public void Jobs_RunFromEntryLevelToAdvanced()
    {
        Assert.Contains(_catalog.All, j => j.MinLevel <= 1 && j.MinTier <= 1);
        Assert.Contains(_catalog.All, j => j.MinLevel > 1);
        Assert.Contains(_catalog.All, j => j.MinTier > 1);

        // Gated work pays more than every entry-level job.
        int bestEntryPay = _catalog.All.Where(j => j.MinLevel <= 1 && j.MinTier <= 1).Max(j => j.Payout);
        Assert.All(_catalog.All.Where(j => j.MinLevel > 1 || j.MinTier > 1), j => Assert.True(j.Payout > bestEntryPay));
    }

    [Fact]
    public void EveryRequirement_IsReachableThroughPlay()
    {
        // Only level and tier gate jobs, and both rise by working; tiers stop at MaxTier.
        Assert.All(_catalog.All, j => Assert.InRange(j.MinTier, 0, ProgressionManager.MaxTier));
    }

    [Theory]
    [InlineData(-1, 10, 0, 1, "negative")]
    [InlineData(10, -5, 0, 1, "negative")]
    [InlineData(10, 10, 0, 0, "task")]
    [InlineData(10, 10, 9, 1, "range")]
    public void Validate_RejectsBadDefinitions(int payout, int xp, int minTier, int tasks, string expected)
    {
        var job = new Job { Id = "x", Title = "X", Payout = payout, XPReward = xp, MinTier = minTier, Tasks = Enumerable.Repeat("Task", tasks).ToList() };

        Assert.Contains(expected, JobCatalog.Validate(job));
    }

    [Fact]
    public void Catalog_RefusesToLoadInvalidJobData()
    {
        using var dir = new TempSaveDirectory();
        string path = dir.File("jobs.json");
        File.WriteAllText(path, """[ { "Id": "broken", "Title": "Broken", "Payout": -10, "Tasks": ["One"] } ]""");

        Assert.Throws<InvalidDataException>(() => new JobCatalog(path));
    }
}

public class JobEligibilityTests
{
    private readonly CareerService _careers = new(TestJobs.RealCatalog());
    private readonly JobCatalog _catalog = TestJobs.RealCatalog();

    private static SyndicatePlayer NewCharacter() => NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);

    [Fact]
    public void EntryJobs_HaveNoRequirements()
    {
        var player = NewCharacter();

        foreach (string id in new[] { "trash_cleanup", "grocery_delivery", "diner_night_shift" })
        {
            Assert.Empty(JobEligibility.Requirements(player, _catalog.Find(id)!));
            Assert.True(JobEligibility.IsEligible(player, _catalog.Find(id)!));
        }
    }

    [Fact]
    public void Requirements_DescribeWhatIsMissing()
    {
        var player = NewCharacter();

        var warehouse = JobEligibility.Requirements(player, _catalog.Find("warehouse_inventory")!);
        var security = JobEligibility.Requirements(player, _catalog.Find("night_security")!);

        Assert.Equal([new JobRequirement("Level 2", false)], warehouse);
        Assert.Equal([new JobRequirement("Tier 2 · Worker", false)], security);
    }

    [Fact]
    public void LockedJob_IsRefusedByTheRules_NotJustTheUi()
    {
        var player = NewCharacter();

        var result = _careers.AcceptJob(player, "warehouse_inventory");

        Assert.False(result.Success);
        Assert.Contains("Level 2", result.Message);
        Assert.Equal(string.Empty, player.JobData.AssignedJobId);
    }

    [Fact]
    public void LevelGatedJob_OpensAtThatLevel()
    {
        var player = NewCharacter();
        player.AddXP(100);

        Assert.Equal(2, player.Level);
        Assert.True(_careers.AcceptJob(player, "warehouse_inventory").Success);
    }

    [Fact]
    public void TierGatedJob_OpensAfterPromotion()
    {
        var player = NewCharacter();
        player.AddXP(150);
        player.Cash = 1500;
        Assert.False(_careers.AcceptJob(player, "night_security").Success);

        Assert.True(_careers.Promote(player).Success);
        Assert.True(_careers.AcceptJob(player, "night_security").Success);
    }

    [Fact]
    public async Task LockedJob_RequestedDirectly_IsRefused_AndNothingIsSaved()
    {
        using var dir = new TempSaveDirectory();
        var game = TestJobs.CreateGame(new JsonFilePlayerRepository(dir.Path));
        string id = (await game.CreateCharacterAsync("Vinnie", Guid.NewGuid())).CharacterId!;
        string path = dir.File(id + ".json");
        string before = await File.ReadAllTextAsync(path);

        var outcome = await game.AcceptJobAsync(id, "night_security");

        Assert.False(outcome.Result!.Success);
        Assert.Equal(before, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void NewJob_PaysExactlyOnce()
    {
        var player = NewCharacter();
        _careers.AcceptJob(player, "diner_night_shift");

        var rewards = Enumerable.Range(0, 7).Select(_ => _careers.CompleteCurrentTask(player)).ToList();

        var paid = Assert.Single(rewards, r => r.Reward is not null);
        Assert.Equal(new JobReward("diner_night_shift", "Diner Night Shift", 100, 15, 0, 1), paid.Reward);
        Assert.Equal(NewCharacters.StartingCash + 100, player.Cash);
        Assert.Equal(15, player.TotalXP);
        Assert.Equal(2, rewards.Count(r => !r.Success)); // the two clicks after the job ended
    }
}

public class CareerBalanceTests
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();

    // Fewest completed jobs, choosing freely among the jobs a character can take at each step, until
    // the character qualifies for promotion to Tier 2. Breadth-first over (cash, lifetime XP) states.
    private static int FewestJobsToTier2(SyndicatePlayer start, int maxJobs = 40)
    {
        var frontier = new HashSet<(float Cash, int TotalXp)> { (start.Cash, start.TotalXP) };
        for (int jobs = 0; jobs <= maxJobs; jobs++)
        {
            if (frontier.Any(s => ProgressionManager.CanPromote(At(start, s)))) return jobs;

            var next = new HashSet<(float, int)>();
            foreach (var state in frontier)
            {
                var player = At(start, state);
                foreach (var job in Catalog.All.Where(j => JobEligibility.IsEligible(player, j)))
                    next.Add((state.Cash + job.Payout, state.TotalXp + job.XPReward));
            }
            frontier = next;
        }
        return int.MaxValue;
    }

    // Rebuilds the character at a given cash and lifetime XP (level follows from XP).
    private static SyndicatePlayer At(SyndicatePlayer start, (float Cash, int TotalXp) state)
    {
        var player = new SyndicatePlayer(start.Name) { Cash = state.Cash, Tier = start.Tier };
        player.AddXP(state.TotalXp);
        return player;
    }

    [Fact]
    public void NewCharacter_ReachesTier2_InTenToFifteenJobs()
    {
        int fewest = FewestJobsToTier2(NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow));

        Assert.InRange(fewest, 10, 15);
    }

    [Theory]
    [InlineData("sonny-perelli")]
    [InlineData("carlo-valentini")]
    [InlineData("giovanni-ragoli")]
    public void DemoCharacters_ReachTier2_WithoutGrinding(string id)
    {
        // Carlo ($1,200) needed about 60 Street Trash Cleanups before; the new jobs cut that sharply.
        Assert.InRange(FewestJobsToTier2(DemoCharacters.Find(id)!.CreatePlayer()), 1, 15);
    }

    [Fact]
    public async Task TypicalMix_PlayedThroughTheGame_PromotesWithinFifteenJobs()
    {
        // A natural route: quick XP jobs to reach level 2, then the better-paying warehouse work.
        using var dir = new TempSaveDirectory();
        var game = TestJobs.CreateGame(new JsonFilePlayerRepository(dir.Path));
        string id = (await game.CreateCharacterAsync("Vinnie", Guid.NewGuid())).CharacterId!;
        var route = new[] { "trash_cleanup", "trash_cleanup", "grocery_delivery", "grocery_delivery" };

        int jobs = 0;
        foreach (string job in route) { await PlayAsync(game, id, job); jobs++; }
        Assert.Equal(2, (await game.GetOrCreatePlayerAsync(id)).Player!.Level);

        while (!ProgressionManager.CanPromote((await game.GetOrCreatePlayerAsync(id)).Player!))
        {
            await PlayAsync(game, id, "warehouse_inventory");
            jobs++;
            Assert.True(jobs <= 15, "took more than 15 jobs");
        }

        Assert.InRange(jobs, 10, 15);
        Assert.True((await game.PromoteAsync(id)).Result!.Success);
        Assert.Equal(2, (await game.GetOrCreatePlayerAsync(id)).Player!.Tier);
    }

    private static async Task PlayAsync(GameService game, string id, string jobId)
    {
        var accepted = await game.AcceptJobAsync(id, jobId);
        Assert.True(accepted.Result!.Success, accepted.Result.Message);
        GameActionOutcome last;
        do last = await game.CompleteTaskAsync(id); while (last.Result!.Reward is null);
    }
}
