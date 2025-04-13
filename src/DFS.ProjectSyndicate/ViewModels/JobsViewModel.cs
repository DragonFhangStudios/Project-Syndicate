using System.Collections.ObjectModel;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.JobSystem.Data;
using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.ViewModels;
using System.Windows.Input;
using DFS.ProjectSyndicate.Services;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class JobsViewModel
	{
		public string JobTitle { get; set; } = "No Job Assigned";
		public string JobDescription { get; set; } = "";
		public ObservableCollection<string> JobTasks { get; set; } = new();
		public string TotalPayout { get; set; } = "";
		public bool HasJob => !string.IsNullOrWhiteSpace(_player.JobData.AssignedJobId);


		private readonly SyndicatePlayer _player;
		private readonly JobManager _jobManager;
		public ICommand CompleteTaskCommand { get; }

		private int _currentTaskIndex = 0;

		public JobsViewModel()
		{
			_player = GameSession.CurrentPlayer;
			_jobManager = new JobManager();
			JobLoader.RegisterAllJobs(_jobManager);

			LoadActiveJob();
			
			CompleteTaskCommand = new RelayCommand(CompleteCurrentJobTask);
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
		private void CompleteCurrentJobTask()
		{
			if (string.IsNullOrWhiteSpace(_player.JobData.AssignedJobId))
				return;

			var job = _jobManager.GetJob(_player.JobData.AssignedJobId);
			if (job == null || _currentTaskIndex >= job.Tasks.Count)
				return;

			var task = job.Tasks[_currentTaskIndex];

			_player.JobData.CompletedTasks.Add(task.Name);
			_player.JobData.TotalEarned += task.Reward;
			_currentTaskIndex++;

			JobTasks.Clear();
			foreach (var t in job.Tasks)
			{
				bool done = _player.JobData.CompletedTasks.Contains(t.Name);
				JobTasks.Add($"{(done ? "✔️" : "🕓")} {t.Name} - ${t.Reward}");
			}

			TotalPayout = $"Earnings: ${_player.JobData.TotalEarned}";

			// Optional: save state
			JsonSaveManager.Save(_player);
		}
	}
}
