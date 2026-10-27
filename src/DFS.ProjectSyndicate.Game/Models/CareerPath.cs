namespace DFS.ProjectSyndicate.Models
{
	// A character's primary career: the top-level path whose ranks they climb. Only Civilian is playable so far;
	// Criminal and Emergency Services exist as identities so jobs and rules can name them.
	//
	// Web saves store the name ("Civilian"); the legacy WPF save stores the number, so the values are fixed.
	public enum CareerPath
	{
		Civilian = 0,
		Criminal = 1,
		EmergencyServices = 2
	}
}
