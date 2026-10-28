using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Managers
{
	// Promotion rules, in the form the web game and the legacy WPF app call them. Every method that takes a player
	// judges them against their own career's ranks (CareerProgressions, through PromotionStatus); this class holds
	// no tables of its own. The tier-only members are Civilian aliases, kept so existing callers, including the
	// WPF app, keep their exact output.
	public static class ProgressionManager
	{
		// Civilian's top tier. For any career, use MaxTierFor.
		public const int MaxTier = 5;

		// Civilian rank name for a tier, or "Unknown" outside 1-5. For any career, use GetTierName(career, tier).
		public static string GetTierName(int tier) => GetTierName(CareerPath.Civilian, tier) ?? "Unknown";

		// A career's rank name for a tier; null when the career has no ranks or the tier is outside them.
		public static string? GetTierName(CareerPath career, int tier) => CareerProgressions.For(career)?.Rank(tier)?.TierName;

		// A career's top tier; null when the career has no ranks.
		public static int? MaxTierFor(CareerPath career) => CareerProgressions.For(career)?.MaxTier;

		// Requirements for the player's next rank in their career; null when there is none to work towards
		// (top rank, no ranks configured, or an impossible tier).
		public static TierRequirement? GetNextTierRequirement(SyndicatePlayer player) => PromotionStatus.For(player).Next;

		public static bool CanPromote(SyndicatePlayer player) => PromotionStatus.For(player).CanPromote;

		public static string GetPromotionRequirements(SyndicatePlayer player)
		{
			var status = PromotionStatus.For(player);
			if (status.Availability == PromotionAvailability.MaxRankReached) return "⭐ Maximum Tier Reached!";

			var req = status.Next;
			if (req is null) return status.Problem!;

			return $"Promotion to {req.TierName} Requirements:\n" +
				   $"• Lifetime XP: {req.XPNeeded} (You have: {player.TotalXP})\n" +
				   $"• Total Cash: ${req.CashNeeded:N0} (You have: ${player.Cash:N0})\n" +
				   $"• Promotion Fee: ${req.PromotionCost:N0}";
		}

		public static bool TryPromote(SyndicatePlayer player)
		{
			var status = PromotionStatus.For(player);
			if (!status.CanPromote) return false;

			player.Cash -= status.Next!.PromotionCost;
			player.Tier = status.Next.TierLevel;
			return true;
		}
	}
}
