using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SIL.Scripture;

namespace MarkerCheck
{
    /// <summary>
    /// Parser for USFM. Sends parse information to an optional sink.
    /// The parser parses one token at a time, looking ahead as necessary
    /// for such elements as figures, links and alternate verses and chapters.
    /// 
    /// The parser first updates the UsfmParserState and then calls the 
    /// parser sink as necessary.
    /// </summary>
    public class UsfmParser
    {
        private static readonly Regex optBreakSplitter = new Regex("(//)", RegexOptions.Compiled);
        private static readonly Enum<AttributeName>[] figureAttributes = new[]{
            AttributeName.AlternateDescription, AttributeName.Source,
            AttributeName.Size, AttributeName.Location, AttributeName.Copyright, AttributeName.Reference
        };
        private readonly bool tokensPreserveWhitespace;

        /// <summary>Tokens that will be parsed</summary>
        private readonly List<UsfmToken> tokens;

        /// <summary>Stylesheet to use for parsing</summary>
        private readonly ScrStylesheet scrStylesheet;

        /// <summary>Sink to send parse events to</summary>
        private readonly UsfmParserSink sink;

        /// <summary>Current state of the USFM parser</summary>
        private UsfmParserState state;

        private UsfmParser tokenClosedParser;

        public bool InventoryMode { get; set; }

        /// <summary>Index of last token processed</summary>
        private int index = -1;

        /// <summary>
        /// Number of tokens to skip over because have been processed in advance
        /// (i.e. for figures which are three tokens, or links, or chapter/verse alternates)
        /// </summary>
        private int skip = 0;

        /// <summary>
        /// Creates a USFM parser
        /// </summary>
        /// <param name="scrStylesheet"></param>
        /// <param name="tokens">list of tokens to parse</param>
        /// <param name="state">initial state of the parser</param>
        /// <param name="sink">optional sink to send parse events to. Null for none</param>
        /// <param name="tokensPreserveWhitespace">True if the tokens were created while preserving whitespace, 
        /// false otherwise</param>
        public UsfmParser(ScrStylesheet scrStylesheet, List<UsfmToken> tokens, UsfmParserState state, UsfmParserSink sink,
            bool tokensPreserveWhitespace = false)
        {
            this.scrStylesheet = scrStylesheet;
            this.tokens = tokens;
            this.state = state;
            this.sink = sink;
            this.tokensPreserveWhitespace = tokensPreserveWhitespace;
        }

        /// <summary>
        /// Creates a USFM parser
        /// </summary>
        /// <param name="scrStylesheet"></param>
        /// <param name="tokens">list of tokens to parse</param>
        /// <param name="verseRef">initial reference for the parser</param>
        /// <param name="sink">optional sink to send parse events to. Null for none</param>
        public UsfmParser(ScrStylesheet scrStylesheet, List<UsfmToken> tokens, VerseRef verseRef, UsfmParserSink sink)
        {
            this.scrStylesheet = scrStylesheet;
            this.tokens = tokens;
            this.state = new UsfmParserState(scrStylesheet, verseRef);
            this.sink = sink;
        }

        /// <summary>
        /// Returns index of last token processed
        /// </summary>
        public int Index
        {
            get { return index; }
        }

        /// <summary>
        /// Returns last token processed
        /// </summary>
        public UsfmToken Token
        {
            get { return index >= 0 ? tokens[index] : null; }
        }

        /// <summary>
        /// Gets the current parser state. Note: Will change with each token parsed
        /// </summary>
        public UsfmParserState State
        {
            get { return state; }
        }

        /// <summary>
        /// Constructor for making a duplicate for looking ahead to find closing
        /// tokens of notes and character styles.
        /// </summary>
        UsfmParser(UsfmParser usfmParser, UsfmParserSink sink=null)
        {
            scrStylesheet = usfmParser.scrStylesheet;
            tokens = usfmParser.tokens;
            this.sink = sink;
        }

