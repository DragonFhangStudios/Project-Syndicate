using DFS.JobSystem.Core;

namespace DFS.JobSystem.Data
{
	public static class DeliveryJob
	{
		public static Job Create()
		{
			var job = new Job(
				id: "delivery_driver",
				title: "Delivery Driver",
				description: "Deliver packages across the city. Watch out for traffic and tight deadlines.",
				tier: 1
			);

			job.Tasks.Add(new JobTask("Pick up package", "Collect the package from Warehouse A", 150));
			job.Tasks.Add(new JobTask("Drive to client", "Travel to the delivery location", 200));
			job.Tasks.Add(new JobTask("Drop off package", "Hand the package over to the client", 250));

			return job;
		}
	}
}
