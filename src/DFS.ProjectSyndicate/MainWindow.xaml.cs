using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Services;
using DFS.ProjectSyndicate.Views;
using System.Windows;
using System.Windows.Controls;

namespace DFS.ProjectSyndicate
{
	public partial class MainWindow : Window
	{
        private readonly IDialogService _dialogService;

		public MainWindow()
		{
			InitializeComponent();
            _dialogService = new DialogService();
			LoadLogin();
		}
		public void LoadLogin()
		{
			MainContent.Content = new LoginView();
		}
		public void LoadDashboard()
		{
			MainContent.Content = new DashboardView();
		}

		private void Dashboard_Click(object sender, RoutedEventArgs e)
		{
			if (!GameSession.IsLoggedIn)
			{
                _dialogService.ShowMessage("You must log in first.", "Access Denied");
				return;
			}

			LoadDashboard();
		}
		private void Crimes_Click(object sender, RoutedEventArgs e)
		{
			if (!GameSession.IsLoggedIn)
			{
                _dialogService.ShowMessage("You must log in first.", "Access Denied");
				return;
			}

			MainContent.Content = new CrimeView();
		}
		private void JobsTab_Click(object sender, RoutedEventArgs e)
		{
			if (!GameSession.IsLoggedIn)
			{
                _dialogService.ShowMessage("You must log in first.", "Access Denied");
				return;
			}

			MainContent.Content = new JobsView();
		}
	}
}
