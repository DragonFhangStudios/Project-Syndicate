namespace DFS.ProjectSyndicate.Models
{
	public class Crime
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public float SuccessChance { get; set; } // 0.0 to 1.0
		public int Reward { get; set; }

		public Crime(string name, string description, float chance, int reward)
		{
			Name = name;
			Description = description;
			SuccessChance = chance;
			Reward = reward;
		}
	}
}
