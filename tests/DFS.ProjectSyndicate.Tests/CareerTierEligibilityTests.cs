using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Tests;

// Career-specific tiers in job rules (Phase 6B): impossible tiers, invalid job definitions from any source, and
// tier requirements judged only within the job's own career.
public class CareerTierEligibilityTests
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();
    private readonly CareerService _careers = new(Catalog);

    private static SyndicatePlayer Character(CareerPath career, int tier, int level = 10, float cash = 500)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.CareerPath = career;
        player.AddXP(50 * level * (level - 1));
        player.Tier = tier;
        player.Cash = cash;
        return player;
    }

    private static Job ValidJob(string id, string? access = null) =>
        new() { Id = id, Title = "Valid Job", Payout = 40, XPReward = 10, Tasks = ["Do it"], Access = access };

    // ---- Characters with an impossible tier ----

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(6)]
    [InlineData(99)]
    public void ImpossibleTier_CanTakeNoWork_NotEvenSharedOrUntieredJobs(int tier)
    {
        var player = Character(CareerPath.Civilian, tier);

        Assert.All(Catalog.All, job =>
        {
            Assert.Equal([new JobRequirement("A valid Civilian rank", false)], JobEligibility.Requirements(player, job));
            Assert.False(JobEligibility.IsEligible(player, job));
        });

        var result = _careers.AcceptJob(player, "trash_cleanup");
        Assert.Equal((false, $"Tier {tier} isn't a valid Civilian rank."), (result.Success, result.Message));
        Assert.Equal(string.Empty, player.JobData.AssignedJobId);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(0)]
    public void ImpossibleTier_DoesNotMeetAnyTierRequirement(int tier)
    {
        // Before 6B, a Civilian at tier 6 or above passed every tier gate.
        var player = Character(CareerPath.Civilian, tier);

        Assert.All(Catalog.All.Where(j => j.MinTier > 1), job => Assert.False(JobEligibility.IsEligible(player, job), job.Id));
    }

    [Theory]
    [InlineData("trash_cleanup")]
    [InlineData("city_contracts_manager")]
    public void ImpossibleTier_CannotFinishAnActiveJob_OrBePaid(string jobId)
    {
        var player = Character(CareerPath.Civilian, 9, cash: 700);
        var job = Catalog.Find(jobId)!;
        player.JobData.AssignedJobId = jobId;
        player.JobData.CurrentTaskIndex = job.Tasks.Count - 1; // one click from being paid
        var before = (player.Cash, player.TotalXP, player.JobData.JobsCompleted, player.JobData.CurrentTaskIndex);

        Assert.Null(_careers.ActiveJob(player));
        var complete = _careers.CompleteCurrentTask(player);

        Assert.Equal((false, "Tier 9 isn't a valid Civilian rank."), (complete.Success, complete.Message));
        Assert.Null(complete.Reward);
        Assert.Equal(before, (player.Cash, player.TotalXP, player.JobData.JobsCompleted, player.JobData.CurrentTaskIndex));
    }

    [Fact]
    public void ImpossibleTier_IsStillListed_SoTheBoardCanShowWhy()
    {
        var player = Character(CareerPath.Civilian, 9);

        Assert.Equal(13, Catalog.All.Count(j => JobEligibility.IsListedFor(player, j)));
    }

    [Fact]
    public void UnconfiguredCareer_KeepsItsStartingTier_AndSharedWork()
    {
        // Careers without ranks have no tier ceiling to check; they can't be promoted, so their tier stays put.
        var criminal = Character(CareerPath.Criminal, 1, level: 2);

        Assert.True(_careers.AcceptJob(criminal, "warehouse_inventory").Success);
        Assert.Equal("A valid Criminal rank", JobEligibility.Requirements(Character(CareerPath.Criminal, 0), Catalog.Find("trash_cleanup")!)[0].Label);
    }

    // ---- Invalid job definitions, from a source that skips the catalog's checks ----

    public static TheoryData<CareerPath, int> EveryCareerAtEveryTier()
    {
        var data = new TheoryData<CareerPath, int>();
        foreach (var career in Enum.GetValues<CareerPath>())
            for (int tier = 1; tier <= 5; tier++) data.Add(career, tier);
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryCareerAtEveryTier))]
    public void SharedJobWithATierRequirement_IsRefusedToEveryCareer_IncludingItsOwner(CareerPath career, int tier)
    {
        var invalid = ValidJob("shared_gated", "Shared");
        invalid.MinTier = 2;
        var careers = new CareerService(new FakeJobCatalog(invalid));
        var player = Character(career, career == CareerPath.Civilian ? tier : 1);

        Assert.Equal([new JobRequirement("Valid career data for this job", false)], JobEligibility.Requirements(player, invalid));
        Assert.False(careers.AcceptJob(player, "shared_gated").Success);
        Assert.False(JobEligibility.IsListedFor(player, invalid));
    }

    [Fact]
    public void InvalidActiveJob_PaysNothing_AndDoesNotBlockNewWork()
    {
        var invalid = ValidJob("shared_gated", "Shared");
        invalid.MinTier = 2;
        var careers = new CareerService(new FakeJobCatalog(invalid, ValidJob("honest_work")));
        var player = Character(CareerPath.Civilian, 3);
        player.JobData.AssignedJobId = "shared_gated";

        Assert.Null(careers.ActiveJob(player));
        var complete = careers.CompleteCurrentTask(player);
        Assert.Equal((false, "You don't have an active job."), (complete.Success, complete.Message));
        Assert.Equal(500f, player.Cash);

        Assert.True(careers.AcceptJob(player, "honest_work").Success);
        Assert.Equal(40, FinishJob(careers, player).Cash);
    }

    private static JobReward FinishJob(CareerService careers, SyndicatePlayer player)
    {
        CareerResult step;
        do step = careers.CompleteCurrentTask(player); while (step.Success && step.Reward is null);
        return step.Reward!;
    }

    [Theory]
    [InlineData(null, 9, "Valid career data for this job")]          // above the Civilian top rank
    [InlineData("Criminal", 2, "Valid career data for this job")]    // a career without ranks
    public void TierRequirementTheOwnerCannotHave_IsRefusedEvenToTheOwner(string? career, int minTier, string label)
    {
        var invalid = ValidJob("bad_tier");
        invalid.Career = career;
        invalid.MinTier = minTier;
        var owner = Character(career is null ? CareerPath.Civilian : CareerPath.Criminal, 5);

        Assert.Equal([new JobRequirement(label, false)], JobEligibility.Requirements(owner, invalid));
        Assert.False(new CareerService(new FakeJobCatalog(invalid)).AcceptJob(owner, "bad_tier").Success);
    }

    [Theory]
    [InlineData(-50, 10)]
    [InlineData(10, -5)]
    public void JobThatWouldTakeRewardsAway_IsRefused(int payout, int xp)
    {
        var invalid = ValidJob("bad_terms");
        invalid.Payout = payout;
        invalid.XPReward = xp;
        var player = Character(CareerPath.Civilian, 1, cash: 500);

        Assert.Equal([new JobRequirement("A valid job definition", false)], JobEligibility.Requirements(player, invalid));
        Assert.False(new CareerService(new FakeJobCatalog(invalid)).AcceptJob(player, "bad_terms").Success);
        Assert.Equal(500f, player.Cash);
    }

    [Fact]
    public void JobWithNoTaskList_IsRefusedWithoutCrashing()
    {
        var broken = ValidJob("no_tasks");
        broken.Tasks = null!;

        var result = new CareerService(new FakeJobCatalog(broken)).AcceptJob(Character(CareerPath.Civilian, 1), "no_tasks");

        Assert.Equal((false, "That job is not available."), (result.Success, result.Message));
    }

    // ---- Tier requirements belong to the job's own career ----

    [Theory]
    [InlineData(CareerPath.Criminal)]
    [InlineData(CareerPath.EmergencyServices)]
    public void AnotherCareersTier_NeverMeetsACivilianTier_EvenAtTheSameNumber(CareerPath career)
    {
        foreach (var job in Catalog.All.Where(j => j.MinTier > 1))
        {
            var requirement = JobEligibility.Requirements(Character(career, job.MinTier), job).Single(r => r.Label.StartsWith("Tier"));
            Assert.False(requirement.Met, job.Id);
        }
    }

    [Fact]
    public void TierLabels_ComeFromTheOwningCareersRanks()
    {
        var labels = Catalog.All.Where(j => j.MinTier > 1)
            .Select(j => JobEligibility.Requirements(Character(CareerPath.Civilian, 1), j).Single(r => r.Label.StartsWith("Tier")).Label)
            .Distinct();

        Assert.Equal(["Tier 2 · Worker", "Tier 3 · Professional"], labels);
    }

    // ---- Catalog validation reads the owning career's ranks ----

    private static string LoadError(string fields, int minTier)
    {
        using var dir = new TempSaveDirectory();
        string path = dir.File("jobs.json");
        File.WriteAllText(path, $$"""[ { "Id": "test_job", "Title": "Test", "Payout": 10, "XPReward": 5, "MinTier": {{minTier}}, {{fields}} "Tasks": ["Do it"] } ]""");
        return Assert.Throws<InvalidDataException>(() => new JobCatalog(path)).Message;
    }

    [Theory]
    [InlineData("", 6, "requirements out of range")]
    [InlineData("\"Access\": \"Shared\",", 6, "requirements out of range")]
    [InlineData("\"Access\": \"Shared\",", 2, "a shared job can't require a tier")]
    [InlineData("\"Career\": \"Criminal\",", 2, "a Criminal job can't require a tier until that career has ranks")]
    [InlineData("\"Career\": \"Criminal\",", 6, "a Criminal job can't require a tier until that career has ranks")]
    public void CatalogValidation_UsesTheOwningCareersRanks(string fields, int minTier, string expected)
    {
        Assert.Contains($"job 'test_job' (entry 1): {expected}", LoadError(fields, minTier));
    }

    [Fact]
    public void CivilianJobAtTheTopRank_IsValid()
    {
        using var dir = new TempSaveDirectory();
        string path = dir.File("jobs.json");
        File.WriteAllText(path, """[ { "Id": "tycoon_job", "Title": "Board Seat", "Payout": 10, "XPReward": 5, "MinTier": 5, "Tasks": ["Do it"] } ]""");

        var catalog = new JobCatalog(path);

        Assert.True(JobEligibility.IsEligible(Character(CareerPath.Civilian, 5), catalog.Find("tycoon_job")!));
        Assert.False(JobEligibility.IsEligible(Character(CareerPath.Civilian, 4), catalog.Find("tycoon_job")!));
    }
}
