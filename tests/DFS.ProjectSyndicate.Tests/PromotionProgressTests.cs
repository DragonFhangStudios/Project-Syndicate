using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Tests;

public class PromotionProgressTests
{
    private static SyndicatePlayer Player(int tier, int totalXp, float cash)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(totalXp);
        player.Tier = tier;
        player.Cash = cash;
        return player;
    }

    [Fact]
    public void NewCharacter_IsShownEverythingStillNeededForWorker()
    {
        var progress = PromotionProgress.For(Player(1, 0, 500))!;

        Assert.Equal((2, 150, 1000f, 1500f), (progress.Next.TierLevel, progress.XpRemaining, progress.CashRemaining, progress.CashOnHandNeeded));
        Assert.False(progress.XpMet || progress.CashMet || progress.CanPromote);
    }

    [Fact]
    public void XpMet_ButCashShort_ShowsOnlyTheCashGap()
    {
        var progress = PromotionProgress.For(Player(2, 650, 6800))!;

        Assert.Equal((0, 200f), (progress.XpRemaining, progress.CashRemaining));
        Assert.True(progress.XpMet);
        Assert.False(progress.CanPromote);
    }

    [Fact]
    public void CashMet_ButXpShort_ShowsOnlyTheXpGap()
    {
        var progress = PromotionProgress.For(Player(2, 330, 9000))!;

        Assert.Equal((70, 0f), (progress.XpRemaining, progress.CashRemaining));
        Assert.True(progress.CashMet);
        Assert.False(progress.CanPromote);
    }

    [Theory]
    [InlineData(1, 150, 1500, 1000)]
    [InlineData(2, 400, 7000, 5000)]
    [InlineData(2, 900, 7100, 5100)]
    [InlineData(4, 1500, 60000, 50000)]
    public void Qualified_AgreesWithTheRules_AndShowsCashAfterTheFee(int tier, int totalXp, float cash, float after)
    {
        var player = Player(tier, totalXp, cash);
        var progress = PromotionProgress.For(player)!;

        Assert.True(progress.CanPromote);
        Assert.True(ProgressionManager.CanPromote(player));
        Assert.Equal(after, progress.CashAfterPromotion);

        Assert.True(ProgressionManager.TryPromote(player));
        Assert.Equal(after, player.Cash);
    }

    [Theory]
    [InlineData(1, 149, 1500)]
    [InlineData(1, 150, 1499)]
    [InlineData(2, 399, 7000)]
    [InlineData(2, 400, 6999)]
    public void JustShort_AgreesWithTheRules(int tier, int totalXp, float cash)
    {
        var player = Player(tier, totalXp, cash);

        Assert.False(PromotionProgress.For(player)!.CanPromote);
        Assert.False(ProgressionManager.CanPromote(player));
    }

    [Fact]
    public void MaxTier_HasNoProgress()
    {
        Assert.Null(PromotionProgress.For(Player(ProgressionManager.MaxTier, 5000, 100000)));
    }
}
