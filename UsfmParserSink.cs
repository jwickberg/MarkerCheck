namespace MarkerCheck
{
    /// <summary>
    /// Class which receives parse events from a USFM parser.
    /// Base class here ignores all events. Override necessary in subclasses.
    /// </summary>
    public class UsfmParserSink
    {
        /// <summary>
        /// Got a marker of any kind
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void GotMarker(UsfmParserState state, string marker) { } 

        /// <summary>
        /// Start of a book element
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        /// <param name="code">code e.g. GEN</param>
        public virtual void StartBook(UsfmParserState state, string marker, string code) { }

        /// <summary>
        /// End of a book element, not the end of the entire book.
        /// Book element contains the description as text
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void EndBook(UsfmParserState state, string marker) { }

        /// <summary>
        /// Chapter element
        /// </summary>
        /// <param name="state"></param>
        /// <param name="number">chapter number</param>
        /// <param name="marker"></param>
        /// <param name="altNumber">alternate chapter number or null for none</param>
        /// <param name="pubNumber">publishable chapter number or null for none</param>
        public virtual void Chapter(UsfmParserState state, string number, string marker, string altNumber, string pubNumber) { }

        /// <summary>
        /// Verse element
        /// </summary>
        /// <param name="state"></param>
        /// <param name="number">verse number</param>
        /// <param name="marker"></param>
        /// <param name="altNumber">alternate verse number or null for none</param>
        /// <param name="pubNumber">publishable verse number or null for none</param>
        public virtual void Verse(UsfmParserState state, string number, string marker, string altNumber, string pubNumber) { }

        /// <summary>
        /// Start of a paragraph
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        /// <param name="unknown">true if marker is an unknown marker</param>
        public virtual void StartPara(UsfmParserState state, string marker, bool unknown) { }

        /// <summary>
        /// End of a paragraph
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void EndPara(UsfmParserState state, string marker) { }

        /// <summary>
        /// Start of a character style
        /// </summary>
        /// <param name="state"></param>
        /// <param name="markerWithoutPlus">marker of the character style, with the plus prefix removed if present</param>
        /// <param name="closed">true if character style is closed with a close marker</param>
        /// <param name="unknown">true if marker is an unknown marker</param>
        /// <param name="namedAttributes">extra named namedAttributes</param>
        public virtual void StartChar(UsfmParserState state, string markerWithoutPlus, bool closed, bool unknown, NamedAttribute[] namedAttributes) { }

        /// <summary>
        /// End of a character style
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        /// <param name="namedAttributes"></param>
        public virtual void EndChar(UsfmParserState state, string marker, NamedAttribute[] namedAttributes) { }

        /// <summary>
        /// Start of a note
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        /// <param name="caller"></param>
        /// <param name="category">optional category for the note. Null if none</param>
        /// <param name="closed">true if note is closed</param>
        public virtual void StartNote(UsfmParserState state, string marker, string caller, string category, bool closed) { }

        /// <summary>
        /// End of a note
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void EndNote(UsfmParserState state, string marker) { }

        /// <summary>
        /// Start of a table
        /// </summary>
        /// <param name="state"></param>
        public virtual void StartTable(UsfmParserState state) { }

        /// <summary>
        /// End of a table
        /// </summary>
        /// <param name="state"></param>
        public virtual void EndTable(UsfmParserState state) { }

        /// <summary>
        /// Start of a row of a table
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void StartRow(UsfmParserState state, string marker) { }

        /// <summary>
        /// End of a row of a table
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void EndRow(UsfmParserState state, string marker) { }

        /// <summary>
        /// Start of a cell within a table row
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        /// <param name="align">"start", "center" or "end"</param>
        public virtual void StartCell(UsfmParserState state, string marker, string align) { }

        /// <summary>
        /// End of a cell within a table row
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void EndCell(UsfmParserState state, string marker) { }

        /// <summary>
        /// Text element
        /// </summary>
        /// <param name="state"></param>
        /// <param name="text"></param>
        public virtual void Text(UsfmParserState state, string text) { }

        /// <summary>
        /// Unmatched end marker
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void Unmatched(UsfmParserState state, string marker) { }

        /// <summary>
        /// Automatically extracted ref to a Scripture location
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        /// <param name="display">String to be displayed</param>
        /// <param name="target">target of ref. See USX documentation</param>
        public virtual void Ref(UsfmParserState state, string marker, string display, string target) { }

        /// <summary>
        /// Start of a study Bible sidebar
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker">"esb"</param>
        /// <param name="category">optional category or null if none</param>
        /// <param name="closed">true if sidebars closed by \esbe marker</param>
        public virtual void StartSidebar(UsfmParserState state, string marker, string category, bool closed) { }

        /// <summary>
        /// End of a study Bible sidebar
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        public virtual void EndSidebar(UsfmParserState state, string marker) { }

        /// <summary>
        /// Optional break (// in usfm)
        /// </summary>
        /// <param name="state"></param>
        public virtual void OptBreak(UsfmParserState state) { }

        /// <summary>
        /// Milestone start or end
        /// </summary>
        /// <param name="state"></param>
        /// <param name="marker"></param>
        /// <param name="startMilestone"></param>
        /// <param name="namedAttributes"></param>
        public virtual void Milestone(UsfmParserState state, string marker, bool startMilestone, NamedAttribute[] namedAttributes) { }
    }
}