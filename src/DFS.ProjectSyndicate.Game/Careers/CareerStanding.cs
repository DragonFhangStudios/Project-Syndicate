using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Careers
{
	// Whether the rules can act on a character at all: a career the game knows, and a tier that career has.
	// A character failing either can't take or finish work, earn rewards or be promoted.
	public static class CareerStanding
	{
		public const string UnknownCareerMessage = "This character's career isn't recognized.";

		public static string InvalidTierMessage(CareerPath career, int tier) =>
			$"Tier {tier} isn't a valid {CareerInfo.DisplayName(career)} rank.";

		// A tier the character's career has. A career without ranks has none to check beyond the floor of 1;
		// such characters can't be promoted, so their tier never moves from where it started.
		public static bool HasValidTier(SyndicatePlayer player) =>
			player.Tier >= 1 && (CareerProgressions.For(player.CareerPath)?.IsValidTier(player.Tier) ?? true);

		// Why the character can't act, or null when they can.
		public static string? Problem(SyndicatePlayer player)
		{
			if (!Enum.IsDefined(player.CareerPath)) return UnknownCareerMessage;
			if (!HasValidTier(player)) return InvalidTierMessage(player.CareerPath, player.Tier);
			return null;
		}
	}
}
