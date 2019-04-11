using System;
using System.Reflection;
using Brandless.ObjectSerializer.Tests.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace Brandless.ObjectSerializer.Tests
{
    public class NameConverter : ICSharpObjectSerializerConverter
    {
        public bool CanConvert(Type objectType, object @object, object propertyOwner, PropertyInfo propertyBeingAssigned)
        {
            if (propertyBeingAssigned == null)
            {
                return false;
            }

            if (propertyBeingAssigned.DeclaringType == typeof(Person) &&
                propertyBeingAssigned.Name == nameof(Person.Name))
            {
                return true;
            }

            return false;
        }

        public ConversionResult Convert(Type objectType, object @object, object propertyOwner, PropertyInfo propertyBeingAssigned)
        {
            var str = (@object as string) ?? "";
            var owner = propertyOwner as Person;
            str = $"{str} (age: {owner.Age})";
            return new ConversionResult(SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(
                    str
                )
            ), true);
        }
    }
}