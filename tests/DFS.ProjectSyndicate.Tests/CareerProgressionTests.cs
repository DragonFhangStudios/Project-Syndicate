using System.Reflection;
using System.Runtime.CompilerServices;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Tests;

// Career-specific progression (Phase 6B): the rank registry, its immutability, rank lookups and promotion states.
public class CareerProgressionTests
{
    private static SyndicatePlayer Character(CareerPath career, int tier, int totalXp = 0, float cash = 0)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.CareerPath = career;
        player.AddXP(totalXp);
        player.Tier = tier;
        player.Cash = cash;
        return player;
    }

    // ---- The registry ----

    [Fact]
    public void CivilianRanks_AreExactlyTheFiveExistingRanks()
    {
        var ranks = CareerProgressions.For(CareerPath.Civilian)!;

        Assert.Equal(CareerPath.Civilian, ranks.Career);
        Assert.Equal<TierRequirement[]>(
        [
            new TierRequirement { TierLevel = 1, TierName = "New Comer", XPNeeded = 0, CashNeeded = 0, PromotionCost = 0 },
            new TierRequirement { TierLevel = 2, TierName = "Worker", XPNeeded = 150, CashNeeded = 1000, PromotionCost = 500 },
            new TierRequirement { TierLevel = 3, TierName = "Professional", XPNeeded = 400, CashNeeded = 5000, PromotionCost = 2000 },
            new TierRequirement { TierLevel = 4, TierName = "Executive", XPNeeded = 800, CashNeeded = 15000, PromotionCost = 5000 },
            new TierRequirement { TierLevel = 5, TierName = "Business Tycoon", XPNeeded = 1500, CashNeeded = 50000, PromotionCost = 10000 }
        ], ranks.Ranks.ToArray());
        Assert.Equal(ProgressionManager.MaxTier, ranks.MaxTier);
    }

    [Theory]
    [InlineData(CareerPath.Criminal)]
    [InlineData(CareerPath.EmergencyServices)]
    [InlineData((CareerPath)7)]
    public void OtherCareers_HaveNoRanks(CareerPath career)
    {
        Assert.Null(CareerProgressions.For(career));
        Assert.False(CareerProgressions.IsConfigured(career));
        Assert.Null(ProgressionManager.MaxTierFor(career));
        Assert.Null(ProgressionManager.GetTierName(career, 1));
    }

    [Fact]
    public void EveryPlayableCareer_HasRanks()
    {
        Assert.All(Enum.GetValues<CareerPath>().Where(CareerInfo.IsPlayable), c => Assert.True(CareerProgressions.IsConfigured(c), c.ToString()));
    }

    [Fact]
    public void EveryConfiguredTable_IsWellFormed()
    {
        foreach (var career in Enum.GetValues<CareerPath>().Where(CareerProgressions.IsConfigured))
        {
            var ranks = CareerProgressions.For(career)!.Ranks;
            Assert.Equal(Enumerable.Range(1, ranks.Length), ranks.Select(r => r.TierLevel));
            Assert.Equal((0, 0, 0), (ranks[0].XPNeeded, ranks[0].CashNeeded, ranks[0].PromotionCost));
            Assert.All(ranks, r => Assert.False(string.IsNullOrWhiteSpace(r.TierName)));
            Assert.Equal(ranks.Length, ranks.Select(r => r.TierName).Distinct().Count());
            for (int i = 1; i < ranks.Length; i++)
            {
                Assert.True(ranks[i].XPNeeded >= ranks[i - 1].XPNeeded, $"{career} tier {i + 1} XP");
                Assert.True(ranks[i].CashNeeded >= ranks[i - 1].CashNeeded, $"{career} tier {i + 1} cash");
                Assert.True(ranks[i].PromotionCost >= ranks[i - 1].PromotionCost, $"{career} tier {i + 1} fee");
            }
        }
    }

    // ---- Nothing outside the registry can change a rank ----

    private static bool IsInitOnly(PropertyInfo property) =>
        property.SetMethod is null
        || property.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    [Fact]
    public void RankRows_HaveNoSettableProperties()
    {
        Assert.All(typeof(TierRequirement).GetProperties(), p => Assert.True(IsInitOnly(p), p.Name));
    }

    [Fact]
    public void RankTables_CannotBeChangedOrCreatedOutsideTheRegistry()
    {
        IList<TierRequirement> ranks = CareerProgressions.For(CareerPath.Civilian)!.Ranks;

        Assert.True(ranks.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ranks[1] = ranks[0]);
        Assert.Empty(typeof(CareerRankTable).GetConstructors());
        Assert.All(typeof(CareerRankTable).GetProperties(), p => Assert.Null(p.SetMethod));
    }

    [Fact]
    public void PromotionStatus_CannotBeAlteredByCallers()
    {
        Assert.Empty(typeof(PromotionStatus).GetConstructors());
        Assert.All(typeof(PromotionStatus).GetProperties(), p => Assert.True(p.SetMethod is null || !p.SetMethod.IsPublic, p.Name));
    }

    // ---- Career-aware lookups ----

    [Fact]
    public void CivilianRankNames_ByCareer_MatchTheLegacyLookup()
    {
        Assert.Equal(
            ["New Comer", "Worker", "Professional", "Executive", "Business Tycoon"],
            Enumerable.Range(1, 5).Select(t => ProgressionManager.GetTierName(CareerPath.Civilian, t)));
        Assert.Null(ProgressionManager.GetTierName(CareerPath.Civilian, 0));
        Assert.Null(ProgressionManager.GetTierName(CareerPath.Civilian, 6));
        Assert.Equal(5, ProgressionManager.MaxTierFor(CareerPath.Civilian));
    }

    // ---- Promotion states ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void CivilianBelowTheTop_IsNotMet_OrReady(int tier)
    {
        var next = CareerProgressions.For(CareerPath.Civilian)!.Rank(tier + 1)!;

        var shortOf = PromotionStatus.For(Character(CareerPath.Civilian, tier, next.XPNeeded, next.CashNeeded + next.PromotionCost - 1));
        var ready = PromotionStatus.For(Character(CareerPath.Civilian, tier, next.XPNeeded, next.CashNeeded + next.PromotionCost));

        Assert.Equal((PromotionAvailability.RequirementsNotMet, next, false, null), (shortOf.Availability, shortOf.Next, shortOf.CanPromote, shortOf.Problem));
        Assert.Equal((PromotionAvailability.Ready, next, true, null), (ready.Availability, ready.Next, ready.CanPromote, ready.Problem));
    }

    [Fact]
    public void CivilianAtTheTop_IsMaxRankReached()
    {
        var status = PromotionStatus.For(Character(CareerPath.Civilian, 5, 100000, 1_000_000));

        Assert.Equal((PromotionAvailability.MaxRankReached, (TierRequirement?)null, false, (string?)null),
            (status.Availability, status.Next, status.CanPromote, status.Problem));
    }

    [Theory]
    [InlineData(CareerPath.Criminal, 1)]
    [InlineData(CareerPath.Criminal, 5)]
    [InlineData(CareerPath.Criminal, 0)]
    [InlineData(CareerPath.EmergencyServices, 1)]
    [InlineData(CareerPath.EmergencyServices, 5)]
    public void UnconfiguredCareer_IsNeverMistakenForTheTopRank(CareerPath career, int tier)
    {
        var player = Character(career, tier, 100000, 1_000_000);

        var status = PromotionStatus.For(player);

        Assert.Equal(PromotionAvailability.CareerNotConfigured, status.Availability);
        Assert.Equal($"Promotion isn't available for the {CareerInfo.DisplayName(career)} career yet.", status.Problem);
        Assert.Equal($"Promotion isn't available for the {CareerInfo.DisplayName(career)} career yet.", ProgressionManager.GetPromotionRequirements(player));
        Assert.Null(ProgressionManager.GetNextTierRequirement(player));
    }

    [Fact]
    public void UnknownCareer_IsItsOwnState()
    {
        var player = Character((CareerPath)7, 1, 100000, 1_000_000);

        var status = PromotionStatus.For(player);

        Assert.Equal((PromotionAvailability.UnknownCareer, "This character's career isn't recognized."), (status.Availability, status.Problem));
        Assert.False(ProgressionManager.TryPromote(player));
        Assert.Equal("This character's career isn't recognized.", new CareerService(TestJobs.RealCatalog()).Promote(player).Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(99)]
    public void ImpossibleCivilianTier_IsInvalid_AndNeverPromotes(int tier)
    {
        var player = Character(CareerPath.Civilian, tier, 100000, 1_000_000);

        var status = PromotionStatus.For(player);

        Assert.Equal(PromotionAvailability.InvalidTier, status.Availability);
        Assert.Equal($"Tier {tier} isn't a valid Civilian rank.", status.Problem);
        Assert.Equal((null, 0, 0f, false, false), (status.Next, status.XpRemaining, status.CashRemaining, status.XpMet, status.CashMet));
        Assert.Equal($"Tier {tier} isn't a valid Civilian rank.", ProgressionManager.GetPromotionRequirements(player));

        Assert.False(ProgressionManager.CanPromote(player));
        Assert.False(ProgressionManager.TryPromote(player));
        var result = new CareerService(TestJobs.RealCatalog()).Promote(player);
        Assert.Equal((false, $"Tier {tier} isn't a valid Civilian rank."), (result.Success, result.Message));
        Assert.Equal((tier, 1_000_000f), (player.Tier, player.Cash));
    }

    [Fact]
    public void TierZero_NoLongerPromotesForFree()
    {
        // Before 6B, tier 0 looked up the free tier 1 row and "promoted" to New Comer.
        var player = Character(CareerPath.Civilian, 0, 0, 0);

        Assert.False(ProgressionManager.TryPromote(player));
        Assert.Equal(0, player.Tier);
    }
}
