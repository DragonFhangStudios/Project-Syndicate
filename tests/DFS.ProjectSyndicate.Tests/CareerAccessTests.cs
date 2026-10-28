using System.Text.Json;
using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.Persistence;

namespace DFS.ProjectSyndicate.Tests;

// Career identity and job access (Phase 6A): which careers may take which jobs, enforced by the rules.
// Criminal and Emergency Services characters are synthetic here: no player can create or load one yet.
public class CareerAccessTests
{
    private static readonly JobCatalog Catalog = TestJobs.RealCatalog();
    private readonly CareerService _careers = new(Catalog);

    public static readonly string[] SharedJobIds = ["trash_cleanup", "grocery_delivery", "diner_night_shift", "warehouse_inventory"];

    public static readonly string[] CivilianOnlyJobIds =
    [
        "night_security", "night_freight_sorting", "fleet_maintenance_crew", "municipal_records_contract",
        "logistics_dispatch_coordinator", "commercial_property_inspector", "hospital_supply_coordinator",
        "port_customs_broker", "city_contracts_manager"
    ];

    private static SyndicatePlayer Character(CareerPath career, int tier = 1, int level = 1, float cash = 500)
    {
        var player = NewCharacters.Create("char-0000000000aa", "Vinnie", DateTime.UtcNow);
        player.CareerPath = career;
        player.AddXP(50 * level * (level - 1));
        player.Tier = tier;
        player.Cash = cash;
        return player;
    }

    public static TheoryData<CareerPath, string> OtherCareersAndSharedJobs()
    {
        var data = new TheoryData<CareerPath, string>();
        foreach (var career in new[] { CareerPath.Criminal, CareerPath.EmergencyServices })
            foreach (string id in SharedJobIds) data.Add(career, id);
        return data;
    }

    public static TheoryData<CareerPath, int> OtherCareersAndTiers()
    {
        var data = new TheoryData<CareerPath, int>();
        foreach (var career in new[] { CareerPath.Criminal, CareerPath.EmergencyServices })
            for (int tier = 1; tier <= ProgressionManager.MaxTier; tier++) data.Add(career, tier);
        return data;
    }

    // ---- Identities ----

    [Fact]
    public void CareerIdentities_KeepFixedNumbers_ForTheLegacyWpfSave()
    {
        Assert.Equal([0, 1, 2], new[] { CareerPath.Civilian, CareerPath.Criminal, CareerPath.EmergencyServices }.Select(c => (int)c));
        Assert.Equal(["Civilian", "Criminal", "EmergencyServices"], Enum.GetNames<CareerPath>());
    }

    [Fact]
    public void OnlyCivilian_IsPlayable()
    {
        Assert.Equal([CareerPath.Civilian], Enum.GetValues<CareerPath>().Where(CareerInfo.IsPlayable));
    }

    [Fact]
    public void LegacyWpfSaveFormat_StillRoundTripsACivilian()
    {
        // JsonSaveManager serializes SyndicatePlayer with default options, which write the career as a number.
        var player = new SyndicatePlayer("PlayerOne");
        string json = JsonSerializer.Serialize(player, new JsonSerializerOptions { WriteIndented = true });

        Assert.Contains("\"CareerPath\": 0", json);
        Assert.Equal(CareerPath.Civilian, JsonSerializer.Deserialize<SyndicatePlayer>(json)!.CareerPath);
    }

    // ---- Job ownership and access ----

    [Fact]
    public void EveryShippedJob_IsCivilian_FourShared_NineRestricted()
    {
        var access = Catalog.All.ToDictionary(j => j.Id, j => JobAccess.Read(j, out _)!);

        Assert.All(access.Values, a => Assert.Equal(CareerPath.Civilian, a.Owner));
        Assert.Equal(SharedJobIds, access.Where(a => a.Value.Policy == JobAccessPolicy.Shared).Select(a => a.Key));
        Assert.Equal(CivilianOnlyJobIds, access.Where(a => a.Value.Policy == JobAccessPolicy.Restricted).Select(a => a.Key));
        // Ownership is the default: no shipped job names its career, and only the shared four state their access.
        Assert.All(Catalog.All, j => Assert.Null(j.Career));
    }

    [Fact]
    public void JobWithoutCareerFields_IsCivilianAndRestricted()
    {
        var access = JobAccess.Read(new Job { Id = "x", Title = "X", Tasks = ["Do it"] }, out string? problem);

        Assert.Null(problem);
        Assert.Equal(new JobAccess(CareerPath.Civilian, JobAccessPolicy.Restricted), access);
    }

