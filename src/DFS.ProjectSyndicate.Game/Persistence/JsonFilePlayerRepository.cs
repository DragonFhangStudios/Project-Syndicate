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

			// Write the whole file first, then swap it in, so an interrupted save never leaves a partial file.
			string tempPath = await WriteTempAsync(player, path, cancellationToken);
			File.Move(tempPath, path, overwrite: true);
		}

		public async Task<bool> CreateAsync(SyndicatePlayer player, CancellationToken cancellationToken = default)
		{
			string path = GetSavePath(player.Id);
			if (File.Exists(path)) return false;

			string tempPath = await WriteTempAsync(player, path, cancellationToken);
			try
			{
				// Without overwrite the move fails if the file appeared meanwhile, so an existing save is never replaced.
				File.Move(tempPath, path, overwrite: false);
				return true;
			}
			catch (IOException) when (File.Exists(path))
			{
				File.Delete(tempPath);
				return false;
			}
		}

		public Task<IReadOnlyList<string>> ListCharacterIdsAsync(CancellationToken cancellationToken = default)
		{
			IReadOnlyList<string> ids = Directory.Exists(_directory)
				? Directory.EnumerateFiles(_directory, "*.json")
					.Select(Path.GetFileNameWithoutExtension)
					.Where(CharacterIds.IsValid)
					.Select(id => id!)
					.Order(StringComparer.Ordinal)
					.ToList()
				: [];
			return Task.FromResult(ids);
		}

		private async Task<string> WriteTempAsync(SyndicatePlayer player, string path, CancellationToken cancellationToken)
		{
			Directory.CreateDirectory(_directory);
			string json = JsonSerializer.Serialize(PlayerSaveData.FromPlayer(player), JsonOptions);
			string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			await File.WriteAllTextAsync(tempPath, json, cancellationToken);
			return tempPath;
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
