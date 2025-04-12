using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectSyndicate.Models
{
	public class SyndicatePlayer
	{
		public string Name { get; set; }
		public int Level { get; set; }
		public float Cash { get; set; }
		public float Strength { get; set; }
		public float Intellect { get; set; }
		public float Endurance { get; set; }

		public SyndicatePlayer(string name)
		{
			Name = name;
			Level = 1;
			Cash = 500;
			Strength = 5;
			Intellect = 5;
			Endurance = 5;
		}
	}
}
