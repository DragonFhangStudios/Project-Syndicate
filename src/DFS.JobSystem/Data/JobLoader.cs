using DFS.JobSystem.Core;
using DFS.JobSystem.Managers;

namespace DFS.JobSystem.Data
{
	public static class JobLoader
	{
		public static void RegisterAllJobs(JobManager manager)
		{
			manager.RegisterJob(DeliveryJob.Create());

			// Future jobs: manager.RegisterJob(FirefighterJob.Create());
		}
	}
}