    [Fact]
    public void JobBoardListing_IsEveryJobForACivilian_AndOnlySharedWorkForOtherCareers()
    {
        Assert.Equal(13, Catalog.All.Count(j => JobEligibility.IsListedFor(Character(CareerPath.Civilian), j)));
        foreach (var career in new[] { CareerPath.Criminal, CareerPath.EmergencyServices })
            Assert.Equal(SharedJobIds, Catalog.All.Where(j => JobEligibility.IsListedFor(Character(career), j)).Select(j => j.Id));
    }

    // ---- Shared work for other careers ----

    [Theory]
    [MemberData(nameof(OtherCareersAndSharedJobs))]
    public void OtherCareers_CanTakeSharedWork_AndArePaidOnce(CareerPath career, string jobId)
    {
        var job = Catalog.Find(jobId)!;
        var player = Character(career, level: 2);

        var reward = BalanceSearch.PlayJob(_careers, player, jobId);

        Assert.Equal((job.Payout, job.XPReward), (reward.Cash, reward.XP));
        Assert.Equal((500f + job.Payout, 100 + job.XPReward, 1), (player.Cash, player.TotalXP, player.JobData.JobsCompleted));
        Assert.False(_careers.CompleteCurrentTask(player).Success);
        Assert.Equal(500f + job.Payout, player.Cash);
    }

    [Theory]
    [InlineData(CareerPath.Criminal)]
    [InlineData(CareerPath.EmergencyServices)]
    public void SharedWork_StillHonoursItsLevelGate(CareerPath career)
    {
        var result = _careers.AcceptJob(Character(career, level: 1), "warehouse_inventory");

        Assert.False(result.Success);
        Assert.Equal("Warehouse Inventory requires Level 2.", result.Message);
    }

    // ---- Civilian-only work ----

    [Theory]
    [MemberData(nameof(OtherCareersAndTiers))]
    public void OtherCareers_CannotTakeCivilianContracts_AtAnyTier(CareerPath career, int tier)
    {
        foreach (string id in CivilianOnlyJobIds)
        {
            var player = Character(career, tier, level: 10, cash: 50000);

            var result = _careers.AcceptJob(player, id);

            Assert.False(result.Success, id);
            Assert.Contains("Civilian career", result.Message);
            Assert.Equal(string.Empty, player.JobData.AssignedJobId);
        }
    }

    [Fact]
    public void AnotherCareersTier_DoesNotCountTowardsACivilianTier()
    {
        var criminal = Character(CareerPath.Criminal, tier: 5, level: 10);

        Assert.Equal(
            [new JobRequirement("Civilian career", false), new JobRequirement("Level 6", true), new JobRequirement("Tier 3 · Professional", false)],
            JobEligibility.Requirements(criminal, Catalog.Find("city_contracts_manager")!));
    }

    [Fact]
    public async Task OtherCareersRefusal_ComesFromTheRules_NotTheUi()
    {
        // A direct request through GameService for a Civilian, to show the same check applies there.
        using var dir = new TempSaveDirectory();
        var game = TestJobs.CreateGame(new JsonFilePlayerRepository(dir.Path));
        string id = (await game.CreateCharacterAsync("Vinnie", Guid.NewGuid())).CharacterId!;

        var refused = await game.AcceptJobAsync(id, "night_security");
        Assert.Equal("Night Security Patrol requires Tier 2 · Worker.", refused.Result!.Message);

        // And for another career, straight through the rules every request goes through. Their tier 2 is not
        // a Civilian Worker rank, so the tier requirement stays unmet too.
        var criminal = Character(CareerPath.Criminal, tier: 2, level: 5);
        Assert.Equal("Night Security Patrol requires Civilian career and Tier 2 · Worker.", _careers.AcceptJob(criminal, "night_security").Message);
    }

    // ---- Promotion stays Civilian ----

