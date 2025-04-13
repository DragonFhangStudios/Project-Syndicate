using System.Windows.Controls;
using DFS.ProjectSyndicate;
using DFS.ProjectSyndicate.ViewModels;

namespace DFS.ProjectSyndicate.Views
{
	public partial class DashboardView : UserControl
	{
		public DashboardView()
		{
			InitializeComponent();
			DataContext = new DashboardViewModel();
		}
	}
}
