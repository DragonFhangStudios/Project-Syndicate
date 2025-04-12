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
			LoadDashboard();
		}

		private void LoadDashboard()
		{
			MainContent.Content = new DashboardView();
		}

		private void Dashboard_Click(object sender, RoutedEventArgs e)
		{
			LoadDashboard();
		}
		private void Crimes_Click(object sender, RoutedEventArgs e)
		{
			MainContent.Content = new CrimeView();
		}
		private void JobsTab_Click(object sender, RoutedEventArgs e)
		{
			MainContent.Content = new JobsView(); // ← now loading the real deal!
		}
	}
}
