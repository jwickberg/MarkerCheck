using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MarkerCheck
{
    /// <summary>
    /// Main parser for Scripture texts. Uses UsfmToken as the
    /// tokenizer, and then implements a variety of routines for getting
    /// the verse text, text tokens (for checking), etc.
    ///
    /// It can maintain a cache of UsfmTokens to speed parsing. The cache
    /// is automatically cleared with scripture changes.
    /// </summary>
    public class ScrParser
    {
        #region Member variables

        static readonly Regex nonTextMarksRegex = new Regex(@"(\s*//\s*|~)", RegexOptions.Compiled);

        private string bookText;
        private ScrStylesheet stylesheet;
        #endregion

        #region Constructor

        /// <summary>
        /// Creates a parser
        /// </summary>
        internal ScrParser(ScrStylesheet stylesheet, string text)
        {
            this.stylesheet = stylesheet;
            bookText = text;
        }

        #endregion

        #region Methods for getting (possibly cached) UsfmTokens
        /// <summary>
        /// Get specified text as a list of tokens.
        /// </summary>
        public List<UsfmToken> GetUsfmTokens(int bookNum)
        {
            List<UsfmToken> tokens;
            tokens = UsfmToken.Tokenize(stylesheet, bookText, false);
            return tokens;
        }

        #endregion
    }
}
