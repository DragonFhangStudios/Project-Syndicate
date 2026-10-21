using System.Collections.Concurrent;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Gameplay
{
	// Application entry point for player state. Holds no player data itself: every call reads the
	// current save, so separate sessions never share in-memory state. Calls for the same character
	// are serialized with a per-character lock.
	public sealed class GameService
	{
		private readonly IPlayerRepository _repository;
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

		public GameService(IPlayerRepository repository)
		{
			_repository = repository;
		}

		// Loads the character's save, creating it from the demo profile on first use.
		public async Task<PlayerLoadResult> GetOrCreatePlayerAsync(string characterId, CancellationToken cancellationToken = default)
		{
			var profile = DemoCharacters.Find(characterId);
			if (profile is null) return PlayerLoadResult.UnknownCharacter();

			using var _ = await LockAsync(characterId, cancellationToken);

			var result = await _repository.LoadAsync(characterId, cancellationToken);
			if (result.Status != PlayerLoadStatus.NotFound) return result;

			var player = profile.CreatePlayer();
			await _repository.SaveAsync(player, cancellationToken);
			return PlayerLoadResult.Loaded(player);
		}

		// Read-only view for listings: an unsaved character shows its starting profile and nothing is written.
		public async Task<PlayerLoadResult> PreviewPlayerAsync(string characterId, CancellationToken cancellationToken = default)
		{
			var profile = DemoCharacters.Find(characterId);
			if (profile is null) return PlayerLoadResult.UnknownCharacter();

			var result = await _repository.LoadAsync(characterId, cancellationToken);
			return result.Status == PlayerLoadStatus.NotFound ? PlayerLoadResult.Loaded(profile.CreatePlayer()) : result;
		}

		private async Task<IDisposable> LockAsync(string characterId, CancellationToken cancellationToken)
		{
			var gate = _locks.GetOrAdd(characterId, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync(cancellationToken);
			return new Releaser(gate);
		}

		private sealed class Releaser(SemaphoreSlim gate) : IDisposable
		{
			public void Dispose() => gate.Release();
		}
	}
}
