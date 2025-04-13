using System.Collections.Generic;

namespace DFS.JobSystem.Core
{
	public class Job
	{
		public string Id { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public int Payout { get; set; }
		public List<string> Tasks { get; set; } = new();
	}
}