    [Theory]
    [InlineData(CareerPath.Criminal)]
    [InlineData(CareerPath.EmergencyServices)]
    public void OtherCareers_CannotPromoteThroughCivilianRanks(CareerPath career)
    {
        var player = Character(career, tier: 1, level: 10, cash: 100000);
        // Phase 6B: promotion is judged against the player's own career, which has no ranks yet. (Until 6B this
        // returned true, because the Civilian table was the only one and CareerService alone refused it.)
        Assert.False(ProgressionManager.CanPromote(player));
        Assert.Equal(PromotionAvailability.CareerNotConfigured, PromotionStatus.For(player).Availability);

        var result = _careers.Promote(player);

        Assert.False(result.Success);
        Assert.Equal($"Promotion isn't available for the {CareerInfo.DisplayName(career)} career yet.", result.Message);
        Assert.Equal((1, 100000f), (player.Tier, player.Cash));
    }

    // ---- Invalid careers and stale assignments ----

    [Fact]
    public void UnknownCareer_CanTakeNothing_NotEvenSharedWork()
    {
        var player = Character((CareerPath)7, level: 10);

        Assert.All(Catalog.All, job =>
        {
            Assert.False(JobEligibility.IsEligible(player, job));
            Assert.False(JobEligibility.IsListedFor(player, job));
            Assert.Equal(new JobRequirement("A recognized career", false), JobEligibility.Requirements(player, job)[0]);
        });
        Assert.False(_careers.AcceptJob(player, "trash_cleanup").Success);
    }

    [Theory]
    [InlineData(CareerPath.Criminal, "fleet_maintenance_crew")]
    [InlineData(CareerPath.EmergencyServices, "city_contracts_manager")]
    [InlineData((CareerPath)7, "trash_cleanup")]
    public void ActiveJobTheCareerCannotTake_PaysNothing_AndDoesNotBlockSharedWork(CareerPath career, string assignedJobId)
    {
        var player = Character(career, tier: 3, level: 6);
        var job = Catalog.Find(assignedJobId)!;
        player.JobData.AssignedJobId = assignedJobId;
        player.JobData.CurrentTaskIndex = job.Tasks.Count - 1; // one click from being paid

        Assert.Null(_careers.ActiveJob(player));
        var complete = _careers.CompleteCurrentTask(player);
        Assert.False(complete.Success);
        Assert.Null(complete.Reward);
        Assert.Equal((500f, 0), (player.Cash, player.JobData.JobsCompleted));

        if (Enum.IsDefined(career))
            Assert.True(_careers.AcceptJob(player, "diner_night_shift").Success);
    }

    [Fact]
    public void JobWithUnreadableCareerData_IsClosedToEveryone()
    {
        // FakeJobCatalog skips JobCatalog's checks, as data from any other source might.
        var broken = new Job { Id = "odd_job", Title = "Odd Job", Payout = 10, XPReward = 5, Tasks = ["Do it"], Career = "Pirate" };
        var careers = new CareerService(new FakeJobCatalog(broken));
        var civilian = Character(CareerPath.Civilian);

        Assert.Equal([new JobRequirement("Valid career data for this job", false)], JobEligibility.Requirements(civilian, broken));
        Assert.False(careers.AcceptJob(civilian, "odd_job").Success);

        civilian.JobData.AssignedJobId = "odd_job";
        Assert.Null(careers.CompleteCurrentTask(civilian).Reward);
        Assert.Equal(500f, civilian.Cash);
    }

    // ---- Job data validation ----

    private static InvalidDataException LoadFails(string fields, int minTier = 0)
    {
        using var dir = new TempSaveDirectory();
        string path = dir.File("jobs.json");
        File.WriteAllText(path, $$"""[ { "Id": "test_job", "Title": "Test", "Payout": 10, "XPReward": 5, "MinTier": {{minTier}}, {{fields}} "Tasks": ["Do it"] } ]""");
        return Assert.Throws<InvalidDataException>(() => new JobCatalog(path));
    }

    private static JobCatalog Loads(string fields, int minTier = 0)
    {
        using var dir = new TempSaveDirectory();
        string path = dir.File("jobs.json");
        File.WriteAllText(path, $$"""[ { "Id": "test_job", "Title": "Test", "Payout": 10, "XPReward": 5, "MinTier": {{minTier}}, {{fields}} "Tasks": ["Do it"] } ]""");
        return new JobCatalog(path);
    }

