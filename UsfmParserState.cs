using System.Collections.Generic;
using System.Linq;
using SIL.Scripture;

namespace MarkerCheck
{
    /// <summary>
	/// Class for maintaining the state when parsing scripture.
	/// Maintains the current verse reference, paragraph, character and note styles.
	/// Note that book of verse reference is not updated unless blank
	/// </summary>
    public class UsfmParserState
    {
        /// <summary>
        /// Scripture stylesheet
        /// </summary>
        public readonly ScrStylesheet ScrStylesheet;

        /// <summary>
        /// Stack of elements that are open
        /// </summary>
        public List<UsfmParserElement> Stack;

        /// <summary>
        /// Current verse reference
        /// </summary>
        public VerseRef VerseRef; 

        /// <summary>
        /// Offset of start of token in verse
        /// </summary>
        public int VerseOffset;

        /// <summary>
        /// True if the token processed is part of a special indivisible group 
        /// of tokens (link or chapter/verse alternate/publishable)
        /// </summary>
        public bool SpecialToken;

        /// <summary>
        /// True if the token processed is a figure.
        /// </summary>
        public bool IsFigure => CharTag?.Marker == "fig";

        public UsfmParserState(ScrStylesheet scrStylesheet)
        {
            ScrStylesheet = scrStylesheet;
            Stack = new List<UsfmParserElement>();
            VerseRef = new VerseRef();
            VerseOffset = 0;
        }

        public UsfmParserState(ScrStylesheet scrStylesheet, VerseRef verseRef)
        {
            ScrStylesheet = scrStylesheet;
            Stack = new List<UsfmParserElement>();
            VerseRef = verseRef.Clone();
            VerseOffset = 0;
        }

        /// <summary>
        /// Current paragraph tag or null for none.
        /// Note that book and table rows are considered paragraphs for legacy checking reasons.
        /// </summary>
        public ScrTag ParaTag
        {
            get
            {
                UsfmParserElement elem = Stack.FindLast(e => 
                    e.Type == UsfmElementTypes.Para || 
                    e.Type == UsfmElementTypes.Book || 
                    e.Type == UsfmElementTypes.Row ||
                    e.Type == UsfmElementTypes.Sidebar);
                if (elem != null)
                    return ScrStylesheet.GetTag(elem.Marker);
                return null;
            }
        }

        /// <summary>
        /// Innermost character tag or null for none
        /// </summary>
        public ScrTag CharTag
        {
            get { return CharTags.FirstOrDefault(); }
        }

        /// <summary>
        /// Current note tag or null for none
        /// </summary>
        public ScrTag NoteTag
        {
            get
            {
                UsfmParserElement elem = Stack.LastOrDefault(e => e.Type == UsfmElementTypes.Note);
                return (elem != null) ? ScrStylesheet.GetTag(elem.Marker) : null;
            }
        }

        /// <summary>
        /// Character tags, starting with innermost
        /// </summary>
        public IEnumerable<ScrTag> CharTags
        {
            get
            {
                for (int i = Stack.Count - 1; i >= 0; i--)
                {
                    if (Stack[i].Type == UsfmElementTypes.Char)
                        yield return ScrStylesheet.GetTag(Stack[i].Marker);
                    else
                        break;
                }
            }
        }

        /// <summary>
		/// Determines if text tokens in the current state are verse text
		/// </summary>
        public bool IsVerseText
        {
            get
            {
                // Sidebars and notes are not verse text 
                if (Stack.Exists(e => e.Type == UsfmElementTypes.Sidebar || e.Type == UsfmElementTypes.Note))
                    return false;

                // If the user enters no markers except just \c and \v we want the text to be
                // considered verse text. This is covered by the empty stack that makes ParaTag null.
                ScrTag paraTag = ParaTag;
                if (paraTag == null)
                    return true;

                // Not specified text type is verse text
                if ((paraTag.TextType != ScrTextType.scVerseText) && (paraTag.TextType != 0))
                    return false;

                // All character tags must be verse text
                foreach (ScrTag charTag in CharTags)
                {
                    // Not specified text type is verse text
                    if (charTag.TextType != ScrTextType.scVerseText && charTag.TextType != 0)
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Determines if text tokens in the current state are publishable
        /// </summary>
        public bool IsPublishable
        {
            get
            {
                // Special tokens not publishable
                if (SpecialToken)
                    return false;

                // Non-paragraphs or unknown paragraphs are publishable
                if (ParaTag != null)
                {
                    if ((ParaTag.TextProperties & TextProperties.scNonpublishable) > 0)
                        return false;
                }

                if (CharTags.Any(charTag => (charTag.TextProperties & TextProperties.scNonpublishable) > 0))
                    return false;
                return !IsSpecialText;
            }
        }

        /// <summary>
        /// Determines if text tokens in the current state are publishable vernacular
        /// </summary>
        public bool IsPublishableVernacular
        {
            get
            {
                // Non-paragraphs or unknown paragraphs are publishable
                if (ParaTag != null)
                {
                    if ((ParaTag.TextProperties & TextProperties.scNonpublishable) > 0)
                        return false;
                    if ((ParaTag.TextProperties & TextProperties.scNonvernacular) > 0)
                        return false;
                }
                if (CharTag != null)
                {
                    if ((CharTag.TextProperties & TextProperties.scNonpublishable) > 0)
                        return false;
                    if ((CharTag.TextProperties & TextProperties.scNonvernacular) > 0)
                        return false;
                }
                return !IsSpecialText;
            }
        }

        /// <summary>
        /// Determines if text is special text like links and figures that are not in the 
        /// vernacular.
        /// </summary>
        public bool IsSpecialText
        {
            get { return SpecialToken; }
        }

        public virtual UsfmParserState Clone()
        {
            return new UsfmParserState(ScrStylesheet, VerseRef)
                {
                    Stack = new List<UsfmParserElement>(Stack),
                    VerseOffset = VerseOffset
                };
        }
    }

    /// <summary>
    /// Element types on the stack
    /// </summary>
    public enum UsfmElementTypes
    {
        Book,
        Para,
        Char,
        Table,
        Row,
        Cell,
        Note,
        Sidebar
    } ;

    /// <summary>
    /// Element that can be on the parser stack
    /// </summary>
    public sealed class UsfmParserElement
    {
        public readonly UsfmElementTypes Type;
        public readonly string Marker;
        public NamedAttribute[] Attributes;
        public bool IsClosed;

        public UsfmParserElement(UsfmElementTypes type, string marker, NamedAttribute[] attributes = null)
        {
            Type = type;
            Marker = marker;
            Attributes = attributes;
        }

        public override bool Equals(object obj)
        {
            UsfmParserElement elm = obj as UsfmParserElement;
            return elm != null && elm.Type == Type && elm.Marker == Marker;
        }

        public override int GetHashCode()
        {
            return Marker.GetHashCode();
        }
    }
}
