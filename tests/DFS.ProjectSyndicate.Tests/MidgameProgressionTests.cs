using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

// The four Worker contracts added in Phase 4: terms, roles and who may take them.
public class MidgameJobTests
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();
    private readonly CareerService _careers = new(Catalog);

    public static readonly string[] MidgameJobIds =
        ["night_freight_sorting", "fleet_maintenance_crew", "municipal_records_contract", "logistics_dispatch_coordinator"];

    // Lifetime XP at which a character reaches a level: 100 × L per level, so level L starts at 50 × L × (L − 1).
    private static int TotalXpForLevel(int level) => 50 * level * (level - 1);

    private static SyndicatePlayer Worker(int totalXp, float cash = 1000)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(totalXp);
        player.Tier = 2;
        player.Cash = cash;
        return player;
    }

    [Theory]
    [InlineData("night_freight_sorting", "Night Freight Sorting", 260, 40, 2, 5)]
    [InlineData("fleet_maintenance_crew", "Fleet Maintenance Crew", 340, 50, 3, 6)]
    [InlineData("municipal_records_contract", "Municipal Records Contract", 420, 35, 3, 6)]
    [InlineData("logistics_dispatch_coordinator", "Logistics Dispatch Coordinator", 575, 55, 4, 7)]
    public void MidgameJobs_HaveTheirAuthorizedTerms(string id, string title, int pay, int xp, int minLevel, int tasks)
    {
        var job = Catalog.Find(id)!;

        Assert.Equal((title, pay, xp, minLevel, 2, tasks),
            (job.Title, job.Payout, job.XPReward, job.MinLevel, job.MinTier, job.Tasks.Count));
        Assert.Equal(job.Tasks.Count, job.Tasks.Distinct().Count());
        Assert.True(job.Description.Length >= 80, "descriptions set the scene, not just name the job");
    }

    [Fact]
    public void MidgameJobs_EachHaveADistinctRole()
    {
        var worker = Catalog.All.Where(j => j.MinTier == 2).ToList();
        var freight = Catalog.Find("night_freight_sorting")!;
        var fleet = Catalog.Find("fleet_maintenance_crew")!;
        var records = Catalog.Find("municipal_records_contract")!;
        var dispatch = Catalog.Find("logistics_dispatch_coordinator")!;
        var security = Catalog.Find("night_security")!;

        // Freight: the first raise after promotion, trading a little XP for more pay than Night Security.
        Assert.True(freight.Payout > security.Payout && freight.XPReward < security.XPReward);
        // Fleet: the most XP of any contract below level 4; Records: the best pay at level 3, with the least XP.
        Assert.Equal(fleet, worker.Where(j => j.MinLevel < 4).MaxBy(j => j.XPReward));
        Assert.Equal(records, worker.Where(j => j.MinLevel <= 3).MaxBy(j => j.Payout));
        Assert.Equal(records, worker.MinBy(j => j.XPReward));
        // Dispatch: the top Worker contract, best on both pay and XP.
        Assert.All(worker.Where(j => j != dispatch), j => Assert.True(dispatch.Payout > j.Payout && dispatch.XPReward > j.XPReward));
    }

    private static double PayPerTask(Job job) => (double)job.Payout / job.Tasks.Count;
    private static double XpPerTask(Job job) => (double)job.XPReward / job.Tasks.Count;

    [Fact]
    public void NoWorkerContract_LeadsOnBothPayAndXpPerTask()
    {
        // Rewards are paid per contract, but the board compares averages per task. If one contract led on
        // both, there would be little reason to unlock the next; the top contract, Dispatch, leads on pay.
        var worker = Catalog.All.Where(j => j.MinTier == 2).ToList();

        var bestPay = worker.MaxBy(PayPerTask)!;
        var bestXp = worker.MaxBy(XpPerTask)!;

        Assert.NotEqual(bestPay, bestXp);
        Assert.Equal("logistics_dispatch_coordinator", bestPay.Id);
        Assert.Equal("fleet_maintenance_crew", bestXp.Id);
    }

    [Theory]
    [InlineData("night_freight_sorting", 2)]
    [InlineData("fleet_maintenance_crew", 3)]
    [InlineData("municipal_records_contract", 3)]
    [InlineData("logistics_dispatch_coordinator", 4)]
    public void MidgameJob_OpensForAWorker_ExactlyAtItsLevel(string id, int minLevel)
    {
        var player = Worker(TotalXpForLevel(minLevel) - 1);
        Assert.Equal(minLevel - 1, player.Level);

        var refused = _careers.AcceptJob(player, id);
        Assert.False(refused.Success);
        Assert.Contains($"Level {minLevel}", refused.Message);
        Assert.Equal(string.Empty, player.JobData.AssignedJobId);

        player.AddXP(1);
        Assert.Equal(minLevel, player.Level);
        Assert.True(_careers.AcceptJob(player, id).Success);
        Assert.Equal(id, player.JobData.AssignedJobId);
    }

    [Theory]
    [MemberData(nameof(MidgameJobIdData))]
    public void MidgameJob_IsRefusedToANewComer_AtAnyLevel(string id)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(TotalXpForLevel(10));

        var result = _careers.AcceptJob(player, id);

        Assert.False(result.Success);
        Assert.Contains("Tier 2 · Worker", result.Message);
        Assert.Equal(string.Empty, player.JobData.AssignedJobId);
    }

    public static TheoryData<string> MidgameJobIdData() => new(MidgameJobIds);

    [Fact]
    public void Requirements_ListBothUnmetGates_ForANewComer()
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);

        Assert.Equal(
            [new JobRequirement("Level 3", false), new JobRequirement("Tier 2 · Worker", false)],
            JobEligibility.Requirements(player, Catalog.Find("fleet_maintenance_crew")!));
    }

    [Fact]
    public void NewWorker_SeesOnlyFreightAndNightSecurityAmongWorkerContracts()
    {
        // Straight after the fastest promotion: level 2, Tier 2.
        var open = Catalog.All.Where(j => j.MinTier == 2 && JobEligibility.IsEligible(Worker(150), j)).Select(j => j.Id);

        Assert.Equal(["night_security", "night_freight_sorting"], open);
    }

    [Fact]
    public void HigherTiers_KeepAccessToEveryJob()
    {
        var professional = Worker(TotalXpForLevel(4));
        professional.Tier = 3;

        Assert.All(Catalog.All, job => Assert.True(JobEligibility.IsEligible(professional, job), job.Id));
    }
}

