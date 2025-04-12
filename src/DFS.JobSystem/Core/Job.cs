using System.Collections.Generic;

namespace DFS.JobSystem.Core
{
	public class Job
	{
		public string Id { get; set; }  // Unique identifier
		public string Title { get; set; }
		public string Description { get; set; }
		public int Tier { get; set; }
		public List<JobTask> Tasks { get; set; } = new();

		public Job(string id, string title, string description, int tier)
		{
			Id = id;
			Title = title;
			Description = description;
			Tier = tier;
		}
	}
}
