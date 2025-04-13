using DFS.JobSystem.Core;
using System.Collections.Generic;

namespace DFS.JobSystem.Managers
{
	public class JobManager
	{
		private readonly Dictionary<string, Job> _jobs = new();

		public void Register(Job job)
		{
			if (!_jobs.ContainsKey(job.Id))
				_jobs.Add(job.Id, job);
		}

		public Job? GetJob(string id)
		{
			_jobs.TryGetValue(id, out var job);
			return job;
		}

		public IEnumerable<Job> GetAllJobs()
		{
			return _jobs.Values;
		}


		public void Clear()
		{
			_jobs.Clear();
		}
	}
}
