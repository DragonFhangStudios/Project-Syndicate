using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Views
{
	public partial class LoginView : UserControl
	{
		private readonly Dictionary<string, string> _loginCodes = new()
		{
			{ "Sonny Perelli", "0330" },
			{ "Carlo Valentini", "0824" },
			{ "Giovanni Ragoli", "0209" }
		};

		public LoginView()
		{
			InitializeComponent();
		}

		private void LoginButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button btn && btn.Tag is SyndicatePlayer player)
			{
				if (PasswordInput == null) return;
				string enteredCode = PasswordInput.Password;

#pragma warning disable CS8600
				if (_loginCodes.TryGetValue(player.Name, out string expectedCode) &&
					enteredCode == expectedCode)
				{
					GameSession.CurrentPlayer = player;

					var mainWindow = Application.Current.Windows
										.OfType<MainWindow>().FirstOrDefault();

					mainWindow?.LoadDashboard();
				}
				else
				{
					MessageBox.Show("❌ Incorrect passcode. Try again.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
#pragma warning restore CS8600
			}
		}
	}
}
