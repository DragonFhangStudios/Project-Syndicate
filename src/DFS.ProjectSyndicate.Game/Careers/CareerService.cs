using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Jobs;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Careers
{
	public sealed record JobReward(string JobId, string JobTitle, int Cash, int XP, int LevelsGained, int NewLevel);

	public sealed record CareerResult(bool Success, string Message, JobReward? Reward = null)
	{
		public static CareerResult Ok(string message, JobReward? reward = null) => new(true, message, reward);
		public static CareerResult Fail(string message) => new(false, message);
	}

	// Civilian career rules. Pure functions on a player: no I/O, no shared state.
	public sealed class CareerService
	{
		private readonly IJobCatalog _jobs;

		public CareerService(IJobCatalog jobs)
		{
			_jobs = jobs;
		}

		// The character's job in progress, or null. An assignment that can't be worked (the job is no longer in
		// the catalog, the saved progress is outside its task list, e.g. after the job data changed, the job's
		// definition is invalid or isn't open to the character's career, or the character itself is in a state the
		// rules can't act on) is treated as no job: it never pays, and it never blocks taking new work.
		public Job? ActiveJob(SyndicatePlayer player)
		{
			var job = _jobs.Find(player.JobData.AssignedJobId);
			int index = player.JobData.CurrentTaskIndex;
			// CareerMayWork first: it validates the definition, so the task list is known to exist before it is read.
			return job is not null && CareerMayWork(player, job) && index >= 0 && index < job.Tasks.Count ? job : null;
		}

		public CareerResult AcceptJob(SyndicatePlayer player, string jobId)
		{
			string? standing = CareerStanding.Problem(player);
			if (standing is not null)
				return CareerResult.Fail(standing);

			if (ActiveJob(player) is not null)
				return CareerResult.Fail("Finish your current job before taking another.");

			var job = _jobs.Find(jobId);
			if (job is null || job.Tasks is null || job.Tasks.Count == 0)
				return CareerResult.Fail("That job is not available.");

			// Enforced here, not only by the UI: a request for a locked job is refused.
			var unmet = JobEligibility.Requirements(player, job).Where(r => !r.Met).Select(r => r.Label).ToList();
			if (unmet.Count > 0)
				return CareerResult.Fail($"{job.Title} requires {string.Join(" and ", unmet)}.");

			player.JobData.AssignedJobId = job.Id;
			player.JobData.CurrentTaskIndex = 0;
			return CareerResult.Ok($"You took the {job.Title} job. First task: {job.Tasks[0]}.");
		}

		public CareerResult CompleteCurrentTask(SyndicatePlayer player)
		{
			string? standing = CareerStanding.Problem(player);
			if (standing is not null)
				return CareerResult.Fail(standing);

			var job = _jobs.Find(player.JobData.AssignedJobId);
			if (job is null || !CareerMayWork(player, job))
				return CareerResult.Fail("You don't have an active job.");

			int index = player.JobData.CurrentTaskIndex;
			if (index < 0 || index >= job.Tasks.Count)
				return CareerResult.Fail("This job's progress is out of range.");

			string task = job.Tasks[index];
			player.JobData.CurrentTaskIndex = index + 1;

			if (index + 1 < job.Tasks.Count)
				return CareerResult.Ok($"Done: {task}. Next: {job.Tasks[index + 1]}.");

			// Final task: close the job before paying out, so the same job can never pay twice.
			player.JobData.AssignedJobId = string.Empty;
			player.JobData.CurrentTaskIndex = 0;
			player.JobData.JobsCompleted++;
			player.JobData.TotalEarned += job.Payout;
			player.Cash += job.Payout;
			int levelsGained = player.AddXP(job.XPReward);

			var reward = new JobReward(job.Id, job.Title, job.Payout, job.XPReward, levelsGained, player.Level);
			string message = $"{job.Title} complete: earned ${job.Payout} and {job.XPReward} XP."
				+ (levelsGained > 0 ? $" You reached level {player.Level}." : string.Empty);
			return CareerResult.Ok(message, reward);
		}

		public CareerResult Promote(SyndicatePlayer player)
		{
			// Judged against the player's own career's ranks. A career without ranks, an unknown career or an
			// impossible tier is refused with its own reason; short of the requirements or at the top rank, the
			// message is the long-standing one.
			string? problem = PromotionStatus.For(player).Problem;
			if (problem is not null)
				return CareerResult.Fail(problem);

			if (!ProgressionManager.TryPromote(player))
				return CareerResult.Fail("You don't meet the requirements for promotion yet.");

			return CareerResult.Ok($"Promoted to Tier {player.Tier}: {ProgressionManager.GetTierName(player.CareerPath, player.Tier)}.");
		}

		// The character can act, the job is a valid definition, and it is open to their career. Level and tier
		// requirements are checked when a job is accepted; losing them later doesn't cancel work in progress.
		private static bool CareerMayWork(SyndicatePlayer player, Job job) =>
			CareerStanding.Problem(player) is null
			&& JobCatalog.Validate(job) is null
			&& JobAccess.Read(job, out _)!.Admits(player.CareerPath);
	}
}
