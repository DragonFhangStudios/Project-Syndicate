using ProjectSyndicate.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectSyndicate.ViewModels
{
	public class DashboardViewModel
	{
		public SyndicatePlayer Player { get; set; }

		public DashboardViewModel()
		{
			// Hardcoded for now; later pull from save file or login
			Player = new SyndicatePlayer("PlayerOne");
		}
	}
}