// Worker (Tier 2) to Professional (Tier 3): 400 lifetime XP and $7,000 on hand ($5,000 + $2,000 fee).
// Every route here is played through the real rules; the counts are what those rules produce.
public class Tier3BalanceTests
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();
    private static readonly CareerService Careers = new(Catalog);

    // The README's natural Tier 1 route (2 Trash Cleanups, 2 Grocery runs, 7 Warehouse jobs), then promotion.
    // Leaves a level 3 Worker with $1,020 and 330 lifetime XP.
    private static SyndicatePlayer WorkerAfterNaturalRoute()
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        foreach (string id in new[] { "trash_cleanup", "trash_cleanup", "grocery_delivery", "grocery_delivery" })
            BalanceSearch.PlayJob(Careers, player, id);
        while (!ProgressionManager.CanPromote(player))
            BalanceSearch.PlayJob(Careers, player, "warehouse_inventory");
        Assert.True(Careers.Promote(player).Success);
        return player;
    }

    // The fastest Tier 1 route (ten Diner shifts), then promotion. Leaves a level 2 Worker with $1,000 and 150 lifetime XP.
    private static SyndicatePlayer WorkerAfterFastestRoute()
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        for (int i = 0; i < 10; i++) BalanceSearch.PlayJob(Careers, player, "diner_night_shift");
        Assert.True(Careers.Promote(player).Success);
        return player;
    }

    private static SyndicatePlayer StartingWorker(string start) => start switch
    {
        "natural" => WorkerAfterNaturalRoute(),
        "fastest" => WorkerAfterFastestRoute(),
        _ => throw new ArgumentOutOfRangeException(nameof(start))
    };

    private static Strategy StrategyNamed(string name) => name switch
    {
        "cash" => Strategies.CashFocused,
        "xp" => Strategies.XpFocused,
        "rotation" => Strategies.WorkerRotation,
        "security" => Strategies.NightSecurityOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    [Fact]
    public void StartingWorkers_AreWhereTheReadmeSays()
    {
        var natural = WorkerAfterNaturalRoute();
        var fastest = WorkerAfterFastestRoute();

        Assert.Equal((2, 3, 1020f, 330), (natural.Tier, natural.Level, natural.Cash, natural.TotalXP));
        Assert.Equal((2, 2, 1000f, 150), (fastest.Tier, fastest.Level, fastest.Cash, fastest.TotalXP));
    }

    [Fact]
    public void Tier3Requirements_AreUnchanged()
    {
        var next = ProgressionManager.GetNextTierRequirement(WorkerAfterNaturalRoute())!;

        Assert.Equal((3, "Professional", 400, 5000, 2000), (next.TierLevel, next.TierName, next.XPNeeded, next.CashNeeded, next.PromotionCost));
    }

    // Exact counts for each documented route. The design target is roughly 12–20 Worker contracts for a
    // reasonable strategy; MidgameStrategies_MeetTheDesignTarget states that band, these pin the numbers.
    [Theory]
    [InlineData("natural", "cash", 13)]
    [InlineData("natural", "xp", 13)]
    [InlineData("natural", "rotation", 18)]
    [InlineData("fastest", "cash", 16)]
    [InlineData("fastest", "xp", 16)]
    [InlineData("fastest", "rotation", 20)]
    [InlineData("natural", "security", 28)]   // the Phase 3 grind these contracts replace
    [InlineData("fastest", "security", 28)]
    public void Strategy_ReachesTier3_InTheDocumentedNumberOfContracts(string start, string strategy, int expected)
    {
        var played = BalanceSearch.PlayUntilPromotable(Catalog, StartingWorker(start), StrategyNamed(strategy));

        Assert.Equal(expected, played.Count);
    }

    [Theory]
    [InlineData("natural", 13)]
    [InlineData("fastest", 15)]
    public void FewestContractsToTier3_FromEachStartingWorker(string start, int expected)
    {
        Assert.Equal(expected, BalanceSearch.FewestJobsToPromotion(Catalog, StartingWorker(start)));
    }

    [Theory]
    [InlineData("natural", "cash")]
    [InlineData("natural", "xp")]
    [InlineData("natural", "rotation")]
    [InlineData("fastest", "cash")]
    [InlineData("fastest", "xp")]
    [InlineData("fastest", "rotation")]
    public void MidgameStrategies_MeetTheDesignTarget(string start, string strategy)
    {
        var played = BalanceSearch.PlayUntilPromotable(Catalog, StartingWorker(start), StrategyNamed(strategy));

        Assert.InRange(played.Count, 12, 20);
    }

    [Fact]
    public void CashFocusedRoute_MovesUpThroughTheContracts()
    {
        var played = BalanceSearch.PlayUntilPromotable(Catalog, WorkerAfterFastestRoute(), Strategies.CashFocused);

        // Freight until level 3, Records until level 4, then Dispatch: each unlock is a raise.
        Assert.Equal(
            Enumerable.Repeat("night_freight_sorting", 4)
                .Concat(Enumerable.Repeat("municipal_records_contract", 9))
                .Concat(Enumerable.Repeat("logistics_dispatch_coordinator", 3)),
            played);
    }

    [Fact]
    public void RotationRoute_UsesEveryWorkerContract()
    {
        var played = BalanceSearch.PlayUntilPromotable(Catalog, WorkerAfterNaturalRoute(), Strategies.WorkerRotation);

        Assert.Equal(Catalog.All.Where(j => j.MinTier == 2).Select(j => j.Id).Order(), played.Distinct().Order());
    }

    [Fact]
    public void Cash_IsTheLimit_AtTier3_NotXp()
    {
        // Every strategy has far more than 400 lifetime XP by the time the cash is there.
        var player = WorkerAfterNaturalRoute();
        BalanceSearch.PlayUntilPromotable(Catalog, player, Strategies.CashFocused);

        Assert.True(player.TotalXP >= 800);
        Assert.InRange(player.Cash, 7000, 7000 + 575);
    }

    [Fact]
    public void EnoughXp_ButNotEnoughCash_CannotPromote()
    {
        var player = WorkerAfterNaturalRoute();
        player.AddXP(1000);
        player.Cash = 6999;

        var result = Careers.Promote(player);

        Assert.False(result.Success);
        Assert.Equal((2, 6999f), (player.Tier, player.Cash));
    }

    [Fact]
    public void EnoughCash_ButNotEnoughXp_CannotPromote()
    {
        var player = WorkerAfterFastestRoute();
        player.AddXP(399 - player.TotalXP);
        player.Cash = 20000;

        var result = Careers.Promote(player);

        Assert.False(result.Success);
        Assert.Equal((2, 20000f, 399), (player.Tier, player.Cash, player.TotalXP));
    }

    [Fact]
    public void ExactlyMeetingTheRequirements_Promotes_AndDeductsTheFee()
    {
        var player = WorkerAfterFastestRoute();
        player.AddXP(400 - player.TotalXP);
        player.Cash = 7000;

        var result = Careers.Promote(player);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Promoted to Tier 3: Professional.", result.Message);
        Assert.Equal((3, 5000f, 400), (player.Tier, player.Cash, player.TotalXP));
    }

    [Fact]
    public void Ranks_StillStopAtTheFifthTier()
    {
        Assert.Equal(5, ProgressionManager.MaxTier);
        Assert.Equal("Business Tycoon", ProgressionManager.GetTierName(5));
        Assert.Equal("Unknown", ProgressionManager.GetTierName(6));
    }

    [Fact]
    public async Task NewCharacter_PlaysFromCreationToProfessional_AndKeepsIt()
    {
        using var dir = new TempSaveDirectory();
        var game = TestJobs.CreateGame(new JsonFilePlayerRepository(dir.Path));
        string id = (await game.CreateCharacterAsync("Vinnie", Guid.NewGuid())).CharacterId!;

        // Tier 1: the natural route.
        foreach (string job in new[] { "trash_cleanup", "trash_cleanup", "grocery_delivery", "grocery_delivery" })
            await TestJobs.PlayJobAsync(game, id, job);
        while (!ProgressionManager.CanPromote((await game.GetOrCreatePlayerAsync(id)).Player!))
            await TestJobs.PlayJobAsync(game, id, "warehouse_inventory");
        Assert.True((await game.PromoteAsync(id)).Result!.Success);

        // Tier 2: rotate through every Worker contract on offer.
        int contracts = 0;
        while (true)
        {
            var player = (await game.GetOrCreatePlayerAsync(id)).Player!;
            if (ProgressionManager.CanPromote(player)) break;
            var open = Catalog.All.Where(j => JobEligibility.IsEligible(player, j)).ToList();
            await TestJobs.PlayJobAsync(game, id, Strategies.WorkerRotation(open, contracts).Id);
            contracts++;
            Assert.True(contracts <= 40, "took more than 40 Worker contracts");
        }
        Assert.Equal(18, contracts);

        var before = (await game.GetOrCreatePlayerAsync(id)).Player!;
        var promoted = await game.PromoteAsync(id);

        Assert.True(promoted.Result!.Success, promoted.Result.Message);
        Assert.Equal(before.Cash - 2000, promoted.Player!.Cash);

        // A fresh game over the same save folder: the promotion and everything earned are on disk.
        var reloaded = (await TestJobs.CreateGame(new JsonFilePlayerRepository(dir.Path)).GetOrCreatePlayerAsync(id)).Player!;
        Assert.Equal((3, before.Cash - 2000, before.TotalXP, 11 + 18), (reloaded.Tier, reloaded.Cash, reloaded.TotalXP, reloaded.JobData.JobsCompleted));
    }
}
