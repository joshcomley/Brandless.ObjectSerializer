using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Brandless.Extensions;
using Brandless.ObjectSerializer.Extensions;
using Brandless.ObjectSerializer.GuidTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TsBeautify;

namespace Brandless.ObjectSerializer
{
    public class CSharpObjectSerializer
    {
        public List<ICSharpObjectSerializerConverter> Converters { get; set; } = new List<ICSharpObjectSerializerConverter>();
        private const string ObjectSerializedGuidKey = @"[#ObjectSerializedGuid#]";
        private static readonly Guid Rfc4112Namespace = new Guid("23c5ef80-5369-4c11-b900-c52cfe7a3f3e");
        private object AllowThisObject { get; set; }

        public CSharpObjectSerializer(
            CSharpSerializerParameters parameters = null
        )
        {
            Parameters = parameters ?? new CSharpSerializerParameters();
        }

        public CSharpSerializerParameters Parameters { get; set; }

        private ObjectToObjectSerializeOutput Output { get; set; }

        public Type ReturnType { get; set; }

        public string SerializeToString(object @object)
        {
            return Serialize(@object).Class;
        }

        private string SerializeToString(object @object, string className)
        {
            return Serialize(@object, className).Class;
        }

        public ObjectToObjectSerializeOutput Serialize(object @object)
        {
            return Serialize(@object, null);
        }

        private ObjectToObjectSerializeOutput Serialize(object @object, string className)
        {
            var serialize = Serialize(
                new CSharpObjectSerializerInstanceArguments(
                    @object,
                    new DependencyAnalyser().Analyse(@object),
                    SyntaxFactory.CompilationUnit()),
                className);
            return serialize;
        }

        private ObjectToObjectSerializeOutput Serialize(CSharpObjectSerializerInstanceArguments args, string className = null)
        {
            Output = new ObjectToObjectSerializeOutput();
            string baseClassName = null;
            var rootData = args.GetObjectData(args.Object);
            rootData.HasBeenSerialized = true;
            rootData.IsRoot = true;
            var code = "";

            AllowThisObject = args.Object;
            SerializeToStandaloneObject(
                args,
                args.Object,
                Parameters.InstanceName);
            if (className == null)
            {
                className = Parameters.ClassName;
            }

            baseClassName = Parameters.BaseClassName;

            if (className == null)
            {
                className = "SerializedObject";
            }

            var orderedObjectStatements = GetOrderedObjectStatements(args).ToList();
            if (args.ThisStatements.Any())
            {
                orderedObjectStatements = orderedObjectStatements
                    .AddComment(ToComment("Self"))
                    .Union(args.ThisStatements)
                    .ToList();
            }

            if (!string.IsNullOrEmpty(Parameters.BeforeInstanceComment))
            {
                orderedObjectStatements.Insert(0, ToSingleLineCommentSyntax(Parameters.BeforeInstanceComment));
            }
            if (!string.IsNullOrEmpty(Parameters.AfterInstanceComment))
            {
                orderedObjectStatements.Add(ToSingleLineCommentSyntax(Parameters.AfterInstanceComment));
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
                                TypeSyntax(args.Object.GetType()),
                                SyntaxFactory.Identifier(
                                    "GetData")).NormalizeWhitespace()
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
                            SyntaxFactory.SimpleBaseType(
                                SyntaxFactory.IdentifierName(
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
                        .Select(
                            ns => SyntaxFactory.UsingDirective(
                                SyntaxFactory.IdentifierName(ns)).NormalizeWhitespace2())
                ));
            code = FormatCode(args.CompilationUnit);

            if (code.IndexOf(ObjectSerializedGuidKey) != -1)
            {
                code = code.Replace(
                    ObjectSerializedGuidKey,
                    Rfc4122Guid.Create(
                            Rfc4112Namespace,
                            code)
                        .ToString());
            }

            Output.CompilationUnit = args.CompilationUnit;
            //code = Regex.Replace(code,
            //    @"\}[\w\r\n\t]+(?<Symbol>\;|,)",
            //    "}${Symbol}\r\n");
            Output.Class = code;
            if (Parameters.Beautify)
            {
                var tsBeautifier = new TsBeautifier().Configure(c => c.OpenBlockOnNewLine = true);
                Output.Class = tsBeautifier.Beautify(Output.Class);
                Output.Initialiser = tsBeautifier.Beautify(Output.Initialiser);
                Output.Instance = tsBeautifier.Beautify(Output.Instance);
            }

            //			code = Regex.Replace(code, @"\, ", ",\r\n");
            //code = code.Replace(NewLineComment,
            //    "\n");
            return Output;
        }

