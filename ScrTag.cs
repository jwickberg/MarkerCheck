using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Globalization;
using System.Linq;

namespace MarkerCheck
{
    #region ScrStyleType enum
    /// <summary>
    /// Specify behavior of marker.
    /// </summary>
    public enum ScrStyleType
    {
        /// <summary>This is an unknown marker.</summary>
        scUnknownStyle = 0,
        /// <summary>
        /// Marker applies to a string of characters.
        /// Does not cause on-line break in output.
        /// Cannot contain embedded paragraph, note or character styles.
        /// Character style is terminated by 1) end marker, 2) next note style marker,
        /// 3) next paragraph style marker, or 4) next character style marker
        /// </summary>
        scCharacterStyle = 1,
        /// <summary>
        /// Marker starts an embedded note.
        /// Note is terminated by 1) end marker, 2) next note style marker,
        /// or 3) next paragraph style marker.
        /// May contain embededded character styles but NOT
        /// embedded paragraph or note styles.
        /// </summary>
        scNoteStyle = 2,
        /// <summary>
        /// Marker starts beginning of new
        /// paragraph. Causes a line break.
        /// May contain embedded note or character styles.
        /// </summary>
        scParagraphStyle = 3,
        /// <summary>
        /// Marker is end marker for a note or character style.
        /// If this marker is NthTag(i), associated begin marker is NthTag(i-1)
        /// </summary>
        scEndStyle = 4,
        /// <summary>
        /// Marker is the start of a milestone.
        /// </summary>
        scMilestone = 5,
        /// <summary>
        /// Marker is an end of a milestone.
        /// </summary>
        scMilestoneEnd = 6
    };
    #endregion

    #region ScrJustificationType enum
    public enum ScrJustificationType
    {
        scLeft = 1,
        scCenter = 2,
        scRight = 3,
        scBoth = 4
    };
    #endregion

    #region AttributeName enum
    public sealed class AttributeName : EnumType
    {
        public static Enum<AttributeName> Gloss = new Enum<AttributeName>("gloss");
        public static Enum<AttributeName> Lemma = new Enum<AttributeName>("lemma");
        public static Enum<AttributeName> AlternateDescription = new Enum<AttributeName>("alt");
        public static Enum<AttributeName> Source = new Enum<AttributeName>("src");
        public static Enum<AttributeName> Size = new Enum<AttributeName>("size");
        public static Enum<AttributeName> Location = new Enum<AttributeName>("loc");
        public static Enum<AttributeName> Copyright = new Enum<AttributeName>("copy");
        public static Enum<AttributeName> Reference = new Enum<AttributeName>("ref");
        public static Enum<AttributeName> LinkReference = new Enum<AttributeName>("link-href");
        public static Enum<AttributeName> LinkTitle = new Enum<AttributeName>("link-title");
        public static Enum<AttributeName> LinkName = new Enum<AttributeName>("link-name");
        public static Enum<AttributeName> Id = new Enum<AttributeName>("id");

        private AttributeName() { }
    }
    #endregion

    #region ScrTextType enum
    /// <summary>
    /// Specify overall type of text associated with a marker.
    /// Text may only have a single type although
    /// multiple types may be added together when specifying a filter.
    /// </summary>
    [Flags]
    public enum ScrTextType
    {
        /// <summary>For some styles that can be applied anywhere.</summary>
        scNotSpecified = 0,
        /// <summary>Title for a book, e.g. "The Gospel of Matthew"</summary>
        scTitle = 1,
        /// <summary>A section heading, e.g. "Jesus Feeds the 5000"</summary>
        scSection = 2,
        /// <summary>Normal verse body text.</summary>
        scVerseText = 4,
        /// <summary>
        /// Notes present at specific locations within
        /// the text. Examples: footnotes, cross
        /// references, consultant notes [only if they
        /// are stored within the text, not if they are
        /// store in SCNotesDatabase].
        /// </summary>
        scNoteText = 8,
        /// <summary>
        /// Not a Title, Section, VerseText or Note,
        /// e.g., introduction, glossary.
        /// </summary>
        scOther = 16,
        /// <summary>For a back translation</summary>
        scBackTranslation = 32,
        /// <summary>
        /// Note made recording a translation issue or decision.
        /// Not a publishable note (see scNoteText)
        /// </summary>
        scTranslationNote = 64
    };
    #endregion

