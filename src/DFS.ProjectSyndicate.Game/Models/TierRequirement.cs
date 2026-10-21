namespace DFS.ProjectSyndicate.Models
{
	public class TierRequirement
	{
		public int TierLevel { get; set; }
		public int XPNeeded { get; set; }
		public int CashNeeded { get; set; }
		public int PromotionCost { get; set; }
		public string TierName { get; set; } = string.Empty;
	}
}