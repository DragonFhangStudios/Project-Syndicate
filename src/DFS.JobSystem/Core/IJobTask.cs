namespace DFS.JobSystem.Core
{
	public interface IJobTask
	{
		string Name { get; }
		string Description { get; }
		int Reward { get; }
		bool IsCompleted { get; }

		void Execute();
	}
}
