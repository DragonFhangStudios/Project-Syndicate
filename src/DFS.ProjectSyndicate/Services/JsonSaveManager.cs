using System.IO;
using System.Text.Json;
using DFS.ProjectSyndicate.Models;
using DFS.ProjectSyndicate.ViewModels;

namespace DFS.ProjectSyndicate.Services
{
	public static class JsonSaveManager
	{
		private static readonly string SavePath = "player_save.json";

		public static void Save(SyndicatePlayer player)
		{
			var options = new JsonSerializerOptions
			{
				WriteIndented = true
			};

			string json = JsonSerializer.Serialize(player, options);
			File.WriteAllText(SavePath, json);
		}

		public static SyndicatePlayer Load()
		{
			if (!File.Exists(SavePath))
				return new SyndicatePlayer("PlayerOne");

			string json = File.ReadAllText(SavePath);
			return JsonSerializer.Deserialize<SyndicatePlayer>(json) ?? new SyndicatePlayer("PlayerOne");
		}
	}
}
