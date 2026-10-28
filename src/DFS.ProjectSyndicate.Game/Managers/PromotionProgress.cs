using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Managers
{
	// How far a character is from their next rank: lifetime XP, and cash on hand covering the requirement plus the
	// promotion fee. A view of PromotionStatus for the existing dashboard.
	public sealed record PromotionProgress(TierRequirement Next, int XpRemaining, float CashRemaining, float CashAfterPromotion)
	{
		// Cash the character must hold to be promoted: the requirement plus the fee, which is deducted.
		public float CashOnHandNeeded => Next.CashNeeded + Next.PromotionCost;

		public bool XpMet => XpRemaining == 0;
		public bool CashMet => CashRemaining == 0;
		public bool CanPromote => XpMet && CashMet;

		// Null when there is no next rank to work towards. Kept for the dashboard until it moves to PromotionStatus,
		// which tells those cases apart.
		public static PromotionProgress? For(SyndicatePlayer player)
		{
			var status = Careers.PromotionStatus.For(player);
			if (status.Next is null) return null;

			return new PromotionProgress(status.Next, status.XpRemaining, status.CashRemaining, status.CashAfterPromotion);
		}
	}
}
