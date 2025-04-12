using System.Collections.Generic;

namespace DFS.JobSystem.Core
{
	public class JobContext
	{
		public string PlayerId { get; set; }

		public Dictionary<string, object> Data { get; set; } = new();

		public JobContext(string playerId)
		{
			PlayerId = playerId;
		}

		// Optional helper method to retrieve data safely
		public T? Get<T>(string key)
		{
			if (Data.TryGetValue(key, out var value) && value is T typed)
				return typed;

			return default;
		}

		public void Set<T>(string key, T value)
		{
			Data[key] = value!;
		}
	}
}