    #region TextProperties enum
    /// <summary>
    /// Specify properties of text associated with a marker.
    /// Text may have multiple properties
    /// </summary>
    [Flags]
    public enum TextProperties
    {
        /// <summary>Beginning of new verse.</summary>
        scVerse = 1,

        /// <summary>Beginning of the chapter.</summary>
        scChapter = 2,

        /// <summary>
        /// Beginning of new paragraph.
        /// Note this would probably not be set for marker such as \m
        /// which normally indicates a continuation of the previous paragraph.
        /// </summary>
        scParagraph = 4,

        /// <summary>
        /// Text is publishable.
        /// This is present in addition to scNonpublishable
        /// to allow nested character styles to inherit this property.
        /// </summary>
        scPublishable = 8,

        /// <summary>
        /// Text is in vernacular language.
        /// This is present in addition to scNonvernacular
        /// to allow nested character styles to inherit this property.
        /// </summary>
        scVernacular = 16,

        /// <summary>Text is poetic.</summary>
        scPoetic = 32,

        /// <summary>Begin range of text with type scOther. //! NOT IMPLEMENTED</summary>
        scOtherTextBegin = 64,

        /// <summary>End range of text with type scOther. //! NOT IMPLEMENTED</summary>
        scOtherTextEnd = 128,

        /// <summary>Level of title, section, or poetic line</summary>
        scLevel_1 = 256,
        /// <summary>Level of title, section, or poetic line</summary>
        scLevel_2 = 512,
        /// <summary>Level of title, section, or poetic line</summary>
        scLevel_3 = 1024,
        /// <summary>Level of title, section, or poetic line</summary>
        scLevel_4 = 2048,
        /// <summary>Level of title, section, or poetic line</summary>
        scLevel_5 = 4096,

        /// <summary>Text contains reference to another portion of Scripture</summary>
        scCrossReference = 8192,

        /// <summary>
        /// Text is not publishable.
        /// This is present in addition to scPublishable
        /// to allow nested character styles to inherit this property.
        /// </summary>
        scNonpublishable = 16384,

        /// <summary>
        /// This is present in addition to scVernacular
        /// to allow nested character styles to inherit this property.
        /// </summary>
        scNonvernacular = 32768,

        /// <summary>Beginning of new book.</summary>
        scBook = 2 * 32768,

        /// <summary>Beginning of new note.</summary>
        scNote = 4 * 32768,
    };
    #endregion

    #region StyleAttribute class
    public sealed class StyleAttribute
    {
        public readonly Enum<AttributeName> Name;
        public readonly bool IsRequired;

        public StyleAttribute(Enum<AttributeName> name, bool isRequired)
        {
            Name = name;
            IsRequired = isRequired;
        }
    }
    #endregion

    #region ScrTag class
    /// <summary>
    /// Single tag (marker) from a stylesheet
    /// <remarks>Thread safe for reading only</remarks>
    /// </summary>
    public sealed class ScrTag
    {
        #region Constants
        private static readonly char[] spaceSep = { ' ' };

        private static readonly List<KeyValuePair<string, ScrJustificationType>> propToJustification = 
            new List<KeyValuePair<string, ScrJustificationType>>(new [] {
                new KeyValuePair<string, ScrJustificationType>("left", ScrJustificationType.scLeft),
                new KeyValuePair<string, ScrJustificationType>("center", ScrJustificationType.scCenter),
                new KeyValuePair<string, ScrJustificationType>("right", ScrJustificationType.scRight),
                new KeyValuePair<string, ScrJustificationType>("both", ScrJustificationType.scBoth)});

        private static readonly List<KeyValuePair<string, ScrStyleType>> propToStyleType = 
            new List<KeyValuePair<string, ScrStyleType>>(new [] {
                new KeyValuePair<string, ScrStyleType>("character", ScrStyleType.scCharacterStyle),
                new KeyValuePair<string, ScrStyleType>("paragraph", ScrStyleType.scParagraphStyle),
                new KeyValuePair<string, ScrStyleType>("note", ScrStyleType.scNoteStyle),
                new KeyValuePair<string, ScrStyleType>("milestone", ScrStyleType.scMilestone)
            });

