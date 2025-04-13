using DFS.JobSystem.Core;
using DFS.JobSystem.Managers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DFS.JobSystem.Data
{
	public static class JobLoader
	{
		public static void LoadAndRegisterJobs(JobManager manager, string filePath)
		{
			if (!File.Exists(filePath))
				return;

			var json = File.ReadAllText(filePath);
			var jobs = JsonSerializer.Deserialize<List<Job>>(json);
			if (jobs == null) return;

			foreach (var job in jobs)
			{
				manager.Register(job);
			}
		}
	}
}
