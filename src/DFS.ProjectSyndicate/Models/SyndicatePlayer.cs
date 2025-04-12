namespace DFS.ProjectSyndicate.Models
{
	public class SyndicatePlayer
	{
		public string Name { get; set; }
		public int Level { get; set; }
		public float Cash { get; set; }
		public float Strength { get; set; }
		public float Intellect { get; set; }
		public float Endurance { get; set; }
		public PlayerJobData JobData { get; set; } = new();

		public SyndicatePlayer(string name)
		{
			Name = name;
			Level = 1;
			Cash = 500;
			Strength = 5;
			Intellect = 5;
			Endurance = 5;
		}
	}
}
