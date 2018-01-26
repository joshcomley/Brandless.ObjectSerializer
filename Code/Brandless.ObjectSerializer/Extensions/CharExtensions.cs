// ReSharper disable CheckNamespace
namespace System
// ReSharper restore CheckNamespace
{
	public static class CharExtensions
	{
		public static char ToLower(this char c)
		{
			if (c <= 90 && c >= 65)
			{
				return (char)(c + 32);
			}
			return c;
		}
	
		public static char ToUpper(this char c)
		{
			if (c >= 97 && c <= 122)
			{
				return (char)(c - 32);
			}
			return c;
		}
	}
}