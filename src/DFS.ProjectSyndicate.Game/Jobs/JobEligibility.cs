using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Jobs
{
	public sealed record JobRequirement(string Label, bool Met);

	// Who may take a job. Requirements are level and career tier only: both rise by working, so no
	// character is ever locked out permanently.
	public static class JobEligibility
	{
		public static IReadOnlyList<JobRequirement> Requirements(SyndicatePlayer player, Job job)
		{
			var requirements = new List<JobRequirement>(2);
			if (job.MinLevel > 1)
				requirements.Add(new JobRequirement($"Level {job.MinLevel}", player.Level >= job.MinLevel));
			if (job.MinTier > 1)
				requirements.Add(new JobRequirement(
					$"Tier {job.MinTier} · {ProgressionManager.GetTierName(job.MinTier)}", player.Tier >= job.MinTier));
			return requirements;
		}

		public static bool IsEligible(SyndicatePlayer player, Job job) => Requirements(player, job).All(r => r.Met);
	}
}