        /// <summary>
        /// Processes all tokens
        /// </summary>
        public void ProcessTokens()
        {
            while (ProcessToken()) { }
        }

        /// <summary>
        /// Processes a single token
        /// </summary>
        /// <returns>false if there were no more tokens process</returns>
        public bool ProcessToken()
        {
            // If past end
            if (index >= tokens.Count - 1)
                return false;

            // Move to next token
            index++;

            // Update verse offset with previous token (since verse offset is from start of current token)
            if (index > 0)
                State.VerseOffset += tokens[index - 1].GetLength(false, !tokensPreserveWhitespace);

            // Skip over tokens that are to be skipped, ensuring that 
            // SpecialToken state is true.
            if (skip > 0)
            {
                skip--;
                State.SpecialToken = true;
                return true;
            }
            
            // Reset special token and figure status
            State.SpecialToken = false;

            UsfmToken token = tokens[index];

            // Switch unknown types to either character or paragraph
            UsfmTokenType tokenType = token.Type;
            if (tokenType == UsfmTokenType.Unknown)
                tokenType = DetermineUnknownTokenType();

            if (sink != null && !string.IsNullOrEmpty(token.Marker))
                sink.GotMarker(State, token.Marker);

            // Close open elements
            switch (tokenType)
            {
                case UsfmTokenType.Book:
                case UsfmTokenType.Chapter:
                    CloseAll();
                    break;
                case UsfmTokenType.Paragraph:
                    // Handle special case of table rows
                    if (token.Marker == "tr")
                    {
                        // Close all but table and sidebar
                        while (State.Stack.Count > 0
                               && Peek().Type != UsfmElementTypes.Table
                               && Peek().Type != UsfmElementTypes.Sidebar)
                            CloseElement();
                        break;
                    }

                    // Handle special case of sidebars
                    if (token.Marker == "esb")
                    {
                        // Close all
                        CloseAll();
                        break;
                    }

                    // Close all but sidebar
                    while (State.Stack.Count > 0 && Peek().Type != UsfmElementTypes.Sidebar)
                        CloseElement();
                    break;
                case UsfmTokenType.Character:
                    // Handle special case of table cell
                    if (IsCell(token))
                    {
                        // Close until row
                        while (Peek().Type != UsfmElementTypes.Row)
                            CloseElement();
                        break;
                    }

                    // Handle refs
                    if (IsRef(token))
                    {
                        // Refs don't close anything
                        break;
                    }

                    // If non-nested character style, close all character styles
                    if (!token.Marker.StartsWith("+"))
                        CloseCharStyles();
                    break;
                case UsfmTokenType.Verse:
                    CloseNote();
                    break;
                case UsfmTokenType.Note:
                    CloseNote();
                    break;
                case UsfmTokenType.End:
                    // If end marker for an active note
                    if (State.Stack.Exists(e => e.Type == UsfmElementTypes.Note && (e.Marker + "*" == token.Marker)))
                    {
                        CloseNote();
                        break;
                    }

                    // If end marker for a character style on stack, close it
                    // If no matching end marker, close all character styles on top of stack
                    UsfmParserElement elem;
                    bool unmatched = true;
                    while (State.Stack.Count > 0)
                    {
                        elem = Peek();
                        if (elem.Type != UsfmElementTypes.Char)
                            break;
                        CloseElement();

                        // Determine if a + prefix is needed to close it (was nested char style)
                        bool plusPrefix = (State.Stack.Count > 0 && Peek().Type == UsfmElementTypes.Char);

                        // If is a match
                        if ((plusPrefix ? "+" : "") + elem.Marker + "*" == token.Marker)
                        {
                            unmatched = false;
                            break;
                        }
                    }

                    // Unmatched end marker
                    if (unmatched)
                        if (sink != null) sink.Unmatched(State, token.Marker);
                    break;
            }

            // Handle tokens
            switch (tokenType)
            {
                case UsfmTokenType.Book:
                    State.Stack.Add(new UsfmParserElement(UsfmElementTypes.Book, token.Marker));

                    // Code is always upper case
                    string code = token.Data[0].ToUpperInvariant();

                    // Update verse ref. Leave book alone if not empty to prevent parsing errors
                    // on books with bad id lines.
                    if (State.VerseRef.Book == "" && Canon.BookIdToNumber(code) != 0)
                        State.VerseRef.Book = code;
                    State.VerseRef.ChapterNum = 1;
                    State.VerseRef.VerseNum = 0;
                    State.VerseOffset = 0;

                    // Book start. 
                    if (sink != null) sink.StartBook(State, token.Marker, code);
                    break;
                case UsfmTokenType.Chapter:
                    // Get alternate chapter number
                    string altChapter = null;
                    string pubChapter = null;
                    if (!InventoryMode)
                    {
                        if (index < tokens.Count - 3
                            && tokens[index + 1].Marker == "ca"
                            && tokens[index + 2].Text != null
                            && tokens[index + 3].Marker == "ca*")
                        {
                            altChapter = tokens[index + 2].Text.Trim();
                            skip += 3;

                            // Skip blank space after if present
                            if (index + skip < tokens.Count - 1
                                && tokens[index + skip + 1].Text != null
                                && tokens[index + skip + 1].Text.Trim().Length == 0)
                                skip++;
                        }

                        // Get publishable chapter number
                        if (index + skip < tokens.Count - 2
                            && tokens[index + skip + 1].Marker == "cp"
                            && tokens[index + skip + 2].Text != null)
                        {
                            pubChapter = tokens[index + skip + 2].Text.Trim();
                            skip += 2;
                        }
                    }

                    // Chapter
                    State.VerseRef.Chapter = token.Data[0];
                    State.VerseRef.VerseNum = 0;
                    // Verse offset is not zeroed for chapter 1, as it is part of intro
                    if (State.VerseRef.ChapterNum != 1)
                        State.VerseOffset = 0;

                    if (sink != null) sink.Chapter(State, token.Data[0], token.Marker, altChapter, pubChapter);
                    break;
                case UsfmTokenType.Verse:
                    string pubVerse = null;
                    string altVerse = null;

                    if (!InventoryMode)
                    {
                        if (index < tokens.Count - 3
                            && tokens[index + 1].Marker == "va"
                            && tokens[index + 2].Text != null
                            && tokens[index + 3].Marker == "va*")
                        {
                            // Get alternate verse number
                            altVerse = tokens[index + 2].Text.Trim();
                            skip += 3;
                        }
                        if (index + skip < tokens.Count - 3
                            && tokens[index + skip + 1].Marker == "vp"
                            && tokens[index + skip + 2].Text != null
                            && tokens[index + skip + 3].Marker == "vp*")
                        {
                            // Get publishable verse number
                            pubVerse = tokens[index + skip + 2].Text.Trim();
                            skip += 3;
                        }
                    }
                    
                    // Verse 
                    State.VerseRef.Verse = token.Data[0];
                    State.VerseOffset = 0;

                    if (sink != null) sink.Verse(State, token.Data[0], token.Marker, altVerse, pubVerse);
                    break;
                case UsfmTokenType.Paragraph:
                    // Handle special case of table rows
                    if (token.Marker == "tr")
                    {
                        // Start table if not open
                        if (State.Stack.TrueForAll(e => e.Type != UsfmElementTypes.Table))
                        {
                            State.Stack.Add(new UsfmParserElement(UsfmElementTypes.Table, null));
                            if (sink != null) sink.StartTable(State);
                        }

                        State.Stack.Add(new UsfmParserElement(UsfmElementTypes.Row, token.Marker));

                        // Row start
                        if (sink != null) sink.StartRow(State, token.Marker);
                        break;
                    }

                    // Handle special case of sidebars
                    if (token.Marker == "esb")
                    {
                        bool isClosed = IsStudyBibleItemClosed("esb", "esbe");

                        // TODO - see FB 23934
                        // Would like to only add start sidebar if it is closed - adding unclosed marker will cause
                        // an end marker to be created in an unexpected place in the editor.
                        // if (isClosed)
                            State.Stack.Add(new UsfmParserElement(UsfmElementTypes.Sidebar, token.Marker));

                        // Look for category
                        string sidebarCategory = null;
                        if (index < tokens.Count - 3
                            && tokens[index + 1].Marker == "cat"
                            && tokens[index + 2].Text != null
                            && tokens[index + 3].Marker == "cat*")
                        {
                            // Get category
                            sidebarCategory = tokens[index + 2].Text.Trim();
                            skip += 3;
                        }

                        if (sink != null) sink.StartSidebar(State, token.Marker, sidebarCategory, isClosed);
                        break;
                    }

                    // Close sidebar if in sidebar
                    if (token.Marker == "esbe")
                    {
                        if (State.Stack.Exists(e => e.Type == UsfmElementTypes.Sidebar))
                            CloseAll();
                        else if (sink != null)
                            sink.Unmatched(State, token.Marker);
                        break;
                    }

                    State.Stack.Add(new UsfmParserElement(UsfmElementTypes.Para, token.Marker));

                    // Paragraph opening
                    if (sink != null) sink.StartPara(State, token.Marker, token.Type == UsfmTokenType.Unknown);
                    break;
                case UsfmTokenType.Character:
                    // Handle special case of table cells (treated as special character style)
                    if (IsCell(token))
                    {
                        string align = "start";
                        if (token.Marker.Length > 2 && token.Marker[2] == 'c')
                            align = "center";
                        else if (token.Marker.Length > 2 && token.Marker[2] == 'r')
                            align = "end";

                        State.Stack.Add(new UsfmParserElement(UsfmElementTypes.Cell, token.Marker));

                        if (sink != null) sink.StartCell(State, token.Marker, align);
                        break;
                    }

                    if (IsRef(token))
                    {
                        // xrefs are special tokens (they do not stand alone)
                        State.SpecialToken = true;

                        string display;
                        string target;
                        ParseDisplayAndTarget(out display, out target);

                        skip += 2;

                        if (sink != null) sink.Ref(State, token.Marker, display, target);
                        break;
                    }

                    string actualMarker;
                    bool invalidMarker = false;
                    if (token.Marker.StartsWith("+"))
                    {
                        // Only strip + if properly nested
                        actualMarker = state.CharTag != null ? token.Marker.TrimStart('+') : token.Marker;
                        invalidMarker = state.CharTag == null;
                    }
                    else
                        actualMarker = token.Marker;

                    State.Stack.Add(new UsfmParserElement(UsfmElementTypes.Char, actualMarker, tokens[index].Attributes));
                    if (sink != null)
                    {
                        bool charIsClosed = IsTokenClosed();
                        State.Stack.Last().IsClosed = charIsClosed; // save for attribute check in Text method
                        sink.StartChar(State, actualMarker, charIsClosed,
                            token.Type == UsfmTokenType.Unknown || invalidMarker, tokens[index].Attributes);
                    }
                    break;
                case UsfmTokenType.Note:
                    // Look for category
                    string noteCategory = null;
                    if (index < tokens.Count - 3
                        && tokens[index + 1].Marker == "cat"
                        && tokens[index + 2].Text != null
                        && tokens[index + 3].Marker == "cat*")
                    {
                        // Get category
                        noteCategory = tokens[index + 2].Text.Trim();
                        skip += 3;
                    }

                    State.Stack.Add(new UsfmParserElement(UsfmElementTypes.Note, token.Marker));

                    if (sink != null) sink.StartNote(State, token.Marker, token.Data[0], noteCategory, IsTokenClosed());
                    break;
                case UsfmTokenType.Text:
                    string text = token.Text;

                    // If last token before a paragraph, book or chapter, esb, esbe (both are paragraph types),
                    // or at very end, strip final space
                    // This is because USFM requires these to be on a new line, therefore adding whitespace
                    if ((index == tokens.Count - 1
                         || tokens[index + 1].Type == UsfmTokenType.Paragraph
                         || tokens[index + 1].Type == UsfmTokenType.Book
                         || tokens[index + 1].Type == UsfmTokenType.Chapter)
                        && text.Length > 0 && text[text.Length - 1] == ' ')
                    {
                        text = text.Substring(0, text.Length - 1);
                    }

                    if (sink != null)
                    {
                        // Replace ~ with nbsp
                        text = text.Replace('~', '\u00A0');

                        // Replace // with <optbreak/>
                        foreach (string str in optBreakSplitter.Split(text))
                        {
                            if (str == "//")
                                sink.OptBreak(state);
                            else
                                sink.Text(state, str);
                        }
                    }
                    break;

                case UsfmTokenType.Milestone:
                case UsfmTokenType.MilestoneEnd:
                    // currently, parse state doesn't need to be update, so just inform the sink about the milestone.
                    sink?.Milestone(state, token.Marker, token.Type == UsfmTokenType.Milestone, token.Attributes);
                    break;
            }

            return true;
        }

