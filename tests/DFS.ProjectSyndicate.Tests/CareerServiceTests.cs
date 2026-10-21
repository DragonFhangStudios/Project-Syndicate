using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Characters;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Tests;

public class CareerServiceTests
{
    private readonly CareerService _careers = new(TestJobs.RealCatalog());

    private static SyndicatePlayer NewSonny() => DemoCharacters.Find("sonny-perelli")!.CreatePlayer();

    [Fact]
    public void RealJobData_ContainsStreetTrashCleanup()
    {
        var job = TestJobs.RealCatalog().Find(TestJobs.TrashCleanup);

        Assert.NotNull(job);
        Assert.Equal("Street Trash Cleanup", job.Title);
        Assert.Equal(5, job.Payout);
        Assert.Equal(40, job.XPReward);
        Assert.Equal(4, job.Tasks.Count);
    }

    [Fact]
    public void AcceptJob_AssignsJobAtFirstTask()
    {
        var player = NewSonny();

        var result = _careers.AcceptJob(player, TestJobs.TrashCleanup);

        Assert.True(result.Success);
        Assert.Equal(TestJobs.TrashCleanup, player.JobData.AssignedJobId);
        Assert.Equal(0, player.JobData.CurrentTaskIndex);
    }

    [Fact]
    public void AcceptJob_UnknownJob_FailsWithoutChanges()
    {
        var player = NewSonny();

        var result = _careers.AcceptJob(player, "no_such_job");

        Assert.False(result.Success);
        Assert.Equal(string.Empty, player.JobData.AssignedJobId);
    }

    [Fact]
    public void AcceptJob_WhileAJobIsActive_Fails()
    {
        var player = NewSonny();
        _careers.AcceptJob(player, TestJobs.TrashCleanup);
        _careers.CompleteCurrentTask(player);

        var result = _careers.AcceptJob(player, TestJobs.TrashCleanup);

        Assert.False(result.Success);
        Assert.Equal(1, player.JobData.CurrentTaskIndex); // progress kept
    }

    [Fact]
    public void AcceptJob_ReplacesAssignmentMissingFromCatalog()
    {
        var player = NewSonny();
        player.JobData.AssignedJobId = "retired_job";
        player.JobData.CurrentTaskIndex = 2;

        var result = _careers.AcceptJob(player, TestJobs.TrashCleanup);

        Assert.True(result.Success);
        Assert.Equal(TestJobs.TrashCleanup, player.JobData.AssignedJobId);
        Assert.Equal(0, player.JobData.CurrentTaskIndex);
    }

    [Fact]
    public void CompleteTask_AdvancesOneTaskAtATime_WithoutPayingEarly()
    {
        var player = NewSonny();
        _careers.AcceptJob(player, TestJobs.TrashCleanup);

        for (int expected = 1; expected <= 3; expected++)
        {
            var result = _careers.CompleteCurrentTask(player);

            Assert.True(result.Success);
            Assert.Null(result.Reward);
            Assert.Equal(expected, player.JobData.CurrentTaskIndex);
            Assert.Equal(1500, player.Cash);
            Assert.Equal(0, player.TotalXP);
        }
    }

    [Fact]
    public void CompleteFinalTask_AwardsCashAndXpAndClosesJob()
    {
        var player = NewSonny();
        _careers.AcceptJob(player, TestJobs.TrashCleanup);

        CareerResult last = null!;
        for (int i = 0; i < 4; i++) last = _careers.CompleteCurrentTask(player);

        Assert.True(last.Success);
        Assert.Equal(new JobReward(TestJobs.TrashCleanup, "Street Trash Cleanup", 5, 40, 0, 1), last.Reward);
        Assert.Equal(1505, player.Cash);
        Assert.Equal(40, player.XP);
        Assert.Equal(40, player.TotalXP);
        Assert.Equal(5, player.JobData.TotalEarned);
        Assert.Equal(1, player.JobData.JobsCompleted);
        Assert.Equal(string.Empty, player.JobData.AssignedJobId);
        Assert.Equal(0, player.JobData.CurrentTaskIndex);
    }

    [Fact]
    public void CompleteTask_AfterJobFinished_FailsAndPaysNothing()
    {
        var player = NewSonny();
        _careers.AcceptJob(player, TestJobs.TrashCleanup);
        for (int i = 0; i < 4; i++) _careers.CompleteCurrentTask(player);

        var extra = _careers.CompleteCurrentTask(player);

        Assert.False(extra.Success);
        Assert.Null(extra.Reward);
        Assert.Equal(1505, player.Cash);
        Assert.Equal(40, player.TotalXP);
        Assert.Equal(1, player.JobData.JobsCompleted);
    }

    [Fact]
    public void CompletedJob_CanBeTakenAgain_AndPaysAgain()
    {
        var player = NewSonny();

        for (int run = 0; run < 2; run++)
        {
            Assert.True(_careers.AcceptJob(player, TestJobs.TrashCleanup).Success);
            for (int i = 0; i < 4; i++) _careers.CompleteCurrentTask(player);
        }

        Assert.Equal(1510, player.Cash);
        Assert.Equal(80, player.TotalXP);
        Assert.Equal(2, player.JobData.JobsCompleted);
    }

    [Fact]
    public void CompleteTask_OutOfRangeProgress_FailsWithoutReward()
    {
        var player = NewSonny();
        player.JobData.AssignedJobId = TestJobs.TrashCleanup;
        player.JobData.CurrentTaskIndex = 9;

        var result = _careers.CompleteCurrentTask(player);

        Assert.False(result.Success);
        Assert.Equal(1500, player.Cash);
    }

    [Fact]
    public void JobReward_ReportsLevelUp()
    {
        var careers = new CareerService(new FakeJobCatalog(new Job
        {
            Id = "big_job", Title = "Big Job", Payout = 10, XPReward = 350, Tasks = ["Only task"]
        }));
        var player = NewSonny();
        careers.AcceptJob(player, "big_job");

        var reward = careers.CompleteCurrentTask(player).Reward!;

        // 350 XP from level 1: 100 to reach 2, 200 to reach 3, 50 left over.
        Assert.Equal(2, reward.LevelsGained);
        Assert.Equal(3, reward.NewLevel);
        Assert.Equal(3, player.Level);
        Assert.Equal(50, player.XP);
        Assert.Equal(350, player.TotalXP);
    }
}
