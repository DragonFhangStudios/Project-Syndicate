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

	// An entry in character selection. Player is null when the save could not be read.
	public sealed record CharacterSummary(string Id, bool IsDemo, PlayerLoadResult State);

	public sealed record CharacterCreationResult(bool Success, string? CharacterId, string? Error)
	{
		public static CharacterCreationResult Created(string id) => new(true, id, null);
		public static CharacterCreationResult Failed(string error) => new(false, null, error);
	}

	// Application entry point for player state. Holds no player data itself: every call reads the
	// current save, so separate sessions never share in-memory state. Calls for the same character
	// are serialized with a per-character lock.
	//
	// Two kinds of character: the built-in demo characters (fixed ids, created from their profile on
	// first use) and player-created characters (random "char-" ids, only ever loaded once created).
	public sealed class GameService
	{
		private const int MaxIdAttempts = 5;
		private const int MaxRememberedRequests = 1024;

		private readonly IPlayerRepository _repository;
		private readonly CareerService _careers;
		private readonly Func<string> _newId;
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
		private readonly SemaphoreSlim _createLock = new(1, 1);
		private readonly Dictionary<Guid, string> _completedRequests = new();

		public GameService(IPlayerRepository repository, CareerService careers, Func<string>? newCharacterId = null)
		{
			_repository = repository;
			_careers = careers;
			_newId = newCharacterId ?? CharacterIds.NewCustomId;
		}

		public static bool IsKnownCharacterId(string? characterId) =>
			characterId is not null && (DemoCharacters.Find(characterId) is not null || CharacterIds.IsCustom(characterId));

		// Loads the character's save. Demo characters are created from their profile on first use.
		public async Task<PlayerLoadResult> GetOrCreatePlayerAsync(string characterId, CancellationToken cancellationToken = default)
		{
			if (!IsKnownCharacterId(characterId)) return PlayerLoadResult.UnknownCharacter();

			using var _ = await LockAsync(characterId, cancellationToken);
			return await LoadOrCreateLockedAsync(characterId, cancellationToken);
		}

		// Read-only view for listings: an unsaved demo character shows its starting profile and nothing is written.
		public async Task<PlayerLoadResult> PreviewPlayerAsync(string characterId, CancellationToken cancellationToken = default)
		{
			if (!IsKnownCharacterId(characterId)) return PlayerLoadResult.UnknownCharacter();

			var result = await _repository.LoadAsync(characterId, cancellationToken);
			if (result.Status != PlayerLoadStatus.NotFound) return result;

			var profile = DemoCharacters.Find(characterId);
			return profile is null ? PlayerLoadResult.UnknownCharacter() : PlayerLoadResult.Loaded(profile.CreatePlayer());
		}

		// Every character for the selection screen: player-created (newest first, unreadable last) and demo.
		// Read-only: listing never writes or upgrades a save.
		public async Task<(IReadOnlyList<CharacterSummary> Created, IReadOnlyList<CharacterSummary> Demo)> ListCharactersAsync(
			CancellationToken cancellationToken = default)
		{
			var created = new List<CharacterSummary>();
			foreach (string id in await _repository.ListCharacterIdsAsync(cancellationToken))
			{
				if (!CharacterIds.IsCustom(id)) continue;
				created.Add(new CharacterSummary(id, IsDemo: false, await _repository.LoadAsync(id, cancellationToken)));
			}

			var demo = new List<CharacterSummary>();
			foreach (var profile in DemoCharacters.All)
			{
				demo.Add(new CharacterSummary(profile.Id, IsDemo: true, await PreviewPlayerAsync(profile.Id, cancellationToken)));
			}

			var ordered = created
				.OrderBy(c => c.State.Player is null)
				.ThenByDescending(c => c.State.Player?.CreatedAtUtc)
				.ThenBy(c => c.Id, StringComparer.Ordinal)
				.ToList();
			return (ordered, demo);
		}

		// Creates a new civilian character. requestId identifies one submission of the create form:
		// repeating it (a double click, a resent request) returns the character it already made.
		public async Task<CharacterCreationResult> CreateCharacterAsync(string? name, Guid requestId,
			CancellationToken cancellationToken = default)
		{
			string? normalized = CharacterNames.Normalize(name, out string? error);
			if (normalized is null) return CharacterCreationResult.Failed(error!);

			await _createLock.WaitAsync(cancellationToken);
			try
			{
				if (_completedRequests.TryGetValue(requestId, out string? existingId))
					return CharacterCreationResult.Created(existingId);

				if (await IsNameTakenAsync(normalized, cancellationToken))
					return CharacterCreationResult.Failed($"There is already a character called {normalized}.");

				for (int attempt = 0; attempt < MaxIdAttempts; attempt++)
				{
					string id = _newId();
					if (!CharacterIds.IsCustom(id)) throw new InvalidOperationException($"Generated id '{id}' is not a valid character id.");

					var player = NewCharacters.Create(id, normalized, DateTime.UtcNow);
					if (!await _repository.CreateAsync(player, cancellationToken)) continue; // id already in use

					if (_completedRequests.Count >= MaxRememberedRequests) _completedRequests.Clear();
					_completedRequests[requestId] = id;
					return CharacterCreationResult.Created(id);
				}

				return CharacterCreationResult.Failed("Couldn't create the character. Please try again.");
			}
			finally
			{
				_createLock.Release();
			}
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
			if (!IsKnownCharacterId(characterId)) return new(PlayerLoadResult.UnknownCharacter(), null);

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

			// Only demo characters are created on demand; a missing player-created save is not recreated.
			var profile = DemoCharacters.Find(characterId);
			if (profile is null) return PlayerLoadResult.UnknownCharacter();

			var player = profile.CreatePlayer();
			await _repository.SaveAsync(player, cancellationToken);
			return PlayerLoadResult.Loaded(player);
		}

		private async Task<bool> IsNameTakenAsync(string name, CancellationToken cancellationToken)
		{
			if (DemoCharacters.All.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))) return true;

			foreach (string id in await _repository.ListCharacterIdsAsync(cancellationToken))
			{
				if (!CharacterIds.IsCustom(id)) continue;
				var existing = await _repository.LoadAsync(id, cancellationToken);
				if (string.Equals(existing.Player?.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
			}
			return false;
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
