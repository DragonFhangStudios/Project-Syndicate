using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Characters
{
	// Starting profile for a built-in demonstration character (local development only, no authentication).
	public sealed record DemoCharacter(string Id, string Name, float Cash, float Strength, float Intellect, float Endurance)
	{
		// Always returns a new instance, so callers never share mutable player state.
		public SyndicatePlayer CreatePlayer() => new(Name)
		{
			Id = Id,
			Cash = Cash,
			Strength = Strength,
			Intellect = Intellect,
			Endurance = Endurance
		};
	}

	public static class DemoCharacters
	{
		public static IReadOnlyList<DemoCharacter> All { get; } =
		[
			new("sonny-perelli", "Sonny Perelli", Cash: 1500, Strength: 8, Intellect: 5, Endurance: 7),
			new("carlo-valentini", "Carlo Valentini", Cash: 1200, Strength: 6, Intellect: 9, Endurance: 5),
			new("giovanni-ragoli", "Giovanni Ragoli", Cash: 3000, Strength: 4, Intellect: 10, Endurance: 6)
		];

		public static DemoCharacter? Find(string id) =>
			All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
	}
}
