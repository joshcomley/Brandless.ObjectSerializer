using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brandless.ObjectSerializer
{
	public class DependencyAnalyser
	{
		private Stack<object> _stack;

		public DependencyAnalysisResult Analyse(object @object)
		{
			var results = new DependencyAnalysisResult();
			Analyse(@object, results, null);
			foreach (var result in results)
			{
				foreach(var selfReference in result.DependsOn.Where(r => r.Object == result.Object && r.TopLevel))
				{
					MarkCircular(selfReference, results);
				}
			}
			foreach (var result in results)
			{
				foreach (var result2 in results)
				{
					if (result2 == result) continue;
					// Do they reference each other
					var d1s = result.DependsOn.Where(r => r.Object == result2.Object);
					var d2s = result2.DependsOn.Where(r => r.Object == result.Object);
					if (!d1s.Any() || !d2s.Any()) continue;
					foreach (var d1 in d1s)
//						if (!(d1.Object == result2.Object && d1.GraphParent == result.Object))
							MarkCircular(d1, results);
					foreach (var d2 in d2s)
	//					if (!(d2.Object == result.Object && d2.GraphParent == result2.Object))
							MarkCircular(d2, results);
				}
			}
			return results;
		}

		private static void MarkCircular(Dependency d1, DependencyAnalysisResult results)
		{
			d1.Circular = true;
			if (!(d1.GraphParent is IEnumerable)) return;
			var dependency = results[d1.GraphParent].DependsOn
				.FirstOrDefault(d => d.Object == d1.Object);
			if (dependency != null)
				dependency.Circular = true;
		}

		private void Analyse(object @object, DependencyAnalysisResult dependencies, object parent)
		{
			if (_stack == null) _stack = new Stack<object>();
			var isNew = false;
			if (!dependencies.Contains(@object))
			{
				dependencies.Add(new DependencyResult(@object));
				isNew = true;
			}
			foreach (var stack in _stack.Where(stack => !dependencies[stack].Contains(@object, parent)))
			{
				dependencies[stack].Add(@object, parent, false);
			}
			if (_stack.Any())
			{
				var topLevels = dependencies[_stack.Peek()].DependsOn.Where(d => d.Object == @object && d.GraphParent == parent)
					.ToList();
				topLevels.ForEach(tl => tl.TopLevel = true);
			}
			if (!isNew) return;
			_stack.Push(@object);
			if (@object is IEnumerable)
			{
				foreach (var item in @object as IEnumerable)
				{
					Analyse(item, dependencies, @object);
				}
			}
			if (@object.GetType()
				.IsSubclassOfRawGeneric(typeof (KeyValuePair<,>)))
			{
				Analyse(@object.GetPropertyValue("Key"), dependencies, @object);
				Analyse(@object.GetPropertyValue("Value"), dependencies, @object);
			}
			foreach (var property in GetSerializableProperties(@object))
			{
				var value = property.GetValue(@object);
				if (!IsDependableObject(value)) continue;
				Analyse(value, dependencies, @object);
			}
			_stack.Pop();
		}

		public static IEnumerable<PropertyInfo> GetSerializableProperties(object @object)
		{
			return GetSerializableProperties(@object.GetType());
		}

		public static IEnumerable<PropertyInfo> GetSerializableProperties(Type type)
		{
			return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => !p.GetIndexParameters().Any() &&
				p.CanWrite);
		}

		public static bool IsDependableObject(object @object)
		{
			if (@object == null) return false;
			if (@object is string) return false;
			if (@object.GetType().IsValueType) return false;
			if (@object.GetType().IsPrimitive) return false;
			if (@object.GetType().IsArray) return false;
			return true;
		}
	}
}