        private static readonly List<KeyValuePair<string, TextProperties>> propToTextProps =
            new List<KeyValuePair<string, TextProperties>>(new[] {
                new KeyValuePair<string, TextProperties>("verse", TextProperties.scVerse),
                new KeyValuePair<string, TextProperties>("chapter", TextProperties.scChapter),
                new KeyValuePair<string, TextProperties>("paragraph", TextProperties.scParagraph),
                new KeyValuePair<string, TextProperties>("publishable", TextProperties.scPublishable),
                new KeyValuePair<string, TextProperties>("vernacular", TextProperties.scVernacular),
                new KeyValuePair<string, TextProperties>("poetic", TextProperties.scPoetic),
                new KeyValuePair<string, TextProperties>("level_1", TextProperties.scLevel_1),
                new KeyValuePair<string, TextProperties>("level_2", TextProperties.scLevel_2),
                new KeyValuePair<string, TextProperties>("level_3", TextProperties.scLevel_3),
                new KeyValuePair<string, TextProperties>("level_4", TextProperties.scLevel_4),
                new KeyValuePair<string, TextProperties>("level_5", TextProperties.scLevel_5),
                new KeyValuePair<string, TextProperties>("crossreference", TextProperties.scCrossReference),
                new KeyValuePair<string, TextProperties>("nonpublishable", TextProperties.scNonpublishable),
                new KeyValuePair<string, TextProperties>("nonvernacular", TextProperties.scNonvernacular),
                new KeyValuePair<string, TextProperties>("book", TextProperties.scBook),
                new KeyValuePair<string, TextProperties>("note", TextProperties.scNote)});

        private static readonly List<KeyValuePair<string, ScrTextType>> propToTextType =
            new List<KeyValuePair<string, ScrTextType>>(new[] {
                new KeyValuePair<string, ScrTextType>("title", ScrTextType.scTitle),
                new KeyValuePair<string, ScrTextType>("section", ScrTextType.scSection),
                new KeyValuePair<string, ScrTextType>("versetext", ScrTextType.scVerseText),
                new KeyValuePair<string, ScrTextType>("notetext", ScrTextType.scNoteText),
                new KeyValuePair<string, ScrTextType>("other", ScrTextType.scOther),
                new KeyValuePair<string, ScrTextType>("backtranslation", ScrTextType.scBackTranslation),
                new KeyValuePair<string, ScrTextType>("translationnote", ScrTextType.scTranslationNote),
                new KeyValuePair<string, ScrTextType>("versenumber", ScrTextType.scVerseText),
                new KeyValuePair<string, ScrTextType>("chapternumber", ScrTextType.scOther)});
        #endregion

        #region Member variables
        public string Marker;
        /// <summary>Will be null if not set</summary>
        public bool? RawBold;
        /// <summary>Will be null if not set</summary>
        public int? RawColor;
        /// <summary>Will be null if not set</summary>
        public string RawDescription;
        /// <summary>Will be null if not set</summary>
        public string RawEncoding;
        /// <summary>Will be null if not set</summary>
        public string RawEndmarker;
        /// <summary>Will be null if not set</summary>
        public int? RawFirstLineIndent;
        /// <summary>Will be null if not set</summary>
        public string RawFontname;
        /// <summary>Will be null if not set</summary>
        public int? RawFontSize;
        /// <summary>Will be null if not set</summary>
        public bool? RawItalic;
        /// <summary>Will be null if not set</summary>
        public ScrJustificationType? RawJustificationType;
        /// <summary>Will be null if not set</summary>
        public int? RawLeftMargin;
        /// <summary>Will be null if not set</summary>
        public int? RawLineSpacing;
        /// <summary>Will be null if not set</summary>
        public string RawName;
        /// <summary>Will be null if not set</summary>
        public bool? RawNotRepeatable;
        /// <summary>Will be null if not set</summary>
        public string RawOccursUnder;
        /// <summary>Will be null if not set</summary>
        public int? RawRank;
        /// <summary>Will be null if not set</summary>
        public bool? RawRegular;
        /// <summary>Will be null if not set</summary>
        public int? RawRightMargin;
        /// <summary>Will be null if not set</summary>
        public bool? RawSmallCaps;
        /// <summary>Will be null if not set</summary>
        public int? RawSpaceAfter;
        /// <summary>Will be null if not set</summary>
        public int? RawSpaceBefore;
        /// <summary>Will be null if not set</summary>
        public ScrStyleType? RawStyleType;
        /// <summary>Will be null if not set</summary>
        public bool? RawSubscript;
        /// <summary>Will be null if not set</summary>
        public bool? RawSuperscript;
        /// <summary>Will be null if not set</summary>
        public TextProperties? RawTextProperties;
        /// <summary>Will be null if not set</summary>
        public ScrTextType? RawTextType;
        /// <summary>Will be null if not set</summary>
        public bool? RawUnderline;
        /// <summary>Will be null if not set</summary>
        public string RawXmlTag;

