using System.Collections.ObjectModel;
using System.Windows.Input;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class LoginViewModel
	{
		public ObservableCollection<SyndicatePlayer> Players { get; }
		public ICommand LoginCommand { get; }

		public LoginViewModel()
		{
			Players = new ObservableCollection<SyndicatePlayer>(PlayerDatabase.AllPlayers);
			LoginCommand = new LoginPlayer<SyndicatePlayer>(Login);
		}

		private void Login(SyndicatePlayer player)
		{
			GameSession.CurrentPlayer = player;

			// Navigate to dashboard
			var window = System.Windows.Application.Current.Windows
							.OfType<MainWindow>().FirstOrDefault();
			window?.LoadDashboard();
		}
	}
}
