namespace DFS.ProjectSyndicate.Models
{
	public class SyndicatePlayer
	{
		public string Name { get; set; }
		public int Level { get; set; }
		public int XP { get; set; }
		public int Tier { get; set; } = 1;
		public float Cash { get; set; }
		public float Strength { get; set; }
		public float Intellect { get; set; }
		public float Endurance { get; set; }
		public PlayerJobData JobData { get; set; } = new();

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

		// ← NEW METHOD
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

		// ← NEW METHOD
		public bool MeetsRequirements(float minStr, float minInt, float minEnd)
		{
			return Strength >= minStr && Intellect >= minInt && Endurance >= minEnd;
		}
	}
}
