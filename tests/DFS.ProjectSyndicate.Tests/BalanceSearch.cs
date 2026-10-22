using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Tests;

// Balance helpers that play by the real rules: jobs a character may take, their rewards, and promotion.
public static class BalanceSearch
{
    // Fewest completed jobs, choosing freely among the jobs the character can take at each step, until
    // the character qualifies for promotion to the next tier. Breadth-first over (cash, lifetime XP).
    //
    // At a fixed tier, more cash and more lifetime XP never close a job or delay promotion, so a state
    // beaten on both by another state can never finish sooner. Only the unbeaten states are kept, which
    // keeps each step to a handful of states however many jobs the catalog has.
    public static int FewestJobsToPromotion(IJobCatalog catalog, SyndicatePlayer start, int maxJobs = 40)
    {
        var frontier = new List<(float Cash, int TotalXp)> { (start.Cash, start.TotalXP) };
        for (int jobs = 0; jobs <= maxJobs; jobs++)
        {
            if (frontier.Any(s => ProgressionManager.CanPromote(At(start, s)))) return jobs;

            var next = new List<(float Cash, int TotalXp)>();
            foreach (var state in frontier)
            {
                var player = At(start, state);
                foreach (var job in catalog.All.Where(j => JobEligibility.IsEligible(player, j)))
                    next.Add((state.Cash + job.Payout, state.TotalXp + job.XPReward));
            }
            frontier = Unbeaten(next);
        }
        return int.MaxValue;
    }

    // Plays whole jobs through CareerService, letting the strategy pick each one, until the character
    // qualifies for promotion. Returns the ids of the jobs played, in order.
    public static IReadOnlyList<string> PlayUntilPromotable(IJobCatalog catalog, SyndicatePlayer player, Strategy choose, int maxJobs = 60)
    {
        var careers = new CareerService(catalog);
        var played = new List<string>();
        while (!ProgressionManager.CanPromote(player))
        {
            Assert.True(played.Count < maxJobs, $"Not promotable after {maxJobs} jobs: {string.Join(", ", played)}");

            var open = catalog.All.Where(j => JobEligibility.IsEligible(player, j)).ToList();
            played.Add(PlayJob(careers, player, choose(open, played.Count).Id).JobId);
        }
        return played;
    }

    // Accepts a job and completes every task, asserting the rules allowed each step.
    public static JobReward PlayJob(CareerService careers, SyndicatePlayer player, string jobId)
    {
        var accepted = careers.AcceptJob(player, jobId);
        Assert.True(accepted.Success, accepted.Message);

        CareerResult step;
        do
        {
            step = careers.CompleteCurrentTask(player);
            Assert.True(step.Success, step.Message);
        }
        while (step.Reward is null);

        return step.Reward;
    }

    // Rebuilds a character at a given cash and lifetime XP (level follows from XP).
    public static SyndicatePlayer At(SyndicatePlayer start, (float Cash, int TotalXp) state)
    {
        var player = new SyndicatePlayer(start.Name) { Cash = state.Cash, Tier = start.Tier };
        player.AddXP(state.TotalXp);
        return player;
    }

    private static List<(float Cash, int TotalXp)> Unbeaten(IEnumerable<(float Cash, int TotalXp)> states)
    {
        var kept = new List<(float Cash, int TotalXp)>();
        int bestXp = int.MinValue;
        foreach (var state in states.Distinct().OrderByDescending(s => s.Cash).ThenByDescending(s => s.TotalXp))
        {
            if (state.TotalXp <= bestXp) continue; // another state has at least this much cash and XP
            kept.Add(state);
            bestXp = state.TotalXp;
        }
        return kept;
    }
}

// Picks the next job from those open to the character; jobsPlayed counts the jobs played so far in the route.
public delegate Job Strategy(IReadOnlyList<Job> open, int jobsPlayed);

public static class Strategies
{
    // Always the best-paying job on offer.
    public static readonly Strategy CashFocused = (open, _) => open.MaxBy(j => j.Payout)!;

    // Always the job with the most XP, the better-paid one on a tie.
    public static readonly Strategy XpFocused = (open, _) => open.OrderByDescending(j => j.XPReward).ThenByDescending(j => j.Payout).First();

    // A Worker trying every Worker contract open to them in turn, in board order.
    public static readonly Strategy WorkerRotation = (open, jobsPlayed) =>
    {
        var contracts = open.Where(j => j.MinTier >= 2).ToList();
        return contracts[jobsPlayed % contracts.Count];
    };

    // The only Worker contract before Phase 4, repeated.
    public static readonly Strategy NightSecurityOnly = (open, _) => open.Single(j => j.Id == "night_security");
}
