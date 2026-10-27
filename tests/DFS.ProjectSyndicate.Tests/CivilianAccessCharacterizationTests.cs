using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Tests;

// Pins down what a Civilian may take, as it stood before careers were introduced (Phase 5, 952d84a), so the
// career work can be shown not to change it: every job, tiers 1–5, levels 1–8.
public class CivilianAccessCharacterizationTests
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();

    // The pre-career rule, written out independently of JobEligibility: a level gate, then a tier gate.
    private static List<JobRequirement> RequirementsBeforeCareers(SyndicatePlayer player, DFS.JobSystem.Core.Job job)
    {
        var requirements = new List<JobRequirement>();
        if (job.MinLevel > 1) requirements.Add(new($"Level {job.MinLevel}", player.Level >= job.MinLevel));
        if (job.MinTier > 1) requirements.Add(new($"Tier {job.MinTier} · {ProgressionManager.GetTierName(job.MinTier)}", player.Tier >= job.MinTier));
        return requirements;
    }

    private static SyndicatePlayer Civilian(int tier, int level)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.AddXP(50 * level * (level - 1));
        player.Tier = tier;
        Assert.Equal((CareerPath.Civilian, level), (player.CareerPath, player.Level));
        return player;
    }

    public static TheoryData<int, int> TiersAndLevels()
    {
        var data = new TheoryData<int, int>();
        for (int tier = 1; tier <= ProgressionManager.MaxTier; tier++)
            for (int level = 1; level <= 8; level++)
                data.Add(tier, level);
        return data;
    }

    [Theory]
    [MemberData(nameof(TiersAndLevels))]
    public void CivilianRequirementsAndEligibility_AreUnchanged(int tier, int level)
    {
        var player = Civilian(tier, level);

        Assert.All(Catalog.All, job =>
        {
            var expected = RequirementsBeforeCareers(player, job);
            Assert.Equal(expected, JobEligibility.Requirements(player, job));
            Assert.Equal(expected.All(r => r.Met), JobEligibility.IsEligible(player, job));
        });
    }

    [Theory]
    [MemberData(nameof(TiersAndLevels))]
    public void CivilianAcceptance_MatchesEligibility_ForEveryJob(int tier, int level)
    {
        var careers = new CareerService(Catalog);

        foreach (var job in Catalog.All)
        {
            var player = Civilian(tier, level);
            bool eligible = RequirementsBeforeCareers(player, job).All(r => r.Met);

            var result = careers.AcceptJob(player, job.Id);

            Assert.Equal(eligible, result.Success);
            Assert.Equal(eligible ? job.Id : string.Empty, player.JobData.AssignedJobId);
        }
    }

    [Fact]
    public void CivilianJobCounts_ByRank_AreUnchanged()
    {
        // Entry work (no tier), Worker contracts (Tier 2), Professional contracts (Tier 3).
        Assert.Equal(
            [(0, 4), (2, 5), (3, 4)],
            Catalog.All.GroupBy(j => j.MinTier).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())));
    }
}
