namespace DFS.JobSystem.Core
{
	public class JobTask
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public int Reward { get; set; } // payout in $ or points

		public bool IsCompleted { get; set; } = false;

		public JobTask(string name, string description, int reward)
		{
			Name = name;
			Description = description;
			Reward = reward;
		}
	}
}
