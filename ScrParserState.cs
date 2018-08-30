using System;
using System.Collections.Generic;
using SIL.Scripture;

namespace MarkerCheck
{
    /// <summary>
    /// This class exists to allow older code to continue to use ScrParserState
    /// without changes. Under the covers, it uses UsfmParserState and UsfmParser.
    /// </summary>
    public class ScrParserState : UsfmParserState
    {
        UsfmParser parser;
        List<UsfmToken> tokens;
        private bool preserveWhitespace;

	    /// <summary>
		/// Initializes the parser state
		/// </summary>
        /// <param name="scrStylesheet"></param>
		/// <param name="verseRef"></param>
		public ScrParserState(ScrStylesheet scrStylesheet, VerseRef verseRef)
            : base(scrStylesheet, verseRef)
	    {
        }

        /// <summary>
		/// Updates the state of the parser by processing the specified token. 
		/// State is updated based on contents of token (e.g. verse change), but position
		/// is the beginning of token, not the end.
		/// </summary>
		/// <param name="tokens">list of all tokens being processed</param>
		/// <param name="index">index of the token to be processed</param>
        /// <param name="tokensPreserveWhitespace">True if the tokens were created while preserving whitespace, 
        /// false otherwise</param>
		public void UpdateState(List<UsfmToken> tokens, int index, bool tokensPreserveWhitespace = false)
        {
            if (this.tokens != null && this.tokens != tokens)
                parser = null; // Reset parser

            this.tokens = tokens;
            this.preserveWhitespace = tokensPreserveWhitespace;

            // Create parser if not done already
            if (parser == null)
                parser = new UsfmParser(ScrStylesheet, tokens, this, new ScrParserUsfmParserSink(this), tokensPreserveWhitespace);

            // Check that token to process is next token
            if (index != parser.Index + 1)
                throw new ArgumentException("Tokens must be processed sequentially");

            // Reset paraStart and cellStart
            ParaStart = false;
            CellStart = false;

            parser.ProcessToken();
        }

        /// <summary>
        /// True if token just started a new paragraph
        /// </summary>
        public bool ParaStart { get; private set; }

        /// <summary>
        /// True if token just started a new cell
        /// </summary>
        public bool CellStart { get; private set; }

        /// <summary>
        /// True if currently in a side bar
        /// </summary>
        public bool InSideBar { get; private set; }

        public override UsfmParserState Clone()
        {
            return new ScrParserState(ScrStylesheet, VerseRef)
                {
                    Stack = new List<UsfmParserElement>(Stack),
                    VerseOffset = VerseOffset,
                    ParaStart = ParaStart
                };
        }

        /// <summary>
        /// Sink to catch paragraph starts
        /// </summary>
        class ScrParserUsfmParserSink : UsfmParserSink
        {
            readonly ScrParserState state;

            public ScrParserUsfmParserSink(ScrParserState state)
            {
                this.state = state;
            }

            public override void StartPara(UsfmParserState s, string marker, bool unknown)
            {
                state.ParaStart = true;
            }

            public override void StartCell(UsfmParserState s, string marker, string align)
            {
                state.CellStart = true;
            }

            public override void StartSidebar(UsfmParserState state, string marker, string category, bool closed)
            {
                this.state.InSideBar = true;
            }

            public override void EndSidebar(UsfmParserState state, string marker)
            {
                this.state.InSideBar = false;
            }
        }
    }
}
