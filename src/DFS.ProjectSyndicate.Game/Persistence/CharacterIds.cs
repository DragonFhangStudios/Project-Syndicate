using System.Text.RegularExpressions;

namespace DFS.ProjectSyndicate.Persistence
{
	public static partial class CharacterIds
	{
		// Lowercase words joined by single hyphens, e.g. "sonny-perelli". Safe to use as a file name.
		[GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
		private static partial Regex Pattern();

		public static bool IsValid(string? id) => id is { Length: > 0 and <= 64 } && Pattern().IsMatch(id);

		public static void Validate(string? id)
		{
			if (!IsValid(id)) throw new ArgumentException($"Invalid character id '{id}'.", nameof(id));
		}
	}
}
