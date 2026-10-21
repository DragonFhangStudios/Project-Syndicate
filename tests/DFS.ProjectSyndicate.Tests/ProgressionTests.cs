using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Tests;

public class ProgressionTests
{
    private static SyndicatePlayer NewPlayer(string id = "sonny-perelli") => DemoCharacters.Find(id)!.CreatePlayer();

    [Fact]
    public void AddXP_LevelUp_ResetsLevelProgress_ButKeepsLifetimeTotal()
    {
        var player = NewPlayer();

        int levels = player.AddXP(120);

        Assert.Equal(1, levels);
        Assert.Equal(2, player.Level);
        Assert.Equal(20, player.XP);
        Assert.Equal(120, player.TotalXP);
    }

    [Fact]
    public void AddXP_CanGainSeveralLevelsAtOnce()
    {
        var player = NewPlayer();

        int levels = player.AddXP(600); // 100 + 200 + 300

        Assert.Equal(3, levels);
        Assert.Equal(4, player.Level);
        Assert.Equal(0, player.XP);
        Assert.Equal(600, player.TotalXP);
    }

    [Fact]
    public void AddXP_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewPlayer().AddXP(-1));
    }

    [Fact]
    public void CanPromote_UsesLifetimeXp_NotLevelProgress()
    {
        var player = NewPlayer(); // $1,500: exactly Tier 2's $1,000 + $500 fee
        player.AddXP(160);        // level 2 with 60 XP progress, 160 lifetime

        Assert.Equal(60, player.XP);
        Assert.True(ProgressionManager.CanPromote(player));
    }

    [Fact]
    public void CanPromote_FalseWithoutEnoughXp()
    {
        var player = NewPlayer();
        player.AddXP(149);

        Assert.False(ProgressionManager.CanPromote(player));
    }

    [Fact]
    public void CanPromote_FalseWithoutEnoughCash()
    {
        var player = NewPlayer("carlo-valentini"); // $1,200 < $1,500
        player.AddXP(500);

        Assert.False(ProgressionManager.CanPromote(player));
    }

    [Fact]
    public void TryPromote_DeductsFeeAndRaisesTier()
    {
        var player = NewPlayer("giovanni-ragoli"); // $3,000
        player.AddXP(150);

        Assert.True(ProgressionManager.TryPromote(player));
        Assert.Equal(2, player.Tier);
        Assert.Equal(2500, player.Cash);
        Assert.Equal(150, player.TotalXP); // promotion does not spend XP
    }

    [Fact]
    public void MaxTier_HasNoNextRequirement()
    {
        var player = NewPlayer();
        player.Tier = ProgressionManager.MaxTier;

        Assert.Null(ProgressionManager.GetNextTierRequirement(player));
        Assert.False(ProgressionManager.CanPromote(player));
    }

    [Fact]
    public void FourStreetCleanups_EarnPromotionToWorker()
    {
        var careers = new CareerService(TestJobs.RealCatalog());
        var player = NewPlayer();

        for (int job = 0; job < 4; job++)
        {
            careers.AcceptJob(player, TestJobs.TrashCleanup);
            for (int task = 0; task < 4; task++) careers.CompleteCurrentTask(player);
        }

        Assert.Equal(160, player.TotalXP);
        Assert.Equal(2, player.Level);
        Assert.Equal(1520, player.Cash);

        var promotion = careers.Promote(player);

        Assert.True(promotion.Success);
        Assert.Equal(2, player.Tier);
        Assert.Equal("Worker", ProgressionManager.GetTierName(player.Tier));
        Assert.Equal(1020, player.Cash);
    }

    [Fact]
    public void Promote_WhenNotEligible_FailsWithoutChanges()
    {
        var careers = new CareerService(TestJobs.RealCatalog());
        var player = NewPlayer();

        var result = careers.Promote(player);

        Assert.False(result.Success);
        Assert.Equal(1, player.Tier);
        Assert.Equal(1500, player.Cash);
    }
}
