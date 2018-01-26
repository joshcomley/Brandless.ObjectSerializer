using System.Collections.Generic;
using System.Linq;

namespace Brandless.ObjectSerializer
{
	public class DependencyAnalysisResult : List<DependencyResult>
	{
		public DependencyResult this[object key]
		{
			get
			{
				return this.SingleOrDefault(k => k.Object == key);
			}
		}
		public bool IsDependedUponMultipleTimes(object key)
		{
			return this.Count(k => k.DependsOn.Any(d => d.Object == key && d.TopLevel)) > 1;
		}
		public bool Contains(object key)
		{
			return this.Any(k => k.Object == key);
		}
	}
}