        private void ParseDisplayAndTarget(out string display, out string target)
        {
            display = tokens[index + 1].Text.Substring(
                0, tokens[index + 1].Text.IndexOf('|'));
            target = tokens[index + 1].Text.Substring(
                tokens[index + 1].Text.IndexOf('|') + 1);
        }

        /// <summary>
        /// Closes all open elements on stack
        /// </summary>
        public void CloseAll()
        {
            while (State.Stack.Count > 0)
                CloseElement();
        }

        /// <summary>
        /// Updates the state of this parser to be the same as the state of the specified parser.
        /// </summary>
        internal void UpdateParser(UsfmParser usfmParser)
        {
            state = usfmParser.State.Clone();
            index = usfmParser.index;
            skip = 0;
        }

        /// <summary>
        /// Determine if Study Bible item closed (ending marker before book or chapter)
        /// </summary>
        bool IsStudyBibleItemClosed(string startMarker, string endingMarker)
        {
            for (int i = index + 1; i < tokens.Count; i++)
            {
                if (tokens[i].Marker == endingMarker)
                    return true;

                if (tokens[i].Marker == startMarker
                    || tokens[i].Type == UsfmTokenType.Book
                    || tokens[i].Type == UsfmTokenType.Chapter)
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Determine type that an unknown token should be treated as
        /// </summary>
        /// <returns>character or paragraph type</returns>
        UsfmTokenType DetermineUnknownTokenType()
        {
            // Unknown inside notes are character
            if (State.Stack.Exists(e => e.Type == UsfmElementTypes.Note))
                return UsfmTokenType.Character;

            return UsfmTokenType.Paragraph;
        }

        /// <summary>
        /// Peeks at top of stack
        /// </summary>
        /// <returns></returns>
        UsfmParserElement Peek()
        {
            return State.Stack[State.Stack.Count - 1];
        }

        /// <summary>
        /// Determines if the current token is closed by a matching end marker
        /// </summary>
        /// <returns></returns>
        bool IsTokenClosed()
        {
            // Clone current parser
            if (tokenClosedParser == null)
                tokenClosedParser = new UsfmParser(this);
            tokenClosedParser.UpdateParser(this);

            bool isTokenClosed;
            string marker = tokens[index].Marker;
            LookaheadParser(State, tokenClosedParser, marker, out isTokenClosed);
            return isTokenClosed;
        }

        private static void LookaheadParser(UsfmParserState state, UsfmParser lookaheadParser, string marker, out bool isTokenClosed)
        {
            // BEWARE: This method is fairly performance-critical
            // Determine current marker
            string endMarker = marker + "*";

            // Process tokens until either the start of the stack doesn't match (it was closed
            // improperly) or a matching close marker is found
            while (lookaheadParser.ProcessToken())
            {
                UsfmToken currentToken = lookaheadParser.tokens[lookaheadParser.index];

                // Check if same marker was reopened without a close
                bool reopened = currentToken.Marker == marker &&
                    lookaheadParser.State.Stack.SequenceEqual(state.Stack);
                if (reopened)
                {
                    isTokenClosed = false;
                    return;
                }

                // Check if beginning of stack is unchanged. If token is unclosed, it will be unchanged
                bool markerStillOpen = lookaheadParser.State.Stack.Take(state.Stack.Count).SequenceEqual(state.Stack);
                if (!markerStillOpen)
                {
                    // Record whether marker is an end for this marker 
                    isTokenClosed = currentToken.Marker == endMarker && currentToken.Type == UsfmTokenType.End;
                    return;
                }
            }
            isTokenClosed = false;
        }

        /// <summary>
        /// Closes any note that is open
        /// </summary>
        private void CloseNote()
        {
            if (State.Stack.Exists(elem => elem.Type == UsfmElementTypes.Note))
            {
                UsfmParserElement elem;
                do
                {
                    if (State.Stack.Count == 0)
                        break;

                    elem = Peek();
                    CloseElement();
                } while (elem.Type != UsfmElementTypes.Note);
            }
        }

        /// <summary>
        /// Closes all open characters styles at top of stack
        /// </summary>
        private void CloseCharStyles()
        {
            while (State.Stack.Count > 0 && Peek().Type == UsfmElementTypes.Char)
                CloseElement();
        }

        /// <summary>
        /// Closes the top element on the stack
        /// </summary>
        void CloseElement()
        {
            UsfmParserElement element = State.Stack[State.Stack.Count - 1];
            State.Stack.RemoveAt(State.Stack.Count - 1);
            switch (element.Type)
            {
                case UsfmElementTypes.Book:
                    if (sink != null) sink.EndBook(State, element.Marker);
                    break;
                case UsfmElementTypes.Para:
                    if (sink != null) sink.EndPara(State, element.Marker);
                    break;
                case UsfmElementTypes.Char:
                    if (sink != null) sink.EndChar(State, element.Marker, element.Attributes);
                    break;
                case UsfmElementTypes.Note:
                    if (sink != null) sink.EndNote(State, element.Marker);
                    break;
                case UsfmElementTypes.Table:
                    if (sink != null) sink.EndTable(State);
                    break;
                case UsfmElementTypes.Row:
                    if (sink != null) sink.EndRow(State, element.Marker);
                    break;
                case UsfmElementTypes.Cell:
                    if (sink != null) sink.EndCell(State, element.Marker);
                    break;
                case UsfmElementTypes.Sidebar:
                    if (sink != null) sink.EndSidebar(State, element.Marker);
                    break;
            }
        }

        /// <summary>
        /// Determines if token is a cell of a table
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        bool IsCell(UsfmToken token)
        {
            return token.Type == UsfmTokenType.Character
                    && (token.Marker.StartsWith("th") || token.Marker.StartsWith("tc"))
                    && State.Stack.Exists(elem => elem.Type == UsfmElementTypes.Row);
        }

        /// <summary>
        /// Determines if token is a jump to a reference
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        bool IsRef(UsfmToken token)
        {
            return (index < tokens.Count - 2)
                   && (tokens[index + 1].Text != null)
                   && (tokens[index + 1].Text.Contains("|"))
                   && (tokens[index + 2].Type == UsfmTokenType.End)
                   && (tokens[index + 2].Marker == token.EndMarker)
                   && (token.Marker == "ref");
        }
    }
}