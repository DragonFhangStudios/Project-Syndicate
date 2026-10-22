using System.Text;

namespace DFS.ProjectSyndicate.Characters
{
	public static class CharacterNames
	{
		public const int MinLength = 2;
		public const int MaxLength = 24;

		// Collapses runs of whitespace and trims. Returns the cleaned name, or null with an error message.
		public static string? Normalize(string? raw, out string? error)
		{
			// Compose accents first so "é" typed as e + combining mark counts as one letter.
			string composed = (raw ?? string.Empty).Normalize(NormalizationForm.FormC);
			string name = string.Join(' ', composed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

			if (name.Length < MinLength || name.Length > MaxLength)
			{
				error = $"Names must be {MinLength} to {MaxLength} characters long.";
				return null;
			}

			foreach (var rune in name.EnumerateRunes())
			{
				if (!Rune.IsLetter(rune) && rune.Value is not (' ' or '\'' or '-' or '.'))
				{
					error = "Names may use letters, spaces, apostrophes, hyphens and periods.";
					return null;
				}
			}

			if (!Rune.IsLetter(name.EnumerateRunes().First()))
			{
				error = "Names must start with a letter.";
				return null;
			}

			error = null;
			return name;
		}
	}
}
