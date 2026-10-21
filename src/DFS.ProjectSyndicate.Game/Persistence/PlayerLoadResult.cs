using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Persistence
{
	public enum PlayerLoadStatus
	{
		Loaded,
		NotFound,
		Corrupt,
		UnknownCharacter
	}

	public sealed record PlayerLoadResult(PlayerLoadStatus Status, SyndicatePlayer? Player = null, string? Problem = null)
	{
		public static PlayerLoadResult Loaded(SyndicatePlayer player) => new(PlayerLoadStatus.Loaded, player);
		public static PlayerLoadResult NotFound() => new(PlayerLoadStatus.NotFound);
		public static PlayerLoadResult Corrupt(string problem) => new(PlayerLoadStatus.Corrupt, Problem: problem);
		public static PlayerLoadResult UnknownCharacter() => new(PlayerLoadStatus.UnknownCharacter);
	}

	// Thrown when a save would replace a file that exists but cannot be read; the file is left untouched.
	public sealed class CorruptSaveException(string characterId, string problem)
		: Exception($"Save for '{characterId}' is unreadable and was not overwritten: {problem}")
	{
		public string CharacterId { get; } = characterId;
	}
}
