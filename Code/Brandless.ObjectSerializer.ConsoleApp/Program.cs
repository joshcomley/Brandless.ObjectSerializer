using System;
using System.Collections.Generic;
using System.IO;
using Brandless.ObjectSerializer.Tests.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Brandless.ObjectSerializer.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var list = new List<Person>();
            var person1 = new Person("Paulina", 24, new DateTimeOffset(1993, 7, 10, 12, 33, 22, new TimeSpan()));
            var person2 = new Person("Josh", 33, new DateTimeOffset(1984, 7, 2, 1, 15, 5, new TimeSpan()));
            var person3 = new Person("Bob", 33, new DateTimeOffset(1988, 10, 11, 18, 47, 12, new TimeSpan()));
            person3.FavouritePerson = person2;
            person1.FavouritePerson = person1;
            person1.Addresses.Add(new Address("My house", "AB1 C23"));
            person1.Addresses.Add(new Address("My other house", "DE2 F34"));
            person2.Addresses.Add(new Address("Big Place", "XY4 Z56"));
            person2.Addresses.Add(new Address("Another Place", null));
            person3.Addresses = null;
            list.Add(person1);
            list.Add(person2);
            list.Add(person3);
            //var options = new CSharpSerializeToClassParameters("InMemoryDb");
            var options = new CSharpSerializeToClassParameters("MyClass");
            options.AllowObjectInitializer = false;
            var serializer = new CSharpObjectSerializer(options);
            options.IgnoreConditions.Add(new IgnoreCondition((o, info) =>
            {
                if (info.Name == nameof(Person.IgnoreThis))
                {
                    return true;
                }

                return false;
            }));
            var code = serializer.Serialize(list);
            Console.WriteLine(code);
            File.WriteAllText(@"d:\code\temp-formatted.cs", code);
        }

        /// <summary>
        /// Create a class from scratch.
        /// </summary>
        static void CreateClass()
        {
            // Create a namespace: (namespace CodeGenerationSample)
            var @namespace = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName("CodeGenerationSample")).NormalizeWhitespace();

            // Add System using statement: (using System)
            @namespace = @namespace.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")));

            //  Create a class: (class Order)
            var classDeclaration = SyntaxFactory.ClassDeclaration("Order");

            // Add the public modifier: (public class Order)
            classDeclaration = classDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

            // Inherit BaseEntity<T> and implement IHaveIdentity: (public class Order : BaseEntity<T>, IHaveIdentity)
            classDeclaration = classDeclaration.AddBaseListTypes(
                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("BaseEntity<Order>")),
                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("IHaveIdentity")));

            // Create a string variable: (bool canceled;)
            var variableDeclaration = SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("bool"))
                .AddVariables(SyntaxFactory.VariableDeclarator("canceled"));

            // Create a field declaration: (private bool canceled;)
            var fieldDeclaration = SyntaxFactory.FieldDeclaration(variableDeclaration)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

            // Create a Property: (public int Quantity { get; set; })
            var propertyDeclaration = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("int"), "Quantity")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAccessorListAccessors(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));

            // Create a stament with the body of a method.
            var syntax = SyntaxFactory.ParseStatement("canceled = true;");

            // Create a method
            var methodDeclaration = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("void"), "MarkAsCanceled")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(SyntaxFactory.Block(syntax));

            // Add the field, the property and method to the class.
            classDeclaration = classDeclaration.AddMembers(fieldDeclaration, propertyDeclaration, methodDeclaration);

            // Add the class to the namespace.
            @namespace = @namespace.AddMembers(classDeclaration);

            // Normalize and get code as string.
            var code = @namespace
                .NormalizeWhitespace()
                .ToFullString();

            // Output new code to the console.
            Console.WriteLine(code);
        }
    }
}