        private static EmptyStatementSyntax ToSingleLineCommentSyntax(string comment)
        {
            return ToComment(comment).ToCommentStatementSyntax();
        }

        private static EmptyStatementSyntax ToInlineCommentSyntax(string comment)
        {
            return ToInlineComment(comment).ToCommentStatementSyntax(false);
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
                SerializeToString(@object, @object.GetType().Name)
            );
            var tree2 = SyntaxFactory.ParseSyntaxTree(
                File.ReadAllText(pathToCompareTo)
            );
            //			Parameters.DescriptionFormatter.Enabled = commentsEnabled;
            return
                NormaliseForComparison(tree1.GetCompilationUnitRoot()) !=
                NormaliseForComparison(tree2.GetCompilationUnitRoot());
        }

        private static string NormaliseForComparison(SyntaxNode compilationUnit)
        {
            return new TriviaRemover().Visit(compilationUnit)
                .NormalizeWhitespace2()
                .ToFullString();
        }

        private static string FormatCode(SyntaxNode compilationUnit)
        {
            var code = compilationUnit.ToFullString();
            return code;
        }

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
                    false)?.Syntax;
                if (serializeObjectToInitialiser != null)
                {
                    args.ThisStatements.Add(
                        SyntaxFactory.ExpressionStatement(
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
            var data = args.GetObjectData(@object);
            data.Identifier = instanceName;
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
                            SyntaxFactory.LineFeed
                        };
                var objectStatements =
                    isCircular
                        ? (@object is IEnumerable ? args.LateCircularObjectStatements : args.EndObjectStatements)
                        : args.ObjectStatements;
                var initialiser = SerializeObjectToInitialiser(
                    args,
                    @object,
                    true,
                    null,
                    null,
                    false).Syntax;
                if (!string.IsNullOrWhiteSpace(Parameters.BeforeInitialiserComment))
                {
                    initialiser = initialiser.WithLeadingTrivia(SyntaxFactory.Comment(ToInlineComment(Parameters.BeforeInitialiserComment)));
                }
                if (!string.IsNullOrWhiteSpace(Parameters.AfterInitialiserComment))
                {
                    initialiser = initialiser.WithTrailingTrivia(SyntaxFactory.Comment(ToInlineComment(Parameters.AfterInitialiserComment)));
                }
                initialiser = initialiser.NormalizeWhitespace();
                Output.InitialiserSyntax = initialiser;
                Output.Initialiser = initialiser.ToString();
                var inst = SyntaxFactory.LocalDeclarationStatement(
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
                                                initialiser
                                            )))))
                    ;
                var instance = inst.NormalizeWhitespace();
                objectStatements.Add(
                    @object,
                    instance
                );
                Output.Instance = instance.ToFullString();
                Output.InstanceSyntax = instance;
            }
        }

        private static string ToComment(string description)
        {
            return string.Format(
                @"// {0}",
                description);
        }

        private static string ToInlineComment(string description)
        {
            return string.Format(
                @"/* {0} */",
                description);
        }

        private static void AddNamespace(CSharpObjectSerializerInstanceArguments args, object @object)
        {
            if (@object == null)
            {
                return;
            }

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
                nodes.Add(
                    SerializeObjectToInitialiser(
                        args,
                        elm,
                        false,
                        null,
                        null,
                        true).Syntax);
                nodes.Add(CommaWithNewLine());
            }

            RemoveTrailingComma(nodes);
            return nodes.ToArray();
        }

        private static void RemoveTrailingComma(IList nodes)
        {
            if (nodes.Count > 0)
            {
                nodes.RemoveAt(nodes.Count - 1);
            }
        }

        private ConversionResult SerializeObjectToInitialiser(
            CSharpObjectSerializerInstanceArguments args,
            object @object,
            bool allowClasses,
            PropertyInfo propertyBeingAssigned,
            object propertyOwner,
            bool serializeNulls)
        {
            foreach (var stackObject in args.DependencyStack)
            {
                args.RegisterDependencies(
                    stackObject,
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

        private ConversionResult SerializeObjectToInitialiserInner(
            CSharpObjectSerializerInstanceArguments args,
            object @object,
            bool allowClasses,
            PropertyInfo propertyBeingAssigned,
            object propertyOwner,
            bool serializeNulls)
        {
            AddNamespace(
                args,
                @object);
            var objectType = @object?.GetType() ?? propertyBeingAssigned?.PropertyType;
            ReturnType = ReturnType ?? objectType;
            if (objectType != null && Converters != null)
            {
                foreach (var conv in Converters)
                {
                    if (conv.CanConvert(objectType, @object, propertyOwner, propertyBeingAssigned))
                    {
                        var result = conv.Convert(objectType, @object, propertyOwner, propertyBeingAssigned);
                        if (result.DidConvert)
                        {
                            return result;
                        }
                    }
                }
            }

            if (@object == null)
            {
                return new InternalConversionResult(serializeNulls ||
                                                    // If the default is NOT null, then we MUST serialize the null value
                                                    !IsSameAsDefaultValueAfterInitialise(
                                                        propertyBeingAssigned,
                                                        @object)
                    ? SyntaxFactory.LiteralExpression(
                        SyntaxKind.NullLiteralExpression)
                    : null);
            }

            if (ReturnType == null)
            {
                ReturnType = @object.GetType();
            }

            if (SerializeObject(args, @object, out var syntaxFactoryLiteralExpression))
            {
                return syntaxFactoryLiteralExpression.ToResult();
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
                allowClasses ||
                !isCircular &&
                !multipleUsages
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
                            new SyntaxTriviaList()
                        ),
                        TypeSyntax(@object.GetType()),
                        null,
                        null);
                    return new InternalConversionResult(hasSerializableProperties
                        // new X { Y = Z }
                        ? objectCreationExpressionSyntax
                            .WithInitializer(
                                SyntaxFactory.InitializerExpression(
                                    SyntaxKind.ObjectInitializerExpression,
                                    SerializeProperties(
                                        args,
                                        @object,
                                        false)))
                        // new X()
                        : objectCreationExpressionSyntax.WithArgumentList(SyntaxFactory.ArgumentList()));
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
                        new SyntaxTriviaList()
                    ),
                    TypeSyntax(type),
                    null,
                    null
                );
                return new InternalConversionResult(enumerable.Cast<object>().Any()
                    // new X { Y, Z }
                    ? objectCreationExpression
                        .WithInitializer(
                            SyntaxFactory.InitializerExpression(
                                SyntaxKind.CollectionInitializerExpression,
                                SyntaxFactory.SeparatedList<ExpressionSyntax>(
                                    // Serialize the properties to an initialiser
                                    SerializeEnumerableElements(
                                        args,
                                        enumerable))))
                    // new X()
                    : objectCreationExpression.WithArgumentList(SyntaxFactory.ArgumentList()));
            }

            // Serialize to a separate object instance out of the
            // scope of the initialiser
            if (args.GetObjectData(@object)
                .HasBeenSerialized)
            {
                if (!isCircular || propertyBeingAssigned == null)
                {
                    return GetObjectReferenceSyntax(
                        args,
                        @object).ToResult();
                }

                // Queue up a property assignment later on
                SerializePropertyToPostParentInitialiseAssignment(
                    args,
                    propertyOwner,
                    propertyBeingAssigned,
                    false);
                return null;
            }

            // Serialize to local variable
            if (multipleUsages || propertyBeingAssigned == null || true)
            {
                var instanceName = GetInstanceName(
                    args,
                    @object,
                    type);
                var description =
                    Parameters.DescriptionFormatter != null &&
                    Parameters.DescriptionFormatter.Enabled
                        ? Parameters.DescriptionFormatter.FormatOrNull(
                            new DescriptionFormatterArguments(),
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
                return SyntaxFactory.IdentifierName(serializeObjectToInitialiser).ToResult();
            }

            //		if (propertyBeingAssigned != null && isCircular && @object is IEnumerable)
            //			{
            // Queue up a property assignment later on
            if (!args.GetObjectData(@object)
                .HasBeenSerialized)
            {
                SerializePropertyToPostParentInitialiseAssignment(
                    args,
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
                    syntaxFactoryLiteralExpression = Constructor(
                        args,
                        type,
                        ((DateTime)@object).Ticks);
                    return true;
                }
            }

            if (@object is DateTimeOffset)
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = Constructor(
                        args,
                        type,
                        ((DateTimeOffset)@object).Ticks,
                        TimeSpan.Zero
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

                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = Constructor(
                        args,
                        type,
                        ts.Ticks
                    );
                    return true;
                }
            }

            if (@object is Guid)
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = Constructor(
                        args,
                        type,
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
                syntaxFactoryLiteralExpression = TypeOfSyntax(@object as Type);
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
                                    false).Syntax,
                                SyntaxFactory.Token(
                                    SyntaxKind.CommaToken),
                                SerializeObjectToInitialiser(
                                    args,
                                    @object.GetPropertyValue("Value"),
                                    false,
                                    null,
                                    null,
                                    false).Syntax
                            }));
                    return true;
                }
            }

            // Last resort before enumerables or classes
            if (@object is string || type.IsPrimitive || !type.IsClass && type.IsValueType)
            {
                args.GetObjectData(@object)
                    .HasBeenSerialized = true;
                {
                    syntaxFactoryLiteralExpression = SyntaxFactoryLiteralExpression(
                        @object,
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
                                    SerializeEnumerableElements(
                                        args,
                                        @object as IEnumerable)))).NormalizeWhitespace();
                    return true;
                }
            }

            syntaxFactoryLiteralExpression = null;
            return false;
        }

        public static TypeOfExpressionSyntax TypeOfSyntax(Type @object)
        {
            return SyntaxFactory.TypeOfExpression(TypeSyntax(@object));
        }

        private static ExpressionSyntax SyntaxFactoryLiteralExpression(object @object, Type type)
        {
            if (type.IsNumeric())
            {
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    LiteralSyntax(
                        @object
                    )
                );
            }

            if (Equals(
                @object,
                false))
            {
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.FalseLiteralExpression);
            }

            if (Equals(
                @object,
                true))
            {
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.TrueLiteralExpression);
            }

            if (@object is char)
            {
                return SyntaxFactory.LiteralExpression(
                    SyntaxKind.CharacterLiteralExpression,
                    LiteralSyntax(
                        @object
                    )
                );
            }

            return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                LiteralSyntax(
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
                        LiteralSyntax(value)
                    );
                }

                arguments.Add(SyntaxFactory.Argument(syntax));
            }

            return SyntaxFactory.ObjectCreationExpression(
                    TypeSyntax(type))
                .NormalizeWhitespace2()
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(arguments)
                    )
                );
        }

        public static TypeSyntax TypeSyntax(Type type)
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
                                genericArguments.Select(TypeSyntax)
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
                SerializePropertyToPostParentInitialiseAssignment(
                    args,
                    @object,
                    property,
                    false);
            }
        }

        private IEnumerable<PropertyInfo> GetSerializableProperties(object @object, bool serializeNulls)
        {
            var serializableProperties = DependencyAnalyser.GetSerializableProperties(@object)
                .Where(
                    p => !Parameters.IgnoreConditions.Any(
                        ic => ic.Ignore(
                            @object,
                            p)));
            if (!serializeNulls)
            {
                serializableProperties = serializableProperties
                    .Where(
                        p => !Equals(
                            p.GetValue(@object),
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
                    new InstanceNameFormatterArguments(
                        args.InstanceNames,
                        suggestedName),
                    type,
                    @object,
                    out name))
            {
                switch (Parameters.InstanceNameFormatter.SuggestedNameLocation)
                {
                    case InstanceNameFormatterSuggestedNameLocation.Prefix:
                        name = string.Format(
                            "{0}{1}",
                            suggestedName,
                            name);
                        break;
                    case InstanceNameFormatterSuggestedNameLocation.Suffix:
                        name = string.Format(
                            "{0}{1}",
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
                args.InstanceNameCount.Add(
                    name,
                    0);
            }

            Func<string> getName = () =>
            {
                args.InstanceNameCount[name] = args.InstanceNameCount[name] + 1;
                return string.Format(
                    "{0}{1}",
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
            {
                return SyntaxFactory.PredefinedType(
                    SyntaxFactory.Token(
                        resolveSyntaxKindFromType.Value));
            }

            return SyntaxFactory.IdentifierName(type.Name);
        }

        private static SyntaxKind? ResolveSyntaxKindFromType(Type type)
        {
            var name = string.Format(
                "{0}Keyword",
                type.Name);
            var enumMatchedName = Enum.GetNames(typeof(SyntaxKind))
                .SingleOrDefault(
                    n => n.Equals(
                        name,
                        StringComparison.OrdinalIgnoreCase));
            if (enumMatchedName == null)
            {
                return null;
            }

            return (SyntaxKind)Enum.Parse(
                typeof(SyntaxKind),
                enumMatchedName);
        }

        public static SyntaxToken LiteralSyntax(object @object)
        {
            var type = @object.GetType();
            if (type == typeof(byte))
            {
                type = typeof(int);
            }

            var method =
                typeof(SyntaxFactory).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Single(
                        m =>
                        {
                            if (m.Name != "Literal")
                            {
                                return false;
                            }

                            var p = m.GetParameters();
                            if (p.Count() != 1)
                            {
                                return false;
                            }

                            return p.Single()
                                       .ParameterType == type;
                        });
            return (SyntaxToken)method.Invoke(
                null,
                new[] { @object });
        }

        private SeparatedSyntaxList<ExpressionSyntax> SerializeProperties(
            CSharpObjectSerializerInstanceArguments args,
            object @object,
            bool serializeNulls)
        {
            var elms = new List<SyntaxNodeOrToken>();
            foreach (var property in GetSerializableProperties(@object, true))
            {
                var isNull = Equals(null, property.GetValue(@object));
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
                        value == args.Object
                        //&& !(value is IEnumerable)
                        && dependencyResult != null
                        && dependencyResult.HasTopLevelCircular()
                        ;
                }

                var valueToAssignReferenceSyntax =
                    SerializeAndOrGetObjectReferenceSyntax(
                        args,
                        value,
                        property,
                        @object,
                        false);
                if (valueToAssignReferenceSyntax?.Syntax == null)
                {
                    continue;
                }

                if (!serializeNulls &&
                    valueToAssignReferenceSyntax is InternalConversionResult &&
                    valueToAssignReferenceSyntax.Syntax is LiteralExpressionSyntax lit &&
                    lit.IsKind(SyntaxKind.NullLiteralExpression))
                {
                    continue;
                }

                if (isCircular && CanAssignPropertiesTo(
                        args,
                        @object)) // && !(valueToAssignReferenceSyntax is IdentifierNameSyntax))
                {
                    // Assign this property later
                    SerializePropertyToPostParentInitialiseAssignment(
                        args,
                        @object,
                        property,
                        valueToAssignReferenceSyntax?.Syntax);
                }
                else
                {
                    elms.Add(
                        SyntaxFactory.AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            SyntaxFactory.IdentifierName(
                                property.Name),
                            valueToAssignReferenceSyntax?.Syntax
                        )
                    );
                    elms.Add(CommaWithNewLine());
                }
            }

            // Remove trailing comma
            RemoveTrailingComma(elms);
            return SyntaxFactory.SeparatedList<ExpressionSyntax>(
                elms.ToArray());
        }

        private ConversionResult SerializeAndOrGetObjectReferenceSyntax(
            CSharpObjectSerializerInstanceArguments args,
            object value,
            PropertyInfo propertyBeingAssigned,
            object propertyOwner,
            bool allowNestedClasses)
        {
            if (value != null && DependencyAnalyser.IsDependableObject(value) && args.GetObjectData(value)
                    .HasBeenSerialized)
            {
                // This object needs to be initialised after
                // the object it references
                //args.RegisterDependencies(@object, value);
                return new InternalConversionResult(GetObjectReferenceSyntax(
                    args,
                    value));
            }
            return SerializeObjectToInitialiser(
                args,
                value,
                allowNestedClasses,
                propertyBeingAssigned,
                propertyOwner,
                false
            );
        }

        private void SerializePropertyToPostParentInitialiseAssignment(
            CSharpObjectSerializerInstanceArguments args,
            object propertyOwner,
            PropertyInfo property,
            bool allowNestedClasses
        )
        {
            var value = property.GetValue(propertyOwner);
            if (IsSameAsDefaultValueAfterInitialise(
                property,
                value))
            {
                return;
            }

            SerializePropertyToPostParentInitialiseAssignment(
                args,
                propertyOwner,
                property,
                SerializeAndOrGetObjectReferenceSyntax(
                    args,
                    value,
                    property,
                    propertyOwner,
                    allowNestedClasses)?.Syntax);
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

            if (!CanAssignPropertiesTo(
                args,
                propertyOwner))
            {
                throw new InvalidOperationException(
                    "\"this\" setting is not allowed on given instance of object of type \"{0}\""
                        .FormatText(propertyOwner.GetType()));
            }

            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GetObjectReferenceSyntax(
                    args,
                    propertyOwner),
                identifierNameSyntax);
        }

        private bool CanAssignPropertiesTo(CSharpObjectSerializerInstanceArguments args, object propertyOwner)
        {
            if (string.IsNullOrWhiteSpace(
                    args.GetObjectData(propertyOwner)
                        .Identifier) && AllowThisObject != propertyOwner)
            {
                return false;
            }

            return true;
        }

        private static ExpressionSyntax GetObjectReferenceSyntax(CSharpObjectSerializerInstanceArguments args,
            object value)
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
            private readonly Dictionary<SyntaxNode, IList<UsingDirectiveSyntax>> _usings
                = new Dictionary<SyntaxNode, IList<UsingDirectiveSyntax>>();

            private readonly Dictionary<SyntaxNode, int> _usingsIndex
                = new Dictionary<SyntaxNode, int>();

            /// <summary>
            ///     Sorts the using statments alphabetically
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
                    _usingsIndex.Add(
                        node.Parent,
                        0);
                    _usings.Add(
                        node.Parent,
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
}