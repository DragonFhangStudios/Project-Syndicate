using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Persistence
{
	// On-disk save format. Kept separate from SyndicatePlayer so the file format is explicit and versioned.
	public sealed class PlayerSaveData
	{
		public const int CurrentSchemaVersion = 1;

		public int SchemaVersion { get; set; }
		public string CharacterId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int Level { get; set; }
		public int XP { get; set; }
		public int Tier { get; set; }
		public float Cash { get; set; }
		public float Strength { get; set; }
		public float Intellect { get; set; }
		public float Endurance { get; set; }
		public string AssignedJobId { get; set; } = string.Empty;
		public int CurrentTaskIndex { get; set; }
		public int TotalEarned { get; set; }
		public DateTime SavedAtUtc { get; set; }

		public static PlayerSaveData FromPlayer(SyndicatePlayer player) => new()
		{
			SchemaVersion = CurrentSchemaVersion,
			CharacterId = player.Id,
			Name = player.Name,
			Level = player.Level,
			XP = player.XP,
			Tier = player.Tier,
			Cash = player.Cash,
			Strength = player.Strength,
			Intellect = player.Intellect,
			Endurance = player.Endurance,
			AssignedJobId = player.JobData.AssignedJobId,
			CurrentTaskIndex = player.JobData.CurrentTaskIndex,
			TotalEarned = player.JobData.TotalEarned,
			SavedAtUtc = DateTime.UtcNow
		};

		public SyndicatePlayer ToPlayer() => new(Name)
		{
			Id = CharacterId,
			Level = Level,
			XP = XP,
			Tier = Tier,
			Cash = Cash,
			Strength = Strength,
			Intellect = Intellect,
			Endurance = Endurance,
			JobData = new PlayerJobData
			{
				AssignedJobId = AssignedJobId,
				CurrentTaskIndex = CurrentTaskIndex,
				TotalEarned = TotalEarned
			}
		};

		// Returns null when the record is usable, otherwise a description of the problem.
		public string? Validate(string expectedCharacterId)
		{
			if (SchemaVersion != CurrentSchemaVersion) return $"unsupported schema version {SchemaVersion}";
			if (CharacterId != expectedCharacterId) return "character id does not match the file name";
			if (string.IsNullOrWhiteSpace(Name)) return "missing name";
			if (Level < 1 || XP < 0 || Tier < 1) return "level, XP or tier out of range";
			if (!float.IsFinite(Cash)) return "cash is not a number";
			if (CurrentTaskIndex < 0 || TotalEarned < 0) return "job progress out of range";
			return null;
		}
	}
}
