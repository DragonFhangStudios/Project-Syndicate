using System.Text.Json;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Persistence
{
	// One JSON file per character in a server-side directory. Intended for local development.
	public sealed class JsonFilePlayerRepository : IPlayerRepository
	{
		private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

		private readonly string _directory;

		public JsonFilePlayerRepository(string directory)
		{
			_directory = Path.GetFullPath(directory);
		}

		public string GetSavePath(string characterId)
		{
			CharacterIds.Validate(characterId);
			return Path.Combine(_directory, characterId + ".json");
		}

		public async Task<PlayerLoadResult> LoadAsync(string characterId, CancellationToken cancellationToken = default)
		{
			string path = GetSavePath(characterId);
			if (!File.Exists(path)) return PlayerLoadResult.NotFound();

			string json = await File.ReadAllTextAsync(path, cancellationToken);
			return Parse(characterId, json);
		}

		public async Task SaveAsync(SyndicatePlayer player, CancellationToken cancellationToken = default)
		{
			string path = GetSavePath(player.Id);

			if (File.Exists(path))
			{
				var existing = Parse(player.Id, await File.ReadAllTextAsync(path, cancellationToken));
				if (existing.Status == PlayerLoadStatus.Corrupt)
					throw new CorruptSaveException(player.Id, existing.Problem!);
			}

			Directory.CreateDirectory(_directory);
			string json = JsonSerializer.Serialize(PlayerSaveData.FromPlayer(player), JsonOptions);

			// Write the whole file first, then swap it in, so an interrupted save never leaves a partial file.
			string tempPath = path + ".tmp";
			await File.WriteAllTextAsync(tempPath, json, cancellationToken);
			File.Move(tempPath, path, overwrite: true);
		}

		private static PlayerLoadResult Parse(string characterId, string json)
		{
			PlayerSaveData? data;
			try
			{
				data = JsonSerializer.Deserialize<PlayerSaveData>(json, JsonOptions);
			}
			catch (JsonException ex)
			{
				return PlayerLoadResult.Corrupt($"invalid JSON ({ex.Message})");
			}

			if (data is null) return PlayerLoadResult.Corrupt("empty save");
			if (!data.TryUpgrade()) return PlayerLoadResult.Corrupt($"unsupported schema version {data.SchemaVersion}");

			string? problem = data.Validate(characterId);
			return problem is null ? PlayerLoadResult.Loaded(data.ToPlayer()) : PlayerLoadResult.Corrupt(problem);
		}
	}
}
