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
	}
}
