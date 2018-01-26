namespace Brandless.ObjectSerializer
{
	public class CSharpSerializeToClassParameters : CSharpSerializerParameters
	{
		public string ClassName { get; set; }
		public string BaseClassName { get; set; }

		public CSharpSerializeToClassParameters(string className, string instanceName = null, string baseClassName = null)
            :base(instanceName)
		{
			ClassName = className;
			BaseClassName = baseClassName;
		}
	}
}