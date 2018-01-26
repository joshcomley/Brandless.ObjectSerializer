using System;
using System.Xml.Linq;

namespace Brandless.ObjectSerializer.Xml
{
	public class XmlBeautifier
	{
		public static string Beautify(string xml)
		{
			return XDocument.Parse(xml).ToString();
		}

		public static bool TryBeautify(ref string possibleXml)
		{
			try
			{
				possibleXml = Beautify(possibleXml);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}
}