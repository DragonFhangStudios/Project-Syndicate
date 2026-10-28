using DFS.JobSystem.Core;
using DFS.ProjectSyndicate.Models;

namespace DFS.ProjectSyndicate.Careers
{
	public enum JobAccessPolicy
	{
		// Only characters of the owning career may take the job.
		Restricted,
		// Characters of any career may take the job.
		Shared
	}

	// Which careers may take a job. Jobs store their owning career and access policy as text (DFS.JobSystem has no
	// reference to the game); this reads them. A job without either field is Civilian and Restricted.
	public sealed record JobAccess(CareerPath Owner, JobAccessPolicy Policy)
	{
		// Reads a job's career fields. Returns null, with the reason, when either field holds an unknown value.
		// Names must match exactly ("EmergencyServices"); numbers, other casing and combinations are refused.
		public static JobAccess? Read(Job job, out string? problem)
		{
			problem = null;

			var owner = CareerPath.Civilian;
			if (job.Career is not null && !TryParseExact(job.Career, out owner))
			{
				problem = $"unknown career \"{job.Career}\"; use {string.Join(", ", Enum.GetNames<CareerPath>())}";
				return null;
			}

			var policy = JobAccessPolicy.Restricted;
			if (job.Access is not null && !TryParseExact(job.Access, out policy))
			{
				problem = $"unknown access \"{job.Access}\"; use {string.Join(" or ", Enum.GetNames<JobAccessPolicy>())}";
				return null;
			}

			return new JobAccess(owner, policy);
		}

		// Returns null for usable career fields, otherwise what is wrong with them.
		public static string? Validate(Job job)
		{
			var access = Read(job, out string? problem);
			if (access is null) return problem;

			// Tiers are ranks within one career, so a tier requirement only makes sense for one career's own work,
			// and only for a rank that career has (CareerProgressions).
			if (job.MinTier > 1)
			{
				var ranks = CareerProgressions.For(access.Owner);
				if (ranks is not null && job.MinTier > ranks.MaxTier)
					return "requirements out of range";
				if (access.Policy == JobAccessPolicy.Shared)
					return "a shared job can't require a tier, because tiers are ranks within one career";
				if (ranks is null)
					return $"a {CareerInfo.DisplayName(access.Owner)} job can't require a tier until that career has ranks";
			}
			return null;
		}

		// Whether a character of this career may take the job. A career the game doesn't know is never admitted.
		public bool Admits(CareerPath career) =>
			Enum.IsDefined(career) && (Policy == JobAccessPolicy.Shared || career == Owner);

		private static bool TryParseExact<TEnum>(string text, out TEnum value) where TEnum : struct, Enum
		{
			value = default;
			return Enum.GetNames<TEnum>().Contains(text, StringComparer.Ordinal) && Enum.TryParse(text, out value);
		}
	}

	public static class CareerInfo
	{
		// Careers a character can actually have: created with, saved and loaded. Only Civilian until another
		// career has approved content; the others are identities that jobs and rules may name.
		public static bool IsPlayable(CareerPath career) => career == CareerPath.Civilian;

		public static string DisplayName(CareerPath career) => career switch
		{
			CareerPath.Civilian => "Civilian",
			CareerPath.Criminal => "Criminal",
			CareerPath.EmergencyServices => "Emergency Services",
			_ => "unknown"
		};
	}
}
