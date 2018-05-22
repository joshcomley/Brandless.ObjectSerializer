using System;
using System.Collections.Generic;
using Brandless.Extensions;
using Brandless.ObjectSerializer.Extensions;

namespace Brandless.ObjectSerializer
{
	public abstract class CSharpSerializerParameters
	{
		public List<IgnoreCondition> IgnoreConditions { get; set; } 
		private DescriptionFormatter _descriptionFormatter;
		private InstanceNameFormatter _instanceNameFormatter;
        public bool InstanceOnly { get; set; }
        public string InstanceName { get; set; }
	    public bool Beautify { get; set; } = true;
	    public bool AllowObjectInitializer { get; set; } = true;

        public string Namespace { get; set; }

		public DescriptionFormatter DescriptionFormatter
		{
			get { return _descriptionFormatter = _descriptionFormatter ?? new DescriptionFormatter(); }
		}

		public InstanceNameFormatter InstanceNameFormatter
		{
			get { return _instanceNameFormatter = _instanceNameFormatter ?? new InstanceNameFormatter(); }
		}

		protected CSharpSerializerParameters(string instanceName)
		{
			IgnoreConditions = new List<IgnoreCondition>
			{
				new IgnoreCondition(
					(o, p) => p.DeclaringType.IsSubclassOfRawGeneric(typeof (List<>)) && p.Name == "Capacity")
			};
		    InstanceName = string.IsNullOrWhiteSpace(instanceName) ? "instance" : instanceName;
		}
	}
}