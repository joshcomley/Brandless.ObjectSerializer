using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Brandless.ObjectSerializer.Xml;

// ReSharper disable once CheckNamespace
namespace System
{
    public static class BrandlessPortableObjectExtensions
    {
        private static readonly Dictionary<string, List<PropertyInfo>> FindPropertiesCache = new Dictionary<string, List<PropertyInfo>>();

	    public static object GetPropertyValue(this object @object, string propertyName)
	    {
		    var propertyValue = @object.GetType().GetRuntimeProperty(propertyName);
		    return propertyValue.GetValue(@object);
	    }

		public static List<PropertyInfo> FindProperties<T>(this object @object)
        {
            var type = @object.GetType();
            var key = Key<T>(type);
            if (!FindPropertiesCache.ContainsKey(key))
            {
                FindPropertiesCache.Add(key,
                            type
							.GetRuntimeProperties()
                            .Where(p => typeof(T).GetTypeInfo().IsAssignableFrom(p.PropertyType.GetTypeInfo()))
                            .ToList()
                          );
            }
            return FindPropertiesCache[key];
        }

        public static List<PropertyInfo> FindProperties(this object @object, string name)
        {
            var type = @object.GetType();
            var key = Key(type, name);
            if (!FindPropertiesCache.ContainsKey(key))
            {
                FindPropertiesCache.Add(key,
                            type
							.GetRuntimeProperties()
                            .Where(p => p.Name == name)
                            .ToList()
                          );
            }
            return FindPropertiesCache[key];
        }

        private static string Key<T>(Type type)
        {
            return string.Format("{0}##{1}",
	            type.AssemblyQualifiedName,
	            typeof(T).AssemblyQualifiedName);
        }

        private static string Key(Type type, string name)
        {
            return string.Format("{0}##{1}",
	            type.AssemblyQualifiedName,
	            name);
        }

        private static readonly Dictionary<Type, PropertyInfo> FindPropertyCache = new Dictionary<Type, PropertyInfo>();
        public static PropertyInfo FindProperty<T>(this object @object)
        {
            var type = @object.GetType();
            if (!FindPropertyCache.ContainsKey(type))
            {
                FindPropertyCache.Add(type, @object.FindProperties<T>().SingleOrDefault());
            }
            return FindPropertyCache[type];
        }

        #region Serialization
	    public static string SerializeToXml<T>(this T value, bool beautify = false)
		    where T : class
	    {
		    return value == null ? null : SerializeToXml(value, typeof (T), beautify);
	    }

	    public static string SerializeToXml(this object value, Type type, bool beautify = false)
        {
            var serializer = new XmlSerializer(type);

            var settings = new XmlWriterSettings
                               {
                                   Encoding = new UnicodeEncoding(false, false),
                                   Indent = false,
                                   OmitXmlDeclaration = false
                               };

            using (var textWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(textWriter, settings))
                {
                    serializer.Serialize(xmlWriter, value);
                }
	            var xml = textWriter.ToString();
	            if (beautify)
		            XmlBeautifier.TryBeautify(ref xml);
                return xml;
            }
        }

	    public static T DeserializeFromXml<T>(string xml)
	    {
			if (string.IsNullOrEmpty(xml))
			{
				return default(T);
			}
		    return (T) DeserializeFromXml(typeof (T), xml);
	    }

	    public static object DeserializeFromXml(Type type, string xml)
	    {
		    if (string.IsNullOrWhiteSpace(xml)) return null;

            var serializer = new XmlSerializer(type);

            var settings = new XmlReaderSettings();
            // No settings need modifying here

            using (var textReader = new StringReader(xml))
            {
                using (var xmlReader = XmlReader.Create(textReader, settings))
                {
                    return serializer.Deserialize(xmlReader);
                }
            }
        }
        #endregion Serialization

        public static Dictionary<string, object> ToPropertyDictionary(this object obj)
        {
            return obj == null
				? null
				: obj
					.GetType()
					.GetRuntimeProperties()
					.ToDictionary(p => p.Name, p => p.GetValue(obj, null));
        }
    }
}   