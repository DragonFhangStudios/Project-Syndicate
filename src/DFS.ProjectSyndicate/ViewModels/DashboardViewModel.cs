using DFS.JobSystem.Data;
using DFS.JobSystem.Managers;
using DFS.ProjectSyndicate.Commands;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class DashboardViewModel : INotifyPropertyChanged
	{
		public SyndicatePlayer Player => GameSession.CurrentPlayer;
		private readonly JobManager _jobManager;

		public DashboardViewModel()
		{
			_jobManager = new JobManager();
			JobLoader.LoadAndRegisterJobs(_jobManager, "Data/SimpleJobs.json");
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
		public ICommand PromoteCommand => new RelayCommand(AttemptPromotion);
		public ICommand DebugAddXPCommand => new RelayCommand(() => DebugAddXP(200));
		public ICommand DebugAddCashCommand => new RelayCommand(() => DebugAddCash(1000));

		private void AttemptPromotion()
		{
			if (ProgressionManager.TryPromote(Player))
			{
				MessageBox.Show(
					$"🎉 Congratulations! You've been promoted to Tier {Player.Tier}: {ProgressionManager.GetTierName(Player.Tier)}!",
					"Promotion Success",
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
				RefreshAll();
			}
		}

		// DEBUG METHODS (Remove in production)
		private void DebugAddXP(int amount)
		{
			Player.AddXP(amount);
			RefreshAll();
		}

		private void DebugAddCash(int amount)
		{
			Player.Cash += amount;
			RefreshAll();
		}

		private void RefreshAll()
		{
			OnPropertyChanged(nameof(RankDisplay));
			OnPropertyChanged(nameof(XPDisplay));
			OnPropertyChanged(nameof(LevelDisplay));
			OnPropertyChanged(nameof(CanPromote));
			OnPropertyChanged(nameof(PromotionInfo));
			OnPropertyChanged(nameof(Player));
		}

		// INotifyPropertyChanged
		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string? name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}