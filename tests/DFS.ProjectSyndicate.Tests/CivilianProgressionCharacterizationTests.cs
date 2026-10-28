using System.Globalization;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

// Pins down valid Civilian progression as it stands before career-specific ranks (Phase 6B), written against the
// unchanged code at 6cd3167. Everything here must keep passing, unedited, through the 6B refactor: tier names and
// thresholds, promotion outcomes at and just below every threshold, the exact texts the web game and the legacy
// WPF app show, the promotion summary, and saves at every legitimate tier.
//
// Only valid Civilian states are pinned. Behaviour 6B changes on purpose (other careers judged against the
// Civilian table, tiers outside 1-5) is deliberately left out.
public class CivilianProgressionCharacterizationTests
{
    // Tier n+1's requirement row, as promotion from tier n uses it: (tier, name, lifetime XP, cash, fee).
    public static TheoryData<int, string, int, int, int> PromotionRows() => new()
    {
        { 2, "Worker", 150, 1000, 500 },
        { 3, "Professional", 400, 5000, 2000 },
        { 4, "Executive", 800, 15000, 5000 },
        { 5, "Business Tycoon", 1500, 50000, 10000 }
    };

    private static SyndicatePlayer Civilian(int tier, int totalXp, float cash)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(totalXp);
        player.Tier = tier;
        player.Cash = cash;
        Assert.Equal(CareerPath.Civilian, player.CareerPath);
        return player;
    }

    // ---- Tier table ----

    [Fact]
    public void TierNames_AndTopTier_AreUnchanged()
    {
        Assert.Equal(5, ProgressionManager.MaxTier);
        Assert.Equal(
            ["Unknown", "New Comer", "Worker", "Professional", "Executive", "Business Tycoon", "Unknown"],
            Enumerable.Range(0, 7).Select(ProgressionManager.GetTierName));
    }

    [Theory]
    [MemberData(nameof(PromotionRows))]
    public void NextTierRequirement_IsUnchanged(int tier, string name, int xp, int cash, int fee)
    {
        var next = ProgressionManager.GetNextTierRequirement(Civilian(tier - 1, 0, 0))!;

        Assert.Equal((tier, name, xp, cash, fee), (next.TierLevel, next.TierName, next.XPNeeded, next.CashNeeded, next.PromotionCost));
    }

    [Fact]
    public void TopTier_HasNoNextRequirement()
    {
        Assert.Null(ProgressionManager.GetNextTierRequirement(Civilian(5, 5000, 100000)));
    }

    // ---- Promotion outcomes at and just below every threshold ----

    [Theory]
    [MemberData(nameof(PromotionRows))]
    public void ExactlyMeetingTheRequirements_Promotes_DeductingOnlyTheFee(int tier, string name, int xp, int cash, int fee)
    {
        var player = Civilian(tier - 1, xp, cash + fee);
        var before = (player.Level, player.XP, player.TotalXP);
        Assert.Equal(name, ProgressionManager.GetNextTierRequirement(player)!.TierName);

        Assert.True(ProgressionManager.CanPromote(player));
        Assert.True(ProgressionManager.TryPromote(player));

        Assert.Equal((tier, (float)cash), (player.Tier, player.Cash));
        Assert.Equal(before, (player.Level, player.XP, player.TotalXP));
    }

    [Theory]
    [MemberData(nameof(PromotionRows))]
    public void OneXpShort_DoesNotPromote_AndChangesNothing(int tier, string name, int xp, int cash, int fee)
    {
        var player = Civilian(tier - 1, xp - 1, cash + fee);
        Assert.Equal(name, ProgressionManager.GetNextTierRequirement(player)!.TierName);

        Assert.False(ProgressionManager.CanPromote(player));
        Assert.False(ProgressionManager.TryPromote(player));
        Assert.Equal((tier - 1, (float)(cash + fee), xp - 1), (player.Tier, player.Cash, player.TotalXP));
    }

    [Theory]
    [MemberData(nameof(PromotionRows))]
    public void OneDollarShort_DoesNotPromote_AndChangesNothing(int tier, string name, int xp, int cash, int fee)
    {
        var player = Civilian(tier - 1, xp, cash + fee - 1);
        Assert.Equal(name, ProgressionManager.GetNextTierRequirement(player)!.TierName);

        Assert.False(ProgressionManager.CanPromote(player));
        Assert.False(ProgressionManager.TryPromote(player));
        Assert.Equal((tier - 1, (float)(cash + fee - 1)), (player.Tier, player.Cash));
    }

    [Fact]
    public void TopTier_NeverPromotes_HoweverRich()
    {
        var player = Civilian(5, 100000, 1_000_000);

        Assert.False(ProgressionManager.CanPromote(player));
        Assert.False(ProgressionManager.TryPromote(player));
        Assert.Equal((5, 1_000_000f), (player.Tier, player.Cash));
    }

    [Fact]
    public void ClimbingEveryRank_InTurn_EndsAtBusinessTycoon()
    {
        var player = Civilian(1, 1500, 0);
        var careers = new CareerService(TestJobs.RealCatalog());

        foreach (var (tier, name, _, cash, fee) in new[] {
            (2, "Worker", 150, 1000, 500), (3, "Professional", 400, 5000, 2000),
            (4, "Executive", 800, 15000, 5000), (5, "Business Tycoon", 1500, 50000, 10000) })
        {
            player.Cash = cash + fee;
            Assert.Equal($"Promoted to Tier {tier}: {name}.", careers.Promote(player).Message);
            Assert.Equal((tier, (float)cash), (player.Tier, player.Cash));
        }
        Assert.False(careers.Promote(player).Success);
    }

    // ---- Texts the game shows ----

    [Theory]
    [MemberData(nameof(PromotionRows))]
    public void CareerServicePromote_Messages_AreUnchanged(int tier, string name, int xp, int cash, int fee)
    {
        var careers = new CareerService(TestJobs.RealCatalog());

        var shortPlayer = Civilian(tier - 1, xp - 1, cash + fee);
        var refused = careers.Promote(shortPlayer);
        Assert.Equal((false, "You don't meet the requirements for promotion yet."), (refused.Success, refused.Message));
        Assert.Null(refused.Reward);

        var promoted = careers.Promote(Civilian(tier - 1, xp, cash + fee));
        Assert.Equal((true, $"Promoted to Tier {tier}: {name}."), (promoted.Success, promoted.Message));
        Assert.Null(promoted.Reward);
    }

    [Fact]
    public void CareerServicePromote_AtTheTopTier_GivesTheSameRefusal()
    {
        var player = Civilian(5, 100000, 1_000_000);

        var result = new CareerService(TestJobs.RealCatalog()).Promote(player);

        Assert.Equal((false, "You don't meet the requirements for promotion yet."), (result.Success, result.Message));
        Assert.Equal((5, 1_000_000f), (player.Tier, player.Cash));
    }

    // The text the legacy WPF dashboard shows. Money uses the current culture (N0), so it is pinned under en-US.
    [Theory]
    [InlineData(1, "Promotion to Worker Requirements:\n• Lifetime XP: 150 (You have: 120)\n• Total Cash: $1,000 (You have: $1,234)\n• Promotion Fee: $500")]
    [InlineData(2, "Promotion to Professional Requirements:\n• Lifetime XP: 400 (You have: 120)\n• Total Cash: $5,000 (You have: $1,234)\n• Promotion Fee: $2,000")]
    [InlineData(3, "Promotion to Executive Requirements:\n• Lifetime XP: 800 (You have: 120)\n• Total Cash: $15,000 (You have: $1,234)\n• Promotion Fee: $5,000")]
    [InlineData(4, "Promotion to Business Tycoon Requirements:\n• Lifetime XP: 1500 (You have: 120)\n• Total Cash: $50,000 (You have: $1,234)\n• Promotion Fee: $10,000")]
    [InlineData(5, "⭐ Maximum Tier Reached!")]
    public void WpfPromotionRequirementsText_IsUnchanged(int tier, string expected)
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal(expected, ProgressionManager.GetPromotionRequirements(Civilian(tier, 120, 1234)));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    // ---- The dashboard's promotion summary ----
    // Pinned on PromotionProgress in 6B-1; PromotionStatus replaced it in 6B with the same figures.

    [Theory]
    [MemberData(nameof(PromotionRows))]
    public void PromotionSummary_ShowsWhatIsStillNeeded(int tier, string name, int xp, int cash, int fee)
    {
        var progress = PromotionStatus.For(Civilian(tier - 1, xp - 40, cash + fee - 300));

        Assert.Equal(PromotionAvailability.RequirementsNotMet, progress.Availability);
        Assert.Equal((tier, name), (progress.Next!.TierLevel, progress.Next.TierName));
        Assert.Equal((40, 300f, (float)(cash + fee)), (progress.XpRemaining, progress.CashRemaining, progress.CashOnHandNeeded));
        Assert.Equal((false, false, false), (progress.XpMet, progress.CashMet, progress.CanPromote));
        Assert.Equal(cash - 300f, progress.CashAfterPromotion);
    }

    [Theory]
    [MemberData(nameof(PromotionRows))]
    public void PromotionSummary_WhenQualified_ShowsTheCashLeftAfterTheFee(int tier, string name, int xp, int cash, int fee)
    {
        var progress = PromotionStatus.For(Civilian(tier - 1, xp + 25, cash + fee + 70));

        Assert.Equal(PromotionAvailability.Ready, progress.Availability);
        Assert.Equal((tier, name), (progress.Next!.TierLevel, progress.Next.TierName));
        Assert.Equal((0, 0f), (progress.XpRemaining, progress.CashRemaining));
        Assert.Equal((true, true, true), (progress.XpMet, progress.CashMet, progress.CanPromote));
        Assert.Equal(cash + 70f, progress.CashAfterPromotion);
    }

    [Fact]
    public void PromotionSummary_AtTheTopTier_IsEmpty()
    {
        var status = PromotionStatus.For(Civilian(5, 5000, 100000));

        Assert.Equal(PromotionAvailability.MaxRankReached, status.Availability);
        Assert.Null(status.Next);
    }

    // ---- Saves at every legitimate tier ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task SavedCivilian_AtEveryTier_LoadsWithItsTier(int tier)
    {
        using var dir = new TempSaveDirectory();
        var repo = new JsonFilePlayerRepository(dir.Path);
        var player = Civilian(tier, 1500, 60000);
        player.Id = "char-00000000dd01";

        await repo.SaveAsync(player);
        var loaded = await repo.LoadAsync(player.Id);

        Assert.Equal(PlayerLoadStatus.Loaded, loaded.Status);
        Assert.Equal((tier, CareerPath.Civilian, 60000f), (loaded.Player!.Tier, loaded.Player.CareerPath, loaded.Player.Cash));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 5)]
    [InlineData(2, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 1)]
    [InlineData(3, 5)]
    public async Task HandWrittenSave_AtTheLowestAndHighestTier_LoadsInEverySchemaVersion(int schemaVersion, int tier)
    {
        using var dir = new TempSaveDirectory();
        string path = dir.File("char-00000000dd02.json");
        string lifetimeXp = schemaVersion >= 2 ? "\"TotalXP\": 1600, \"JobsCompleted\": 40," : "";
        string career = schemaVersion >= 3 ? "\"CareerPath\": \"Civilian\"," : "";
        await File.WriteAllTextAsync(path, $$"""
            { "SchemaVersion": {{schemaVersion}}, "CharacterId": "char-00000000dd02", "Name": "Marisa Lowe",
              {{career}} "Level": 5, "XP": 100, {{lifetimeXp}} "Tier": {{tier}}, "Cash": 2500,
              "Strength": 5, "Intellect": 5, "Endurance": 5 }
            """);

        var loaded = await new JsonFilePlayerRepository(dir.Path).LoadAsync("char-00000000dd02");

        Assert.Equal(PlayerLoadStatus.Loaded, loaded.Status);
        Assert.Equal((tier, CareerPath.Civilian), (loaded.Player!.Tier, loaded.Player.CareerPath));
    }

    [Fact]
    public async Task PromotionThroughTheGame_IsSaved_AtEveryRank()
    {
        using var dir = new TempSaveDirectory();
        var repo = new JsonFilePlayerRepository(dir.Path);
        var game = TestJobs.CreateGame(repo);
        var player = Civilian(1, 1500, 0);
        player.Id = "char-00000000dd03";

        foreach (var (tier, cash, fee) in new[] { (2, 1000, 500), (3, 5000, 2000), (4, 15000, 5000), (5, 50000, 10000) })
        {
            player.Cash = cash + fee;
            await repo.SaveAsync(player);

            var outcome = await game.PromoteAsync(player.Id);

            Assert.True(outcome.Result!.Success, outcome.Result.Message);
            player = (await new JsonFilePlayerRepository(dir.Path).LoadAsync(player.Id)).Player!;
            Assert.Equal((tier, (float)cash), (player.Tier, player.Cash));
        }
    }
}
