using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AwoDevProxy.Shared.Utils.Tasks
{
	public interface ITaskHandler
	{
		public Type SourceType { get; }
		public Task GetTask(object source);
		public Task<bool> HandleTaskResult(Task task, object source);
	}
}
