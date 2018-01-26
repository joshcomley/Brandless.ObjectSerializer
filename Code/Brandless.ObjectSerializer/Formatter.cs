using System;
using System.Collections.Generic;

namespace Brandless.ObjectSerializer
{
	public abstract class Formatter<TArguments>
	{
		private bool _enabled = true;
		public bool Enabled
		{
			get { return _enabled; }
			set { _enabled = value; }
		}

		private Dictionary<Type, Func<object, TArguments, string>> _commentFormaters;
		private Dictionary<Type, object> _commentFormatersOriginal;

		public void SetForType<T>(Func<T, TArguments, string> formatter)
		{
			InitialiseFormatterDictonaries();
			var type = typeof(T);
			Func<object, TArguments, string> f = (o, a) => formatter((T)o, a);
			if (!_commentFormaters.ContainsKey(type))
			{
				_commentFormaters.Add(type, f);
				_commentFormatersOriginal.Add(type, formatter);
			}
			else if (formatter == null)
			{
				_commentFormaters.Remove(type);
				_commentFormatersOriginal.Remove(type);
			}
			else
			{
				_commentFormaters[type] = f;
				_commentFormatersOriginal[type] = formatter;
			}
		}

		public Func<T, TArguments, string> GetForType<T>()
		{
			InitialiseFormatterDictonaries();
			var type = typeof(T);
			if (!_commentFormatersOriginal.ContainsKey(type)) return null;
			return (Func<T, TArguments, string>)_commentFormatersOriginal[type];
		}

		public Func<object, TArguments, string> GetForType(Type type)
		{
			InitialiseFormatterDictonaries();
			return !_commentFormaters.ContainsKey(type) ? null : _commentFormaters[type];
		}

		public string FormatOrNull<T>(TArguments arguments, T @object)
		{
			string result;
			return !TryFormat(arguments, @object, out result) ? null : result;
		}

		public string FormatOrNull(TArguments arguments, Type type, object @object)
		{
			string result;
			return !TryFormat(arguments, type, @object, out result) ? null : result;
		}

		public bool TryFormat<T>(TArguments arguments, T @object, out string result)
		{
			// Not technically DRY but saves
			// the boxing and unboxing by
			// taking advantage of generics
			var formatter = GetForType<T>();
			if (formatter != null)
			{
				result = formatter(@object, arguments);
				return true;
			}
			result = null;
			return false;
		}

		public bool TryFormat(TArguments arguments, Type type, object @object, out string result)
		{
			var formatter = GetForType(type);
			if (formatter != null)
			{
				result = formatter(@object, arguments);
				return true;
			}
			result = null;
			return false;
		}

		private void InitialiseFormatterDictonaries()
		{
			if (_commentFormaters != null) return;
			_commentFormaters = new Dictionary<Type, Func<object, TArguments, string>>();
			_commentFormatersOriginal = new Dictionary<Type, object>();
		}
	}
}