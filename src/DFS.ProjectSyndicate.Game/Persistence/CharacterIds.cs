using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DFS.ProjectSyndicate.Persistence
{
	public static partial class CharacterIds
	{
		// Lowercase words joined by single hyphens, e.g. "sonny-perelli". Safe to use as a file name.
		[GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
		private static partial Regex Pattern();

		// Player-created characters: "char-" plus 12 random hex digits, e.g. "char-3f9a1b2c4d5e".
		// Demo ids are names and never take this form, so the two can't collide.
		[GeneratedRegex("^char-[0-9a-f]{12}$")]
		private static partial Regex CustomPattern();

		public static bool IsValid(string? id) => id is { Length: > 0 and <= 64 } && Pattern().IsMatch(id);

		public static bool IsCustom(string? id) => id is not null && CustomPattern().IsMatch(id);

		// A new random id for a player-created character. Independent of the character's name.
		public static string NewCustomId() => "char-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

		public static void Validate(string? id)
		{
			if (!IsValid(id)) throw new ArgumentException($"Invalid character id '{id}'.", nameof(id));
		}
	}
}
