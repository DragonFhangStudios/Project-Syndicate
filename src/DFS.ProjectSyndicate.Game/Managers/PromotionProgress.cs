using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Managers
{
	// How far a character is from their next rank, in the terms ProgressionManager.CanPromote uses:
	// lifetime XP, and cash on hand covering the requirement plus the promotion fee.
	public sealed record PromotionProgress(TierRequirement Next, int XpRemaining, float CashRemaining, float CashAfterPromotion)
	{
		// Cash the character must hold to be promoted: the requirement plus the fee, which is deducted.
		public float CashOnHandNeeded => Next.CashNeeded + Next.PromotionCost;

		public bool XpMet => XpRemaining == 0;
		public bool CashMet => CashRemaining == 0;
		public bool CanPromote => XpMet && CashMet;

		// Null at the maximum tier.
		public static PromotionProgress? For(SyndicatePlayer player)
		{
			var next = ProgressionManager.GetNextTierRequirement(player);
			if (next is null || player.Tier >= ProgressionManager.MaxTier) return null;

			float cashOnHandNeeded = next.CashNeeded + next.PromotionCost;
			return new PromotionProgress(
				next,
				XpRemaining: Math.Max(0, next.XPNeeded - player.TotalXP),
				CashRemaining: Math.Max(0, cashOnHandNeeded - player.Cash),
				CashAfterPromotion: player.Cash - next.PromotionCost);
		}
	}
}
