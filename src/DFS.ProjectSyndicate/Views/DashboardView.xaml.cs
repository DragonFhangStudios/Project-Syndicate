using DFS.ProjectSyndicate.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace DFS.ProjectSyndicate.Views
{
    public partial class DashboardView : UserControl
    {
        private DashboardViewModel ViewModel => (DashboardViewModel)this.DataContext;

        public DashboardView()
        {
            InitializeComponent();
            this.Unloaded += DashboardView_Unloaded;
        }

        private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel?.Cleanup();
        }
    }
}
