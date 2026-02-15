using System.Collections.ObjectModel;
using DFS.JobSystem.Core;
using DFS.JobSystem.Data;
using DFS.ProjectSyndicate.Models;
using System.Windows.Input;
using DFS.ProjectSyndicate.Commands;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Core;
using DFS.ProjectSyndicate.Services;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class JobSelectorViewModel : ObservableObject
	{
		private readonly JobManager _jobManager;
        private readonly IDialogService _dialogService;

        private ObservableCollection<Job> _availableJobs;
        public ObservableCollection<Job> AvailableJobs
        {
            get => _availableJobs;
            set => SetProperty(ref _availableJobs, value);
        }

        private Job? _selectedJob;
		public Job? SelectedJob
        {
            get => _selectedJob;
            set => SetProperty(ref _selectedJob, value);
        }

		public ICommand AssignJobCommand { get; }

		public JobSelectorViewModel()
		{
			_jobManager = new JobManager();
            _dialogService = new DialogService(); // Ideally injected
			JobLoader.LoadAndRegisterJobs(_jobManager, "Data/SimpleJobs.json");

			AvailableJobs = new ObservableCollection<Job>(_jobManager.GetAllJobs());
			AssignJobCommand = new RelayCommand(AssignJob);
		}

		private void AssignJob()
		{
			if (SelectedJob == null) return;
            if (GameSession.CurrentPlayer == null) return;

			GameSession.CurrentPlayer.JobData.AssignedJobId = SelectedJob.Id;
			GameSession.CurrentPlayer.JobData.CurrentTaskIndex = 0;

			// Confirmation popup
            _dialogService.ShowMessage(
				$"✅ You’ve accepted the job: {SelectedJob.Title}\n\nGet ready to get to work!",
				"Job Assigned"
			);
		}
	}
}
