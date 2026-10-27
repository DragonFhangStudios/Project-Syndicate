using System.Text.Json;
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
    public void ShipsThirteenJobs_InTheOrderTheyWereAdded()
    {
        // Phase 3's five, Phase 4's four Worker contracts, Phase 5's four Professional contracts.
        Assert.Equal(
            ["trash_cleanup", "grocery_delivery", "diner_night_shift", "warehouse_inventory", "night_security",
             "night_freight_sorting", "fleet_maintenance_crew", "municipal_records_contract", "logistics_dispatch_coordinator",
             "commercial_property_inspector", "hospital_supply_coordinator", "port_customs_broker", "city_contracts_manager"],
            _catalog.All.Select(j => j.Id));
    }

    // The five Phase 3 jobs, unchanged by later expansions: balance and existing saves depend on them.
    [Theory]
    [InlineData("trash_cleanup", "Street Trash Cleanup", 5, 40, 0, 0, 4)]
    [InlineData("grocery_delivery", "Grocery Delivery Run", 50, 20, 0, 0, 3)]
    [InlineData("diner_night_shift", "Diner Night Shift", 100, 15, 0, 0, 5)]
    [InlineData("warehouse_inventory", "Warehouse Inventory", 130, 30, 2, 0, 5)]
    [InlineData("night_security", "Night Security Patrol", 220, 45, 0, 2, 6)]
    public void OriginalJobs_AreUnchanged(string id, string title, int pay, int xp, int minLevel, int minTier, int tasks)
    {
        var job = _catalog.Find(id)!;

        Assert.Equal((title, pay, xp, minLevel, minTier, tasks),
            (job.Title, job.Payout, job.XPReward, job.MinLevel, job.MinTier, job.Tasks.Count));
    }

    [Fact]
    public void OriginalTrashCleanup_KeepsItsTasks()
    {
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
        // Checked on the file itself: the catalog's list can't show a duplicate, because JobManager keeps only the first.
        // Case-insensitive, so ids can't differ by case alone either.
        var rawIds = ShippedDefinitions().Select(j => j!.Id).ToList();
        Assert.Equal(rawIds.Count, rawIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
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

    private static string JobJson(string idProperty, string title = "A job") =>
        $$"""{ {{idProperty}} "Title": "{{title}}", "Payout": 10, "XPReward": 5, "Tasks": ["Do the work"] }""";

    private static InvalidDataException LoadFails(string json)
    {
        using var dir = new TempSaveDirectory();
        string path = dir.File("jobs.json");
        File.WriteAllText(path, json);
        return Assert.Throws<InvalidDataException>(() => new JobCatalog(path));
    }

    [Fact]
    public void DuplicateId_IsRejected_NamingTheIdAndBothEntries()
    {
        // JobManager would keep the first and silently drop the second; the catalog refuses the file instead.
        var error = LoadFails($"[ {JobJson("\"Id\": \"dock_work\",", "Dock Work")}, {JobJson("\"Id\": \"other_job\",")}, {JobJson("\"Id\": \"dock_work\",", "Dock Work Again")} ]");

        Assert.Contains("'dock_work'", error.Message);
        Assert.Contains("entry 1 and entry 3", error.Message);
    }

    [Fact]
    public void DuplicateOfAShippedJob_IsRejected()
    {
        string shipped = File.ReadAllText(JobCatalog.DefaultPath).TrimEnd().TrimEnd(']');
        var error = LoadFails(shipped + ", " + JobJson("\"Id\": \"trash_cleanup\",", "Second Trash Cleanup") + " ]");

        Assert.Contains("'trash_cleanup'", error.Message);
    }

    [Theory]
    [InlineData("")]                 // no Id property at all
    [InlineData("\"Id\": null,")]
    [InlineData("\"Id\": \"\",")]
    [InlineData("\"Id\": \"   \",")]
    public void MissingId_IsRejected_WithItsEntryNumber(string idProperty)
    {
        var error = LoadFails($"[ {JobJson("\"Id\": \"fine_job\",")}, {JobJson(idProperty)} ]");

        Assert.Contains("entry 2: missing id", error.Message);
    }

    [Theory]
    [InlineData("Trash_Cleanup")]
    [InlineData("trash cleanup")]
    [InlineData(" trash_cleanup")]
    [InlineData("trash-cleanup")]
    [InlineData("trash__cleanup")]
    [InlineData("_trash")]
    [InlineData("trash_")]
    [InlineData("träsh")]
    public void MalformedId_IsRejected_NamingTheId(string id)
    {
        var error = LoadFails($"[ {JobJson($"\"Id\": \"{id}\",")} ]");

        Assert.Contains($"'{id}'", error.Message);
        Assert.Contains("lowercase", error.Message);
    }

    private static Job JobWithId(string id) =>
        new() { Id = id, Title = "A job", Payout = 10, XPReward = 5, Tasks = ["Do the work"] };

    // In .NET, a regex ending in $ also matches before a final "\n", so "trash_cleanup\n" used to pass.
    [Theory]
    [InlineData("trash_cleanup\\n")]        // the JSON escapes for a trailing newline
    [InlineData("trash_cleanup\\u000a")]
    [InlineData("trash_cleanup\\r\\n")]
    public void IdEndingInALineBreak_IsRejected(string jsonEscapedId)
    {
        var error = LoadFails($"[ {JobJson($"\"Id\": \"{jsonEscapedId}\",")} ]");

        Assert.Contains("(entry 1): id must be lowercase letters and digits", error.Message);
    }

    [Theory]
    [InlineData("night_security\n")]
    [InlineData("night_security\r\n")]
    [InlineData("\nnight_security")]
    [InlineData("night\n_security")]
    public void Validate_RejectsIdsContainingLineBreaks(string id)
    {
        Assert.Contains("lowercase", JobCatalog.Validate(JobWithId(id)));
    }

    [Fact]
    public void IdLengthLimit_Is64Characters_AndTheErrorSaysSo()
    {
        Assert.Null(JobCatalog.Validate(JobWithId(new string('a', 64))));

        string tooLong = new('a', 65);
        var error = LoadFails($"[ {JobJson($"\"Id\": \"{tooLong}\",")} ]");

        Assert.Contains("id is 65 characters long; ids can be at most 64 characters", error.Message);
        Assert.DoesNotContain("lowercase", error.Message);
    }

    [Fact]
    public void OverlongSnakeCaseId_IsRejected_ForItsLength()
    {
        string id = string.Join("_", Enumerable.Repeat("night", 20)); // well-formed, 119 characters

        Assert.Contains("id is 119 characters long", LoadFails($"[ {JobJson($"\"Id\": \"{id}\",")} ]").Message);
    }

    [Theory]
    [InlineData("[ null ]", "entry 1 is empty")]
    [InlineData("[]", "no jobs")]
    [InlineData("null", "null instead of a list")]
    [InlineData("[ { \"Id\": \"null_steps\", \"Title\": \"Null steps\", \"Tasks\": null } ]", "job 'null_steps' (entry 1): needs at least one named task")]
    [InlineData("[ { \"Id\": \"half_written\", ", "not valid JSON")]
    [InlineData("{ \"Id\": \"not_a_list\" }", "not valid JSON")]
    public void MalformedFile_IsRejected_WithADescription(string json, string expected)
    {
        Assert.Contains(expected, LoadFails(json).Message);
    }

    // The shipped file as written, read the way JobLoader reads it, before JobManager can drop anything.
    private static List<Job?> ShippedDefinitions() =>
        JsonSerializer.Deserialize<List<Job?>>(File.ReadAllText(JobCatalog.DefaultPath))!;

    [Fact]
    public void ShippedFile_PassesRawValidation_AndEveryEntryReachesTheCatalog()
    {
        var raw = ShippedDefinitions();

        Assert.Null(JobCatalog.ValidateAll(raw));
        Assert.Equal(raw.Select(j => j!.Id), _catalog.All.Select(j => j.Id));
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

    // Fewest jobs to qualify for Worker. New jobs that open at Tier 2 or later cannot change this.
    private static int FewestJobsToTier2(SyndicatePlayer start) => BalanceSearch.FewestJobsToPromotion(Catalog, start);

    // Exact, as documented in the README: a change to jobs or tiers that lengthens (or shortens) the
    // fastest route must update this test and the README together.
    [Fact]
    public void NewCharacter_FastestRouteToTier2_IsExactlyTenJobs()
    {
        int fewest = FewestJobsToTier2(NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow));

        Assert.Equal(10, fewest);
    }

    [Theory]
    [InlineData("sonny-perelli", 4)]     // $1,500: XP is the only limit (four Street Trash Cleanups)
    [InlineData("carlo-valentini", 5)]   // $1,200: needed about 60 Street Trash Cleanups before Phase 3
    [InlineData("giovanni-ragoli", 4)]   // $3,000: XP is the only limit
    public void DemoCharacters_ReachTier2_WithoutGrinding(string id, int expectedJobs)
    {
        Assert.Equal(expectedJobs, FewestJobsToTier2(DemoCharacters.Find(id)!.CreatePlayer()));
    }

    [Fact]
    public async Task TypicalMix_PlayedThroughTheGame_PromotesAfterExactlyElevenJobs()
    {
        // A natural route: quick XP jobs to reach level 2, then the better-paying warehouse work.
        using var dir = new TempSaveDirectory();
        var game = TestJobs.CreateGame(new JsonFilePlayerRepository(dir.Path));
        string id = (await game.CreateCharacterAsync("Vinnie", Guid.NewGuid())).CharacterId!;
        var route = new[] { "trash_cleanup", "trash_cleanup", "grocery_delivery", "grocery_delivery" };

        int jobs = 0;
        foreach (string job in route) { await TestJobs.PlayJobAsync(game, id, job); jobs++; }
        Assert.Equal(2, (await game.GetOrCreatePlayerAsync(id)).Player!.Level);

        while (!ProgressionManager.CanPromote((await game.GetOrCreatePlayerAsync(id)).Player!))
        {
            await TestJobs.PlayJobAsync(game, id, "warehouse_inventory");
            jobs++;
            Assert.True(jobs <= 15, "took more than 15 jobs");
        }

        // The route is fixed, so the count is exact: 2 Trash Cleanups, 2 Grocery runs, 7 Warehouse jobs.
        Assert.Equal(11, jobs);
        Assert.True((await game.PromoteAsync(id)).Result!.Success);
        Assert.Equal(2, (await game.GetOrCreatePlayerAsync(id)).Player!.Tier);
    }
}
