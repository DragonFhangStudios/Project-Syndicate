namespace DFS.ProjectSyndicate.Models
{
	public class PlayerJobData
	{
		public string? AssignedJobId { get; set; }
		public List<string> CompletedTasks { get; set; } = new();
		public int TotalEarned { get; set; } = 0;

		// Optional: Add timestamp or job history later
	}
}
