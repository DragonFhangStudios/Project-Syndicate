using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DFS.JobSystem.Core;
using DFS.JobSystem.Data;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Commands;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class JobsViewModel : INotifyPropertyChanged
	{
		private readonly SyndicatePlayer _player;
		private readonly JobManager _jobManager;

		public event PropertyChangedEventHandler? PropertyChanged;

		public ICommand CompleteTaskCommand { get; }

		public JobsViewModel()
		{
			_player = GameSession.CurrentPlayer;
			_jobManager = new JobManager();
			JobLoader.LoadAndRegisterJobs(_jobManager, "Data/SimpleJobs.json");

			CompleteTaskCommand = new RelayCommand(CompleteCurrentTask);
			UpdateJobInfo();
		}

		private string _jobTitle = "No active job assigned.";
		public string JobTitle
		{
			get => _jobTitle;
			set => SetField(ref _jobTitle, value);
		}

		private string _jobDescription = string.Empty;
		public string JobDescription
		{
			get => _jobDescription;
			set => SetField(ref _jobDescription, value);
		}

		private ObservableCollection<string> _taskList = new();
		public ObservableCollection<string> TaskList
		{
			get => _taskList;
			set => SetField(ref _taskList, value);
		}

		private int _currentTaskIndex;
		public int CurrentTaskIndex
		{
			get => _currentTaskIndex;
			set => SetField(ref _currentTaskIndex, value);
		}

		private bool _jobCompleted;
		public bool JobCompleted
		{
			get => _jobCompleted;
			set => SetField(ref _jobCompleted, value);
		}

		private void UpdateJobInfo()
		{
			if (string.IsNullOrWhiteSpace(_player.JobData.AssignedJobId))
			{
				JobTitle = "No active job assigned.";
				JobDescription = string.Empty;
				TaskList.Clear();
				CurrentTaskIndex = 0;
				return;
			}

			var job = _jobManager.GetJob(_player.JobData.AssignedJobId);
			if (job == null)
			{
				JobTitle = "Job not found.";
				return;
			}

			JobTitle = job.Title;
			JobDescription = job.Description;
			TaskList = new ObservableCollection<string>(job.Tasks);
			CurrentTaskIndex = _player.JobData.CurrentTaskIndex;
		}

		private void CompleteCurrentTask()
		{
			var job = _jobManager.GetJob(_player.JobData.AssignedJobId);
			if (job == null) return;

			if (_player.JobData.CurrentTaskIndex >= job.Tasks.Count)
				return; // All tasks already completed

			_player.JobData.CurrentTaskIndex++;

			if (_player.JobData.CurrentTaskIndex >= job.Tasks.Count)
			{
				_player.JobData.TotalEarned += job.Payout;
				JobCompleted = true;
				_player.JobData.AssignedJobId = string.Empty;
				_player.JobData.CurrentTaskIndex = 0;
			}

			UpdateJobInfo();
		}

		private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
			if (Equals(field, value)) return;
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
