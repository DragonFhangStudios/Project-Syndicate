using DFS.JobSystem.Data;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Commands;
using DFS.ProjectSyndicate.Core;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Services;
using System.ComponentModel;
using System.Windows.Input;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class DashboardViewModel : ObservableObject
	{
		public SyndicatePlayer Player => GameSession.CurrentPlayer;
		private readonly JobManager _jobManager;
        private readonly IDialogService _dialogService;

		public DashboardViewModel()
		{
			_jobManager = new JobManager();
            _dialogService = new DialogService(); // Ideally injected
			JobLoader.LoadAndRegisterJobs(_jobManager, "Data/SimpleJobs.json");

            if (Player != null)
            {
                Player.PropertyChanged += OnPlayerPropertyChanged;
            }

            PromoteCommand = new RelayCommand(AttemptPromotion);
            DebugAddXPCommand = new RelayCommand(() => DebugAddXP(200));
            DebugAddCashCommand = new RelayCommand(() => DebugAddCash(1000));
		}

        public void Cleanup()
        {
            if (Player != null)
            {
                Player.PropertyChanged -= OnPlayerPropertyChanged;
            }
        }

        private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Refresh computed properties when Player changes
            OnPropertyChanged(nameof(RankDisplay));
            OnPropertyChanged(nameof(XPDisplay));
            OnPropertyChanged(nameof(LevelDisplay));
            OnPropertyChanged(nameof(CanPromote));
            OnPropertyChanged(nameof(PromotionInfo));
            OnPropertyChanged(nameof(JobEarnings));
            OnPropertyChanged(nameof(CurrentJobTitle));
        }

		// Display Properties
		public string RankDisplay => $"Tier {Player.Tier}: {ProgressionManager.GetTierName(Player.Tier)}";
		public string XPDisplay => $"XP: {Player.XP}";
		public string LevelDisplay => $"Level: {Player.Level}";

		public string CurrentJobTitle => string.IsNullOrWhiteSpace(Player.JobData.AssignedJobId)
			? "None Assigned"
			: $"Working: {_jobManager.GetJob(Player.JobData.AssignedJobId)?.Title ?? "Unknown"}";

		public string JobEarnings => $"Total Earned: ${Player.JobData.TotalEarned}";

		// Promotion Properties
		public bool CanPromote => ProgressionManager.CanPromote(Player);
		public string PromotionInfo => ProgressionManager.GetPromotionRequirements(Player);

		// Commands
		public ICommand PromoteCommand { get; }
		public ICommand DebugAddXPCommand { get; }
		public ICommand DebugAddCashCommand { get; }

		private void AttemptPromotion()
		{
			if (ProgressionManager.TryPromote(Player))
			{
                _dialogService.ShowMessage(
					$"🎉 Congratulations! You've been promoted to Tier {Player.Tier}: {ProgressionManager.GetTierName(Player.Tier)}!",
					"Promotion Success"
				);
                // No need to manually refresh, PropertyChanged event handles it
			}
		}

		// DEBUG METHODS (Remove in production)
		private void DebugAddXP(int amount)
		{
			Player.AddXP(amount);
		}

		private void DebugAddCash(int amount)
		{
			Player.Cash += amount;
		}
	}
}
