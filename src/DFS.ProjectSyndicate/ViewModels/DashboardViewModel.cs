using DFS.JobSystem.Core;
using DFS.JobSystem.Data;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class DashboardViewModel
	{
#pragma warning disable CS8603
		public SyndicatePlayer Player => GameSession.CurrentPlayer;
#pragma warning restore CS8603
		private readonly JobManager _jobManager;

		public DashboardViewModel()
		{			
			_jobManager = new JobManager();
			JobLoader.LoadAndRegisterJobs(_jobManager, "Data/SimpleJobs.json");
		}

		public string RankDisplay => "Rank: Godfather";
		public string CurrentJobTitle => string.IsNullOrWhiteSpace(Player.JobData.AssignedJobId)
				? "None Assigned"
				: $"Working: {_jobManager.GetJob(Player.JobData.AssignedJobId)?.Title ?? "Unknown"}";

		public string JobEarnings =>
			$"Earnings: ${Player.JobData.TotalEarned}";
	}
}
