using DFS.JobSystem.Core;
using DFS.JobSystem.Data;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.ViewModels;

namespace DFS.ProjectSyndicate.Views
{
	public class DashboardViewModel
	{
		private readonly SyndicatePlayer _player;
		private readonly JobManager _jobManager;

		public DashboardViewModel()
		{
			_player = GameSession.CurrentPlayer;
			_jobManager = new JobManager();

			JobLoader.RegisterAllJobs(_jobManager);
		}

		public string CurrentJobTitle =>
			string.IsNullOrWhiteSpace(_player.JobData.AssignedJobId)
				? "None Assigned"
				: $"Working: {_jobManager.GetJob(_player.JobData.AssignedJobId)?.Title ?? "Unknown"}";

		public string JobEarnings =>
			$"Earnings: ${_player.JobData.TotalEarned}";
	}
}
