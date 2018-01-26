using System;
using System.Runtime.InteropServices;

namespace Brandless.ObjectSerializer.GuidTools
{
	public class SequentialGuid
	{
		[DllImport("rpcrt4.dll", SetLastError = true)]
		static extern int UuidCreateSequential(out Guid guid);

		public static Guid NewSequentialGuid()
		{
			const int rpcSOk = 0;
			Guid g;
			var hr = UuidCreateSequential(out g);
			if (hr != rpcSOk)
				throw new ApplicationException
				  ("UuidCreateSequential failed: " + hr);
			return g;
		}
	}
}
