using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Careers;
using DFS.ProjectSyndicate.Managers;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Jobs
{
	public sealed record JobRequirement(string Label, bool Met);

	// Who may take a job: the job's career access, then level and career tier. Within a character's own career,
	// level and tier rise by working, so no job open to that career is ever out of reach for good.
	public static class JobEligibility
	{
		public static IReadOnlyList<JobRequirement> Requirements(SyndicatePlayer player, Job job)
		{
			var requirements = new List<JobRequirement>(3);

			// Listed only when it fails, so a Civilian sees exactly the requirements they always have.
			var access = JobAccess.Read(job, out _);
			bool careerAdmitted = access is not null && access.Admits(player.CareerPath);
			if (!careerAdmitted)
				requirements.Add(new JobRequirement(CareerRequirementLabel(player, access), false));

			if (job.MinLevel > 1)
				requirements.Add(new JobRequirement($"Level {job.MinLevel}", player.Level >= job.MinLevel));
			// Tiers are ranks within the job's own career: another career's tier never counts towards it.
			if (job.MinTier > 1)
				requirements.Add(new JobRequirement(
					$"Tier {job.MinTier} · {ProgressionManager.GetTierName(job.MinTier)}", careerAdmitted && player.Tier >= job.MinTier));
			return requirements;
		}

		public static bool IsEligible(SyndicatePlayer player, Job job) => Requirements(player, job).All(r => r.Met);

		// Whether a job belongs on this character's job board at all (locked or not): it is shared, or it belongs to
		// their career. Other careers' restricted work is left off rather than shown as locked.
		public static bool IsListedFor(SyndicatePlayer player, Job job) =>
			JobAccess.Read(job, out _)?.Admits(player.CareerPath) == true;

		private static string CareerRequirementLabel(SyndicatePlayer player, JobAccess? access)
		{
			if (access is null) return "Valid career data for this job";
			if (!Enum.IsDefined(player.CareerPath)) return "A recognized career";
			return $"{CareerInfo.DisplayName(access.Owner)} career";
		}
	}
}
