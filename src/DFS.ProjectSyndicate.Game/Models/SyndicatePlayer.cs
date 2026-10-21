using DFS.ProjectSyndicate.Core;

namespace DFS.ProjectSyndicate.Models
{
	public class SyndicatePlayer : ObservableObject
	{
		private string _id = string.Empty;
		private string _name;
		private int _level;
		private int _xp;
		private int _totalXp;
		private int _tier = 1;
		private float _cash;
		private float _strength;
		private float _intellect;
		private float _endurance;
		private PlayerJobData _jobData = new();

		// Stable character identifier, used as the save-file key (e.g. "sonny-perelli").
		public string Id
		{
			get => _id;
			set => SetProperty(ref _id, value);
		}

		public string Name
		{
			get => _name;
			set => SetProperty(ref _name, value);
		}

		public int Level
		{
			get => _level;
			set => SetProperty(ref _level, value);
		}

		// Progress within the current level; reduced when a level is gained.
		public int XP
		{
			get => _xp;
			set => SetProperty(ref _xp, value);
		}

		// Lifetime XP earned. Never reduced; career tiers are measured against this.
		public int TotalXP
		{
			get => _totalXp;
			set => SetProperty(ref _totalXp, value);
		}

		public int Tier
		{
			get => _tier;
			set => SetProperty(ref _tier, value);
		}

		public float Cash
		{
			get => _cash;
			set => SetProperty(ref _cash, value);
		}

		public float Strength
		{
			get => _strength;
			set => SetProperty(ref _strength, value);
		}

		public float Intellect
		{
			get => _intellect;
			set => SetProperty(ref _intellect, value);
		}

		public float Endurance
		{
			get => _endurance;
			set => SetProperty(ref _endurance, value);
		}

		public PlayerJobData JobData
		{
			get => _jobData;
			set => SetProperty(ref _jobData, value);
		}

		public SyndicatePlayer(string name)
		{
			Name = name;
			Level = 1;
			XP = 0;
			Tier = 1;
			Cash = 500;
			Strength = 5;
			Intellect = 5;
			Endurance = 5;
		}

		// Adds XP to both level progress and the lifetime total. Returns the number of levels gained.
		public int AddXP(int amount)
		{
			if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "XP cannot be negative.");

			TotalXP += amount;
			XP += amount;
			return CheckLevelUp();
		}

		// XP needed to advance from the current level.
		public int XPToNextLevel => Level * 100;

		private int CheckLevelUp()
		{
			int levelsGained = 0;
			int xpNeeded = Level * 100;
			while (XP >= xpNeeded)
			{
				Level++;
				XP -= xpNeeded;
				xpNeeded = Level * 100;
				levelsGained++;
			}
			return levelsGained;
		}

		public bool MeetsRequirements(float minStr, float minInt, float minEnd)
		{
			return Strength >= minStr && Intellect >= minInt && Endurance >= minEnd;
		}
	}
}
