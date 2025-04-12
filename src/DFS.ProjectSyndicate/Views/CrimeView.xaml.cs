using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace DFS.ProjectSyndicate.Views
{
	public partial class CrimeView : UserControl
	{
		private CrimeViewModel ViewModel => (CrimeViewModel)this.DataContext;

		public CrimeView()
		{
			InitializeComponent();
		}

		private void AttemptCrime_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button btn && btn.Tag is Crime crime)
			{
				ViewModel.AttemptCrime(crime);
				// Force UI to update
				//this.DataContext = null;
				//this.DataContext = ViewModel;
			}
		}
	}
}
