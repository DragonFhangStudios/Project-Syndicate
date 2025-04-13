namespace DFS.ProjectSyndicate.Models
{
	public class PlayerJobData
	{
		// The unique Job ID assigned to this player
		public string AssignedJobId { get; set; } = string.Empty;

		// Tracks which task they're on in the active job
		public int CurrentTaskIndex { get; set; } = 0;

		// Total money earned from jobs
		public int TotalEarned { get; set; } = 0;
	}
}
