using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Careers
{
	public enum PromotionAvailability
	{
		// The character's career isn't one the game defines.
		UnknownCareer,
		// A real career with no ranks configured yet (Criminal and Emergency Services for now).
		CareerNotConfigured,
		// The character's tier is outside its career's ranks.
		InvalidTier,
		// At the top rank of the character's own career.
		MaxRankReached,
		// A next rank exists, but lifetime XP or cash on hand falls short.
		RequirementsNotMet,
		// A next rank exists and every requirement is met.
		Ready
	}

	// Where a character stands on promotion, judged against their own career's ranks (CareerProgressions).
	// "No next rank" is never ambiguous: a career without ranks, an impossible tier and the top rank are separate
	// states. Next and the figures are only set when a next rank exists (RequirementsNotMet and Ready).
	public sealed record PromotionStatus
	{
		private PromotionStatus(PromotionAvailability availability, CareerPath career, int tier)
		{
			Availability = availability;
			Career = career;
			Tier = tier;
		}

		public PromotionAvailability Availability { get; }
		public CareerPath Career { get; }
		public int Tier { get; }

		// The next rank and what is still missing for it; null and zero unless a next rank exists.
		public TierRequirement? Next { get; private init; }
		public int XpRemaining { get; private init; }
		public float CashRemaining { get; private init; }
		// Cash the character must hold to be promoted: the requirement plus the fee, which is deducted.
		public float CashOnHandNeeded { get; private init; }
		public float CashAfterPromotion { get; private init; }

		public bool XpMet => Next is not null && XpRemaining == 0;
		public bool CashMet => Next is not null && CashRemaining == 0;
		public bool CanPromote => Availability == PromotionAvailability.Ready;

		// Why promotion can't be considered at all, for the states that aren't about requirements; otherwise null.
		public string? Problem => Availability switch
		{
			PromotionAvailability.UnknownCareer => CareerStanding.UnknownCareerMessage,
			PromotionAvailability.CareerNotConfigured => $"Promotion isn't available for the {CareerInfo.DisplayName(Career)} career yet.",
			PromotionAvailability.InvalidTier => CareerStanding.InvalidTierMessage(Career, Tier),
			_ => null
		};

		public static PromotionStatus For(SyndicatePlayer player)
		{
			var career = player.CareerPath;
			int tier = player.Tier;

			if (!Enum.IsDefined(career)) return new(PromotionAvailability.UnknownCareer, career, tier);

			var ranks = CareerProgressions.For(career);
			if (ranks is null) return new(PromotionAvailability.CareerNotConfigured, career, tier);
			if (!ranks.IsValidTier(tier)) return new(PromotionAvailability.InvalidTier, career, tier);

			var next = ranks.Rank(tier + 1);
			if (next is null) return new(PromotionAvailability.MaxRankReached, career, tier);

			// Lifetime XP, not level progress: level-ups reduce XP but never TotalXP.
			float cashOnHandNeeded = next.CashNeeded + next.PromotionCost;
			int xpRemaining = Math.Max(0, next.XPNeeded - player.TotalXP);
			float cashRemaining = Math.Max(0, cashOnHandNeeded - player.Cash);
			bool ready = xpRemaining == 0 && cashRemaining == 0;

			return new(ready ? PromotionAvailability.Ready : PromotionAvailability.RequirementsNotMet, career, tier)
			{
				Next = next,
				XpRemaining = xpRemaining,
				CashRemaining = cashRemaining,
				CashOnHandNeeded = cashOnHandNeeded,
				CashAfterPromotion = player.Cash - next.PromotionCost
			};
		}
	}
}
