using System.Collections.Concurrent;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Gameplay
{
	// Result of a game action: the character's state afterwards and what the action did.
	public sealed record GameActionOutcome(PlayerLoadResult State, CareerResult? Result)
	{
		public SyndicatePlayer? Player => State.Player;
	}

	// Application entry point for player state. Holds no player data itself: every call reads the
	// current save, so separate sessions never share in-memory state. Calls for the same character
	// are serialized with a per-character lock.
	public sealed class GameService
	{
		private readonly IPlayerRepository _repository;
		private readonly CareerService _careers;
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

		public GameService(IPlayerRepository repository, CareerService careers)
		{
			_repository = repository;
			_careers = careers;
		}

		// Loads the character's save, creating it from the demo profile on first use.
		public async Task<PlayerLoadResult> GetOrCreatePlayerAsync(string characterId, CancellationToken cancellationToken = default)
		{
			if (DemoCharacters.Find(characterId) is null) return PlayerLoadResult.UnknownCharacter();

			using var _ = await LockAsync(characterId, cancellationToken);
			return await LoadOrCreateLockedAsync(characterId, cancellationToken);
		}

		// Read-only view for listings: an unsaved character shows its starting profile and nothing is written.
		public async Task<PlayerLoadResult> PreviewPlayerAsync(string characterId, CancellationToken cancellationToken = default)
		{
			var profile = DemoCharacters.Find(characterId);
			if (profile is null) return PlayerLoadResult.UnknownCharacter();

			var result = await _repository.LoadAsync(characterId, cancellationToken);
			return result.Status == PlayerLoadStatus.NotFound ? PlayerLoadResult.Loaded(profile.CreatePlayer()) : result;
		}

		public Task<GameActionOutcome> AcceptJobAsync(string characterId, string jobId, CancellationToken cancellationToken = default) =>
			ExecuteAsync(characterId, player => _careers.AcceptJob(player, jobId), cancellationToken);

		public Task<GameActionOutcome> CompleteTaskAsync(string characterId, CancellationToken cancellationToken = default) =>
			ExecuteAsync(characterId, _careers.CompleteCurrentTask, cancellationToken);

		public Task<GameActionOutcome> PromoteAsync(string characterId, CancellationToken cancellationToken = default) =>
			ExecuteAsync(characterId, _careers.Promote, cancellationToken);

		// Load the latest save, apply the action, and save only if it succeeded. Because the action always
		// runs against freshly loaded state under the lock, a stale browser tab cannot repeat a reward.
		private async Task<GameActionOutcome> ExecuteAsync(string characterId, Func<SyndicatePlayer, CareerResult> action,
			CancellationToken cancellationToken)
		{
			if (DemoCharacters.Find(characterId) is null) return new(PlayerLoadResult.UnknownCharacter(), null);

			using var _ = await LockAsync(characterId, cancellationToken);

			var state = await LoadOrCreateLockedAsync(characterId, cancellationToken);
			if (state.Status != PlayerLoadStatus.Loaded) return new(state, null);

			var result = action(state.Player!);
			if (!result.Success)
			{
				// Rules may have touched the in-memory copy before failing; return the saved state instead.
				return new(await _repository.LoadAsync(characterId, cancellationToken), result);
			}

			await _repository.SaveAsync(state.Player!, cancellationToken);
			return new(state, result);
		}

		private async Task<PlayerLoadResult> LoadOrCreateLockedAsync(string characterId, CancellationToken cancellationToken)
		{
			var result = await _repository.LoadAsync(characterId, cancellationToken);
			if (result.Status != PlayerLoadStatus.NotFound) return result;

			var player = DemoCharacters.Find(characterId)!.CreatePlayer();
			await _repository.SaveAsync(player, cancellationToken);
			return PlayerLoadResult.Loaded(player);
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
