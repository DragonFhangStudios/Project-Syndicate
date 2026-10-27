using System.Text.Json.Serialization;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Persistence
{
	// On-disk save format. Kept separate from SyndicatePlayer so the file format is explicit and versioned.
	//   v1: initial format.
	//   v2: adds TotalXP (lifetime XP, used for career tiers) and JobsCompleted.
	//       v1 files load with TotalXP rebuilt from Level and XP, and JobsCompleted = 0.
	//   v3: adds CareerPath and CreatedAtUtc (player-created characters).
	//       v1/v2 files load as Civilian with no creation time.
	// Older files are upgraded in memory and rewritten in the current version on their next save.
	public sealed class PlayerSaveData
	{
		public const int CurrentSchemaVersion = 3;

		public int SchemaVersion { get; set; }
		public string CharacterId { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		[JsonConverter(typeof(JsonStringEnumConverter<CareerPath>))]
		public CareerPath CareerPath { get; set; } = CareerPath.Civilian;
		public DateTime? CreatedAtUtc { get; set; }
		public int Level { get; set; }
		public int XP { get; set; }
		public int TotalXP { get; set; }
		public int Tier { get; set; }
		public float Cash { get; set; }
		public float Strength { get; set; }
		public float Intellect { get; set; }
		public float Endurance { get; set; }
		public string AssignedJobId { get; set; } = string.Empty;
		public int CurrentTaskIndex { get; set; }
		public int TotalEarned { get; set; }
		public int JobsCompleted { get; set; }
		public DateTime SavedAtUtc { get; set; }

		public static PlayerSaveData FromPlayer(SyndicatePlayer player) => new()
		{
			SchemaVersion = CurrentSchemaVersion,
			CharacterId = player.Id,
			Name = player.Name,
			CareerPath = player.CareerPath,
			CreatedAtUtc = player.CreatedAtUtc,
			Level = player.Level,
			XP = player.XP,
			TotalXP = player.TotalXP,
			Tier = player.Tier,
			Cash = player.Cash,
			Strength = player.Strength,
			Intellect = player.Intellect,
			Endurance = player.Endurance,
			AssignedJobId = player.JobData.AssignedJobId,
			CurrentTaskIndex = player.JobData.CurrentTaskIndex,
			TotalEarned = player.JobData.TotalEarned,
			JobsCompleted = player.JobData.JobsCompleted,
			SavedAtUtc = DateTime.UtcNow
		};

		public SyndicatePlayer ToPlayer() => new(Name)
		{
			Id = CharacterId,
			CareerPath = CareerPath,
			CreatedAtUtc = CreatedAtUtc,
			Level = Level,
			XP = XP,
			TotalXP = TotalXP,
			Tier = Tier,
			Cash = Cash,
			Strength = Strength,
			Intellect = Intellect,
			Endurance = Endurance,
			JobData = new PlayerJobData
			{
				AssignedJobId = AssignedJobId,
				CurrentTaskIndex = CurrentTaskIndex,
				TotalEarned = TotalEarned,
				JobsCompleted = JobsCompleted
			}
		};

		// Brings an older record up to the current schema in memory. Returns false for unknown versions.
		public bool TryUpgrade()
		{
			if (SchemaVersion < 1 || SchemaVersion > CurrentSchemaVersion) return false;

			if (SchemaVersion == 1)
			{
				// Reaching level L takes 100 * (1 + 2 + ... + (L - 1)) XP; add progress within the level.
				long rebuilt = 100L * (Level - 1) * Level / 2 + XP;
				TotalXP = (int)Math.Clamp(rebuilt, 0, int.MaxValue);
				JobsCompleted = 0;
				SchemaVersion = 2;
			}

			if (SchemaVersion == 2)
			{
				CareerPath = CareerPath.Civilian;
				CreatedAtUtc = null;
				SchemaVersion = 3;
			}

			return true;
		}

		// Returns null when the record is usable, otherwise a description of the problem.
		public string? Validate(string expectedCharacterId)
		{
			if (SchemaVersion != CurrentSchemaVersion) return $"unsupported schema version {SchemaVersion}";
			if (CharacterId != expectedCharacterId) return "character id does not match the file name";
			if (string.IsNullOrWhiteSpace(Name)) return "missing name";
			if (!Enum.IsDefined(CareerPath)) return "unknown career path";
			// Reported, not loaded, so no character can be played in a career that has no content yet.
			if (!Careers.CareerInfo.IsPlayable(CareerPath)) return $"the {Careers.CareerInfo.DisplayName(CareerPath)} career isn't playable yet";
			if (Level < 1 || XP < 0 || TotalXP < XP || Tier < 1) return "level, XP or tier out of range";
			if (!float.IsFinite(Cash)) return "cash is not a number";
			if (CurrentTaskIndex < 0 || TotalEarned < 0 || JobsCompleted < 0) return "job progress out of range";
			return null;
		}
	}
}
