namespace Brandless.ObjectSerializer
{
	public class Dependency
	{
		public object Object { get; set; }
		public object GraphParent { get; set; }
		public bool TopLevel { get; set; }
		public bool Circular { get; set; }

		public Dependency(object @object, object graphParent, bool topLevel)
		{
			Object = @object;
			GraphParent = graphParent;
			TopLevel = topLevel;
		}
	}
}