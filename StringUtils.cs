using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.IO;
using System.Text.RegularExpressions;

namespace MarkerCheck
{
    public static class StringUtils
    {
        public const char rtlMarker = '\u200F';
        public const char zeroWidthSpace = '\u200B';

        /// <summary>
        /// Returns whether this string starts with the specified character
        /// </summary>
        public static bool StartsWith(this string s, char c)
        {
            return s?.Length > 0 && s[0] == c;
        }

        /// <summary>
        /// Normalizes spaces in the specified string
        /// replacing multiple spaces with a single character.
        /// </summary>
        public static string FastNormalizeSpacesWithoutTrim(this string str, bool allWhitespace = true)
        {
            var sb = new StringBuilder(20);

            bool skipWhiteSpace = false;
            foreach (var c in str)
            {
                var ws = allWhitespace ? char.IsWhiteSpace(c) : c == ' ';
                if (skipWhiteSpace && ws)
                    continue;
                skipWhiteSpace = ws;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
