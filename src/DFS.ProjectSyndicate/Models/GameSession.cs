namespace DFS.ProjectSyndicate.Models
{
	public static class GameSession
	{
		public static SyndicatePlayer? CurrentPlayer { get; set; }

		public static bool IsLoggedIn => CurrentPlayer != null;
	}
}
