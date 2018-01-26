using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace System
{
	public static class BrandlessStringExtensions
	{
		public static byte[] Sha256Hash(this string @string, Encoding encoding = null)
		{
			using (var sha256 = SHA256.Create())
			{
				return sha256.ComputeHash((encoding ?? Encoding.UTF8).GetBytes(@string));
			}
		}

		public static string Sha256HashBitConverted(this string @string, Encoding encoding = null)
		{
			return BitConverter.ToString(@string.Sha256Hash(encoding));
		}

		public static string XmlEncode(this string @string)
		{
			return SecurityElement.Escape(@string);
		}

		public static Guid GetDeterministicGuid(this string input)
		{
			var provider = new MD5CryptoServiceProvider();
			var inputBytes = Encoding.Default.GetBytes(input);
			var hashBytes = provider.ComputeHash(inputBytes);
			var hashGuid = new Guid(hashBytes);
			return hashGuid;
		}

        public static string FirstLetterToLowerInvariant(this string @string)
        {
            return @string.Length == 1
                ? @string.ToLowerInvariant()
                : string.Format("{0}{1}", @string[0].ToLower(), @string.Substring(1));
        }

        public static Stream ToStream(this string @string)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(@string);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Removes a string from the end of a string:
        /// "MyHouse".RemoveSuffix("House") > "My"
        /// </summary>
        /// <param name="str"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static string RemoveSuffix(this string str, string end)
        {
            return str.EndsWith(end)
                ? str.Substring(0, str.Length - end.Length)
                : str;
        }

        /// <summary>
        /// Removes the "Controller" suffix
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveControllerSuffix(this string str)
        {
            return str.RemoveSuffix("Controller");
        }

        /// <summary>
        /// Finds common start to strings, so given:
        /// 
        /// "I like beef burgers"
        /// "I like being a coder"
        /// "I like beeps and bops"
        /// 
        /// Result:
        /// 
        /// "I like be"
        /// </summary>
        /// <param name="strs"></param>
        /// <returns></returns>
        public static string FindCommonRoot(this IEnumerable<string> strs, bool ignoreCase = false)
        {
            var index = 0;
            var sb = new StringBuilder();
            var enumerable = strs as string[] ?? strs.ToArray();
            for (; index < enumerable.Select(s => s.Length).Min(); index++)
            {
                var c = enumerable.First()[index];
                if (ignoreCase)
                {
                    if (!enumerable.All(s => s[index].ToUpper().Equals(c.ToUpper())))
                    {
                        break;
                    }
                }
                else
                {
                    if (!enumerable.All(s => s[index].Equals(c)))
                    {
                        break;
                    }
                }
                sb.Append(enumerable.First()[index]);
            }
            return sb.ToString();
        }

        public static string Replace(this string str, string oldValue, string newValue, StringComparison comparison)
        {
            var sb = new StringBuilder();
            var previousIndex = 0;
            var index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        public static string Truncate(this string str, int length)
        {
            if (str.Length <= length)
                return str;
            return str.Substring(0, length);
        }

        public static bool IsUpperCase(this char c)
        {
            return c.ToString().ToUpper() == c.ToString();
        }

        public static bool IsAlpha(this char c)
        {
            return Regex.IsMatch(c.ToString(), "[A-Za-z]");
        }

        public static bool IsNumeric(this string str, bool allowDecimal = false)
        {
            return Regex.IsMatch(str, @"^[0-9]+(\.[0-9]+){0,1}$");
        }

        public static bool IsNumeric(this char c)
        {
            return Regex.IsMatch(c.ToString(), "[0-9]");
        }

        /// <summary>
        /// Takes a string like: "SomeWords" and converts it to "Some Words"
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string IntelliSpace(this string str)
        {
            if (str == null)
                return null;
            if (str.ToUpper() == str) return str;
            str = str.Replace('_', ' ');
            var sb = new StringBuilder();
            for (var i = 0; i < str.Length; i++)
            {
                // if it's upper case, inject a space
                if (i > 0)
                {
                    if (str[i].IsUpperCase() && str[i].IsAlpha())
                    {
                        // only if the last letter WAS NOT uppercase
                        if (!str[i - 1].IsUpperCase() && (str[i - 1].IsAlpha() || str[i - 1].IsNumeric()))
                        {
                            sb.Append(' ');
                            sb.Append(str[i]);
                        }
                        // or only if it is not the last letter and the next letter IS NOT uppercase
                        else if (i != str.Length - 1 && !str[i + 1].IsUpperCase() && (str[i - 1].IsAlpha() || str[i - 1].IsNumeric()))
                        {
                            sb.Append(' ');
                            sb.Append(str[i]);
                        }
                        else
                        {
                            sb.Append(str[i]);
                        }
                    }
                    else if (str[i].IsNumeric() && str[i - 1].IsAlpha())
                    {
                        sb.Append(' ');
                        sb.Append(str[i]);
                    }
                    else if (
                        i != str.Length - 1 &&
                        !str[i].IsAlpha() && !str[i].IsNumeric() && str[i] != ' ' &&
                        (str[i + 1].IsAlpha() || str[i + 1].IsNumeric() || str[i + 1] == ' '))
                    {
                        sb.Append(' ');
                        sb.Append(str[i]);
                    }
                    else
                    {
                        sb.Append(str[i]);
                    }
                }
                else
                {
                    sb.Append(str[i]);
                }
            }

            return sb.ToString();
        }

        public static string RemoveDuplicates(this string str, char c)
        {
            var toRemove = new string(c, 2);
            while (str.Contains(toRemove))
            {
                str = str.Replace(toRemove, "");
            }
            return str;
        }

        public static string CsvAdd(this string csv, params string[] strings)
        {
            for (var i = 0; i < strings.Length; i++)
            {
                csv = String.Format("{0}{1}", strings[i], i == strings.Length - 1 ? "" : ",");
            }
            return csv;
        }

        public static string FormatText(this string text, params object[] args)
        {
            return string.Format(text, args);
        }

        public static string TrimToLastSpace(this string str, int max)
        {
            if (str.Length < max)
                return str;
            var lastIndexOfSpace = str.LastIndexOf(' ');
            return lastIndexOfSpace == -1 ? str : str.Substring(0, lastIndexOfSpace).Trim();
        }

        // Define other methods and classes here
        public static string EncodeToBase64(this string toEncode, Encoding encoding = null)
        {
            var toEncodeAsBytes
                  = (encoding ?? DefaultBase64Encoding()).GetBytes(toEncode);
            var returnValue
                  = Convert.ToBase64String(toEncodeAsBytes);
            return returnValue;
        }

        public static string DecodeFromBase64(this string encodedData, Encoding encoding = null)
        {
            var encodedDataAsBytes
                = Convert.FromBase64String(encodedData);
            var returnValue =
                (encoding ?? DefaultBase64Encoding()).GetString(encodedDataAsBytes, 0, encodedDataAsBytes.Length);
            return returnValue;
        }

        private static Encoding DefaultBase64Encoding()
        {
            return Encoding.UTF8;
        }


        public static string AlphaNumeric(this string str)
        {
            str = Regex.Replace(str, "[^A-z0-9\\s]", "");
            str = str.ReplaceWordThatMatches("By", "");
            var commonWords = new[]
            {
                "Edit",
                "Feat",
                "Remix",
                "Mix",
                "Extended",
                "xtended",
                "Full",
                "Length"
            };
            foreach (var word in commonWords)
            {
                str = str.ReplaceWordThatMatches(word,
                    "");
            }
            // Arbritrary
            if (str.Length > 6)
                str = str.ReplaceWordThatMatches(@"[A-z0-9]{1,2}", "");
            while (str.IndexOf("  ", StringComparison.Ordinal) != -1)
            {
                str = str.Replace("  ", " ");
            }
            //			str = Regex.Replace(str, "By", "", RegexOptions.IgnoreCase);
            return str.Trim();
        }

        public static string ReplaceWordThatMatches(this string str, string wordRegex, string replacement)
        {
            return Regex.Replace(str, "(?<Start>([^A-z0-9]|^))" + wordRegex + @"(?<End>([^A-z0-9]|$))", "${Start}" + replacement + "${End}", RegexOptions.IgnoreCase);
        }

        public static bool IsWeb(this string link)
        {
            return link.StartsWith("http", StringComparison.CurrentCultureIgnoreCase);
        }

        public static string ReplaceWhitespacesWithSpace(this string str)
        {
            try
            {
                return new Regex(@"\s+").Replace(str, " ").TrimEnd().TrimStart();
            }
            catch (Exception)
            {
                return str;
            }
        }

        private static readonly Random Random = new Random();
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        public static string RandomString(int size, string chars = Chars)
        {
            var buffer = new char[size];

            for (var i = 0; i < size; i++)
            {
                buffer[i] = chars[Random.Next(chars.Length)];
            }
            return new string(buffer);
        }
    }
}