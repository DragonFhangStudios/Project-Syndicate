using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Views;
using System.Windows;
using System.Windows.Controls;

namespace DFS.ProjectSyndicate
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
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
				MessageBox.Show("You must log in first.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			LoadDashboard();
		}
		private void Crimes_Click(object sender, RoutedEventArgs e)
		{
			if (!GameSession.IsLoggedIn)
			{
				MessageBox.Show("You must log in first.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			MainContent.Content = new CrimeView();
		}
		private void JobsTab_Click(object sender, RoutedEventArgs e)
		{
			if (!GameSession.IsLoggedIn)
			{
				MessageBox.Show("You must log in first.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			MainContent.Content = new JobsView();
		}
	}
}
