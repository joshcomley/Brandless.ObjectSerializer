using System.Collections.Generic;

namespace Brandless.ObjectSerializer
{
	public class InstanceNameFormatterArguments : FormatterArguments
	{
		public IEnumerable<string> InstanceNames { get; private set; }
		public string SuggestedName { get; set; }

		public InstanceNameFormatterArguments(
			IEnumerable<string> instanceNames, 
			string suggestedName
			)
		{
			InstanceNames = instanceNames;
			SuggestedName = suggestedName;
		}
	}
}