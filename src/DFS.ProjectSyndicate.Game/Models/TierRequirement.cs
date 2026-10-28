namespace DFS.ProjectSyndicate.Models
{
	// One rank in a career's progression and what promotion to it requires. Immutable: the rank tables in
	// CareerProgressions are shared by every caller, so nothing may change a row once it is defined.
	public sealed record TierRequirement
	{
		public int TierLevel { get; init; }
		public int XPNeeded { get; init; }
		public int CashNeeded { get; init; }
		public int PromotionCost { get; init; }
		public string TierName { get; init; } = string.Empty;
	}
}
