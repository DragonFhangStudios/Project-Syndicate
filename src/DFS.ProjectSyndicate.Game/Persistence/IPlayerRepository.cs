using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Persistence
{
	// Server-side storage for one save record per character.
	public interface IPlayerRepository
	{
		// Never modifies storage: a missing save is NotFound, an unreadable one is Corrupt.
		Task<PlayerLoadResult> LoadAsync(string characterId, CancellationToken cancellationToken = default);

		// Throws CorruptSaveException rather than replace an existing save that cannot be read.
		Task SaveAsync(SyndicatePlayer player, CancellationToken cancellationToken = default);

		// Writes a brand-new save. Returns false, writing nothing, if a save with that id already exists.
		Task<bool> CreateAsync(SyndicatePlayer player, CancellationToken cancellationToken = default);

		// Ids of every stored save, readable or not. Never modifies storage.
		Task<IReadOnlyList<string>> ListCharacterIdsAsync(CancellationToken cancellationToken = default);
	}
}
