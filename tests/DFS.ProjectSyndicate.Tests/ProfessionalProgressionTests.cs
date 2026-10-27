using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

// The four Professional contracts added in Phase 5: terms, roles and who may take them.
public class ProfessionalJobTests
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();
    private readonly CareerService _careers = new(Catalog);

    public static readonly string[] ProfessionalJobIds =
        ["commercial_property_inspector", "hospital_supply_coordinator", "port_customs_broker", "city_contracts_manager"];

    public static TheoryData<string> ProfessionalJobIdData() => new(ProfessionalJobIds);

    // Lifetime XP at which a character reaches a level: 100 × L per level, so level L starts at 50 × L × (L − 1).
    private static int TotalXpForLevel(int level) => 50 * level * (level - 1);

    private static SyndicatePlayer Character(int tier, int totalXp)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(totalXp);
        player.Tier = tier;
        player.Cash = 6000;
        return player;
    }

    private static double PayPerTask(Job job) => (double)job.Payout / job.Tasks.Count;
    private static double XpPerTask(Job job) => (double)job.XPReward / job.Tasks.Count;

    [Theory]
    [InlineData("commercial_property_inspector", "Commercial Property Inspector", 720, 60, 4, 6)]
    [InlineData("hospital_supply_coordinator", "Hospital Supply Coordinator", 860, 75, 5, 7)]
    [InlineData("port_customs_broker", "Port Customs Broker", 1050, 50, 5, 8)]
    [InlineData("city_contracts_manager", "City Contracts Manager", 1300, 80, 6, 8)]
    public void ProfessionalJobs_HaveTheirProposedTerms(string id, string title, int pay, int xp, int minLevel, int tasks)
    {
        var job = Catalog.Find(id)!;

        Assert.Equal((title, pay, xp, minLevel, 3, tasks),
            (job.Title, job.Payout, job.XPReward, job.MinLevel, job.MinTier, job.Tasks.Count));
        Assert.Equal(job.Tasks.Count, job.Tasks.Distinct().Count());
        Assert.True(job.Description.Length >= 80, "descriptions set the scene, not just name the job");
    }

    [Fact]
    public void EveryProfessionalContract_PaysMoreThanEveryWorkerContract()
    {
        int bestWorkerPay = Catalog.All.Where(j => j.MinTier == 2).Max(j => j.Payout);

        Assert.All(Catalog.All.Where(j => j.MinTier == 3), j => Assert.True(j.Payout > bestWorkerPay, j.Id));
    }

    [Fact]
    public void ProfessionalJobs_EachHaveADistinctRole()
    {
        var professional = Catalog.All.Where(j => j.MinTier == 3).ToList();
        var inspector = Catalog.Find("commercial_property_inspector")!;
        var supply = Catalog.Find("hospital_supply_coordinator")!;
        var customs = Catalog.Find("port_customs_broker")!;
        var contracts = Catalog.Find("city_contracts_manager")!;

        // Inspector: the first Professional contract to open, a raise over Dispatch.
        Assert.Equal(inspector, professional.MinBy(j => j.MinLevel));
        // Supply: the most XP for each task. Customs: the best pay at level 5, with the least XP.
        Assert.Equal(supply, professional.MaxBy(XpPerTask));
        Assert.Equal(customs, professional.Where(j => j.MinLevel <= 5).MaxBy(j => j.Payout));
        Assert.Equal(customs, professional.MinBy(j => j.XPReward));
        // Contracts Manager: the top Professional contract, best on both pay and XP per contract.
        Assert.All(professional.Where(j => j != contracts),
            j => Assert.True(contracts.Payout > j.Payout && contracts.XPReward > j.XPReward, j.Id));
    }

    [Fact]
    public void NoProfessionalContract_LeadsOnBothPayAndXpPerTask()
    {
        // As with the Worker contracts: rewards are per contract, but the board compares averages per task.
        var professional = Catalog.All.Where(j => j.MinTier == 3).ToList();

        Assert.Equal("city_contracts_manager", professional.MaxBy(PayPerTask)!.Id);
        Assert.Equal("hospital_supply_coordinator", professional.MaxBy(XpPerTask)!.Id);
    }

    [Theory]
    [InlineData("commercial_property_inspector", 4)]
    [InlineData("hospital_supply_coordinator", 5)]
    [InlineData("port_customs_broker", 5)]
    [InlineData("city_contracts_manager", 6)]
    public void ProfessionalJob_OpensForAProfessional_ExactlyAtItsLevel(string id, int minLevel)
    {
        var player = Character(tier: 3, TotalXpForLevel(minLevel) - 1);
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
    [MemberData(nameof(ProfessionalJobIdData))]
    public void ProfessionalJob_IsRefusedToAWorker_AtAnyLevel(string id)
    {
        var worker = Character(tier: 2, TotalXpForLevel(10));

        var result = _careers.AcceptJob(worker, id);

        Assert.False(result.Success);
        Assert.Contains("Tier 3 · Professional", result.Message);
        Assert.Equal(string.Empty, worker.JobData.AssignedJobId);
    }

    [Fact]
    public void Requirements_ShowTheRankStillMissing_ForAHighLevelWorker()
    {
        var worker = Character(tier: 2, TotalXpForLevel(6));

        Assert.Equal(
            [new JobRequirement("Level 6", true), new JobRequirement("Tier 3 · Professional", false)],
            JobEligibility.Requirements(worker, Catalog.Find("city_contracts_manager")!));
    }

    [Fact]
    public void NewProfessional_AtLevel4_SeesOnlyTheInspectorAmongProfessionalContracts()
    {
        var open = Catalog.All.Where(j => j.MinTier == 3 && JobEligibility.IsEligible(Character(3, TotalXpForLevel(4)), j));

        Assert.Equal(["commercial_property_inspector"], open.Select(j => j.Id));
    }

    [Fact]
    public void Executives_KeepAccessToEveryJob()
    {
        var executive = Character(tier: 4, TotalXpForLevel(6));

        Assert.All(Catalog.All, job => Assert.True(JobEligibility.IsEligible(executive, job), job.Id));
    }
}

