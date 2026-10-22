using System.Text.Json;
using System.Text.RegularExpressions;
using DFS.JobSystem.Core;
using DFS.JobSystem.Managers;

namespace DFS.ProjectSyndicate.Jobs
{
	public interface IJobCatalog
	{
		IReadOnlyList<Job> All { get; }
		Job? Find(string jobId);
	}

	// Job definitions loaded once from DFS.JobSystem's SimpleJobs.json.
	public sealed class JobCatalog : IJobCatalog
	{
		// Lowercase words joined by underscores, e.g. "night_security". Ids are stored in saves and used in page markup.
		private static readonly Regex IdPattern = new("^[a-z0-9]+(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);
		private const int MaxIdLength = 64;

		private readonly JobManager _manager = new();

		// Resolved from the app's own folder, not the working directory.
		public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "SimpleJobs.json");

		public JobCatalog(string jsonPath)
		{
			// JobLoader silently ignores a missing file; fail loudly instead so the app never starts with no jobs.
			if (!File.Exists(jsonPath)) throw new FileNotFoundException("Job data file not found.", jsonPath);

			// Checked on the raw list, before JobManager keeps the first of any duplicate ids and drops the rest.
			// Read the same way JobLoader reads it, so the WPF app and the web game agree on the data.
			var definitions = ReadDefinitions(jsonPath);
			string? problem = ValidateAll(definitions);
			if (problem is not null) throw new InvalidDataException($"Job data in '{jsonPath}' is invalid: {problem}.");

			foreach (var job in definitions) _manager.Register(job!);
			All = _manager.GetAllJobs().ToList();
		}

		// Returns null for a usable job definition, otherwise what is wrong with it.
		public static string? Validate(Job job)
		{
			if (string.IsNullOrWhiteSpace(job.Id)) return "missing id";
			if (job.Id.Length > MaxIdLength || !IdPattern.IsMatch(job.Id))
				return "id must be lowercase letters and digits joined by underscores, e.g. \"night_security\"";
			if (string.IsNullOrWhiteSpace(job.Title)) return "missing title";
			if (job.Tasks is null || job.Tasks.Count == 0 || job.Tasks.Any(string.IsNullOrWhiteSpace)) return "needs at least one named task";
			if (job.Payout < 0 || job.XPReward < 0) return "payout and XP cannot be negative";
			if (job.MinLevel < 0 || job.MinTier < 0 || job.MinTier > Managers.ProgressionManager.MaxTier) return "requirements out of range";
			return null;
		}

		// Checks every definition and that no two share an id. Returns null when the whole list is usable.
		// Entries are numbered from 1 in messages, as they appear in the file.
		public static string? ValidateAll(IReadOnlyList<Job?> definitions)
		{
			if (definitions.Count == 0) return "no jobs are defined";

			var firstEntryById = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < definitions.Count; i++)
			{
				int entry = i + 1;
				var job = definitions[i];
				if (job is null) return $"entry {entry} is empty";

				string? problem = Validate(job);
				if (problem is not null)
					return string.IsNullOrWhiteSpace(job.Id) ? $"entry {entry}: {problem}" : $"job '{job.Id}' (entry {entry}): {problem}";

				if (firstEntryById.TryGetValue(job.Id, out int first))
					return $"job id '{job.Id}' is used by entry {first} and entry {entry}; every job needs its own id";
				firstEntryById.Add(job.Id, entry);
			}
			return null;
		}

		public IReadOnlyList<Job> All { get; }

		public Job? Find(string jobId) => string.IsNullOrEmpty(jobId) ? null : _manager.GetJob(jobId);

		private static List<Job?> ReadDefinitions(string jsonPath)
		{
			try
			{
				return JsonSerializer.Deserialize<List<Job?>>(File.ReadAllText(jsonPath))
					?? throw new InvalidDataException($"Job data in '{jsonPath}' is invalid: the file contains null instead of a list of jobs.");
			}
			catch (JsonException ex)
			{
				throw new InvalidDataException($"Job data in '{jsonPath}' is not valid JSON: {ex.Message}", ex);
			}
		}
	}
}
