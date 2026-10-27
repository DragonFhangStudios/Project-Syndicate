using System.Collections.Generic;

namespace DFS.JobSystem.Core
{
	public class Job
	{
		public string Id { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public int Payout { get; set; }
		// Experience granted when the job is completed
		public int XPReward { get; set; }
		// Minimum character level and career tier to take the job; 0 or 1 means no requirement
		public int MinLevel { get; set; }
		public int MinTier { get; set; }
		public List<string> Tasks { get; set; } = new();
		// Career that owns the job ("Civilian", "Criminal", "EmergencyServices"); null means Civilian.
		// Kept as text here so this library needs no reference to the game; the game validates and interprets it.
		public string? Career { get; set; }
		// Who may take it: "Restricted" (the owning career only) or "Shared" (any career); null means Restricted.
		public string? Access { get; set; }
	}
}
