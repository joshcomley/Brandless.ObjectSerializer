using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Brandless.ObjectSerializer.Extensions;
using Brandless.ObjectSerializer.GuidTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace Brandless.ObjectSerializer
{
    public class CSharpObjectSerializer
    {
        private const string ObjectSerializedGuidKey = @"[#ObjectSerializedGuid#]";
        private const string NewLineComment = @"NEWLINE";
        public const string RemoveSpaceToken = @"/*REMOVE_SPACE*/";
        private static readonly Guid Rfc4112Namespace = new Guid("23c5ef80-5369-4c11-b900-c52cfe7a3f3e");

        public CSharpObjectSerializer(
            CSharpSerializerParameters parameters = null
            )
        {
            Parameters = parameters ?? new CSharpSerializeToObjectParameters();
        }

        public CSharpSerializerParameters Parameters { get; set; }

        private object AllowThisObject { get; set; }

        public string Serialize(object @object)
        {
            return Serialize(@object,
                null);
        }

        private string Serialize(object @object, string className)
        {
            var serialize = Serialize(
                new CSharpObjectSerializerInstanceArguments(
                    @object,
                    new DependencyAnalyser().Analyse(@object),
                    SyntaxFactory.CompilationUnit()),
                className);
            return serialize;
        }

        private string Serialize(CSharpObjectSerializerInstanceArguments args, string className = null)
        {
            string baseClassName = null;
            var rootData = args.GetObjectData(args.Object);
            rootData.HasBeenSerialized = true;
            rootData.IsRoot = true;
            var objectParams = Parameters as CSharpSerializeToObjectParameters;
            if (objectParams != null)
            {
                SerializeToStandaloneObject(args,
                    args.Object,
                    objectParams.InstanceName);
            }
            var classParams = Parameters as CSharpSerializeToClassParameters;
            if (classParams != null)
            {
                AllowThisObject = args.Object;
                SerializeToStandaloneObject(args,
                    args.Object,
                    Parameters.InstanceName);
                if (className == null)
                {
                    className = classParams.ClassName;
                }
                baseClassName = classParams.BaseClassName;
            }
            if (className == null)
            {
                className = "SerializedObject";
            }

            var orderedObjectStatements = GetOrderedObjectStatements(args);
            if (args.ThisStatements.Any())
            {
                orderedObjectStatements = orderedObjectStatements
                    .AddComment(ToComment("Self"))
                    .Union(args.ThisStatements);
            }
            var classDeclarationSyntax = SyntaxFactory.ClassDeclaration(
                className)
                .NormalizeWhitespace2()
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(
                            SyntaxKind.PublicKeyword))).NormalizeWhitespace2()
                .WithMembers(
                    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                        SyntaxFactory.MethodDeclaration(
                            GetFullTypeSyntax(args.Object.GetType()),
                            SyntaxFactory.Identifier(
                                "GetData"))
                            .WithModifiers(
                                SyntaxFactory.TokenList(
                                    SyntaxFactory.Token(
                                        SyntaxKind.PublicKeyword)))
                                        .NormalizeWhitespace2()
                            .WithBody(
                                SyntaxFactory.Block(
                                    SyntaxFactory.List(
                                        orderedObjectStatements
                                        ))).AddBodyStatements(
                            SyntaxFactory.ReturnStatement(
                                SyntaxFactory.IdentifierName(Parameters.InstanceName)).NormalizeWhitespace()))
                );
            if (!string.IsNullOrWhiteSpace(baseClassName))
            {
                classDeclarationSyntax = classDeclarationSyntax.WithBaseList(
                    SyntaxFactory.BaseList(
                        SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(
                            SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(
                                baseClassName))
                            )));
            }
            if (!string.IsNullOrWhiteSpace(Parameters.Namespace))
            {
                args.CompilationUnit = args.CompilationUnit
                    .WithMembers(
                        SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                            SyntaxFactory.NamespaceDeclaration(
                                SyntaxFactory.IdentifierName(
                                    Parameters.Namespace)).NormalizeWhitespace2()
                                .WithMembers(
                                    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                                        classDeclarationSyntax))
                            ))
                    ;
            }
            else
            {
                args.CompilationUnit = args.CompilationUnit
                    .WithMembers(
                        SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                            classDeclarationSyntax)
                    )
                    ;
            }
            args.CompilationUnit = args.CompilationUnit.WithUsings(
                SyntaxFactory.List(
                    args.Namespaces.Distinct()
                        .Where(ns => string.IsNullOrWhiteSpace(Parameters.Namespace) || ns != Parameters.Namespace)
                        .OrderBy(n => n)
                        .Select(ns => SyntaxFactory.UsingDirective(
                            SyntaxFactory.IdentifierName(ns)).NormalizeWhitespace2())
                    ));
            var code = FormatCode(args.CompilationUnit);
            if (code.IndexOf(ObjectSerializedGuidKey) != -1)
            {
                code = code.Replace(ObjectSerializedGuidKey,
                    Rfc4122Guid.Create(Rfc4112Namespace,
                        code)
                        .ToString());
            }
            code = Regex.Replace(code,
                @"\}[\w\r\n\t]+(?<Symbol>\;|,)",
                "}${Symbol}\r\n");
            //			code = Regex.Replace(code, @"\, ", ",\r\n");
            //code = code.Replace(NewLineComment,
            //    "\n");
            return code;
        }

        /// <summary>
        ///     Will check an instance of an object against a file
        ///     to see if the instance has since changed in any way
        ///     more significant than whitespace. It does not mean
        ///     the two objects they end up representing are
        ///     necessarily different; only that something reasonable
        ///     within the structure of the code has changed.
        /// </summary>
        /// <param name="@object"></param>
        /// <param name="pathToCompareTo"></param>
        /// <returns></returns>
        public bool HasChanged(
            object @object,
            string pathToCompareTo)
        {
            //var commentsEnabled = Parameters.DescriptionFormatter.Enabled;
            //Parameters.DescriptionFormatter.Enabled = false;
            var tree1 = SyntaxFactory.ParseSyntaxTree(
                Serialize(@object, @object.GetType().Name)
                );
            var tree2 = SyntaxFactory.ParseSyntaxTree(
                File.ReadAllText(pathToCompareTo)
                );
            //			Parameters.DescriptionFormatter.Enabled = commentsEnabled;
            return
                NormaliseForComparison(tree1.GetCompilationUnitRoot()) !=
                NormaliseForComparison(tree2.GetCompilationUnitRoot());
        }

        /// <summary>
        ///     Serializes and object and then writes the serialized code
        ///     and the actual code on file next to each other in the specified
        ///     folder for comparison with a comparison tool
        /// </summary>
        /// <param name="path"></param>
        /// <param name="@object"></param>
        /// <param name="pathToCompareTo"></param>
        public void WriteForCompare(
            string path,
            object @object,
            string pathToCompareTo)
        {
            if (path == null)
            {
                path = @"C:\Admin\CodeCompare";
            }
            var tree1 = SyntaxFactory.ParseSyntaxTree(
                Serialize(@object)
                );
            var tree2 = SyntaxFactory.ParseSyntaxTree(
                File.ReadAllText(pathToCompareTo)
                );
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            File.WriteAllText(Path.Combine(path,
                "Compare_CompiledObject.cs"),
                NormaliseForComparison(tree1.GetCompilationUnitRoot()));
            File.WriteAllText(Path.Combine(path,
                "Compare_ObjectCodeFile.cs"),
                NormaliseForComparison(tree2.GetCompilationUnitRoot()));
        }

        public void SerializeTo(
            object @object,
            string path
            )
        {
            File.WriteAllText(path,
                Serialize(@object));
        }

        private static string NormaliseForComparison(SyntaxNode compilationUnit)
        {
            return new TriviaRemover().Visit(compilationUnit)
                .NormalizeWhitespace2()
                .ToFullString();
        }

        private static string FormatCode(SyntaxNode compilationUnit)
        {
            //compilationUnit = new KeywordRewriter().Visit(compilationUnit);
            var code =
                    compilationUnit
                        //.NormalizeWhitespace()
                        .ToFullString()
                //.Replace(NewLineComment, "\r\n")
                ;
            //var compilationUnitSyntax = SyntaxFactory.ParseCompilationUnit(code);
            //var node = compilationUnitSyntax.SyntaxTree.GetRoot();
            //node = Formatter.Format(node, new AdhocWorkspace());
            //return node.ToFullString();
            //compilationUnit = SyntaxFactory.ParseCompilationUnit(code);

            // Reformatting, for now, is disgustingly hacky as I cannot figure out
            // how to do it properly yet
            //code = ReformatCode(code);
            code = Regex.Replace(code, @"\s*__NEW__\s*", " ");
            var syntax = SyntaxFactory.ParseCompilationUnit(code).SyntaxTree.GetRoot();
            syntax = new WhiteSpaceRewriter().Visit(syntax);

            syntax = syntax.ReplaceTrivia(syntax.DescendantTrivia()
                .Where(t => t.IsKind(SyntaxKind.EndOfLineTrivia)),
                (t, _) => SyntaxFactory.SyntaxTrivia(t.Kind(), "\nNEWLINE"));
            code = syntax.ToFullString();
            code = Regex.Replace(code, $@"\s*{Regex.Escape(RemoveSpaceToken)}\s*", "");
            code = Regex.Replace(code, $@"^\s*{Regex.Escape("NEWLINE")}\s*$", "DELETELINE", RegexOptions.Multiline);
            var regex = new Regex("DELETELINE");
            code = regex.Replace(code, "NEWLINE", 1);
            code = code.Replace("NEWLINE", "");
            code = string.Join("\n", code.Split('\n').Where(l => l != "DELETELINE").ToArray());
            return code;
        }

        private static string ReformatCode(string code)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var options = workspace.Options;
                options = options.WithChangedOption(CSharpFormattingOptions.NewLineForMembersInObjectInit, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLineForCatch, true);
                options = options.WithChangedOption(
                    CSharpFormattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLineForClausesInQuery, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLineForElse, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLineForFinally, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLineForMembersInAnonymousTypes, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAccessors, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousMethods, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousTypes, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInLambdaExpressionBody, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInProperties, true);
                options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, true);
                options = options.WithChangedOption(FormattingOptions.SmartIndent, "C#", FormattingOptions.IndentStyle.Smart);
                //options = options.WithChangedOption(FormattingOptions.NewLine, "C#",  "\r\n\r\n");
                options = options.WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, true);

                options = options.WithChangedOption(CSharpFormattingOptions.LabelPositioning, LabelPositionOptions.LeftMost);
                options = options.WithChangedOption(CSharpFormattingOptions.SpaceWithinOtherParentheses, false);

                options = options.WithChangedOption(CSharpFormattingOptions.IndentBlock, true);
                options = options.WithChangedOption(CSharpFormattingOptions.IndentBraces, false);
                options = options.WithChangedOption(CSharpFormattingOptions.IndentSwitchCaseSection, true);
                options = options.WithChangedOption(CSharpFormattingOptions.IndentSwitchCaseSectionWhenBlock, true);
                options = options.WithChangedOption(CSharpFormattingOptions.IndentSwitchSection, true);

                options = options.WithChangedOption(CSharpFormattingOptions.SpaceAfterComma, true);
                options = options.WithChangedOption(CSharpFormattingOptions.SpaceBeforeComma, false);
                options = options.WithChangedOption(CSharpFormattingOptions.SpaceAfterMethodCallName, false);

                options = options.WithChangedOption(CSharpFormattingOptions.WrappingPreserveSingleLine, false);
                options = options.WithChangedOption(CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine, false);

                options = options.WithChangedOption(CSharpFormattingOptions.SpacingAroundBinaryOperator,
                    BinaryOperatorSpacingOptions.Remove);

                workspace.Options = options;
                var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default);
                var solution = workspace.AddSolution(solutionInfo);

                var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "TempProject",
                    "TempProject", "C#");
                workspace.AddProject(projectInfo);
                var docInfo = DocumentInfo.Create(
                    DocumentId.CreateNewId(projectInfo.Id),
                    "temp2.cs",
                    //filePath: item,
                    isGenerated: true,
                    loader: TextLoader.From(TextAndVersion.Create(SourceText.From(
                        code
                    ), VersionStamp.Default)));
                var document = workspace.AddDocument(docInfo);
                document.Project.Solution.Workspace.Options = options;
                //workspace.LoadMetadataForReferencedProjects = true;
                //var project = workspace.CurrentSolution.AddProject("TempProject", "TempProject", "C#");
                //workspace.CurrentSolution.Projects.First();
                //var readAllText = File.ReadAllText(item);
                //var document = project.AddDocument("temp2.cs", SourceText.From(readAllText));
                //var project = workspace.CurrentSolution.Projects.First();
                //compilationUnit = Formatter.Format(
                //    compilationUnit, Formatter.Annotation, workspace);
                document = Formatter.FormatAsync(document, options).GetAwaiter().GetResult();
                //await engine.FormatSolutionAsync(project.Solution, cancellationToken);
                //var newDoc = project.Documents.First();
                //foreach (var doc in workspace.CurrentSolution.Projects.First().Documents)
                //{

                //}
                //var root = await document.GetSyntaxRootAsync(cancellationToken);
                //code = root.ToFullString();
                var newRoot = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
                //newRoot = new KeywordRewriter().Visit(newRoot);
                code = newRoot
                        .NormalizeWhitespace()
                        .ToFullString()
                    //.Replace(NewLineComment, "\r\n")
                    ;
                //Console.WriteLine(code);
            }
            return code;
        }
        //var formatted = Formatter.FormatAsync(root, adhocWorkspace, options);
        //return formatted
        //          //.NormalizeWhitespace2("    ", eol:"\n")
        //          .ToFullString();        }

        private static IEnumerable<StatementSyntax> GetOrderedObjectStatements(
        CSharpObjectSerializerInstanceArguments args)
        {
            var sorted = args.ObjectStatements.Keys.TopologicalSort(
                @object => args.InitialiserDependencies.ContainsKey(@object)
                    ? args.InitialiserDependencies[@object]
                    : new List<object>())
                .ToList();
            var statementSyntaxes = args.ObjectStatements
                .OrderBy(x => sorted.IndexOf(x.Key))
                .Select(x => x.Value);
            if (args.CircularStatements.Any())
            {
                statementSyntaxes = new List<StatementSyntax>()
                        .AddComment(ToComment("Circular dependencies"))
                        .Union(args.EndObjectStatements.Values)
                        .Union(args.CircularStatements)
                        .Union(statementSyntaxes)
                        .AddComment(ToComment("Late circular dependencies"))
                        .Union(args.LateCircularObjectStatements.Values)
                        .Union(args.LateCircularStatements)
                    ;
            }
            return statementSyntaxes;
        }

        private void SerializeObjectToThis(CSharpObjectSerializerInstanceArguments args, object @object)
        {
            foreach (var property in GetSerializableProperties(@object, false))
            {
                var value = property.GetValue(@object);
                var serializeObjectToInitialiser = SerializeObjectToInitialiser(
                    args,
                    value,
                    false,
                    property,
                    @object,
                    false);
                if (serializeObjectToInitialiser != null)
                {
                    args.ThisStatements.Add(SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.BinaryExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName(
                                property.Name),
                            serializeObjectToInitialiser)));
                }
            }
        }

        private void SerializeToStandaloneObject(CSharpObjectSerializerInstanceArguments args,
            object @object,
            string instanceName, string description = null, bool isCircular = false)
        {
            AddNamespace(args, @object);
            args.GetObjectData(@object).Identifier = instanceName;
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                SerializeObjectToThis(args, @object);
            }
            else
            {
                var syntaxTrivias =
                    string.IsNullOrWhiteSpace(description)
                        ? new SyntaxTrivia[] { }
                        : new[]
                        {
                            SyntaxFactory.Comment(
                                ToComment(description)),
                            SyntaxFactory.LineFeed,
                        };
                var objectStatements =
                    isCircular
                        ?
                        (@object is IEnumerable ? args.LateCircularObjectStatements : args.EndObjectStatements)
                        : args.ObjectStatements;
                objectStatements.Add(
                    @object,
                    SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.IdentifierName(
                                SyntaxFactory.Identifier(
                                    SyntaxFactory.TriviaList(
                                        syntaxTrivias),
                                    @"var",
                                    SyntaxFactory.TriviaList(
                                        SyntaxFactory.Space)).NormalizeWhitespace())).NormalizeWhitespace2()
                            .WithVariables(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.VariableDeclarator(
                                        SyntaxFactory.Identifier(
                                            instanceName)).NormalizeWhitespace2()
                                        .WithInitializer(
                                            SyntaxFactory.EqualsValueClause(
                                                SerializeObjectToInitialiser(args,
                                                    @object,
                                                    true,
                                                    null,
                                                    null,
                                                    false)
                                                ))))).NormalizeWhitespace()
                    );
            }
        }

        private static string ToComment(string description)
        {
            return string.Format(@"// {0}",
                description);
        }

        private static void AddNamespace(CSharpObjectSerializerInstanceArguments args, object @object)
        {
            if (@object == null) return;
            var @namespace = @object.GetType()
                .Namespace;
            if (!string.IsNullOrWhiteSpace(@namespace) && !args.Namespaces.Contains(@namespace))
            {
                args.Namespaces.Add(@namespace);
            }
        }

        private IEnumerable<SyntaxNodeOrToken> SerializeEnumerableElements(
            CSharpObjectSerializerInstanceArguments args,
            IEnumerable elms
            )
        {
            var nodes = new List<SyntaxNodeOrToken>();
            foreach (var elm in elms)
            {
                nodes.Add(SerializeObjectToInitialiser(args,
                    elm,
                    false,
                    null,
                    null,
                    true));
                nodes.Add(CommaWithNewLine());
            }
            RemoveTrailingComma(nodes);
            return nodes.ToArray();
        }

        private static void RemoveTrailingComma(IList nodes)
        {
            if (nodes.Count > 0) nodes.RemoveAt(nodes.Count - 1);
        }

        private ExpressionSyntax SerializeObjectToInitialiser(
            CSharpObjectSerializerInstanceArguments args,
            object @object,
            bool allowClasses,
            PropertyInfo propertyBeingAssigned,
            object propertyOwner,
            bool serializeNulls)
        {
            foreach (var stackObject in args.DependencyStack)
            {
                args.RegisterDependencies(stackObject,
                    @object);
            }
            args.DependencyStack.Push(@object);
            var syntax = SerializeObjectToInitialiserInner(
                args,
                @object,
                allowClasses,
                propertyBeingAssigned,
                propertyOwner,
                serializeNulls
                );
            args.DependencyStack.Pop();
            return syntax;
        }

        private ExpressionSyntax SerializeObjectToInitialiserInner(
            CSharpObjectSerializerInstanceArguments args,
            object @object,
            bool allowClasses,
            PropertyInfo propertyBeingAssigned,
            object propertyOwner,
            bool serializeNulls)
        {
            if (propertyBeingAssigned?.Name == "ExamCandidateResults")
            {
                int a = 0;
            }
            AddNamespace(args,
                @object);
            if (@object == null)
            {
                return serializeNulls ||
                        // If the default is NOT null, then we MUST serialize the null value
                        !IsSameAsDefaultValueAfterInitialise(propertyBeingAssigned,
                            @object)
                    ? SyntaxFactory.LiteralExpression(
                        SyntaxKind.NullLiteralExpression)
                    : null;
            }

            if (this.ReturnType == null)
            {
                ReturnType = @object.GetType();
            }

            if (SerializeObject(args, @object, out var syntaxFactoryLiteralExpression))
            {
                return syntaxFactoryLiteralExpression;
            }
            var type = @object.GetType();
            var enumerable = @object as IEnumerable;
            // We're serializing an object
            var dependencyResult = args.Dependencies[@object];
            bool isCircular;
            if (!Parameters.AllowObjectInitializer)
            {
                isCircular = true;
            }
            else
            {
                isCircular = dependencyResult != null &&
                    dependencyResult.HasTopLevelCircular();
            }
            var multipleUsages = args.Dependencies.IsDependedUponMultipleTimes(@object);
            if (
                (
                    allowClasses ||
                    (
                        !isCircular &&
                        !multipleUsages
                        )
                    )
                //|| @object is IEnumerable
                )
            {
                if (enumerable == null)
                {
                    args.GetObjectData(@object)
                        .HasBeenSerialized = true;
                    // Serialize the items to an initialiser
                    // and the properties to post-initialise
                    // assignment
                    var hasSerializableProperties = GetSerializableProperties(@object, serializeNulls)
                        .Any();
                    var objectCreationExpressionSyntax = SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.Token(
                            new SyntaxTriviaList(),
                            SyntaxKind.NewKeyword,
                            new SyntaxTriviaList(SyntaxFactory.Comment("__NEW__"))
                        ),
                        GetFullTypeSyntax(@object.GetType()),
                        null,
                        null);
                    return hasSerializableProperties
                        // new X { Y = Z }
                        ? objectCreationExpressionSyntax
                            .WithInitializer(
                                SyntaxFactory.InitializerExpression(
                                    SyntaxKind.ObjectInitializerExpression,
                                    SerializeProperties(args,
                                        @object)))
                        // new X()
                        : objectCreationExpressionSyntax.WithArgumentList(SyntaxFactory.ArgumentList());
                }

                // Any other kind of IEnumerable, so we may have properties, too
                // We will do those separately and take advantage of sleek
                // object intiailiser to cater for 99% of scenarios
                SerializePropertiesToPostInitialiseAssignment(
                    args,
                    @object);
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                var objectCreationExpression = SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.Token(
                        new SyntaxTriviaList(),
                        SyntaxKind.NewKeyword,
                        new SyntaxTriviaList(SyntaxFactory.Comment("__NEW__"))
                    ),
                    GetFullTypeSyntax(type),
                    null,
                    null
                    );
                return enumerable.Cast<object>().Any()
                    // new X { Y, Z }
                    ? objectCreationExpression
                        .WithInitializer(
                            SyntaxFactory.InitializerExpression(
                                SyntaxKind.CollectionInitializerExpression,
                                SyntaxFactory.SeparatedList<ExpressionSyntax>(
                                    // Serialize the properties to an initialiser
                                    SerializeEnumerableElements(args,
                                        enumerable))))
                    // new X()
                    : objectCreationExpression.WithArgumentList(SyntaxFactory.ArgumentList());
            }
            // Serialize to a separate object instance out of the
            // scope of the initialiser
            if (args.GetObjectData(@object)
                .HasBeenSerialized)
            {
                if (!isCircular || propertyBeingAssigned == null)
                    return GetObjectReferenceSyntax(args,
                        @object);
                // Queue up a property assignment later on
                SerializePropertyToPostParentInitialiseAssignment(args,
                    propertyOwner,
                    propertyBeingAssigned,
                    false);
                return null;
            }
            // Serialize to local variable
            if (multipleUsages || propertyBeingAssigned == null || true)
            {
                var instanceName = GetInstanceName(args,
                    @object,
                    type);
                var description =
                    Parameters.DescriptionFormatter != null &&
                    Parameters.DescriptionFormatter.Enabled
                        ? Parameters.DescriptionFormatter.FormatOrNull(new DescriptionFormatterArguments(),
                            type,
                            @object)
                        : null;
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                SerializeToStandaloneObject(
                    args,
                    @object,
                    instanceName,
                    description,
                    isCircular);
                var leadingTrivia = SyntaxFactory.TriviaList();
                if (!string.IsNullOrWhiteSpace(description))
                {
                    leadingTrivia = leadingTrivia.Add(
                        SyntaxFactory.Comment(
                            ToComment(description)));
                }
                var serializeObjectToInitialiser = SyntaxFactory.Identifier(
                    leadingTrivia,
                    instanceName,
                    SyntaxFactory.TriviaList()
                    );
                return SyntaxFactory.IdentifierName(serializeObjectToInitialiser);
            }

            //		if (propertyBeingAssigned != null && isCircular && @object is IEnumerable)
            //			{
            // Queue up a property assignment later on
            if (!args.GetObjectData(@object)
                .HasBeenSerialized)
            {
                SerializePropertyToPostParentInitialiseAssignment(args,
                    propertyOwner,
                    propertyBeingAssigned,
                    true);
            }
            return null;
            //	}
        }

        private bool SerializeObject(CSharpObjectSerializerInstanceArguments args, object @object,
            out ExpressionSyntax syntaxFactoryLiteralExpression)
        {
            var type = @object.GetType();
            if (@object is DateTime)
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = Constructor(args, type,
                        ((DateTime)@object).Ticks);
                    return true;
                }
            }

            if (@object is DateTimeOffset)
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = Constructor(args, type,
                        ((DateTimeOffset)@object).Ticks, TimeSpan.Zero
                    );
                    return true;
                }
            }

            if (@object is TimeSpan)
            {
                var ts = (TimeSpan)@object;
                if (ts == TimeSpan.Zero)
                {
                    var left = SyntaxFactory.IdentifierName(nameof(TimeSpan));
                    var right = SyntaxFactory.IdentifierName(nameof(TimeSpan.Zero));
                    syntaxFactoryLiteralExpression =
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            left,
                            right)
                        ;
                    return true;
                }
                else
                {
                    args.GetObjectData(@object)
                        .HasBeenSerialized = true;
                    {
                        syntaxFactoryLiteralExpression = Constructor(args, type,
                            ts.Ticks
                        );
                        return true;
                    }
                }
            }

            if (@object is Guid)
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = Constructor(args, type,
                        @object.ToString());
                    return true;
                }
            }

            if (type.IsEnum)
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(
                            type.Name),
                        SyntaxFactory.IdentifierName(
                            @object.ToString()));
                    return true;
                }
            }

            if (@object is Type)
            {
                syntaxFactoryLiteralExpression = SyntaxFactory.TypeOfExpression(GetFullTypeSyntax(@object as Type));
                return true;
            }

            if (@object.GetType()
                .IsSubclassOfRawGeneric(typeof(KeyValuePair<,>)))
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = SyntaxFactory.InitializerExpression(
                        SyntaxKind.ComplexElementInitializerExpression,
                        SyntaxFactory.SeparatedList<ExpressionSyntax>(
                            new SyntaxNodeOrToken[]
                            {
                                SerializeObjectToInitialiser(
                                    args,
                                    @object.GetPropertyValue("Key"),
                                    false,
                                    null,
                                    null,
                                    false),
                                SyntaxFactory.Token(
                                    SyntaxKind.CommaToken),
                                SerializeObjectToInitialiser(args,
                                    @object.GetPropertyValue("Value"),
                                    false,
                                    null,
                                    null,
                                    false),
                            }));
                    return true;
                }
            }

            // Last resort before enumerables or classes
            if (@object is string || type.IsPrimitive || (!type.IsClass && type.IsValueType))
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = SyntaxFactoryLiteralExpression(@object,
                        type);
                    return true;
                }
            }

            if (type.IsArray)
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = SyntaxFactory.ArrayCreationExpression(
                            SyntaxFactory.ArrayType(GetTypeSyntax(type.GetElementType()))
                                .WithRankSpecifiers(
                                    SyntaxFactory.SingletonList(
                                        SyntaxFactory.ArrayRankSpecifier(
                                            SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                                                SyntaxFactory.OmittedArraySizeExpression())))))
                        .NormalizeWhitespace()
                        .WithInitializer(
                            SyntaxFactory.InitializerExpression(
                                type.IsArray
                                    ? SyntaxKind.ArrayInitializerExpression
                                    : SyntaxKind.CollectionInitializerExpression,
                                SyntaxFactory.SeparatedList<ExpressionSyntax>(
                                    SerializeEnumerableElements(args,
                                        @object as IEnumerable)))).NormalizeWhitespace();
                    return true;
                }
            }
            syntaxFactoryLiteralExpression = null;
            return false;
        }

        public Type ReturnType { get; set; }

        private static ExpressionSyntax SyntaxFactoryLiteralExpression(object @object, Type type)
        {
            if (type.IsNumeric())
            {
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactoryLiteral(
                        @object
                        )
                    );
            }
            if (Equals(@object,
                false))
            {
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.FalseLiteralExpression);
            }
            if (Equals(@object,
                true))
            {
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.TrueLiteralExpression);
            }
            if (@object is char)
            {
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.CharacterLiteralExpression,
                    SyntaxFactoryLiteral(
                        @object
                    )
                );
            }
            return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactoryLiteral(
                    @object
                    )
                );
        }

        private ObjectCreationExpressionSyntax Constructor(
            CSharpObjectSerializerInstanceArguments args,
            Type type,
            params object[] values)
        {
            var arguments = new List<ArgumentSyntax>();
            foreach (var value in values)
            {
                if (!SerializeObject(args, value, out var syntax))
                {
                    syntax = SyntaxFactory.LiteralExpression(
                        SyntaxKind.TrueLiteralExpression,
                        SyntaxFactoryLiteral(value)
                    );
                }
                arguments.Add(SyntaxFactory.Argument(syntax));
            }
            return SyntaxFactory.ObjectCreationExpression(
                GetFullTypeSyntax(type))
                .NormalizeWhitespace2()
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(arguments)
                        //SyntaxFactory.SingletonSeparatedList(
                        //    SyntaxFactory.Argument(
                        //        syntax
                        //        )
                        //    )
                        )
                );
        }

        private static TypeSyntax GetFullTypeSyntax(Type type)
        {
            TypeSyntax nameForIntialiser;
            var genericArguments = type.GetGenericArguments();
            if (genericArguments.Length > 0)
            {
                nameForIntialiser = SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier(
                        UnwrapGenericName(type)))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SeparatedList(
                                genericArguments.Select(GetFullTypeSyntax)
                                )));
            }
            else
            {
                nameForIntialiser = SyntaxFactory.IdentifierName(type.Name);
            }
            return nameForIntialiser.NormalizeWhitespace();
        }

        private void SerializePropertiesToPostInitialiseAssignment(
            CSharpObjectSerializerInstanceArguments args,
            object @object)
        {
            foreach (var property in GetSerializableProperties(@object, false))
            {
                SerializePropertyToPostParentInitialiseAssignment(args,
                    @object,
                    property,
                    false);
            }
        }

        private IEnumerable<PropertyInfo> GetSerializableProperties(object @object, bool serializeNulls)
        {
            var serializableProperties = DependencyAnalyser.GetSerializableProperties(@object)
                .Where(p => !Parameters.IgnoreConditions.Any(ic => ic.Ignore(@object,
                    p)));
            if (!serializeNulls)
            {
                serializableProperties = serializableProperties
                    .Where(p => !Equals(p.GetValue(@object),
                        null));
            }
            return serializableProperties
                ;
        }

        private string GetInstanceName(CSharpObjectSerializerInstanceArguments args, object @object, Type type)
        {
            string name;
            var suggestedNameBuilder = new StringBuilder();
            suggestedNameBuilder.Append(UnwrapGenericName(type));
            var genericArguments = type.GetGenericArguments();
            foreach (var genericTypeArgument in genericArguments)
            {
                suggestedNameBuilder.Append(UnwrapGenericName(genericTypeArgument));
            }
            var suggestedName = suggestedNameBuilder.ToString();
            if (Parameters.InstanceNameFormatter != null &&
                Parameters.InstanceNameFormatter.Enabled &&
                Parameters.InstanceNameFormatter.TryFormat(
                    new InstanceNameFormatterArguments(args.InstanceNames,
                        suggestedName),
                    type,
                    @object,
                    out name))
            {
                switch (Parameters.InstanceNameFormatter.SuggestedNameLocation)
                {
                    case InstanceNameFormatterSuggestedNameLocation.Prefix:
                        name = string.Format("{0}{1}",
                            suggestedName,
                            name);
                        break;
                    case InstanceNameFormatterSuggestedNameLocation.Suffix:
                        name = string.Format("{0}{1}",
                            name,
                            suggestedName);
                        break;
                }
            }
            else
            {
                name = suggestedName;
            }
            // Suffix with a number if we already have one of these
            if (!args.InstanceNameCount.ContainsKey(name))
            {
                args.InstanceNameCount.Add(name,
                    0);
            }
            Func<string> getName = () =>
            {
                args.InstanceNameCount[name] = args.InstanceNameCount[name] + 1;
                return string.Format("{0}{1}",
                    name.FirstLetterToLowerInvariant(),
                    args.InstanceNameCount[name] == 1
                        ? string.Empty
                        : args.InstanceNameCount[name].ToString()
                    );
            };
            var newName = getName();
            while (args.InstanceNames.Contains(newName))
            {
                newName = getName();
            }
            args.InstanceNames.Add(newName);
            return newName;
        }

        private static string UnwrapGenericName(Type type)
        {
            var startIndex = type.Name.IndexOf('`');
            return startIndex == -1
                ? type.Name
                : type.Name.Remove(startIndex);
        }

        private static TypeSyntax GetTypeSyntax(Type type)
        {
            var resolveSyntaxKindFromType = ResolveSyntaxKindFromType(type);
            if (resolveSyntaxKindFromType != null)
                return SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(
                        resolveSyntaxKindFromType.Value));
            return SyntaxFactory.IdentifierName(type.Name);
        }

        //private static Type GetElementType(Type type)
        //{
        //	if (type.HasElementType) return type.GetElementType();
        //	var types =
        //		(from method in type.GetMethods()
        //		 where method.Name == "get_Item"
        //		 select method.ReturnType
        //		).Distinct().ToArray();
        //	if (types.Length == 0)
        //		return null;
        //	if (types.Length != 1)
        //		throw new Exception(string.Format("{0} has multiple item types", type.FullName));
        //	return types[0];
        //}

        private static SyntaxKind? ResolveSyntaxKindFromType(Type type)
        {
            var name = string.Format("{0}Keyword",
                type.Name);
            var enumMatchedName = Enum.GetNames(typeof(SyntaxKind))
                .SingleOrDefault(n => n.Equals(name,
                    StringComparison.OrdinalIgnoreCase));
            if (enumMatchedName == null) return null;
            return (SyntaxKind)Enum.Parse(typeof(SyntaxKind),
                enumMatchedName);
        }

        private static SyntaxToken SyntaxFactoryLiteral(object @object)
        {
            var type = @object.GetType();
            if (type == typeof(byte)) type = typeof(int);
            var method =
                typeof(SyntaxFactory).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Single(m =>
                    {
                        if (m.Name != "Literal") return false;
                        var p = m.GetParameters();
                        if (p.Count() != 1) return false;
                        return p.Single()
                            .ParameterType == type;
                    });
            return (SyntaxToken)method.Invoke(null,
                new[] { @object });
        }

        private SeparatedSyntaxList<ExpressionSyntax> SerializeProperties(
            CSharpObjectSerializerInstanceArguments args,
            object @object)
        {
            var elms = new List<SyntaxNodeOrToken>();
            foreach (var property in GetSerializableProperties(@object, false))
            {
                var value = property.GetValue(@object);
                var dependencyResult = args.Dependencies[value];
                bool isCircular;
                if (!Parameters.AllowObjectInitializer)
                {
                    isCircular = true;
                }
                else
                {
                    isCircular =
                        (!(Parameters is CSharpSerializeToClassParameters) || value != args.Object)
                        //&& !(value is IEnumerable)
                        && dependencyResult != null
                        && dependencyResult.HasTopLevelCircular()
                        ;
                }
                var valueToAssignReferenceSyntax =
                    SerializeAndOrGetObjectReferenceSyntax(args,
                        value,
                        property,
                        @object,
                        false);
                if (valueToAssignReferenceSyntax != null)
                {
                    if (isCircular && CanAssignPropertiesTo(args,
                        @object))  // && !(valueToAssignReferenceSyntax is IdentifierNameSyntax))
                    {
                        // Assign this property later
                        SerializePropertyToPostParentInitialiseAssignment(args,
                            @object,
                            property,
                            valueToAssignReferenceSyntax);
                    }
                    else
                    {
                        elms.Add(
                            SyntaxFactory.AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.IdentifierName(
                                    property.Name),
                                valueToAssignReferenceSyntax
                                )
                            );
                        elms.Add(CommaWithNewLine());
                    }
                }
            }
            // Remove trailing comma
            RemoveTrailingComma(elms);
            return SyntaxFactory.SeparatedList<ExpressionSyntax>(
                elms.ToArray());
        }

        private ExpressionSyntax SerializeAndOrGetObjectReferenceSyntax(
            CSharpObjectSerializerInstanceArguments args,
            object value,
            PropertyInfo propertyBeingAssigned,
            object propertyOwner,
            bool allowNestedClasses)
        {
            ExpressionSyntax valueToAssignReferenceSyntax;
            if (value != null && DependencyAnalyser.IsDependableObject(value) && args.GetObjectData(value)
                .HasBeenSerialized)
            {
                // This object needs to be initialised after
                // the object it references
                //args.RegisterDependencies(@object, value);
                valueToAssignReferenceSyntax = GetObjectReferenceSyntax(args,
                    value);
            }
            else
            {
                valueToAssignReferenceSyntax = SerializeObjectToInitialiser(
                    args,
                    value,
                    allowNestedClasses,
                    propertyBeingAssigned,
                    propertyOwner,
                    false
                    );
            }
            return valueToAssignReferenceSyntax;
        }

        private void SerializePropertyToPostParentInitialiseAssignment(
            CSharpObjectSerializerInstanceArguments args,
            object propertyOwner,
            PropertyInfo property,
            bool allowNestedClasses
            )
        {
            var value = property.GetValue(propertyOwner);
            if (IsSameAsDefaultValueAfterInitialise(property,
                value))
                return;
            SerializePropertyToPostParentInitialiseAssignment(args,
                propertyOwner,
                property,
                SerializeAndOrGetObjectReferenceSyntax(args,
                    value,
                    property,
                    propertyOwner,
                    allowNestedClasses));
        }

        private static bool IsSameAsDefaultValueAfterInitialise(PropertyInfo property, object value)
        {
            if (property.DeclaringType.TryCreateInstance(out var instance))
            {
                return property.GetValue(instance) == value;
            }
            return false;
        }

        private void SerializePropertyToPostParentInitialiseAssignment(
            CSharpObjectSerializerInstanceArguments args,
            object propertyOwner,
            PropertyInfo property,
            ExpressionSyntax valueToAssignReferenceSyntax)
        {
            var assignmentExpression = GetAssignmentExpression(
                args,
                propertyOwner,
                property);
            var assignmentExpressionSyntax = SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                assignmentExpression,
                valueToAssignReferenceSyntax);
            var expressionStatementSyntax =
                SyntaxFactory.ExpressionStatement(assignmentExpressionSyntax);
            if (!typeof(string).IsAssignableFrom(property.PropertyType) &&
                typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                args.LateCircularStatements.Add(expressionStatementSyntax);
            }
            else
            {
                args.CircularStatements.Add(expressionStatementSyntax);
            }
        }

        private ExpressionSyntax GetAssignmentExpression(CSharpObjectSerializerInstanceArguments args,
            object propertyOwner, PropertyInfo property)
        {
            var identifierNameSyntax = SyntaxFactory.IdentifierName(
                property.Name);

            if (!CanAssignPropertiesTo(args,
                propertyOwner))
            {
                throw new InvalidOperationException("\"this\" setting is not allowed on given instance of object of type \"{0}\""
                    .FormatText(propertyOwner.GetType()));
            }
            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GetObjectReferenceSyntax(args,
                    propertyOwner),
                identifierNameSyntax);
        }

        private bool CanAssignPropertiesTo(CSharpObjectSerializerInstanceArguments args, object propertyOwner)
        {
            if (string.IsNullOrWhiteSpace(args.GetObjectData(propertyOwner)
                .Identifier) && AllowThisObject != propertyOwner)
            {
                return false;
            }
            return true;
        }

        private static ExpressionSyntax GetObjectReferenceSyntax(CSharpObjectSerializerInstanceArguments args, object value)
        {
            var name = args.GetObjectData(value)
                .Identifier;
            var valueToAssignReferenceSyntax = name == null
                ? (ExpressionSyntax)SyntaxFactory.ThisExpression()
                    .WithToken(
                        SyntaxFactory.Token(
                            SyntaxKind.ThisKeyword))
                : SyntaxFactory.IdentifierName(
                    name);
            return valueToAssignReferenceSyntax;
        }

        private static SyntaxToken CommaWithNewLine()
        {
            /* 
			This is a hack at the moment
			because using SyntaxKind.NewLineFeed etc.
			doesn't actually add the new line at the
			end but putting a comment forces a new 
			line. However, we must remove the comment
			later.
			*/
            return SyntaxFactory.Token(
                SyntaxFactory.TriviaList(),
                SyntaxKind.CommaToken,
                SyntaxFactory.TriviaList(SyntaxFactory.LineFeed)
                    //SyntaxFactory.TriviaList(SyntaxFactory.Comment(NewLineComment))
                    );
        }

        private class TriviaRemover : CSharpSyntaxRewriter
        {
            private Dictionary<SyntaxNode, IList<UsingDirectiveSyntax>> _usings
                = new Dictionary<SyntaxNode, IList<UsingDirectiveSyntax>>();
            private Dictionary<SyntaxNode, int> _usingsIndex
                = new Dictionary<SyntaxNode, int>();
            /// <summary>
            /// Sorts the using statments alphabetically
            /// </summary>
            /// <param name="node"></param>
            /// <returns></returns>
            public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
            {
                if (!_usings.ContainsKey(node.Parent))
                {
                    var usings = node.Parent.ChildNodes().OfType<UsingDirectiveSyntax>()
                        .ToList()
                        ;
                    _usingsIndex.Add(node.Parent,
                        0);
                    _usings.Add(node.Parent,
                        usings
                            .OrderBy(c => c.Name.ToString())
                            .ToList());
                }
                var syntax = _usings[node.Parent][_usingsIndex[node.Parent]];
                _usingsIndex[node.Parent] = _usingsIndex[node.Parent] + 1;
                return syntax;
            }

            public override SyntaxToken VisitToken(SyntaxToken token)
            {
                token = token.WithLeadingTrivia(SyntaxFactory.TriviaList());
                token = token.WithTrailingTrivia(SyntaxFactory.TriviaList());
                return base.VisitToken(token);
            }
        }
    }

    class KeywordRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.IsKind(SyntaxKind.CloseBraceToken))
            {
                return SyntaxFactory.Token(
                    token.LeadingTrivia.Add(SyntaxFactory.LineFeed),
                    SyntaxKind.CloseBraceToken, "}", "}",
                    token.TrailingTrivia.Add(SyntaxFactory.LineFeed)
                    );
            }
            if (token.IsKind(SyntaxKind.OpenBraceToken))
            {
                return SyntaxFactory.Token(
                    token.LeadingTrivia.Add(SyntaxFactory.LineFeed),
                    SyntaxKind.OpenBraceToken, "{", "{",
                    token.TrailingTrivia.Add(SyntaxFactory.LineFeed)
                    );
            }
            if (token.IsKind(SyntaxKind.CommaToken))
            {
                return SyntaxFactory.Token(
                    token.LeadingTrivia,
                    SyntaxKind.CommaToken, ",", ",",
                    token.TrailingTrivia.Add(SyntaxFactory.LineFeed)
                    );
            }

            return token;
        }
    }

    class WhiteSpaceRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            var kind = trivia.Kind();
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return SyntaxFactory.SyntaxTrivia(
                    trivia.Kind(),
                    "NEWLINE"
                );
            }
            return base.VisitTrivia(trivia);
        }

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.IsKind(SyntaxKind.CommaToken) ||
                token.IsKind(SyntaxKind.GreaterThanToken) ||
                token.IsKind(SyntaxKind.LessThanToken))
            {
                SyntaxTriviaList trailingTrivia =
                    SyntaxFactory.TriviaList(SyntaxFactory.LineFeed);
                var addNewLine = true;
                if (token.IsKind(SyntaxKind.GreaterThanToken))
                {
                    var parent = token.Parent;
                    while (parent != null)
                    {
                        if (parent is BlockSyntax)
                        {
                            break;
                        }
                        if (parent is MethodDeclarationSyntax)
                        {
                            addNewLine = false;
                            break;
                        }
                        parent = parent.Parent;
                    }
                    if (!addNewLine)
                    {
                        trailingTrivia = SyntaxFactory.TriviaList(
                            SyntaxFactory.Space);
                    }
                }
                else if (token.IsKind(SyntaxKind.LessThanToken))
                {
                    addNewLine = false;
                    trailingTrivia =
                        SyntaxTriviaList.Empty;
                }
                SyntaxFactory.TriviaList(SyntaxFactory.LineFeed);
                return SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.Comment(CSharpObjectSerializer.RemoveSpaceToken)),
                    token.Kind(),
                    trailingTrivia
                );
            }

            return token;
        }
    }
}