// Professional (Tier 3) to Executive (Tier 4): 800 lifetime XP and $20,000 on hand ($15,000 + $5,000 fee).
// Starting Professionals are made by playing the documented Tier 1 and Tier 2 routes through the real rules.
public class Tier4BalanceTests
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();
    private static readonly CareerService Careers = new(Catalog);

    // Tier 1: "natural" is 2 Trash Cleanups, 2 Grocery runs, then Warehouse work; "fastest" is ten Diner shifts.
    // Tier 2: "cash" always takes the best pay on offer; "rotation" tries every Worker contract in turn.
    private static SyndicatePlayer NewProfessional(string tier1Route, string tier2Strategy)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        if (tier1Route == "natural")
        {
            foreach (string id in new[] { "trash_cleanup", "trash_cleanup", "grocery_delivery", "grocery_delivery" })
                BalanceSearch.PlayJob(Careers, player, id);
            while (!ProgressionManager.CanPromote(player)) BalanceSearch.PlayJob(Careers, player, "warehouse_inventory");
        }
        else
        {
            for (int i = 0; i < 10; i++) BalanceSearch.PlayJob(Careers, player, "diner_night_shift");
        }
        Assert.True(Careers.Promote(player).Success);

        BalanceSearch.PlayUntilPromotable(Catalog, player,
            tier2Strategy == "cash" ? Strategies.CashFocused : Strategies.WorkerRotation);
        Assert.True(Careers.Promote(player).Success);
        Assert.Equal(3, player.Tier);
        return player;
    }

    private static Strategy StrategyNamed(string name) => name switch
    {
        "cash" => Strategies.CashFocused,
        "xp" => Strategies.XpFocused,
        "rotation" => Strategies.ProfessionalRotation,
        "dispatch" => Strategies.DispatchOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    [Theory]
    [InlineData("natural", "cash", 5255, 885, 4)]
    [InlineData("natural", "rotation", 5050, 1135, 5)]
    [InlineData("fastest", "cash", 5545, 790, 4)]
    [InlineData("fastest", "rotation", 5430, 1030, 5)]
    public void StartingProfessionals_AreWhereTheReadmeSays(string tier1, string tier2, float cash, int totalXp, int level)
    {
        var player = NewProfessional(tier1, tier2);

        Assert.Equal((cash, totalXp, level), (player.Cash, player.TotalXP, player.Level));
    }

    [Fact]
    public void Tier4Requirements_AreUnchanged()
    {
        var next = ProgressionManager.GetNextTierRequirement(NewProfessional("natural", "cash"))!;

        Assert.Equal((4, "Executive", 800, 15000, 5000), (next.TierLevel, next.TierName, next.XPNeeded, next.CashNeeded, next.PromotionCost));
    }

    // Exact counts, pinned like the Tier 2 and Tier 3 tables: changing a job or a tier that moves one of
    // these means updating this test and the README together.
    [Theory]
    [InlineData("natural", "cash", "cash", 15)]
    [InlineData("natural", "cash", "xp", 15)]
    [InlineData("natural", "cash", "rotation", 17)]
    [InlineData("natural", "rotation", "cash", 14)]
    [InlineData("natural", "rotation", "xp", 14)]
    [InlineData("natural", "rotation", "rotation", 16)]
    [InlineData("fastest", "cash", "cash", 15)]
    [InlineData("fastest", "cash", "xp", 16)]
    [InlineData("fastest", "cash", "rotation", 17)]
    [InlineData("fastest", "rotation", "cash", 14)]
    [InlineData("fastest", "rotation", "xp", 14)]
    [InlineData("fastest", "rotation", "rotation", 16)]
    [InlineData("natural", "cash", "dispatch", 26)]   // the best contract before Phase 5, repeated
    [InlineData("fastest", "rotation", "dispatch", 26)]
    public void Strategy_ReachesTier4_InTheDocumentedNumberOfContracts(string tier1, string tier2, string strategy, int expected)
    {
        var played = BalanceSearch.PlayUntilPromotable(Catalog, NewProfessional(tier1, tier2), StrategyNamed(strategy));

        Assert.Equal(expected, played.Count);
    }

    [Theory]
    [InlineData("natural", "cash", 15)]
    [InlineData("natural", "rotation", 13)]
    [InlineData("fastest", "cash", 15)]
    [InlineData("fastest", "rotation", 14)]
    public void FewestContractsToTier4_FromEachStartingProfessional(string tier1, string tier2, int expected)
    {
        Assert.Equal(expected, BalanceSearch.FewestJobsToPromotion(Catalog, NewProfessional(tier1, tier2)));
    }

    [Theory]
    [InlineData("natural", "cash")]
    [InlineData("natural", "rotation")]
    [InlineData("fastest", "cash")]
    [InlineData("fastest", "rotation")]
    public void ProfessionalStrategies_MeetTheDesignTarget(string tier1, string tier2)
    {
        foreach (string strategy in new[] { "cash", "xp", "rotation" })
        {
            var played = BalanceSearch.PlayUntilPromotable(Catalog, NewProfessional(tier1, tier2), StrategyNamed(strategy));
            Assert.InRange(played.Count, 12, 20);
        }
    }

    [Fact]
    public void CashFocusedRoute_MovesUpThroughTheContracts()
    {
        var played = BalanceSearch.PlayUntilPromotable(Catalog, NewProfessional("natural", "cash"), Strategies.CashFocused);

        // Inspector until level 5, Customs until level 6, then the Contracts Manager: each unlock is a raise.
        Assert.Equal(
            Enumerable.Repeat("commercial_property_inspector", 2)
                .Concat(Enumerable.Repeat("port_customs_broker", 10))
                .Concat(Enumerable.Repeat("city_contracts_manager", 3)),
            played);
    }

    [Fact]
    public void RotationRoute_UsesEveryProfessionalContract()
    {
        var played = BalanceSearch.PlayUntilPromotable(Catalog, NewProfessional("natural", "cash"), Strategies.ProfessionalRotation);

        Assert.Equal(ProfessionalJobTests.ProfessionalJobIds.Order(), played.Distinct().Order());
    }

    [Fact]
    public void Cash_IsTheLimit_AtTier4_NotXp()
    {
        var player = NewProfessional("fastest", "cash"); // the least XP of any starting Professional
        BalanceSearch.PlayUntilPromotable(Catalog, player, Strategies.CashFocused);

        Assert.True(player.TotalXP >= 1500);
        Assert.InRange(player.Cash, 20000, 20000 + 1300);
    }

    [Fact]
    public void EnoughXp_ButNotEnoughCash_CannotPromote()
    {
        var player = NewProfessional("natural", "cash");
        player.Cash = 19999;

        Assert.False(Careers.Promote(player).Success);
        Assert.Equal((3, 19999f), (player.Tier, player.Cash));
    }

    [Fact]
    public void EnoughCash_ButNotEnoughXp_CannotPromote()
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(799);
        player.Tier = 3;
        player.Cash = 50000;

        Assert.False(Careers.Promote(player).Success);
        Assert.Equal((3, 50000f), (player.Tier, player.Cash));
    }

    [Fact]
    public void ExactlyMeetingTheRequirements_Promotes_AndDeductsTheFee()
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(800);
        player.Tier = 3;
        player.Cash = 20000;

        var result = Careers.Promote(player);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Promoted to Tier 4: Executive.", result.Message);
        Assert.Equal((4, 15000f, 800), (player.Tier, player.Cash, player.TotalXP));
    }

    [Fact]
    public async Task NewCharacter_PlaysFromCreationToExecutive_AndKeepsIt()
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

        // Tier 2 and Tier 3: try every contract of the character's own rank in turn.
        int worker = await PlayRankAsync(game, id, Strategies.WorkerRotation);
        Assert.True((await game.PromoteAsync(id)).Result!.Success);
        int professional = await PlayRankAsync(game, id, Strategies.ProfessionalRotation);
        Assert.Equal((18, 16), (worker, professional));

        var before = (await game.GetOrCreatePlayerAsync(id)).Player!;
        var promoted = await game.PromoteAsync(id);
        Assert.True(promoted.Result!.Success, promoted.Result.Message);
        Assert.Equal("Promoted to Tier 4: Executive.", promoted.Result.Message);

        // A fresh game over the same save folder: the promotion and everything earned are on disk.
        var reloaded = (await TestJobs.CreateGame(new JsonFilePlayerRepository(dir.Path)).GetOrCreatePlayerAsync(id)).Player!;
        Assert.Equal((4, before.Cash - 5000, before.TotalXP, 11 + 18 + 16),
            (reloaded.Tier, reloaded.Cash, reloaded.TotalXP, reloaded.JobData.JobsCompleted));
    }

    private static async Task<int> PlayRankAsync(Gameplay.GameService game, string id, Strategy strategy)
    {
        int contracts = 0;
        while (true)
        {
            var player = (await game.GetOrCreatePlayerAsync(id)).Player!;
            if (ProgressionManager.CanPromote(player)) return contracts;
            var open = Catalog.All.Where(j => JobEligibility.IsEligible(player, j)).ToList();
            await TestJobs.PlayJobAsync(game, id, strategy(open, contracts).Id);
            contracts++;
            Assert.True(contracts <= 40, "took more than 40 contracts");
        }
    }
}
