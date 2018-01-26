using System;
using System.Reflection;

namespace Brandless.ObjectSerializer
{
	public class IgnoreCondition
	{
		public Func<object, PropertyInfo, bool> Ignore { get; set; }

		public IgnoreCondition(Func<object, PropertyInfo, bool> ignore)
		{
			Ignore = ignore;
		}
	}
}