        private List<StyleAttribute> attributes;
        private string rawAttributes;
        private Enum<AttributeName> defaultAttribute;
        #endregion

        #region Constructor
        public ScrTag(string marker = "")
        {
            Marker = marker;
        }
        #endregion

        #region Properties
        public IEnumerable<StyleAttribute> Attributes => attributes ?? Enumerable.Empty<StyleAttribute>();

        public bool Bold
        {
            get { return RawBold ?? false; }
            set { RawBold = value; }
        }

        /// <summary>
        /// Color value converted to .NET object
        /// </summary>
        public RgbColor Color
        {
            get
            {
                int c = RawColor ?? 0;
                return new RgbColor((c >> 16) & 0xff, (c >> 8) & 0xff, c & 0xff);
            }
            set { RawColor = value.R << 16 | value.G << 8 | value.B; }
        }

        public string Description
        {
            get { return RawDescription ?? ""; }
            set { RawDescription = value; }
        }

        public Enum<AttributeName> DefaultAttribute => defaultAttribute;

        public string Encoding
        {
            get { return RawEncoding ?? ""; }
            set { RawEncoding = value; }
        }

        public string Endmarker
        {
            get { return RawEndmarker ?? ""; }
            set { RawEndmarker = value; }
        }

        public int FirstLineIndent
        {
            get { return RawFirstLineIndent ?? 0; }
            set { RawFirstLineIndent = value; }
        }

        public string Fontname
        {
            get { return RawFontname ?? ""; }
            set { RawFontname = value; }
        }

        public int FontSize
        {
            get { return RawFontSize ?? 0; }
            set { RawFontSize = value; }
        }

        public bool IsBasic => Description.Contains("(basic)");

        public bool Italic
        {
            get { return RawItalic ?? false; }
            set { RawItalic = value; }
        }

        public ScrJustificationType JustificationType
        {
            get { return RawJustificationType ?? ScrJustificationType.scLeft; }
            set { RawJustificationType = value; }
        }

        public int LeftMargin
        {
            get { return RawLeftMargin ?? 0; }
            set { RawLeftMargin = value; }
        }

        public int LineSpacing
        {
            get { return RawLineSpacing ?? 0; }
            set { RawLineSpacing = value; }
        }
        
        public string Name
        {
            get { return RawName ?? ""; }
            set { RawName = value; }
        }

        public bool NotRepeatable
        {
            get { return RawNotRepeatable ?? false; }
            set { RawNotRepeatable = value; }
        }

        public string OccursUnder
        {
            get { return RawOccursUnder ?? ""; }
            set { RawOccursUnder = value; }
        }

        public List<string> OccursUnderList => new List<string>(OccursUnder.Split(spaceSep, StringSplitOptions.RemoveEmptyEntries));

        public int Rank
        {
            get { return RawRank ?? 0; }
            set { RawRank = value; }
        }

        /// <summary>Will be null if not set</summary>
        public string RawAttributes
        {
            get { return rawAttributes; }
            set
            {
                rawAttributes = value;
                string[] attributeNames = value.Split(spaceSep, StringSplitOptions.RemoveEmptyEntries);
                if (attributeNames.Length == 0)
                    throw new ArgumentException(Localizer.Str("Attributes cannot be empty"));
                attributes = new List<StyleAttribute>(attributeNames.Length);
                bool foundOptional = false;
                foreach (string attribute in attributeNames)
                {
                    bool isOptional = attribute.StartsWith('?');
                    if (!isOptional && foundOptional)
                        throw new ArgumentException(Localizer.Str("Required attributes must precede optional attributes"));

                    attributes.Add(new StyleAttribute(isOptional ? new Enum<AttributeName>(attribute.Substring(1)) :
                        new Enum<AttributeName>(attribute), !isOptional));
                    foundOptional |= isOptional;
                }

                defaultAttribute = attributes.Count(a => a.IsRequired) <= 1 ? 
                    attributes[0].Name : Enum<AttributeName>.Null;
            }
        }

        public bool Regular
        {
            get { return RawRegular ?? false; }
            set
            {
                Debug.Assert(value, "We don't support turning Regular 'off' as it's just a style to turn bold, italic, and superscript 'off'");
                RawRegular = true;
            }
        }

        public int RightMargin
        {
            get { return RawRightMargin ?? 0; }
            set { RawRightMargin = value; }
        }

