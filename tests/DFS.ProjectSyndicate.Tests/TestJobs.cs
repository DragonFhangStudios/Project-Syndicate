using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Gameplay;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

// In-memory catalog for edge cases the shipped job data doesn't cover.
public sealed class FakeJobCatalog(params Job[] jobs) : IJobCatalog
{
    public IReadOnlyList<Job> All { get; } = jobs;

    public Job? Find(string jobId) => All.FirstOrDefault(j => j.Id == jobId);
}

public static class TestJobs
{
    public const string TrashCleanup = "trash_cleanup";

    // The real SimpleJobs.json, copied to the test output through the project references.
    public static JobCatalog RealCatalog() => new(JobCatalog.DefaultPath);

    public static GameService CreateGame(IPlayerRepository repository, IJobCatalog? jobs = null) =>
        new(repository, new CareerService(jobs ?? RealCatalog()));
}
