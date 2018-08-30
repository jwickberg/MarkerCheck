using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkerCheck
{
	public enum UsfmTokenType { Book, Chapter, Verse, Text, Paragraph, Character, Note, End, Milestone, MilestoneEnd, Unknown };
    
	/// <summary>
	/// Represents a single indivisible token of USFM. "\v 1" is considered one token since the 1 is tightly
	/// associated with the \v and there is no closing marker to end it. \f + is also one token.
	/// TODO: Organize this file
	/// </summary>
	public class UsfmToken : IEquatable<UsfmToken>
	{
		/// <summary>
		/// Type of the token
		/// </summary>
		public readonly UsfmTokenType Type;

		/// <summary>
		/// Marker if the token is a tag. null if not applicable.
		/// </summary>
        public readonly string Marker;

		/// <summary>
		/// Text if the token is text. null if not applicable.
        /// ENHANCE: Someday we might want to refactor the code that sets this outside of the constructor
        /// so that this variable can also be read-only as well. This would make UsfmToken an
        /// immutable class which might benefit us when caching them (in the ScrParser?).
		/// </summary>
        public string Text;

        /// <summary>
        /// The full string for the attributes on this token or all the text that follows the first | to the end
        /// marker.
        /// ENHANCE: Make this read-only. Currently value is set in tokenizing code after token is created.
        /// </summary>
	    public NamedAttribute[] Attributes;

	    private Enum<AttributeName> defaultAttributeName;

		/// <summary>
		/// Token data if token is a tag. Empty if not applicable.
		/// </summary>
        public readonly string[] Data; 

		/// <summary>
		/// End marker if the token is a tag with a closing marker, null if not applicable
		/// </summary>
        public readonly string EndMarker;

        // Regex for references before rtl processing
        static readonly Regex rtlNumRefRegex = new Regex(@"\u200F*\d+(\u200F*([\p{P}\p{S}])\u200F*\d+)+", RegexOptions.Compiled);
        static readonly Regex rtlRefRegex = new Regex(@"\u200F*(\d+\w?)\u200F*([\p{P}\p{S}])\u200F*(?=\d)", RegexOptions.Compiled);
        static readonly Regex rtlRefSeparators = new Regex(@"[\p{P}\p{S}-[,]]", RegexOptions.Compiled);

        private const string fullAttributeStr = @"(?<name>[-\w]+)\s*\=\s*\""(?<value>.+?)\""\s*";
        private static readonly Regex attributeRegex = new Regex(@"(" + fullAttributeStr + @"(\s*" + fullAttributeStr +
    @")*|(?<default>[^\\=|]*))", RegexOptions.Compiled);


        public UsfmToken(UsfmTokenType type, string marker, string text, string endMarker, params string[] data)
		{
			Type = type;
			Marker = marker;
			Text = text;
			EndMarker = endMarker;
            Data = data;
        }

        public string NestlessMarker
        {
            get { return Marker != null && Marker.StartsWith("+") ? Marker.Substring(1) : Marker; }
        }

	    /// <summary>
	    /// Searches Attributes value for requested named attribute
	    /// </summary>
	    /// <param name="name">name of attribute</param>
	    /// <returns>the requested value or an empty string if not found</returns>
        public string GetAttribute(Enum<AttributeName> name)
        {
            return GetAttribute(Attributes, name);
        }

	    /// <summary>
	    /// Searches Attributes value for requested named attribute
	    /// </summary>
	    /// <param name="attributes">Attributes to be searched</param>
	    /// <param name="name">name of attribute</param>
	    /// <returns>the requested value or an empty string if not found</returns>
	    public static string GetAttribute(NamedAttribute[] attributes, Enum<AttributeName> name)
        {
            if (attributes == null || attributes.Length == 0)
                return "";

            NamedAttribute attribute = attributes.FirstOrDefault(a => a.Name == name);
            return attribute?.Value ?? "";
        }

        /// <summary>
        /// Searches Attributes value for requested named attribute
        /// <para>WARNING: Please use <see cref="GetAttribute(Enum{AttributeName})"/> whenever possible as it's more type-safe.</para>
        /// </summary>
        /// <param name="name">name of attribute</param>
        /// <returns>the requested value or an empty string if not found</returns>
        public string GetAttribute(string name)
	    {
            return GetAttribute(new Enum<AttributeName>(name));
	    }

        public int GetAttributeOffset(Enum<AttributeName> attributeName)
        {
            NamedAttribute attribute = Attributes?.FirstOrDefault(a => a.Name == attributeName);
            return attribute?.Offset ?? 0;
        }

        public void SetAttributes(Enum<AttributeName> defaultAttributeName, params NamedAttribute[] attributes)
        {
            this.defaultAttributeName = defaultAttributeName;
            Attributes = attributes;
        }

        public bool SetAttributes(string attributesValue, Enum<AttributeName> defaultAttributeName, ref string adjustedText, bool preserveWhitespace = false)
        {
            if (string.IsNullOrEmpty(attributesValue))
                return false;


            // for figures, convert 2.0 format to 3.0 format. Will need to write this as the 2.0 format
            // if the project is not upgrated.
            if (NestlessMarker == "fig" && attributesValue.Count(c => c == '|') == 5)
            {
                List<NamedAttribute> attributeList = new List<NamedAttribute>(6);
                string[] parts = attributesValue.Split('|');
                AppendAttribute(attributeList, "alt", adjustedText);
                AppendAttribute(attributeList, "src", parts[0]);
                AppendAttribute(attributeList, "size", parts[1]);
                AppendAttribute(attributeList, "loc", parts[2]);
                AppendAttribute(attributeList, "copy", parts[3]);
                string whitespace = "";
                if (preserveWhitespace)
                    whitespace = adjustedText.Substring(0, adjustedText.Length - adjustedText.TrimStart().Length);
                adjustedText = whitespace + parts[4];
                AppendAttribute(attributeList, "ref", parts[5]);
                Attributes = attributeList.ToArray();
                return true;
            }

            Match match = attributeRegex.Match(attributesValue);
            if (!match.Success || match.Length != attributesValue.Length)
                return false; // must match entire string

            Group defaultValue = match.Groups["default"];
            if (defaultValue.Success)
            {
                // only accept default value it there is a defined default attribute
                if (defaultAttributeName != Enum<AttributeName>.Null)
                {
                    Attributes = new[] { new NamedAttribute(defaultAttributeName, defaultValue.Value) };
                    this.defaultAttributeName = defaultAttributeName;
                    return true;
                }
                return false;
            }

            CaptureCollection attributeNames = match.Groups["name"].Captures;
            CaptureCollection attributeValues = match.Groups["value"].Captures;
            if (attributeNames.Count == 0 || attributeNames.Count != attributeValues.Count)
                return false;

            this.defaultAttributeName = defaultAttributeName;
            NamedAttribute[] attributes = new NamedAttribute[attributeNames.Count];
            for (int i = 0; i < attributeNames.Count; i++)
                attributes[i] = new NamedAttribute(attributeNames[i].Value, attributeValues[i].Value, attributeValues[i].Index);
            Attributes = attributes;
            return true;
        }

        public void CopyAttributes(UsfmToken sourceToken)
        {
            Attributes = sourceToken.Attributes;
            defaultAttributeName = sourceToken.defaultAttributeName;
        }

        public UsfmToken CreateUnnestedCharacterMarker()
        {
            if (Marker == NestlessMarker)
                return this;

            UsfmToken unnestedMarker = new UsfmToken(Type, NestlessMarker, Text, EndMarker, Data);
            unnestedMarker.Attributes = Attributes;
            return unnestedMarker;
        }

        /// <summary>
        /// Creates usfm from tokens
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns>usfm</returns>
        public static string Join(IEnumerable<UsfmToken> tokens)
        {
            return string.Concat(tokens.Select(t => t.ToUsfm()));
        }

        /// <summary>
        /// Creates text from tokens.
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public static string JoinText(IEnumerable<UsfmToken> tokens)
        {
            return string.Concat(tokens.Where(x => x.Type == UsfmTokenType.Text).Select(t => t.Text));
        }

        // TODO: unittest this method.
        /// <summary>
        /// Creates text from tokens.
        /// Ensure spaces exist between TextTokens that aren't direct siblings.
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public static string JoinTextReplacingNonTextTokensWithSpaces(IEnumerable<UsfmToken> tokens)
        {
            return JoinTextReplacingNonTextTokensWithChar(tokens, ' ');
        }

        public static string JoinTextReplacingNonTextTokensWithChar(IEnumerable<UsfmToken> tokens, char c)
        {
            return string.Concat(tokens.Select(t => t.Type == UsfmTokenType.Text ? t.Text : c.ToString()));
        }

        /// <summary>
        /// Tokenize the specified USFM text
        /// </summary>
        /// <param name="scrStylesheet">stylesheet to use</param>
        /// <param name="usfm">usfm string</param>
        /// <param name="preserveWhitespace">true to preserve all whitespaces verbatim in tokens</param>
        /// <returns>list of tokens</returns>
        public static List<UsfmToken> Tokenize(ScrStylesheet scrStylesheet, string usfm, bool preserveWhitespace)
        {
            List<UsfmToken> tokens = new List<UsfmToken>();
            UsfmToken lastTokenWithAttributes = null;

            int index = 0;		// Current position
		    while (index < usfm.Length)
            {
                int nextMarkerIndex = (index < usfm.Length - 1) ? usfm.IndexOf('\\', index + 1) : -1;
                if (nextMarkerIndex == -1)
                    nextMarkerIndex = usfm.Length;

                // If text, create text token until end or next \
                var ch = usfm[index];
                if (ch != '\\')
                {
                    string text = usfm.Substring(index, nextMarkerIndex - index);
					if (!preserveWhitespace)
						text = RegularizeSpaces(text);

                    lastTokenWithAttributes = null;
                    int attributeIndex = text.IndexOf('|');
                    if (attributeIndex >= 0)
                    {
                        UsfmToken matchingToken = FindMatchingStartMarker(usfm, tokens, nextMarkerIndex);
                        if (matchingToken != null)
                        {
                            ScrTag matchingTag = scrStylesheet.GetTag(matchingToken.NestlessMarker);
                            // leave attributes of other styles as regular text
                            if (matchingTag.StyleType == ScrStyleType.scCharacterStyle || matchingTag.StyleType == ScrStyleType.scMilestone ||
                                matchingTag.StyleType == ScrStyleType.scMilestoneEnd)
                            {
                                string adjustedText = text.Substring(0, attributeIndex);
                                if (matchingToken.SetAttributes(text.Substring(attributeIndex + 1),
                                    matchingTag.DefaultAttribute, ref adjustedText, preserveWhitespace))
                                {
                                    text = adjustedText;
                                    // attributes for ending milestone are not copied from the beginning milestone, so don't update last token value
                                    if (matchingTag.StyleType == ScrStyleType.scCharacterStyle)
                                        lastTokenWithAttributes =  matchingToken;
                                }
                            }
                        }
                    }

                    if (text.Length > 0)
                        tokens.Add(new UsfmToken(UsfmTokenType.Text, null, text, null));

                    index = nextMarkerIndex;
                    continue;
                }

                // Get marker (and move past whitespace or star ending)
                index++;
                int markerStart = index;
                while (index < usfm.Length)
                {
                    ch = usfm[index];

                    // Backslash starts a new marker
                    if (ch == '\\')
                        break;

                    // don't require a space before the | that starts attributes - mainly for milestones to allow \qt-s|speaker\*
                    if (ch == '|')
                        break;

					// End star is part of marker
					if (ch == '*')
					{
						index++;
						break;
					}

                    if (IsNonSemanticWhiteSpace(ch))
                    {
						// Preserve whitespace if needed, otherwise skip
						if (!preserveWhitespace)
							index++;
                        break;
                    }
                    index++;
                }
                string marker = usfm.Substring(markerStart, index - markerStart).TrimEnd();
                // Milestone stop/end markers are ended with \*, so marker will just be * and can be skipped
                if (marker == "*")
                {
                    // make sure that previous token was a milestone - have to skip space only tokens that may have been added when 
                    // preserveSpace is true.
                    UsfmToken prevToken = tokens.Count > 0 ? tokens.Last(t => t.Type != UsfmTokenType.Text || t.Text.Trim() != "") : null;
                    if (prevToken != null && (prevToken.Type == UsfmTokenType.Milestone ||
                        prevToken.Type == UsfmTokenType.MilestoneEnd))
                    {
                        // if the last item is an empty text token, remove it so we don't get extra space.
                        if (tokens.Last().Type == UsfmTokenType.Text)
                            tokens.RemoveAt(tokens.Count - 1);
                        continue;
                    }
                }

                // Multiple whitespace after non-end marker is ok
                if (!marker.EndsWith("*", StringComparison.Ordinal) && !preserveWhitespace)
                {
                    while ((index < usfm.Length) && IsNonSemanticWhiteSpace(usfm[index]))
                        index++;
                }

                // Lookup tag
                ScrTag tag = scrStylesheet.GetTag(marker.TrimStart('+'));

                // If starts with a plus and is not a character style or an end style, it is an unknown tag
                if (marker.StartsWith("+", StringComparison.Ordinal) && tag.StyleType != ScrStyleType.scCharacterStyle && tag.StyleType != ScrStyleType.scEndStyle)
                    tag = scrStylesheet.GetTag(marker);

                // Note: Unless this is a milestone, tag.Marker and tag.EndMarker are ignored if maras the plus prefix must be kept
                // and the end marker is always marker + "*"
                string endMarker = tag.StyleType != ScrStyleType.scMilestone ? marker + "*" : tag.Endmarker;

                switch (tag.StyleType)
                {
                    case ScrStyleType.scCharacterStyle:
                        // Handle verse special case
                        UsfmToken newToken;
                        if ((tag.TextProperties & TextProperties.scVerse) > 0)
                            newToken = new UsfmToken(UsfmTokenType.Verse, marker, null, null,
                                GetNextWord(usfm, ref index, preserveWhitespace));
                        else
                            newToken = new UsfmToken(UsfmTokenType.Character, marker, null, endMarker);
                        tokens.Add(newToken);
                        break;
                    case ScrStyleType.scParagraphStyle:
                        // Handle chapter special case
                        if ((tag.TextProperties & TextProperties.scChapter) > 0)
							tokens.Add(new UsfmToken(UsfmTokenType.Chapter, marker, null, null, GetNextWord(usfm, ref index, preserveWhitespace)));
                        else if ((tag.TextProperties & TextProperties.scBook) > 0)
							tokens.Add(new UsfmToken(UsfmTokenType.Book, marker, null, null, GetNextWord(usfm, ref index, preserveWhitespace)));
                        else
                            tokens.Add(new UsfmToken(UsfmTokenType.Paragraph, marker, null, endMarker));
                        break;
                    case ScrStyleType.scNoteStyle:
						tokens.Add(new UsfmToken(UsfmTokenType.Note, marker, null, endMarker, GetNextWord(usfm, ref index, preserveWhitespace)));
                        break;
                    case ScrStyleType.scEndStyle:
                        lastTokenWithAttributes = AddEndMarker(marker, tokens, lastTokenWithAttributes);
                        break;
                    case ScrStyleType.scUnknownStyle:
                        // End tokens are always end tokens, even if unknown
                        if (marker.EndsWith("*", StringComparison.Ordinal))
                            lastTokenWithAttributes = AddEndMarker(marker, tokens, lastTokenWithAttributes);
                        else
                        {
                            // Handle special case of esb and esbe which might not be in basic stylesheet
                            // but are always sidebars and so should be tokenized as paragraphs
                            if (marker=="esb"|| marker=="esbe")
                            {
                                tokens.Add(new UsfmToken(UsfmTokenType.Paragraph, marker, null, endMarker));
                                break;
                            }
                            // Create unknown token with a corresponding end note
                            tokens.Add(new UsfmToken(UsfmTokenType.Unknown, marker, null, marker + "*"));
                        }
                        break;
                    case ScrStyleType.scMilestone:
                    case ScrStyleType.scMilestoneEnd:
                        // if a milestone is not followed by a ending \* treat don't create a milestone token for the begining. Instead create at
                        // text token for all the text up to the beginning of the next marker. This will make typing of milestones easiest since
                        // the partially typed milestone more be reformatted to have a normal ending even if it hasn't been typed yet.
                        if (!MilestoneEnded(usfm, index))
                        {
                            int endOfText = (index < usfm.Length - 1) ? usfm.IndexOf('\\', index + 1) : -1;
                            if (endOfText == -1)
                                endOfText = usfm.Length;
                            string milestoneText = usfm.Substring(index, endOfText - index);
                            // add back space that was removed after marker
                            if (milestoneText.Length > 0 && milestoneText[0] != ' ' && milestoneText[0] != '|')
                                milestoneText = " " + milestoneText;
                            tokens.Add(new UsfmToken(UsfmTokenType.Text, null, @"\" + marker + milestoneText, null));
                            index = endOfText;
                        }
                        else if (tag.StyleType == ScrStyleType.scMilestone)
                            tokens.Add(new UsfmToken(UsfmTokenType.Milestone, marker, null, endMarker));
                        else
                            tokens.Add(new UsfmToken(UsfmTokenType.MilestoneEnd, marker, null, null));
                        break;
                    default:
                        Debug.Fail("Unknown ScrStyleType");
                        break;
                }
            }

            // Forces a space to be present in tokenization if immediately
            // before a token requiring a preceeding CR/LF. This is to ensure 
            // that when written to disk and re-read, that tokenization 
            // will match. For example, "\p test\p here" requires a space
            // after "test". Also, "\p \em test\em*\p here" requires a space
            // token inserted after \em*
			if (!preserveWhitespace)
			{
				for (int i = 1; i < tokens.Count; i++)
				{
					// If requires newline (verses do, except when after '(' or '[')
					if (tokens[i].Type == UsfmTokenType.Book ||
						tokens[i].Type == UsfmTokenType.Chapter ||
						tokens[i].Type == UsfmTokenType.Paragraph ||
						(tokens[i].Type == UsfmTokenType.Verse &&
							!(tokens[i - 1].Type == UsfmTokenType.Text &&
							(tokens[i - 1].Text.EndsWith("(", StringComparison.Ordinal) || tokens[i - 1].Text.EndsWith("[", StringComparison.Ordinal)))))
					{
						// Add space to text token
						if (tokens[i - 1].Type == UsfmTokenType.Text)
						{
							if (!tokens[i - 1].Text.EndsWith(" ", StringComparison.Ordinal))
								tokens[i - 1].Text = tokens[i - 1].Text + " ";
						}
						else if (tokens[i - 1].Type == UsfmTokenType.End)
						{
							// Insert space token after * of end marker
							tokens.Insert(i, new UsfmToken(UsfmTokenType.Text, null, " ", null));
							i++;
						}
					}
				}
			}

            return tokens;
        }

	    private static bool MilestoneEnded(string usfm, int index)
	    {
            int nextMarkerIndex = (index < usfm.Length) ? usfm.IndexOf('\\', index) : -1;
            if (nextMarkerIndex == -1 || nextMarkerIndex > usfm.Length - 2)
                return false;

	        return usfm.Substring(nextMarkerIndex, 2) == @"\*";
	    }

        private static IEnumerable<UsfmToken> ConvertFigureTokensToParatext7Format(UsfmToken token, UsfmToken textToken)
        {
            string figText = ConvertFigureToParatext7Format(token, textToken);
            // create new markers for changed data - don't change cached originals, attributes are dropped on new tokens
            yield return new UsfmToken(UsfmTokenType.Character, "fig", null, "fig*", null);
            yield return new UsfmToken(UsfmTokenType.Text, null, figText, null, null);
            yield return new UsfmToken(UsfmTokenType.End, "fig*", null, null, null);
        }


        private static string ConvertFigureToParatext7Format(UsfmToken startToken, UsfmToken textToken)
        {
            string desc = startToken.GetAttribute(AttributeName.AlternateDescription);
            string src = startToken.GetAttribute(AttributeName.Source);
            string size = startToken.GetAttribute(AttributeName.Size);
            string loc = startToken.GetAttribute(AttributeName.Location);
            string copy = startToken.GetAttribute(AttributeName.Copyright);
            string reference = startToken.GetAttribute(AttributeName.Reference);

            return $"{desc}|{src}|{size}|{loc}|{copy}|{textToken?.Text}|{reference}";
        }


        private static UsfmToken FindMatchingStartMarker(string usfm, List<UsfmToken> tokens, int nextMarkerIndex)
	    {
	        string expectedStartMarker;
	        if (!BeforeEndMarker(usfm, nextMarkerIndex, out expectedStartMarker))
	            return null;

	        if (expectedStartMarker == "" && (tokens.Last().Type == UsfmTokenType.Milestone ||
	            tokens.Last().Type == UsfmTokenType.MilestoneEnd))
	            return tokens.Last();

	        int nestingLevel = 0;
	        for (int i = tokens.Count - 1; i >= 0; i--)
	        {
	            UsfmToken token = tokens[i];
	            if (token.Type == UsfmTokenType.End)
	                nestingLevel++;
	            else if (token.Marker != null)
	            {
	                if (nestingLevel > 0)
	                    nestingLevel--;
	                else if (nestingLevel == 0)
	                    return token.Marker == expectedStartMarker ? token : null;
	            }
	        }

	        return null;
	    }

        private static UsfmToken AddEndMarker(string marker, List<UsfmToken> tokens, UsfmToken lastTokenWithAttributes)
	    {
	        UsfmToken endToken = new UsfmToken(UsfmTokenType.End, marker, null, null);
	        tokens.Add(endToken);
	        if (lastTokenWithAttributes != null && lastTokenWithAttributes.EndMarker == marker)
	        {
	            endToken.CopyAttributes(lastTokenWithAttributes);
	            lastTokenWithAttributes = null;
	        }
	        return lastTokenWithAttributes;
	    }

	    /// <summary>
        /// Parses the given ruby gloss text into segments.
        /// </summary>
        /// <returns>array of segments in gloss</returns>
        public static string[] ParseRubyGlosses(string glossText, bool normalizeSpaces)
        {
            // Empty or null should be treated as no glosses rather than an empty one.
            if (string.IsNullOrEmpty(glossText))
                return new string[0];
            // Spec currently just uses colon, but made this method so we only have to change one location.
            if (normalizeSpaces)
                glossText = glossText.FastNormalizeSpacesWithoutTrim();
            string[] glosses = glossText.Split(':');
            if (normalizeSpaces)
            {
                for (int i = 0; i < glosses.Length; i++)
                    glosses[i] = glosses[i].Trim();
            }
            return glosses;
        }

        /// <summary>
        /// Convenience method that both parses and joins ruby gloss text so that spaces are normalized.
        /// </summary>
        public static string NormalizeRubyGlosses(string glossText)
        {
            if (string.IsNullOrEmpty(glossText))
                return "";

            string[] glosses = ParseRubyGlosses(glossText, true);
            return string.Join(":", glosses);
        }

        private static void AppendAttribute(List<NamedAttribute> attributes, string name, string value)
        {
            value = value?.Trim();  // don't want to have attribute that is just spaces
	        if (!string.IsNullOrEmpty(value))
	            attributes.Add(new NamedAttribute(name, value));
	    }

	    private static bool BeforeEndMarker(string usfm, int nextMarkerIndex, out string startMarker)
        {
            startMarker = null;
            int index = nextMarkerIndex + 1;
            while (index < usfm.Length && usfm[index] != '*' && !char.IsWhiteSpace(usfm[index]))
                index++;

            if (index >= usfm.Length || usfm[index] != '*')
                return false;
            startMarker = usfm.Substring(nextMarkerIndex + 1, index - nextMarkerIndex - 1);
            return true;
        }

        /// <summary>
        /// Fully normalize usfm by converting to tokens and back.
        /// Adds all appropriate CR/LF and removes double-spaces.
        /// </summary>
        /// <param name="scrStylesheet">book stylesheet</param>
        /// <param name="usfm">original usfm</param>
        /// <param name="rtl">true for right-to-left texts</param>
        /// <returns>normalized usfm</returns>
		public static string NormalizeUsfm(ScrStylesheet scrStylesheet, string usfm, bool rtl)
		{
			return NormalizeUsfm(scrStylesheet, usfm, false, rtl);
		}

        /// <summary>
        /// Fully normalize usfm by converting to tokens and back.
        /// Adds all appropriate CR/LF and removes double-spaces.
        /// </summary>
        public static string NormalizeUsfm(ScrStylesheet scrStylesheet, string usfm, bool preserveWhitespace, bool rtl)
        {
            // Build usfm string from tokens
            string dest;

			if (!preserveWhitespace)
			{
				List<UsfmToken> tokens = Tokenize(scrStylesheet, usfm, false);
			    dest = NormalizeTokenUsfm(tokens, rtl);
			}
			else
				dest = usfm;

            return AddRtlAsNeeded(dest, rtl);
        }

        /// <summary>
        /// Fully normalize usfm by converting the list of tokens.
        /// Adds all appropriate CR/LF and removes double-spaces.
        /// </summary>
	    public static string NormalizeTokenUsfm(List<UsfmToken> tokens, bool rtl)
	    {
	        StringBuilder dest = new StringBuilder();
	        for (int i = 0; i < tokens.Count; i++)
	        {
	            switch (tokens[i].Type)
	            {
	                case UsfmTokenType.Book:
	                case UsfmTokenType.Chapter:
	                case UsfmTokenType.Paragraph:
	                    // Strip space from end of string before CR/LF
	                    if (dest.Length > 0)
	                    {
	                        if (dest[dest.Length - 1] == ' ')
	                            dest.Length--;
	                        dest.Append("\r\n");
	                    }
	                    dest.Append(tokens[i].ToUsfm());
	                    break;
	                case UsfmTokenType.Verse:
	                    // Add newline if after anything other than [ or (
	                    if (dest.Length > 0 && dest[dest.Length - 1] != '[' && dest[dest.Length - 1] != '(')
	                    {
	                        if (dest[dest.Length - 1] == ' ')
	                            dest.Length--;
	                        dest.Append("\r\n");
	                    }
	                    if (rtl)
	                        dest.Append(rtlRefRegex.Replace(tokens[i].ToUsfm(), "$1\u200F$2"));
	                    else
	                        dest.Append(tokens[i].ToUsfm());
	                    break;
	                default:
	                    dest.Append(tokens[i].ToUsfm());
	                    break;
	            }
	        }

	        // Make sure begins without space or CR/LF
	        if (dest.Length > 0 && dest[0] == ' ')
	            dest.Remove(0, 1);
	        if (dest.Length > 0 && dest[0] == '\r')
	            dest.Remove(0, 2);

	        // Make sure ends without space and with a CR/LF
	        if (dest.Length > 0 && dest[dest.Length - 1] == ' ')
	            dest.Length--;
	        if (dest.Length > 0 && dest[dest.Length - 1] != '\n')
	            dest.Append("\r\n");

            return dest.ToString();
	    }

	    /// <summary>
        /// Add right-to-left markers on references if they are needed.
        /// </summary>
	    public static string AddRtlAsNeeded(string text, bool rtl)
	    {
	        if (rtl)
	        {
	            // Insert special direction markers in references
	            // U+200F RIGHT-TO-LEFT MARK into text before various punctuation elements in references (colon, period, comma, dash)

	            //return rtlRefRegex.Replace(dest.ToString(), "$1\u200F$2");
	            // Seems we need to look more carefully at the strings we find containing digits and commas.
	            // Some of these are simple numbers (e.g. 600,000) and not references. In these cases we do
	            //   not want to add a RTL mark before the comma, since the entire number should read LTR as a unit.
	            return rtlNumRefRegex.Replace(text, UpdateRtlRefsPresentation);
	        }
            return text;
	    }

	    private static string UpdateRtlRefsPresentation(Match match)
        {
            string refString = match.Value;
            // Do we find any of the punctuation normally present in a reference other than a comma?
            if (ContainsAnyPunctuationOrSymbolsExceptComma(refString))
                return rtlRefRegex.Replace(refString, "$1\u200F$2");

            // If not, this is not a reference, just a number - just return it as is.
            return refString;
        }

        /// <summary>
        /// mono's regex implementation doesn't support [\p{P}]
        /// </summary>
        private static bool ContainsAnyPunctuationOrSymbolsExceptComma(string text)
        {
            if (Platform.IsDotNet)
                return rtlRefSeparators.IsMatch(text);

             foreach(char c in text)
             {
                 if ((Char.IsPunctuation(c) && c != '\u002C') || Char.IsSymbol(c))
                     return true;
             }
            
             return false;
        }

		/// <summary>
		/// Converts all control characters, carriage returns and tabs into
		/// spaces, and then strips duplicate spaces. 
		/// </summary>
		public static string RegularizeSpaces(string str)
		{
			StringBuilder sb = new StringBuilder(str.Length);
			bool wasSpace = false;
		    for (int i = 0; i < str.Length; i++)
			{
			    var ch = str[i];
			    // Control characters and CR/LF and TAB become spaces
				if (ch < 32)
				{
					if (!wasSpace)
						sb.Append(' ');
					wasSpace = true;
				}
                else if (!wasSpace && ch == StringUtils.zeroWidthSpace && i + 1 < str.Length && IsNonSemanticWhiteSpace(str[i + 1]))
                {
                    // ZWSP is redundant if followed by a space
                }
                else if (IsNonSemanticWhiteSpace(ch))
				{
					// Keep other kinds of spaces
					if (!wasSpace)
                        sb.Append(ch);
					wasSpace = true;
				}
				else
				{
                    sb.Append(ch);
					wasSpace = false;
				}
			}

		    return sb.ToString();
		}

		/// <summary>
		/// Gets the next word in the usfm and advances the index past it 
		/// </summary>
		/// <param name="usfm"></param>
		/// <param name="index"></param>
		/// <param name="preserveWhitespace">true to preserve all whitespace in tokens</param>
		/// <returns></returns>
		private static string GetNextWord(string usfm, ref int index, bool preserveWhitespace)
		{
			// Skip over leading spaces
			while ((index < usfm.Length) && IsNonSemanticWhiteSpace(usfm[index]))
				index++;

			int dataStart = index;
			while ((index < usfm.Length) && !IsNonSemanticWhiteSpace(usfm[index]) && (usfm[index] != '\\'))
				index++;

			string data = usfm.Substring(dataStart, index - dataStart);

			// Skip over trailing spaces
			if (!preserveWhitespace)
				while ((index < usfm.Length) && IsNonSemanticWhiteSpace(usfm[index]))
					index++;

			return data;
		}

        /// <summary>
        /// Checks if is whitespace, but not U+3000 (IDEOGRAPHIC SPACE).
        /// Note: ~ is not included as it is considered punctuation, not whitespace for simplicity.
        /// </summary>
        /// <param name="c">character</param>
        /// <returns>true if non-meaningful whitespace</returns>
        private static bool IsNonSemanticWhiteSpace(char c) 
        {
            // Consider \u200B (ZERO-WIDTH SPACE), 
            // FB 18842 -- ZWJ and ZWNJ are not whitespace
            return (c != '\u3000' && Char.IsWhiteSpace(c)) || (c == StringUtils.zeroWidthSpace);
        }

		public override bool Equals(object obj)
		{
			UsfmToken o = obj as UsfmToken;
			if (o==null)
				return false;

			if ((o.Type != Type) 
				|| (o.Text != Text)
				|| (o.Marker != Marker)
				|| (o.EndMarker != EndMarker)
				|| (o.HasData && o.Data.Length != Data.Length))
				return false;
            for (int i = 0; o.HasData  && i < o.Data.Length; i++)
				if (o.Data[i] != Data[i])
					return false;
			return true;
		}

        public int GetLength(bool includeNewlines = false, bool addSpaces = true)
        {
            int totalLength = (Text != null) ? Text.Length : 0;
            if (Marker != null)
            {
                if (includeNewlines && (Type == UsfmTokenType.Paragraph || Type == UsfmTokenType.Chapter ||
                    Type == UsfmTokenType.Verse))
                {
                    totalLength += 2;
                }
                totalLength += Marker.Length + 1; // marker and backslash
                if (addSpaces && (Marker.Length == 0 || Marker[Marker.Length - 1] != '*'))
                    totalLength++; // space

                if (Type == UsfmTokenType.End)
                    totalLength += ToAttributeString().Length;

                foreach (string data in Data.Where(d => d != null))
                {
                    totalLength += data.Length;
                    if (addSpaces)
                        totalLength++;
                }
            }
            return totalLength;
        }

        public string ToUsfm(bool includeNewlines = false, bool addSpaces = true)
        {
            string toReturn = Text ?? "";
            if (Marker != null)
            {
                StringBuilder sb = new StringBuilder();
                if (includeNewlines && (Type == UsfmTokenType.Paragraph || Type == UsfmTokenType.Chapter ||
                    Type == UsfmTokenType.Verse))
                {
                    sb.Append("\r\n");
                }
                if (Type == UsfmTokenType.End)
                    sb.Append(ToAttributeString());
                sb.Append('\\');
                if (Marker.Length > 0)
                    sb.Append(Marker);
                if (addSpaces && (Marker.Length == 0 || Marker[Marker.Length - 1] != '*'))
                    sb.Append(' ');

                for (int i = 0; i < Data?.Length; i++)
                {
                    if (!string.IsNullOrEmpty(Data[i]))
                    {
                        sb.Append(Data[i]);
                        if (addSpaces)
                            sb.Append(' ');
                    }
                }

                if (Type == UsfmTokenType.Milestone || Type == UsfmTokenType.MilestoneEnd)
                {
                    string attributes = ToAttributeString();
                    if (attributes != "")
                        sb.Append(attributes);
                    else
                    {
                        // remove space that was put after marker - not needed when there are no attributes.
                        sb.Length -= 1;
                    }
                    sb.Append(@"\*");
                }
                toReturn += sb.ToString();
            }
            return toReturn;
        }

        public string ToAttributeString()
        {
            if (Attributes == null || Attributes.Length == 0)
                return "";

            if (Attributes.Length == 1 && Attributes[0].Name == defaultAttributeName)
                return "|" + Attributes[0].Value;

            return "|" + string.Join(" ", Attributes.Select(a => a.ToString()));
        }

        /// <summary>
        /// Converts tokens to usfm string
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public static string ToUsfm(IEnumerable<UsfmToken> tokens)
        {
            return string.Join("", tokens.Select(t => t.ToUsfm()));
        }

	    /// <summary>
		/// Returns true if token has any non blank data
		/// </summary>
		public bool HasData
		{
			get
			{
				if (Data == null)
					return false;

			    return Data.Any(text => text.Trim().Length > 0);
			}
		}

	    public bool HasAttributes => Attributes != null && Attributes.Length > 0;

        public bool HasNonDefaultAttribute
        {
            get
            {
                if (!HasAttributes)
                    return false;

                if (Attributes.Length > 1)
                    return true;

                return Attributes[0].Name != defaultAttributeName;
            }
        }

        public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}

		public override string ToString()
		{
			if (Marker != null)
				return string.Format("[{0}] {1}", Marker, Data.Length > 0 ? Data[0] : "");
			else
				return string.Format("{0}", Text);
		}

        #region IEquatable<UsfmToken> Members

        bool IEquatable<UsfmToken>.Equals(UsfmToken other)
        {
            return Equals(other);
        }

        #endregion
	}

    /// <summary>
    /// Used for extra optional or transitional attributes (Currently only relavent for UsxUsfmParserSink usx >= version 2.6)
    /// </summary>
    public sealed class NamedAttribute
    {
        public NamedAttribute(Enum<AttributeName> name, string value, int offset = 0)
        {
            Name = name;
            Value = value;
            Offset = offset;
        }

        public NamedAttribute(string name, string value, int offset = 0) : this(new Enum<AttributeName>(name), value, offset)
        {
        }

        public Enum<AttributeName> Name { get; }
        public string Value { get; }

        public int Offset { get; }

        public override string ToString()
        {
            return Name.InternalValue + $"=\"{Value}\"";
        }
    }

}
