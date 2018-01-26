using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Brandless.ObjectSerializer
{
	internal class CSharpObjectSerializerInstanceArguments
	{
		public object Object { get; set; }
		public Stack<object> DependencyStack = new Stack<object>();
		public Dictionary<string, int> InstanceNameCount = new Dictionary<string, int>();
		public List<string> InstanceNames = new List<string>();
		public DependencyAnalysisResult Dependencies { get; set; }
		public Dictionary<object, List<object>> InitialiserDependencies { get; set; }
		private Dictionary<object, CSharpObjectSerializeData> ObjectsSerialized { get; set; }
		public CompilationUnitSyntax CompilationUnit { get; set; }
		public Dictionary<object, StatementSyntax> ObjectStatements { get; set; }
		public Dictionary<object, StatementSyntax> EndObjectStatements { get; set; }
		public Dictionary<object, StatementSyntax> LateCircularObjectStatements { get; set; }
		public List<StatementSyntax> CircularStatements { get; set; }
		public List<StatementSyntax> LateCircularStatements { get; set; }
		public List<StatementSyntax> ThisStatements { get; set; }
		public List<string> Namespaces { get; set; }

		public CSharpObjectSerializerInstanceArguments(
			object @object, 
			DependencyAnalysisResult dependencies, 
			CompilationUnitSyntax compilationUnit
			)
		{
			Object = @object;
			Dependencies = dependencies;
			CompilationUnit = compilationUnit;
			ObjectStatements = new Dictionary<object, StatementSyntax>();
			EndObjectStatements = new Dictionary<object, StatementSyntax>();
			LateCircularObjectStatements = new Dictionary<object, StatementSyntax>();
			ThisStatements = new List<StatementSyntax>();
			ObjectsSerialized = new Dictionary<object, CSharpObjectSerializeData>();
			CircularStatements = new List<StatementSyntax>();
		    LateCircularStatements = new List<StatementSyntax>();
			InitialiserDependencies = new Dictionary<object, List<object>>();
			Namespaces = new List<string>();
		}

		public CSharpObjectSerializeData GetObjectData(object @object)
		{
			if (!ObjectsSerialized.ContainsKey(@object))
			{
				ObjectsSerialized.Add(@object, new CSharpObjectSerializeData());
			}
			return ObjectsSerialized[@object];
		}

		public void RegisterDependencies(
			object @object,
			params object[] dependencies
			)
		{
			if (!DependencyAnalyser.IsDependableObject(@object)) return;
			if (!InitialiserDependencies.ContainsKey(@object))
			{
				InitialiserDependencies.Add(@object, new List<object>());
			}
			foreach (var dependency in dependencies
				.Where(DependencyAnalyser.IsDependableObject)
				.Where(dependency => !Dependencies.Contains(@object) || !Dependencies[@object].Contains(dependency))
				.Where(dependency => dependency != @object)
				)
			{
				InitialiserDependencies[@object].Add(dependency);
			}
		}

	}
}