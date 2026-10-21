using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Web.Services;

// Per-circuit view of the selected character (registered as scoped). Replaces the WPF app's static
// GameSession, so each browser session has its own state. The save file stays the source of truth.
public sealed class PlayerState(GameService game, ILogger<PlayerState> logger)
{
    public string? CharacterId { get; private set; }
    public SyndicatePlayer? Player { get; private set; }
    public PlayerLoadStatus? Status { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync(string characterId, CancellationToken cancellationToken = default)
    {
        CharacterId = characterId;
        var result = await game.GetOrCreatePlayerAsync(characterId, cancellationToken);
        Apply(result);
    }

    public void Clear()
    {
        CharacterId = null;
        Player = null;
        Status = null;
        Changed?.Invoke();
    }

    private void Apply(PlayerLoadResult result)
    {
        Status = result.Status;
        Player = result.Player;
        if (result.Status == PlayerLoadStatus.Corrupt)
        {
            // Details stay in the server log; the browser only sees a generic message.
            logger.LogWarning("Save for {CharacterId} is unreadable and was left untouched: {Problem}", CharacterId, result.Problem);
        }
        Changed?.Invoke();
    }
}