    [Theory]
    [InlineData("\"Career\": \"Pirate\",", "unknown career \"Pirate\"")]
    [InlineData("\"Career\": \"civilian\",", "unknown career \"civilian\"")]
    [InlineData("\"Career\": \"1\",", "unknown career \"1\"")]
    [InlineData("\"Career\": \"Civilian,Criminal\",", "unknown career \"Civilian,Criminal\"")]
    [InlineData("\"Career\": \"\",", "unknown career \"\"")]
    [InlineData("\"Career\": \"Emergency Services\",", "unknown career \"Emergency Services\"")]
    [InlineData("\"Access\": \"Public\",", "unknown access \"Public\"")]
    [InlineData("\"Access\": \"shared\",", "unknown access \"shared\"")]
    [InlineData("\"Access\": \"\",", "unknown access \"\"")]
    public void UnknownCareerOrAccess_IsRejected_NamingTheJob(string fields, string expected)
    {
        var error = LoadFails(fields);

        Assert.Contains($"job 'test_job' (entry 1): {expected}", error.Message);
    }

    [Fact]
    public void SharedJob_WithATierRequirement_IsRejected()
    {
        Assert.Contains("a shared job can't require a tier", LoadFails("\"Access\": \"Shared\",", minTier: 2).Message);
    }

    [Theory]
    [InlineData("Criminal", "a Criminal job can't require a tier")]
    [InlineData("EmergencyServices", "a Emergency Services job can't require a tier")]
    public void OtherCareersJob_WithATierRequirement_IsRejected(string career, string expected)
    {
        Assert.Contains(expected, LoadFails($"\"Career\": \"{career}\",", minTier: 2).Message);
    }

    [Theory]
    [InlineData("\"Career\": \"Civilian\",", CareerPath.Civilian, JobAccessPolicy.Restricted)]
    [InlineData("\"Career\": \"Criminal\",", CareerPath.Criminal, JobAccessPolicy.Restricted)]
    [InlineData("\"Career\": \"EmergencyServices\", \"Access\": \"Shared\",", CareerPath.EmergencyServices, JobAccessPolicy.Shared)]
    [InlineData("\"Access\": \"Restricted\",", CareerPath.Civilian, JobAccessPolicy.Restricted)]
    public void ValidCareerFields_Load(string fields, CareerPath owner, JobAccessPolicy policy)
    {
        var job = Loads(fields).Find("test_job")!;

        Assert.Equal(new JobAccess(owner, policy), JobAccess.Read(job, out _));
    }

    [Fact]
    public void CriminalOnlyJob_IsClosedToCiviliansAndEmergencyServices()
    {
        var catalog = Loads("\"Career\": \"Criminal\",");
        var careers = new CareerService(catalog);

        Assert.True(careers.AcceptJob(Character(CareerPath.Criminal), "test_job").Success);
        Assert.Equal("Test requires Criminal career.", careers.AcceptJob(Character(CareerPath.Civilian), "test_job").Message);
        Assert.False(careers.AcceptJob(Character(CareerPath.EmergencyServices), "test_job").Success);
    }

    // ---- Saves ----

    [Theory]
    [InlineData("Criminal", "the Criminal career isn't playable yet")]
    [InlineData("EmergencyServices", "the Emergency Services career isn't playable yet")]
    public async Task SaveForACareerWithoutContent_IsReported_AndLeftUntouched(string career, string problem)
    {
        using var dir = new TempSaveDirectory();
        var repo = new JsonFilePlayerRepository(dir.Path);
        string path = dir.File("char-00000000cc01.json");
        string json = $$"""
            { "SchemaVersion": 3, "CharacterId": "char-00000000cc01", "Name": "Rico", "CareerPath": "{{career}}",
              "Level": 2, "XP": 0, "TotalXP": 100, "Tier": 1, "Cash": 900 }
            """;
        await File.WriteAllTextAsync(path, json);

        var loaded = await repo.LoadAsync("char-00000000cc01");
        var action = await TestJobs.CreateGame(repo).AcceptJobAsync("char-00000000cc01", "trash_cleanup");

        Assert.Equal((PlayerLoadStatus.Corrupt, problem), (loaded.Status, loaded.Problem));
        Assert.Null(action.Result);
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task CivilianSave_StillStoresTheCareerByName()
    {
        using var dir = new TempSaveDirectory();
        var game = TestJobs.CreateGame(new JsonFilePlayerRepository(dir.Path));
        string id = (await game.CreateCharacterAsync("Vinnie", Guid.NewGuid())).CharacterId!;
        await TestJobs.PlayJobAsync(game, id, "trash_cleanup");

        string json = await File.ReadAllTextAsync(dir.File(id + ".json"));
        Assert.Contains("\"CareerPath\": \"Civilian\"", json);
        Assert.Contains("\"SchemaVersion\": 3", json);
    }
}