        public bool SmallCaps
        {
            get { return RawSmallCaps ?? false; }
            set { RawSmallCaps = value; }
        }

        public int SpaceAfter
        {
            get { return RawSpaceAfter ?? 0; }
            set { RawSpaceAfter = value; }
        }

        public int SpaceBefore
        {
            get { return RawSpaceBefore ?? 0; }
            set { RawSpaceBefore = value; }
        }

        public ScrStyleType StyleType
        {
            get { return RawStyleType ?? ScrStyleType.scUnknownStyle; }
            set { RawStyleType = value; }
        }

        public bool Subscript
        {
            get { return RawSubscript ?? false; }
            set { RawSubscript = value; }
        }

        public bool Superscript
        {
            get { return RawSuperscript ?? false; }
            set { RawSuperscript = value; }
        }

        public TextProperties TextProperties
        {
            get { return RawTextProperties ?? 0; }
            set { RawTextProperties = value; }
        }

        public ScrTextType TextType
        {
            get { return RawTextType ?? 0; }
            set { RawTextType = value; }
        }

        public bool Underline
        {
            get { return RawUnderline ?? false; }
            set { RawUnderline = value; }
        }

        public string XMLTag
        {
            get { return RawXmlTag ?? ""; }
            set { RawXmlTag = value; }
        }
        #endregion

        public bool HasTextProperty(TextProperties property)
        {
            return (TextProperties & property) != 0;
        }

        public override string ToString()
        {
            string text = Marker + " " + StyleType + " " + TextType;
            if (HasTextProperty(TextProperties.scBook))
                text += " book";
            if (HasTextProperty(TextProperties.scChapter))
                text += " chapter";
            if (HasTextProperty(TextProperties.scVerse))
                text += " verse";

            if (HasTextProperty(TextProperties.scPublishable))
                text += " publishable";
            if (HasTextProperty(TextProperties.scNonpublishable))
                text += " nonpublishable";

            return text;
        }

        /// <summary>
        /// Apply formatting properties of nested marker to this marker
        /// </summary>
        public void AddNestedFormatting(ScrTag nested)
        {
            if (nested.Fontname != "") 
                Fontname = nested.Fontname;

            if (nested.RawFontSize != null) 
                FontSize = nested.FontSize;

            if (nested.RawBold != null) 
                Bold = nested.Bold;

            if (nested.RawSmallCaps != null) 
                SmallCaps = nested.SmallCaps;

            if (nested.RawSubscript != null) 
                Subscript = nested.Subscript;

            if (nested.RawItalic != null) 
                Italic = nested.Italic;

            if (nested.RawUnderline != null) 
                Underline = nested.Underline;

            if (nested.RawSuperscript != null) 
                Superscript = nested.Superscript;

            if (nested.RawColor != null)
                RawColor = (int)nested.RawColor;

            if (nested.Regular)
            {
                Bold = false;
                Italic = false;
                Superscript = false;
            }
        }

        public ScrTag Clone()
        {
            // Since ScrTag only contains value-types, this should always work. If reference-types are ever added to
            // ScrTag, then this needs to change.
            return (ScrTag)MemberwiseClone();
        }

