using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Jobs
{
	public sealed record JobRequirement(string Label, bool Met);

	// Who may take a job. First the job must be a valid definition and the character one the rules can act on
	// (CareerStanding); then the job's career access, level and career tier. Within a character's own career,
	// level and tier rise by working, so no job open to that career is ever out of reach for good.
	public static class JobEligibility
	{
		public static IReadOnlyList<JobRequirement> Requirements(SyndicatePlayer player, Job job)
		{
			// A definition that breaks the job rules is closed to everyone, including its owning career, wherever it
			// came from: the catalog refuses such data, but other sources might not.
			if (JobCatalog.Validate(job) is not null)
				return [new JobRequirement(JobAccess.Validate(job) is not null ? "Valid career data for this job" : "A valid job definition", false)];

			// A character the rules can't act on can take no work at all, shared work included.
			if (!Enum.IsDefined(player.CareerPath))
				return [new JobRequirement("A recognized career", false)];
			if (!CareerStanding.HasValidTier(player))
				return [new JobRequirement($"A valid {CareerInfo.DisplayName(player.CareerPath)} rank", false)];

			var access = JobAccess.Read(job, out _)!;
			var requirements = new List<JobRequirement>(3);

			// Listed only when it fails, so a Civilian sees exactly the requirements they always have.
			if (!access.Admits(player.CareerPath))
				requirements.Add(new JobRequirement($"{CareerInfo.DisplayName(access.Owner)} career", false));

			if (job.MinLevel > 1)
				requirements.Add(new JobRequirement($"Level {job.MinLevel}", player.Level >= job.MinLevel));
			// Tiers are ranks within the job's owning career (a valid job's tier is one that career has): only that
			// career's characters can meet it, and another career's tier never counts.
			if (job.MinTier > 1)
				requirements.Add(new JobRequirement(
					$"Tier {job.MinTier} · {ProgressionManager.GetTierName(access.Owner, job.MinTier)}",
					player.CareerPath == access.Owner && player.Tier >= job.MinTier));
			return requirements;
		}

		public static bool IsEligible(SyndicatePlayer player, Job job) => Requirements(player, job).All(r => r.Met);

		// Whether a job belongs on this character's job board at all (locked or not): a valid job that is shared, or
		// belongs to their career. Other careers' restricted work is left off rather than shown as locked.
		public static bool IsListedFor(SyndicatePlayer player, Job job) =>
			JobCatalog.Validate(job) is null && JobAccess.Read(job, out _)!.Admits(player.CareerPath);
	}
}
