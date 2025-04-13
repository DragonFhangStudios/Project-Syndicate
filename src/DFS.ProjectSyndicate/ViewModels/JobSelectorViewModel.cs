using System.Collections.ObjectModel;
using DFS.JobSystem.Core;
using DFS.JobSystem.Data;
using DFS.ProjectSyndicate.Models;
using System.Windows.Input;
using DFS.ProjectSyndicate.Commands;
using DFS.JobSystem.Managers;
using System.Windows;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class JobSelectorViewModel
	{
		private readonly JobManager _jobManager;
		public ObservableCollection<Job> AvailableJobs { get; set; }
		public Job? SelectedJob { get; set; }

		public ICommand AssignJobCommand { get; }

		public JobSelectorViewModel()
		{
			_jobManager = new JobManager();
			JobLoader.LoadAndRegisterJobs(_jobManager, "Data/SimpleJobs.json");

			AvailableJobs = new ObservableCollection<Job>(_jobManager.GetAllJobs());
			AssignJobCommand = new RelayCommand(AssignJob);
		}

		private void AssignJob()
		{
			if (SelectedJob == null) return;

			GameSession.CurrentPlayer.JobData.AssignedJobId = SelectedJob.Id;
			GameSession.CurrentPlayer.JobData.CurrentTaskIndex = 0;

			// Confirmation popup
			MessageBox.Show(
				$"✅ You’ve accepted the job: {SelectedJob.Title}\n\nGet ready to get to work!",
				"Job Assigned",
				MessageBoxButton.OK,
				MessageBoxImage.Information
			);
		}
	}
}
