using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace System
{
	public static class BrandlessTypeExtensions
	{
		public static T GetCustomAttribute<T>(this ICustomAttributeProvider provider, bool inherited = true)
		{
			return (T)provider.GetCustomAttributes(typeof(T), inherited).FirstOrDefault();
		}
		public static IEnumerable<T> GetCustomAttributes<T>(this ICustomAttributeProvider provider, bool inherited = true)
		{
			return provider.GetCustomAttributes(typeof(T), inherited).Cast<T>();
		}
        public static List<ConstructorInfo> GetRuntimeConstructors(this Type type)
        {
            var constructors = new List<ConstructorInfo>();
            while (type != null && type != typeof(object))
            {
                var typeInfo = type.GetTypeInfo();
                constructors.AddRange(typeInfo.DeclaredConstructors);
                type = typeInfo.BaseType;
            }
            return constructors;
        }

        /// <summary>
        /// Determines whether the supplied object is a .NET numeric system type
        /// </summary>
        /// <returns>true=Is numeric; false=Not numeric</returns>
        public static bool IsNumeric<T>()
        {
            return IsNumeric(typeof(T));
        }

        /// <summary>
        /// Determines whether the supplied object is a .NET numeric system type
        /// </summary>
        /// <returns>true=Is numeric; false=Not numeric</returns>
        public static bool IsNumeric(this Type type)
        {
            if (type == null) throw new ArgumentNullException("type");
            while (true)
            {
                var typeInfo = type.GetTypeInfo();
                if (typeInfo
                    .IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = Nullable.GetUnderlyingType(type);
                    continue;
                }

                var val = typeInfo.IsValueType
                    ? Activator.CreateInstance(type)
                    : null;

                if (val == null)
                    return false;

                // Test for numeric type, returning true if match
                return val is double || val is float || val is int || val is long || val is decimal || val is short || val is uint || val is ushort || val is ulong || val is byte || val is sbyte;
            }
        }

        public static bool IsWrappedNullable(this Type type)
        {
            return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static bool CanBeAssignedNull(this Type type)
        {
            return !type.GetTypeInfo()
                .IsValueType || (Nullable.GetUnderlyingType(type) != null);
        }

        public static MethodInfo FindGenericMethod(this Type type, string methodName, params string[] typeArguments)
        {
            return type
                .GetRuntimeMethods()
                .FirstOrDefault(m =>
                {
                    if (m.Name != methodName) return false;
                    var genericArguments = m.GetGenericArguments();
                    if (genericArguments
                        .Count() != typeArguments.Length)
                        return false;
                    if (typeArguments.Length != genericArguments.Length)
                        return false;
                    for (var i = 0; i < typeArguments.Length; i++)
                    {
                        if (typeArguments[i] != genericArguments[i].Name)
                        {
                            return false;
                        }
                    }
                    return true;
                });
        }

        public static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.GetTypeInfo().IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.GetTypeInfo().BaseType;
            }
            return false;
        }

        public static MethodInfo GetExtensionMethod(this Type extendedType, Assembly assembly, string name)
        {
            return extendedType.GetExtensionMethods(assembly).FirstOrDefault(m => m.Name == name);
        }

        public static IEnumerable<MethodInfo> GetExtensionMethods(this Type extendedType, Assembly assembly)
        {
            var query = from type in assembly.DefinedTypes
                        where type.IsSealed && !type.IsGenericType && !type.IsNested
                        from method in type.AsType().GetRuntimeMethods()
                        where method.IsDefined(typeof(ExtensionAttribute), false)
                        where method.GetParameters()[0].ParameterType == extendedType
                        select method;
            return query;
        }

        public static Type FindIEnumerable(this Type seqType)
        {
            while (true)
            {
                if (seqType == null || seqType == typeof(string))
                    return null;
                if (seqType.IsArray)
                    return typeof(IEnumerable<>).MakeGenericType(seqType.GetElementType());
                var typeInfo = seqType.GetTypeInfo();
                if (typeInfo.IsGenericType)
                {
                    foreach (var arg in typeInfo.GenericTypeArguments)
                    {
                        var ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                        if (ienum.GetTypeInfo().IsAssignableFrom(typeInfo))
                        {
                            return ienum;
                        }
                    }
                }
                var ifaces = typeInfo.ImplementedInterfaces;
                var enumerable = ifaces as IList<Type> ?? ifaces.ToList();
                if (enumerable.Any())
                {
                    foreach (var ienum in enumerable.Select(FindIEnumerable)
                        .Where(ienum => ienum != null))
                    {
                        return ienum;
                    }
                }
                if (typeInfo.BaseType == null || typeInfo.BaseType == typeof(object)) return null;
                seqType = typeInfo.BaseType;
            }
        }

    }
}