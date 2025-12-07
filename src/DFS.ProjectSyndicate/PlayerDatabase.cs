using DFS.ProjectSyndicate.Models;
using System.Collections.Generic;

namespace DFS.ProjectSyndicate
{
	public static class PlayerDatabase
	{
		public static List<SyndicatePlayer> AllPlayers { get; } = new()
		{
			new SyndicatePlayer("Sonny Perelli")
			{
				Cash = 1500,
				Strength = 8,
				Intellect = 5,
				Endurance = 7,
				XP = 0,      // ← ADD
                Tier = 1     // ← ADD
            },
			new SyndicatePlayer("Carlo Valentini")
			{
				Cash = 1200,
				Strength = 6,
				Intellect = 9,
				Endurance = 5,
				XP = 0,      // ← ADD
                Tier = 1     // ← ADD
            },
			new SyndicatePlayer("Giovanni Ragoli")
			{
				Cash = 3000,
				Strength = 4,
				Intellect = 10,
				Endurance = 6,
				XP = 0,      // ← ADD
                Tier = 1     // ← ADD
            }
		};
	}
}