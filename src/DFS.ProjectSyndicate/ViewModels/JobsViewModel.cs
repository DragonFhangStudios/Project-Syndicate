using System.Collections.ObjectModel;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.JobSystem.Data;
using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.ViewModels;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class JobsViewModel
	{
		public string JobTitle { get; set; } = "No Job Assigned";
		public string JobDescription { get; set; } = "";
		public ObservableCollection<string> JobTasks { get; set; } = new();
		public string TotalPayout { get; set; } = "";

		private readonly SyndicatePlayer _player;
		private readonly JobManager _jobManager;

		public JobsViewModel()
		{
			_player = GameSession.CurrentPlayer;
			_jobManager = new JobManager();
			JobLoader.RegisterAllJobs(_jobManager);

			LoadActiveJob();
		}

		private void LoadActiveJob()
		{
			if (string.IsNullOrWhiteSpace(_player.JobData.AssignedJobId))
				return;

			var job = _jobManager.GetJob(_player.JobData.AssignedJobId);
			if (job == null)
				return;

			JobTitle = job.Title;
			JobDescription = job.Description;

			int total = 0;

			foreach (var task in job.Tasks)
			{
				string taskText = $"{task.Name} - ${task.Reward}";
				JobTasks.Add(taskText);
				total += task.Reward;
			}

			TotalPayout = $"Potential Earnings: ${total}";
		}
	}
}
