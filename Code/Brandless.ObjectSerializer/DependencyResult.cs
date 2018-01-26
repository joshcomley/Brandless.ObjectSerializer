using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Brandless.ObjectSerializer
{
	public class DependencyResult : ICollection<object>
	{
		public object Object { get; set; }
		public IEnumerable<Dependency> DependsOn { get { return _dependsOn.AsEnumerable(); } }
		private readonly List<Dependency> _dependsOn = new List<Dependency>();

		public DependencyResult(object @object)
		{
			Object = @object;
			_dependsOn = new List<Dependency>();
		}

		public void Clear()
		{
			_dependsOn.Clear();
		}

		public bool Contains(object dependant)
		{
			return _dependsOn.Any(d => d.Object == dependant);
		}

		public bool Contains(object dependant, object parent)
		{
			return _dependsOn.Any(d => d.Object == dependant && d.GraphParent == parent);
		}

		public void CopyTo(object[] array, int arrayIndex)
		{
			for (var i = 0; i < _dependsOn.Count; i++)
			{
				array[arrayIndex + i] = _dependsOn[i].Object;
			}
		}

		public bool Remove(object item)
		{
			return _dependsOn.RemoveAll(d => d.Object == item) > 0;
		}

		public int Count { get { return _dependsOn.Count; } }
		public bool IsReadOnly { get { return true; } }

		public void Add(object dependant)
		{
			throw new NotImplementedException();
		}

		public void Add(object dependant, object graphParent, bool topLevel)
		{
			_dependsOn.Add(new Dependency(dependant, graphParent, topLevel));
		}

		public bool HasTopLevelCircular()
		{
			return DependsOn.Any(d => d.Circular && d.TopLevel);
		}

		public IEnumerator<object> GetEnumerator()
		{
			return DependsOn.Select(d => d.Object).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}