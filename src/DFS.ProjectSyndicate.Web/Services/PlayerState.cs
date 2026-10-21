using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Web.Services;

// Per-circuit view of the selected character (registered as scoped). Replaces the WPF app's static
// GameSession, so each browser session has its own state. The save file stays the source of truth:
// every action goes through GameService, which reloads and saves under a per-character lock.
public sealed class PlayerState(GameService game, ILogger<PlayerState> logger)
{
    public string? CharacterId { get; private set; }
    public SyndicatePlayer? Player { get; private set; }
    public PlayerLoadStatus? Status { get; private set; }

    // True while an action is in flight; the UI disables actions so a double click does nothing.
    public bool Busy { get; private set; }

    // Most recent action result and reward, shown as feedback until replaced or dismissed.
    public CareerResult? LastResult { get; private set; }
    public JobReward? LastReward { get; private set; }

    // Text for the screen-reader live region in the layout.
    public string Announcement { get; private set; } = string.Empty;

    public event Action? Changed;

    public async Task LoadAsync(string characterId, CancellationToken cancellationToken = default)
    {
        if (CharacterId != characterId)
        {
            // Never show the previous character's data while the new one loads.
            CharacterId = characterId;
            Player = null;
            Status = null;
            ClearFeedback();
            Changed?.Invoke();
        }

        var result = await game.GetOrCreatePlayerAsync(characterId, cancellationToken);
        Apply(result);
        Changed?.Invoke();
    }

    public void Clear()
    {
        CharacterId = null;
        Player = null;
        Status = null;
        ClearFeedback();
        Changed?.Invoke();
    }

    public Task<CareerResult?> AcceptJobAsync(string jobId) => RunAsync(id => game.AcceptJobAsync(id, jobId));

    public Task<CareerResult?> CompleteTaskAsync() => RunAsync(id => game.CompleteTaskAsync(id));

    public Task<CareerResult?> PromoteAsync() => RunAsync(id => game.PromoteAsync(id));

    public void DismissReward()
    {
        LastReward = null;
        Changed?.Invoke();
    }

    private async Task<CareerResult?> RunAsync(Func<string, Task<GameActionOutcome>> action)
    {
        if (Busy || CharacterId is null || Status != PlayerLoadStatus.Loaded) return null;

        Busy = true;
        Changed?.Invoke();
        try
        {
            var outcome = await action(CharacterId);
            Apply(outcome.State);

            LastResult = outcome.Result;
            if (outcome.Result is { Success: true } result)
            {
                // A successful action replaces the previous reward banner (null when it paid nothing).
                LastReward = result.Reward;
            }
            Announcement = outcome.Result?.Message ?? string.Empty;
            return outcome.Result;
        }
        catch (Exception ex)
        {
            // The action's changes were not saved; the last good save is untouched.
            logger.LogError(ex, "Action failed for {CharacterId}", CharacterId);
            LastResult = CareerResult.Fail("Something went wrong and your progress was not changed. Please try again.");
            Announcement = LastResult.Message;
            return LastResult;
        }
        finally
        {
            Busy = false;
            Changed?.Invoke();
        }
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
    }

    private void ClearFeedback()
    {
        LastResult = null;
        LastReward = null;
        Announcement = string.Empty;
    }
}
