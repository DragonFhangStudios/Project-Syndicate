using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Models;
using System.Collections.Generic;

namespace DFS.ProjectSyndicate
{
	public static class PlayerDatabase
	{
		// Character data lives in DFS.ProjectSyndicate.Game (DemoCharacters) and is shared with the web app.
		public static List<SyndicatePlayer> AllPlayers { get; } =
			DemoCharacters.All.Select(c => c.CreatePlayer()).ToList();
	}
}