        public string AsString()
        {
            StringBuilder result = new StringBuilder();

            if (Marker != "")
                result.Append("\\Marker ").Append(Marker).Append("\r\n");
            if (RawName != null)
                result.Append("\\Name ").Append(Name).Append("\r\n");
            if (RawDescription != null)
                result.Append("\\Description ").Append(Description).Append("\r\n");
            if (RawEncoding != null)
                result.Append("\\Encoding ").Append(Encoding).Append("\r\n");
            if (RawEndmarker != null)
                result.Append("\\Endmarker ").Append(Endmarker).Append("\r\n");
            if (RawFontname != null)
                result.Append("\\Fontname ").Append(Fontname).Append("\r\n");
            if (RawOccursUnder != null)
                result.Append("\\OccursUnder ").Append(OccursUnder).Append("\r\n");
            if (RawXmlTag != null)
                result.Append("\\XMLTag ").Append(XMLTag).Append("\r\n");

            if (RawRegular != null)
                result.Append("\\Regular").Append("\r\n"); // Must be before Bold, Italic, and Superscript
            if (RawBold != null)
                result.Append("\\Bold").Append(!Bold ? " -" : "").Append("\r\n");
            if (RawItalic != null)
                result.Append("\\Italic").Append(!Italic ? " -" : "").Append("\r\n");
            if (RawNotRepeatable != null)
                result.Append("\\NotRepeatable").Append(!NotRepeatable ? " -" : "").Append("\r\n");
            if (RawSmallCaps != null)
                result.Append("\\SmallCaps").Append(!SmallCaps ? " -" : "").Append("\r\n");
            if (RawSubscript != null)
                result.Append("\\Subscript").Append(!Subscript ? " -" : "").Append("\r\n");
            if (RawSuperscript != null)
                result.Append("\\Superscript").Append(!Superscript ? " -" : "").Append("\r\n");
            if (RawUnderline != null)
                result.Append("\\Underline").Append(!Underline ? " -" : "").Append("\r\n");

            if (RawColor != null)
                result.Append("\\Color ").Append(RawColor).Append("\r\n");
            if (RawFontSize != null)
                result.Append("\\FontSize ").Append(FontSize).Append("\r\n");
            if (RawLineSpacing != null)
                result.Append("\\LineSpacing ").Append(LineSpacing).Append("\r\n");
            if (RawRank != null)
                result.Append("\\Rank ").Append(Rank).Append("\r\n");
            if (RawSpaceAfter != null)
                result.Append("\\SpaceAfter ").Append(SpaceAfter).Append("\r\n");
            if (RawSpaceBefore != null)
                result.Append("\\SpaceBefore ").Append(SpaceBefore).Append("\r\n");

            if (RawLeftMargin != null)
                result.Append("\\LeftMargin ").Append((LeftMargin / 1000.0).ToString("0.000", CultureInfo.InvariantCulture)).Append("\r\n");
            if (RawRightMargin != null)
                result.Append("\\RightMargin ").Append((RightMargin / 1000.0).ToString("0.000", CultureInfo.InvariantCulture)).Append("\r\n");
            if (RawFirstLineIndent != null)
                result.Append("\\FirstLineIndent ").Append((FirstLineIndent / 1000.0).ToString("0.000", CultureInfo.InvariantCulture)).Append("\r\n");

            if (RawJustificationType != null)
                result.Append("\\Justification ").Append(DecodeEnum(JustificationType, propToJustification)).Append("\r\n");
            if (RawStyleType != null)
                result.Append("\\StyleType ").Append(DecodeEnum(StyleType, propToStyleType)).Append("\r\n");
            if (RawTextType != null && RawTextType.Value != ScrTextType.scNotSpecified)
                result.Append("\\TextType ").Append(DecodeEnum(TextType, propToTextType)).Append("\r\n");
            if (RawTextProperties != null)
                result.Append("\\TextProperties ").Append(DecodeTextProperties(TextProperties)).Append("\r\n");
            if (RawAttributes != null)
                result.Append("\\Attributes ").Append(RawAttributes).Append("\r\n");

            return result.ToString();
        }

        #region Static methods used for parsing stylesheet

