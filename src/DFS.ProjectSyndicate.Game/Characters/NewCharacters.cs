using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Characters
{
	// Starting profile for player-created characters.
	public static class NewCharacters
	{
		public const float StartingCash = 500;
		public const float StartingAttribute = 5;

		public static SyndicatePlayer Create(string id, string name, DateTime createdAtUtc) => new(name)
		{
			Id = id,
			CareerPath = CareerPath.Civilian,
			CreatedAtUtc = createdAtUtc,
			Level = 1,
			XP = 0,
			TotalXP = 0,
			Tier = 1,
			Cash = StartingCash,
			Strength = StartingAttribute,
			Intellect = StartingAttribute,
			Endurance = StartingAttribute
		};
	}
}
