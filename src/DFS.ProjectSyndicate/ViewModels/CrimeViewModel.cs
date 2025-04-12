using DFS.ProjectSyndicate.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class CrimeViewModel : INotifyPropertyChanged
	{
		public ObservableCollection<Crime> Crimes { get; set; }
		public SyndicatePlayer Player { get; set; }

		private string _lastResult = string.Empty;
		public string LastResult
		{
			get => _lastResult;
			set
			{
				_lastResult = value;
				OnPropertyChanged();
			}
		}

		private Random rng = new();

		public CrimeViewModel()
		{
			Player = new SyndicatePlayer("PlayerOne");

			Crimes = new ObservableCollection<Crime>
			{
				new Crime("Mugging", "Snatch a wallet from a pedestrian.", 0.8f, 100),
				new Crime("Car Theft", "Hotwire a parked car.", 0.5f, 300),
				new Crime("Bank Scam", "Fake email phishing attack.", 0.3f, 500)
			};
		}

		public void AttemptCrime(Crime crime)
		{
			if (rng.NextDouble() <= crime.SuccessChance)
			{
				Player.Cash += crime.Reward;
				LastResult = $"✅ Success! You earned ${crime.Reward}.";
			}
			else
			{
				LastResult = "❌ You failed and ran away empty-handed.";
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string? name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}
