using DFS.ProjectSyndicate.Models;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Policy;

namespace DFS.ProjectSyndicate.Managers
{
	public static class ProgressionManager
	{
		// Define civilian tier progression requirements
		private static readonly Dictionary<int, TierRequirement> TierRequirements = new()
		{
			{ 1, new TierRequirement { TierLevel = 1, TierName = "New Comer", XPNeeded = 0, CashNeeded = 0, PromotionCost = 0 } },
			{ 2, new TierRequirement { TierLevel = 2, TierName = "Worker", XPNeeded = 150, CashNeeded = 1000, PromotionCost = 500 } },
			{ 3, new TierRequirement { TierLevel = 3, TierName = "Professional", XPNeeded = 400, CashNeeded = 5000, PromotionCost = 2000 } },
			{ 4, new TierRequirement { TierLevel = 4, TierName = "Executive", XPNeeded = 800, CashNeeded = 15000, PromotionCost = 5000 } },
			{ 5, new TierRequirement { TierLevel = 5, TierName = "Business Tycoon", XPNeeded = 1500, CashNeeded = 50000, PromotionCost = 10000 } }
		};

		public static string GetTierName(int tier)
		{
			return TierRequirements.ContainsKey(tier) ? TierRequirements[tier].TierName : "Unknown";
		}

		public static bool CanPromote(SyndicatePlayer player)
		{
			if (player.Tier >= 5) return false; // Max tier reached

			int nextTier = player.Tier + 1;
			if (!TierRequirements.ContainsKey(nextTier)) return false;

			var req = TierRequirements[nextTier];
			return player.XP >= req.XPNeeded
				&& player.Cash >= (req.CashNeeded + req.PromotionCost);
		}

		public static string GetPromotionRequirements(SyndicatePlayer player)
		{
			if (player.Tier >= 5) return "⭐ Maximum Tier Reached!";

			int nextTier = player.Tier + 1;
			var req = TierRequirements[nextTier];

			return $"Promotion to {req.TierName} Requirements:\n" +
				   $"• XP: {req.XPNeeded} (You have: {player.XP})\n" +
				   $"• Total Cash: ${req.CashNeeded:N0} (You have: ${player.Cash:N0})\n" +
				   $"• Promotion Fee: ${req.PromotionCost:N0}";
		}

		public static bool TryPromote(SyndicatePlayer player)
		{
			if (!CanPromote(player)) return false;

			int nextTier = player.Tier + 1;
			var req = TierRequirements[nextTier];

			player.Cash -= req.PromotionCost;
			player.Tier = nextTier;
			return true;
		}
	}
}

/*### **📚 WHAT THIS CODE DOES:**

**TierRequirements Dictionary: **
-Stores all 5 civilian tiers with their requirements
- Tier 1→2: Need 150 XP, $1000 total, costs $500 to promote
- Tier 2→3: Need 400 XP, $5000 total, costs $2000
- And so on...

**GetTierName():**
-Takes a tier number (1-5)
- Returns the name ("New Comer", "Worker", etc.)

**CanPromote():**
-Checks if player has enough XP AND cash to promote
- Returns true/false

**GetPromotionRequirements():**
-Returns a formatted string showing what's needed
- Shows current vs required for XP and cash

**TryPromote():**
-Actually performs the promotion
- Deducts the promotion cost
- Increases the tier
- Returns true if successful

---

### **✅ CHECKPOINT:**

Your project structure should now look like:
```
DFS.ProjectSyndicate /
├── Models /
│   ├── SyndicatePlayer.cs ✅
│   ├── TierRequirement.cs ✅
│   └── (other models...)
├── Managers /              ← NEW FOLDER
│   └── ProgressionManager.cs ✅ NEW FILE
└── (other folders...)
	*/