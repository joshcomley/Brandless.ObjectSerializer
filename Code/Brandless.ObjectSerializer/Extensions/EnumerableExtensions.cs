using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Brandless.ObjectSerializer.Extensions
{
	public static class EnumerableExtensions
	{
		public static T MaxOrDefault<T>(
			this IEnumerable<T> source)
		{
            if (source == null) throw new ArgumentNullException("source");
			var enumerable = source as IList<T> ?? source.ToList();
			return enumerable.Any()
				? enumerable.Max()
				: default(T);
		}

		public static T MinOrDefault<T>(
			this IEnumerable<T> source)
		{
			if (source == null) throw new ArgumentNullException("source");
			var enumerable = source as IList<T> ?? source.ToList();
			return enumerable.Any()
				? enumerable.Min()
				: default(T);
		}

		public static IEnumerable<T> TopologicalSort<T>(
			this IEnumerable<T> source, 
			Func<T, IEnumerable<T>> dependencies, 
			bool throwOnCycle = false)
		{
			var sorted = new List<T>();
			var visited = new HashSet<T>();

			foreach (var item in source)
				Visit(item, visited, sorted, dependencies, throwOnCycle);

			return sorted;
		}

		private static void Visit<T>(T item, ISet<T> visited, ICollection<T> sorted, Func<T, IEnumerable<T>> dependencies, bool throwOnCycle)
		{
			if (!visited.Contains(item))
			{
				visited.Add(item);

				foreach (var dep in dependencies(item))
					Visit(dep, visited, sorted, dependencies, throwOnCycle);

				sorted.Add(item);
			}
			else
			{
				if (throwOnCycle)
					throw new Exception("Cyclic dependency found");
			}
		}

		private static MethodInfo _castGenericMethod;
        public static IEnumerable Cast(this IEnumerable source, Type type)
		{
			_castGenericMethod = _castGenericMethod ?? 
                typeof(EnumerableExtensions).GetRuntimeMethods().Single(m => m.Name == "CastGeneric");
			return (IEnumerable)_castGenericMethod
			    .MakeGenericMethod(type)
			    .Invoke(null, new object[] { source });
		}

		// ReSharper disable once UnusedMember.Local
		private static IEnumerable<TResult> CastGeneric<TResult>(IEnumerable source)
		{
			var enumerator = source.GetEnumerator();
			while (enumerator.MoveNext())
			{
				var current = enumerator.Current;
				yield return (TResult)current;
			}
		}
	}
}