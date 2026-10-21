using DFS.JobSystem.Core;
using DFS.JobSystem.Data;
using DFS.JobSystem.Managers;

namespace DFS.ProjectSyndicate.Jobs
{
	public interface IJobCatalog
	{
		IReadOnlyList<Job> All { get; }
		Job? Find(string jobId);
	}

	// Job definitions loaded once from DFS.JobSystem's SimpleJobs.json.
	public sealed class JobCatalog : IJobCatalog
	{
		private readonly JobManager _manager = new();

		// Resolved from the app's own folder, not the working directory.
		public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "SimpleJobs.json");

		public JobCatalog(string jsonPath)
		{
			// JobLoader silently ignores a missing file; fail loudly instead so the app never starts with no jobs.
			if (!File.Exists(jsonPath)) throw new FileNotFoundException("Job data file not found.", jsonPath);

			JobLoader.LoadAndRegisterJobs(_manager, jsonPath);
			All = _manager.GetAllJobs().ToList();
			if (All.Count == 0) throw new InvalidDataException($"No jobs were loaded from '{jsonPath}'.");
		}

		public IReadOnlyList<Job> All { get; }

		public Job? Find(string jobId) => string.IsNullOrEmpty(jobId) ? null : _manager.GetJob(jobId);
	}
}
