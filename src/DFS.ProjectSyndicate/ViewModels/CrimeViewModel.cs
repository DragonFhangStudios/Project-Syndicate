using DFS.ProjectSyndicate.Core;
using DFS.ProjectSyndicate.Models;
using System;
using System.Collections.ObjectModel;

namespace DFS.ProjectSyndicate.ViewModels
{
	public class CrimeViewModel : ObservableObject
	{
		public ObservableCollection<Crime> Crimes { get; set; }
		public SyndicatePlayer Player => GameSession.CurrentPlayer;

		private string _lastResult = string.Empty;
		public string LastResult
		{
			get => _lastResult;
			set => SetProperty(ref _lastResult, value);
		}

		private Random rng = new();

		public CrimeViewModel()
		{
			// Player is now retrieved from GameSession

			Crimes = new ObservableCollection<Crime>
			{
				new Crime("Mugging", "Snatch a wallet from a pedestrian.", 0.8f, 100),
				new Crime("Car Theft", "Hotwire a parked car.", 0.5f, 300),
				new Crime("Bank Scam", "Fake email phishing attack.", 0.3f, 500)
			};
		}

		public void AttemptCrime(Crime crime)
		{
            if (Player == null) return;

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
	}
}