        /// <summary>
        /// ScrTag as parsed from the specified stylesheet entries.
        /// </summary>
        /// <param name="qTag">qTag needs to have the Marker set when calling this method</param>
        /// <param name="stylesheetEntries"></param>
        /// <param name="entryIndex"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        internal static ScrTag ParseSTYMarkerEntry(ScrTag qTag, List<StylesheetEntry> stylesheetEntries, int entryIndex, 
            out List<string> errors)
        {
            int markerLineNumber = stylesheetEntries[entryIndex - 1].LineNumber;

            // The following items are present for conformance with
            // Paratext release 5.0 stylesheets.  Release 6.0 and later
            // follows the guidelines set in InitPropertyMaps.
            // Make sure \id gets book property
            if (qTag.Marker == "id")
                qTag.AddTextProperty(TextProperties.scBook);
            
            errors = new List<string>();
            HashSet<string> foundAttribs = new HashSet<string>();
            ScrTag qTagEndMarker = null;
            while (entryIndex < stylesheetEntries.Count)
            {
                StylesheetEntry entry = stylesheetEntries[entryIndex];
                ++entryIndex;

                if (entry.Marker == "marker")
                    break;

                if (foundAttribs.Contains(entry.Marker))
                    errors.Add(GetMessage(entry.LineNumber, string.Format(Localizer.Str("Duplicate style attribute '{0}'"), entry.Marker)));

                try
                {
                    switch (entry.Marker)
                    {
                        case "name": qTag.Name = entry.Text; break;
                        case "description": qTag.Description = entry.Text; break;
                        case "fontname": qTag.Fontname = entry.Text; break;
                        case "fontsize": qTag.FontSize = entry.Text == "-" ? 0 : ParseI(entry); break;
                        case "xmltag": qTag.XMLTag = entry.Text; break;
                        case "encoding": qTag.Encoding = entry.Text; break;
                        case "linespacing": qTag.LineSpacing = ParseI(entry); break;
                        case "spacebefore": qTag.SpaceBefore = ParseI(entry); break;
                        case "spaceafter": qTag.SpaceAfter = ParseI(entry); break;
                        case "leftmargin": qTag.LeftMargin = ParseF(entry); break;
                        case "rightmargin": qTag.RightMargin = ParseF(entry); break;
                        case "firstlineindent": qTag.FirstLineIndent = ParseF(entry); break;
                        case "rank": qTag.Rank = entry.Text == "-" ? 0 : ParseI(entry); break;
                        case "bold": qTag.Bold = (entry.Text != "-"); break;
                        case "smallcaps": qTag.SmallCaps = (entry.Text != "-"); break;
                        case "subscript": qTag.Subscript = (entry.Text != "-"); break;
                        case "italic": qTag.Italic = (entry.Text != "-"); break;
                        // FB 23177 - added the \Regular tag so that there is a way to reset Italic, Bold and Superscript
                        // that is compatible with the ptx2pdf macros used by PrintDraft
                        case "regular":
                            qTag.Italic = qTag.Bold = qTag.Superscript = false;
                            qTag.Regular = true;
                            break;
                        case "underline": qTag.Underline = (entry.Text != "-"); break;
                        case "superscript": qTag.Superscript = (entry.Text != "-"); break;
                        case "testylename": break; // Ignore this tag, later we will use it to tie to FW styles
                        case "notrepeatable": qTag.NotRepeatable = (entry.Text != "-"); break;
                        case "textproperties": ParseTextProperties(qTag, entry); break;
                        case "texttype": ParseTextType(qTag, entry); break;
                        case "color": qTag.RawColor = entry.Text == "-" ? 0 : ParseColor(entry); break;
                        case "colorname": qTag.RawColor = entry.Text == "-" ? 0 : GetThemeColor(entry); break;
                        case "justification": qTag.JustificationType = ParseEnum(entry.Text, propToJustification); break;
                        case "styletype": qTag.StyleType = ParseEnum(entry.Text, propToStyleType); break;
                        case "attributes":
                            try
                            {
                                qTag.RawAttributes = entry.Text;
                            }
                            catch (ArgumentException e)
                            {
                                errors.Add(GetMessage(entry.LineNumber, e.Message));
                            }
                            break;
                        case "occursunder":
                            qTag.OccursUnder = String.Join(" ", entry.Text.Split(spaceSep, StringSplitOptions.RemoveEmptyEntries));
                            break;
                        case "endmarker":
                            qTagEndMarker = MakeEndMarker(entry.Text);
                            qTag.Endmarker = entry.Text;
                            break;
                        default:
                            errors.Add(GetMessage(entry.LineNumber, string.Format(Localizer.Str("Unknown marker: {0}"), entry.Marker)));
                            break;
                    }
                }
                catch (ArgumentOutOfRangeException e)
                {
                    errors.Add(GetMessage(entry.LineNumber, 
                        string.Format(Localizer.Str("Invalid definition for marker '{0}': {1}"), entry.Marker, e.ActualValue)));
                }
                foundAttribs.Add(entry.Marker);
            }

            if (string.IsNullOrEmpty(qTag.Name))
                errors.Add(GetMessage(markerLineNumber, string.Format(Localizer.Str("Missing name for style: {0}"), qTag.Marker)));

            // If we have not seen an end marker but this is a character style
            if (qTag.StyleType == ScrStyleType.scCharacterStyle && qTagEndMarker == null)
            {
                string endMarker = qTag.Marker + "*";
                qTagEndMarker = MakeEndMarker(endMarker);
                qTag.Endmarker = endMarker;
            }
            else if (qTag.StyleType == ScrStyleType.scMilestone)
            {
                if (qTagEndMarker != null)
                {
                    qTagEndMarker.StyleType = ScrStyleType.scMilestoneEnd;
                    qTagEndMarker.RawAttributes = "?id"; // id is always an optional attribute for the end marker
                    qTagEndMarker.Name = qTag.Name;
                }
                else
                    errors.Add(GetMessage(markerLineNumber,
                        string.Format(Localizer.Str("Missing end marker for style: {0}"), qTag.Marker)));
            }

            // Special cases
            if (qTag.TextType == ScrTextType.scOther
                && !qTag.HasTextProperty(TextProperties.scNonpublishable)
                && !qTag.HasTextProperty(TextProperties.scChapter)
                && !qTag.HasTextProperty(TextProperties.scVerse)
                && (qTag.StyleType == ScrStyleType.scCharacterStyle || qTag.StyleType == ScrStyleType.scParagraphStyle))
            {
                qTag.AddTextProperty(TextProperties.scPublishable);
            }

            return qTagEndMarker;
        }

