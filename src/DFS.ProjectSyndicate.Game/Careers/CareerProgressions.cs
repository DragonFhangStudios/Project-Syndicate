using System.Collections.Frozen;
using System.Collections.Immutable;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Careers
{
	// One career's ranks, in order: tier n is Ranks[n - 1]. Immutable, and only CareerProgressions creates them.
	public sealed class CareerRankTable
	{
		internal CareerRankTable(CareerPath career, ImmutableArray<TierRequirement> ranks)
		{
			Career = career;
			Ranks = ranks;
		}

		public CareerPath Career { get; }
		public ImmutableArray<TierRequirement> Ranks { get; }
		public int MaxTier => Ranks.Length;

		public bool IsValidTier(int tier) => tier >= 1 && tier <= MaxTier;

		// The rank at a tier, or null outside 1..MaxTier.
		public TierRequirement? Rank(int tier) => IsValidTier(tier) ? Ranks[tier - 1] : null;
	}

	// The one place career ranks are defined. A career without an entry has no progression yet: its characters
	// can't be promoted and its jobs can't require a tier. A table may exist before its career is playable
	// (CareerInfo.IsPlayable), but a playable career must have one.
	public static class CareerProgressions
	{
		private static readonly FrozenDictionary<CareerPath, CareerRankTable> Tables = new Dictionary<CareerPath, CareerRankTable>
		{
			[CareerPath.Civilian] = new(CareerPath.Civilian,
			[
				new TierRequirement { TierLevel = 1, TierName = "New Comer", XPNeeded = 0, CashNeeded = 0, PromotionCost = 0 },
				new TierRequirement { TierLevel = 2, TierName = "Worker", XPNeeded = 150, CashNeeded = 1000, PromotionCost = 500 },
				new TierRequirement { TierLevel = 3, TierName = "Professional", XPNeeded = 400, CashNeeded = 5000, PromotionCost = 2000 },
				new TierRequirement { TierLevel = 4, TierName = "Executive", XPNeeded = 800, CashNeeded = 15000, PromotionCost = 5000 },
				new TierRequirement { TierLevel = 5, TierName = "Business Tycoon", XPNeeded = 1500, CashNeeded = 50000, PromotionCost = 10000 }
			])
			// Criminal and Emergency Services have no ranks until their progression is approved.
		}.ToFrozenDictionary();

		// The career's ranks, or null when it has none configured (or isn't a career the game knows).
		public static CareerRankTable? For(CareerPath career) => Tables.GetValueOrDefault(career);

		public static bool IsConfigured(CareerPath career) => Tables.ContainsKey(career);
	}
}
