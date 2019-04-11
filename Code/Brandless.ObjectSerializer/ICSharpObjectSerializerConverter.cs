using System;
using System.Reflection;

namespace Brandless.ObjectSerializer
{
    public interface ICSharpObjectSerializerConverter
    {
        bool CanConvert(Type objectType, object @object, object propertyOwner,
            PropertyInfo propertyBeingAssigned);
        ConversionResult Convert(Type objectType, object @object, object propertyOwner,
            PropertyInfo propertyBeingAssigned);
    }
}