        private static int ParseI(StylesheetEntry entry)
        {
            int result;
            if (!Int32.TryParse(entry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) || result < 0)
                throw new ArgumentOutOfRangeException("entry", entry.Text, "");

            return result;
        }

        private static int ParseColor(StylesheetEntry entry)
        {
            int result;
            if (entry.Text.Length <= 1 || entry.Text[0] != 'x')
            {
                result = ParseI(entry);
                // Integer codes have been in B G R order, so convert this to the standard order
                int b = (result >> 16) & 0xFF;
                int g = (result >> 8) & 0xFF;
                int r = result & 0xFF;
                result = (r << 16) | (g << 8) | b;
            }
            else if (!Int32.TryParse(entry.Text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result) || result < 0)
                throw new ArgumentOutOfRangeException("entry", entry.Text, "");

            return result;
        }

        private static int GetThemeColor(StylesheetEntry entry)
        {
            return 0;
        }

        private static int ParseF(StylesheetEntry entry)
        {
            float result;
            if (!Single.TryParse(entry.Text, NumberStyles.Float | NumberStyles.AllowThousands,CultureInfo.InvariantCulture, out result))
                throw new ArgumentOutOfRangeException("entry", entry.Text, "");

            return (int)(result * 1000);
        }

        private static T ParseEnum<T>(string entryText, List<KeyValuePair<string, T>> mapping)
        {
            string text = entryText.ToLowerInvariant();
            int index = mapping.FindIndex(map => map.Key == text);
            if (index == -1)
                throw new ArgumentOutOfRangeException("entryText", text, "");
            return mapping[index].Value;
        }

        private static string DecodeEnum<T>(T enumValue, List<KeyValuePair<string, T>> mapping)
        {
            int index = mapping.FindIndex(map => map.Value.Equals(enumValue));
            if (index == -1)
                throw new ArgumentOutOfRangeException("enumValue", enumValue, "");
            return mapping[index].Key;
        }

        private static void ParseTextType(ScrTag qTag, StylesheetEntry entry)
        {
            string text = entry.Text.ToLowerInvariant();

            if (text == "chapternumber")
                qTag.AddTextProperty(TextProperties.scChapter);
            if (text == "versenumber")
                qTag.AddTextProperty(TextProperties.scVerse);

            qTag.TextType = ParseEnum(entry.Text, propToTextType);
        }

        private static void ParseTextProperties(ScrTag qTag, StylesheetEntry entry)
        {
            string text = entry.Text.ToLowerInvariant();
            string[] parts = text.Split();

            foreach (string part in parts)
            {
                if (part.Trim() == "")
                    continue;

                qTag.AddTextProperty(ParseEnum(part, propToTextProps));
            }

            if (qTag.HasTextProperty(TextProperties.scNonpublishable))
                qTag.RemoveTextProperty(TextProperties.scPublishable);
        }

        private static string DecodeTextProperties(TextProperties textProperties)
        {
            string text = "";

            foreach (var pair in propToTextProps)
            {
                if ((textProperties & pair.Value) != 0)
                    text += " " + pair.Key;
            }

            return text.Trim();
        }

        internal static ScrTag MakeEndMarker(string marker)
        {
            ScrTag qTagEndMarker = new ScrTag(marker);
            qTagEndMarker.StyleType = ScrStyleType.scEndStyle;

            return qTagEndMarker;
        }

        internal static string GetMessage(int lineNumber, string message)
        {
            return string.Format(Localizer.Str("Line {0}"), lineNumber + " - " + message);
        }
        #endregion

        private void AddTextProperty(TextProperties property)
        {
            int val = (int)TextProperties;
            val |= (int)property;
            TextProperties = (TextProperties)val;
        }

        private void RemoveTextProperty(TextProperties property)
        {
            int val = (int)TextProperties;
            val &= ~(int)property;
            TextProperties = (TextProperties)val;
        }
    }
    #endregion
}
