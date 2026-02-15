using DFS.ProjectSyndicate.Core;

namespace DFS.ProjectSyndicate.Models
{
	public class SyndicatePlayer : ObservableObject
	{
		private string _name;
		private int _level;
		private int _xp;
		private int _tier = 1;
		private float _cash;
		private float _strength;
		private float _intellect;
		private float _endurance;
		private PlayerJobData _jobData = new();

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

		public int XP
		{
			get => _xp;
			set => SetProperty(ref _xp, value);
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

		public void AddXP(int amount)
		{
			XP += amount;
			CheckLevelUp();
		}

		private void CheckLevelUp()
		{
			int xpNeeded = Level * 100;
			while (XP >= xpNeeded)
			{
				Level++;
				XP -= xpNeeded;
				xpNeeded = Level * 100;
			}
		}

		public bool MeetsRequirements(float minStr, float minInt, float minEnd)
		{
			return Strength >= minStr && Intellect >= minInt && Endurance >= minEnd;
		}
	}
}
