using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using SIL.Scripture;

namespace MarkerCheck
{
    /// <summary>
    /// Performs checks on all markers, checking for invalid or unknown ones.
    /// </summary>
    public class MarkerCheck
    {
        #region Member variables
        private readonly string missingSpaceBetweenMarkers =
            Localizer.Str("Missing spaces before markers. Click 'Tools > Advanced > Standardize Whitespace' to add spaces.");
        private readonly string missingIdMarker = Localizer.Str("Missing \\id marker");
        private bool markerErrors;
        internal bool allowVersion3Usfm;
        #endregion

        public MarkerCheck(bool alllowVersion3Usfm)
        {
            this.allowVersion3Usfm = alllowVersion3Usfm;
        }

        public bool Run(int bookNum, string text)
        {
            CheckForNoSpaceBetweenMarkers(bookNum, text);
            ScrStylesheet scrStylesheet = new ScrStylesheet("usfm.sty");
            List<UsfmToken> usfmTokens = new ScrParser(scrStylesheet, text).GetUsfmTokens(bookNum);
            return CheckInternal(usfmTokens, bookNum, scrStylesheet);
        }

        #region Private helper methods
        /// <summary>
        /// Split the book text into individual chapters.
        /// Chapter i is at list position i-1.
        /// Introductory material is included with chapter 1.
        /// Throws a localized ChapterizationException if:
        /// * missing chapter number after \c
        /// * \c not followed by a number.
        /// * a chapter occurs more than once.
        /// </summary>
        /// <returns>List of chapters. Item 0 is chapter 1.</returns>
        private static List<string> SplitIntoChapters(int bookNum, string bookText)
        {
            List<string> chapters = new List<string>();
            if (bookText == null)
                return chapters;

            Regex chapterSplitRegex = new Regex(@"\\c\s+", RegexOptions.Compiled);
            string[] foundChapters = chapterSplitRegex.Split(bookText);

            if (foundChapters.Length == 1)
            {
                chapters.Add(bookText);
                return chapters;
            }

            List<ChapterNumber> chapterNumbers = new List<ChapterNumber>();
            for (int i = 1; i < foundChapters.Length; i++)
            {
                string text = foundChapters[i];
                ChapterNumber chapterNum = new ChapterNumber(text);
                chapterNumbers.Add(chapterNum);

                if (chapterNum.ChapterValue == 0)
                    continue;

                // If there is not \c 1 marker then everying before the first chapter
                // marker present intro material and is considered part of chapter 1
                if (i == 1 && chapterNum.ChapterValue != 1)
                    chapters.Add(foundChapters[0]);

                if (chapterNum.ChapterValue == 1)
                    text = foundChapters[0] + "\\c " + text;
                else
                    text = "\\c " + text;

                while (chapters.Count < chapterNum.ChapterValue)
                    chapters.Add("");

                if (chapters[chapterNum.ChapterValue - 1] != "")
                {
                    chapterNum.ChapterValue = 0; // duplicate chapter
                    continue;
                }

                chapters[chapterNum.ChapterValue - 1] = text;
            }

            // check order of chapters
            for (int i = 0; i < chapterNumbers.Count; i++)
            {
                int nextChapter = i < chapterNumbers.Count - 1 ? chapterNumbers[i + 1].ChapterValue : 0;
                if (nextChapter != 0 && chapterNumbers[i].ChapterValue > nextChapter)
                    chapterNumbers[i].ChapterValue = 0;
            }

            if (chapterNumbers.Exists(ch => ch.ChapterValue == 0))
            {
                throw new ArgumentException(string.Format(Localizer.Str("The chapter number(s) in parentheses are invalid, out of order, or duplicated:\n{0}"),
                    string.Join(" ", chapterNumbers.Select(ch => ch.ToString()))));
            }

            return chapters;
        }

        private void CheckForNoSpaceBetweenMarkers(int bookNum, string bookText)
        {
            var chapters = SplitIntoChapters(bookNum, bookText);
            for (int chap = 0; chap < chapters.Count; chap++)
            {
                if (!string.IsNullOrEmpty(chapters[chap]) && Regex.Match(chapters[chap], @"\\\w+\\").Success)
                {
                    var vref = new VerseRef(bookNum, chap + 1, 0, ScrVers.English);
                    RecordError(vref, "", 0, "#" + missingSpaceBetweenMarkers);
                }
            }
        }

        private bool CheckInternal(List<UsfmToken> tokens, int bookNum, ScrStylesheet scrStylesheet)
        {
            VerseRef startVerse = new VerseRef(bookNum, 1, 0, ScrVers.English);
            if (tokens.Count > 0 && tokens[0].Marker != "id")
                RecordError(startVerse, "", 0, "#" + missingIdMarker);

            MarkerCheckSink markerCheckSink = new MarkerCheckSink(scrStylesheet, startVerse.Book, this);
            UsfmParser parser = new UsfmParser(scrStylesheet, tokens, startVerse, markerCheckSink);
            parser.ProcessTokens();
            markerCheckSink.ReportPendingVerseNoParaError();
            markerCheckSink.ReportOpenMilestoneErrors();
            return markerErrors || markerCheckSink.MarkerErrors;
        }

        private void RecordError(VerseRef vref, string text, int offset, string msg)
        {
            Console.WriteLine("MarkerCheck: {0} Offset: {1} Text: {2} Message: {3}", vref, offset, text, msg);
            markerErrors = true;
        }
        #endregion

        #region MarkerCheckSink class
        /// <summary>
        /// Marker check that gets events from parser to perform check
        /// </summary>
        class MarkerCheckSink : UsfmParserSink
        {
            #region Member variables
            private readonly ScrStylesheet scrStylesheet;
            private string prevCharMarker;
            private bool prevMarkerWasChapter;
            private bool stylesheetIsDefault;
            private bool emptyPara, emptyChar;
            private KeyValuePair<MarkerLevel, StringBuilder> innerTextBuilder;
            private readonly bool isRightToLeft;
            private UsfmParserState previousTextState;
            private string previousText;
            private VerseRef pendingVerseNoParaError;
            private int pendingVerseNoParaErrorOffset;
            private string pendingVerseNoParaErrorMarker;
            private int lastCharMarkerOffset;
            private VerseRef lastCharMarkerVerse;
            private int nextTableCell;

            private static readonly List<string> okToBeEmptyMarkers =
                new List<string> { "b", "ib", "ie", "pb", "tc", "xt" };
            private static readonly List<string> linkAttributes =
                new List<string> { AttributeName.LinkReference.InternalValue, AttributeName.LinkTitle.InternalValue, AttributeName.LinkName.InternalValue };
            private static readonly string[] figureAttributes = new string[] {AttributeName.AlternateDescription.InternalValue,
                AttributeName.Source.InternalValue, AttributeName.Size.InternalValue, AttributeName.Copyright.InternalValue,
                AttributeName.Location.InternalValue, AttributeName.Reference.InternalValue };

            private readonly string emptyMarkerMessage = Localizer.Str("Empty marker: {0}");
            private readonly string notHereMessage = Localizer.Str("Marker cannot occur here: {0}");
            private readonly string unknownMarkerMessage = Localizer.Str("Unknown marker: {0}");
            private readonly string charStyleNotClosedMessage = Localizer.Str("Character style not closed: {0}");
            private readonly string charNoParaMessage = Localizer.Str("Character marker without a paragraph marker: {0}");
            private readonly string verseNoParaMessage = Localizer.Str("Verse marker without a paragraph marker");
            private readonly string endNoStartMessage = Localizer.Str("End marker does not have matching start marker: {0}");
            private readonly string noteNotClosedMessage = Localizer.Str("Note not closed: {0}");
            private readonly string sidebarNotClosedMessage = Localizer.Str("Study Bible sidebar not closed: {0}");
            private readonly string missingCallerMessage = Localizer.Str("Missing footnote or cross reference caller");
            private readonly string wrongCallerMessage = Localizer.Str("Note caller is not the expected value ({0})");
            private readonly string noOriginMessage = Localizer.Str("Most notes of this type have an origin reference");
            private readonly string extraOriginMessage = Localizer.Str("Most notes of this type do not have an origin reference");
            private readonly string noteNoParaMessage = Localizer.Str("Note marker without a paragraph marker");
            private readonly string missingTableMarker = Localizer.Str("Missing marker: {0}");
            private readonly string repeatedCharMarker = Localizer.Str("Same character style is closed and reopened: {0}");
            private readonly string invalidAttribute = Localizer.Str("Invalid attribute: {0}");
            private readonly string missingMilestoneEnd = Localizer.Str("Missing milestone end:");
            private readonly string unsupportedMarkerMessage = Localizer.Str("Marker not supported for USFM 2.0 projects: {0}");
            private readonly string unsupportedAttributesMessage = Localizer.Str("Marker has attributes not supported for USFM 2.0 projects: {0}");
            private const string markerSlot = ": {0}";


            // Validate paragraphs
            private readonly List<ScrTag> paraStack = new List<ScrTag>();

            // Many character styles can be repeated, so this is the only ones we warn on.
            private readonly string[] repeatedCharMarkersWarnings = { "qt", "wj", "no", "it", "bd", "bdit", "em", "sc", "add" };
            private readonly MarkerCheck markerCheck;
            private Dictionary<string, string> endMilestoneMarkerMap = new Dictionary<string, string>();
            private Dictionary<string, Tuple<VerseRef, int, string>> openMilestones = new Dictionary<string, Tuple<VerseRef, int, string>>();
            #endregion

            public MarkerCheckSink(ScrStylesheet scrStylesheet, string currentBookId, MarkerCheck markerCheck)
            {
                this.scrStylesheet = scrStylesheet;
                isRightToLeft = false;
                stylesheetIsDefault = true;
                this.markerCheck = markerCheck;
            }

            public bool MarkerErrors;

            private sealed class MarkerLevel
            {
                /// <summary>
                /// Stack of elements that are open
                /// </summary>
                private readonly UsfmParserElement element;
                private readonly int stackLevel;

                internal MarkerLevel(UsfmParserState state)
                {
                    element = state.Stack.Last();
                    stackLevel = state.Stack.Count();
                }

                public override bool Equals(object obj)
                {
                    var ms = obj as MarkerLevel;
                    return ms != null && ms.stackLevel == stackLevel && ms.element.Equals(element);
                }

                public override int GetHashCode()
                {
                    return stackLevel.GetHashCode() + element.GetHashCode();
                }
            }

            #region Public overrides of UsfmParserSink
            public override void StartPara(UsfmParserState state, string marker, bool unknown)
            {
                prevCharMarker = null;

                if (unknown)
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                        GetErrorMessage(unknownMarkerMessage, marker));
                }

                emptyPara = true;

                ValidateParagraphTypeTag(state, marker);
            }

            public override void StartBook(UsfmParserState state, string marker, string code)
            {
                ValidateParagraphTypeTag(state, marker);
            }

            public override void Chapter(UsfmParserState state, string number, string marker, string altNumber, string pubNumber)
            {
                prevCharMarker = null;
                ValidateParagraphTypeTag(state, marker);
                prevMarkerWasChapter = true;
            }

            public override void StartRow(UsfmParserState state, string marker)
            {
                ValidateParagraphTypeTag(state, marker);
                nextTableCell = 1;
            }

            public override void StartSidebar(UsfmParserState state, string marker, string category, bool closed)
            {
                ValidateParagraphTypeTag(state, marker);
                if (!closed)
                {
                    recordError(state.VerseRef, "\\" + marker, state.VerseOffset,
                        GetErrorMessage(sidebarNotClosedMessage, marker));
                }
            }

            public override void StartChar(UsfmParserState state, string markerWithoutPlus, bool closed, bool unknown, params NamedAttribute[] namedAttributes)
            {
                if (markerWithoutPlus == "w" || markerWithoutPlus == "rb")
                {
                    var markerLevel = new MarkerLevel(state);
                    innerTextBuilder = new KeyValuePair<MarkerLevel, StringBuilder>(markerLevel, new StringBuilder());
                }
                if (markerWithoutPlus == "k" && state.VerseRef.Book == "GLO")
                {
                    var markerLevel = new MarkerLevel(state);
                    innerTextBuilder = new KeyValuePair<MarkerLevel, StringBuilder>(markerLevel, new StringBuilder());
                }

                lastCharMarkerOffset = state.VerseOffset;
                lastCharMarkerVerse = state.VerseRef.Clone();

                if (unknown)
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + markerWithoutPlus, state.VerseOffset,
                        GetErrorMessage(unknownMarkerMessage, markerWithoutPlus));
                }
                else if (markerWithoutPlus == "rb" && !markerCheck.allowVersion3Usfm)
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + markerWithoutPlus, state.VerseOffset,
                        GetErrorMessage(unsupportedMarkerMessage, markerWithoutPlus));
                }

                if (markerWithoutPlus == prevCharMarker && repeatedCharMarkersWarnings.Contains(markerWithoutPlus))
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + markerWithoutPlus, state.VerseOffset,
                        GetErrorMessage(repeatedCharMarker, markerWithoutPlus));
                }

                if (!closed && MarkerRequiresClose(markerWithoutPlus, state.CharTag))
                {
                    recordError(state.VerseRef, "\\" + markerWithoutPlus, state.VerseOffset,
                        GetErrorMessage(charStyleNotClosedMessage, markerWithoutPlus));
                }

                if (state.ParaTag == null)
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + markerWithoutPlus, state.VerseOffset,
                        GetErrorMessage(charNoParaMessage, markerWithoutPlus));
                }

                if (state.CharTag != null && closed)
                    ValidateAttributes(state,state.CharTag, markerWithoutPlus, namedAttributes ?? new NamedAttribute[0]);

                ValidateCharacterTypeTag(state, markerWithoutPlus);
                emptyChar = true;
            }

            public override void StartCell(UsfmParserState state, string marker, string align)
            {
                ValidateCharacterTypeTag(state, marker);

                int cellNumber = marker[marker.Length - 1] - '0';
                if (cellNumber != nextTableCell)
                {
                    string expectedMarker = string.Format("\\t{0}{1}", marker[1], nextTableCell);
                    recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                        GetErrorMessage(string.Format(missingTableMarker, expectedMarker), marker));
                }
                nextTableCell = cellNumber + 1;
            }

            public override void EndChar(UsfmParserState state, string marker, NamedAttribute[] attributes)
            {
                if (emptyChar && !okToBeEmptyMarkers.Contains(marker))
                {
                    recordError(state.VerseRef, "\\" + marker, state.VerseOffset,
                        GetErrorMessage(emptyMarkerMessage, marker));
                }
                if (marker == "w")
                    CheckWordlistErrors(state, marker, attributes);
                if (marker == "k")
                    CheckGlossaryCitationFormErrors(state, marker);
                if (marker == "rb")
                    CheckRubyGlossing(state, marker, attributes);
                prevMarkerWasChapter = false;
                prevCharMarker = marker;
            }

            public override void EndPara(UsfmParserState state, string marker)
            {
                if (emptyPara && !okToBeEmptyMarkers.Contains(marker))
                {
                    recordError(state.VerseRef, "\\" + marker, state.VerseOffset,
                        GetErrorMessage(emptyMarkerMessage, marker));
                }
            }

            public override void Verse(UsfmParserState state, string number, string marker, string altNumber, string pubNumber)
            {
                prevCharMarker = null;

                if (state.ParaTag == null)
                {
                    if (!pendingVerseNoParaError.IsDefault)
                        pendingVerseNoParaError.Verse = pendingVerseNoParaError.VerseNum + "-" + state.VerseRef.VerseNum;
                    else
                    {
                        pendingVerseNoParaError = new VerseRef(state.VerseRef);
                        pendingVerseNoParaErrorOffset = state.VerseOffset;
                        pendingVerseNoParaErrorMarker = "\\" + marker;
                    }
                }
                else
                {
                    if (state.CharTag != null)
                    {
                        recordError(lastCharMarkerVerse, "\\" + state.CharTag.Marker, lastCharMarkerOffset,
                            GetErrorMessage(charStyleNotClosedMessage, state.CharTag.Marker));
                    }

                    ValidateOccursUnder(state, marker, state.ParaTag.Marker, false);
                }

                prevMarkerWasChapter = false;
            }

            public override void Unmatched(UsfmParserState state, string marker)
            {
                recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                    GetErrorMessage(endNoStartMessage, marker));
                if (marker == "w")
                    CheckWordlistErrors(state, marker, new NamedAttribute[0]);
            }

            public override void Text(UsfmParserState state, string text)
            {
                if (text.Trim().Length > 0)
                {
                    HandleWordlistOrGlossaryCitationFormInnerText(state, text);
                    emptyPara = false;
                    emptyChar = false;
                    prevCharMarker = null;
                }
                previousTextState = state.Clone();
                previousText = text;

                if (state.NoteTag != null && state.CharTag != null && (state.CharTag.Marker == "fr" || state.CharTag.Marker == "xo"))
                {
                    // removed origin consistency check in this stand alone version
                }

                int attrStart = text.IndexOf('|');
                if (attrStart >= 0 && state.CharTag != null && state.Stack.Last().IsClosed)
                    RecordMarkerError(state, state.CharTag.Marker, invalidAttribute);
            }

            public override void StartNote(UsfmParserState state, string marker, string caller, string category, bool closed)
            {
                prevCharMarker = null;
                // FB-49128 changed check to allow character style to have an embedded cross reference
                if (state.NoteTag.Marker != "x" &&  state.Stack.Count >= 2 && state.Stack[state.Stack.Count - 2].Type == UsfmElementTypes.Char)
                {
                    string charMarker = state.Stack[state.Stack.Count - 2].Marker;
                    recordError(state.VerseRef, "\\" + charMarker, lastCharMarkerOffset,
                        GetErrorMessage(charStyleNotClosedMessage, charMarker));
                }

                if (!closed)
                {
                    recordError(state.VerseRef, "\\" + marker, state.VerseOffset,
                        GetErrorMessage(noteNotClosedMessage, marker));
                }

                if (caller.Length == 0)
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset, "#" + missingCallerMessage);
                }
                else
                {
                    // removed caller consistency check from this stand alone version
                }


                if (state.ParaTag == null)
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                        "#" + noteNoParaMessage);
                }

                prevMarkerWasChapter = false;
            }

            public override void EndNote(UsfmParserState state, string marker)
            {
                // removed origin consistent check from this stand alone version
            }

            public override void Milestone(UsfmParserState state, string marker, bool startMilestone, NamedAttribute[] namedAttributes)
            {
                if (!markerCheck.allowVersion3Usfm)
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                        GetErrorMessage(unsupportedMarkerMessage, marker));
                }

                Tuple<VerseRef, int, string> tuple;
                if (startMilestone)
                {
                    if (openMilestones.TryGetValue(marker, out tuple))
                        recordError(tuple.Item1, marker, tuple.Item2, "#" + missingMilestoneEnd + " \\" + marker);

                    openMilestones[marker] = new Tuple<VerseRef, int, string>(state.VerseRef.Clone(), state.VerseOffset, UsfmToken.GetAttribute(namedAttributes, AttributeName.Id));
                }
                else
                {
                    if (endMilestoneMarkerMap.Count == 0)
                        foreach (var tag in scrStylesheet.Tags.Where(t => t.StyleType == ScrStyleType.scMilestone))
                            endMilestoneMarkerMap[tag.Endmarker] = tag.Marker;

                    string startMarker = endMilestoneMarkerMap[marker];
                    if (openMilestones.TryGetValue(startMarker, out tuple))
                    {
                        if (tuple.Item3 != UsfmToken.GetAttribute(namedAttributes, AttributeName.Id))
                            recordError(tuple.Item1, marker, tuple.Item2, "#" + Localizer.Str("Id on start/end milestones do not match:") + " \\" + startMarker);
                        openMilestones.Remove(startMarker);
                    }
                    else
                    {
                        recordError(state.VerseRef, marker, state.VerseOffset, "#" + Localizer.Str("End milestone has no matching start:") + " \\" + marker);
                    }
                }

                ValidateAttributes(state, scrStylesheet.GetTag(marker), marker, namedAttributes ?? new NamedAttribute[0]);
            }
            #endregion

            #region Private helper methods

            private void recordError(VerseRef verse, string marker, int offset, string msg)
            {
                Console.WriteLine("MarkerCheck: {0} Offset: {1} Marker: {2} Message: {3}", verse, offset, marker, msg);
                MarkerErrors = true;
            }

            private void ValidateAttributes(UsfmParserState state, ScrTag tag, string marker,
                NamedAttribute[] namedAttributes)
            {
                if (!markerCheck.allowVersion3Usfm)
                {
                    if (marker == "fig")
                    {
                        if (namedAttributes.Any(a => !figureAttributes.Contains(a.Name.InternalValue)))
                        {
                            recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                                GetErrorMessage(unsupportedAttributesMessage, marker));
                        }
                    }
                    else if (tag.StyleType == ScrStyleType.scCharacterStyle && HasNonDefaultAttributes(tag, namedAttributes))
                    {
                        recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                            GetErrorMessage(unsupportedAttributesMessage, marker));
                    }
                }

                // for character styles, find missing required attributes
                string[] missingAttributes =
                    tag.Attributes.Where(a => a.IsRequired && namedAttributes.All(na => na.Name != a.Name))
                        .Select(a => a.Name.InternalValue).ToArray();
                if (missingAttributes.Length > 0)
                {
                    string errMsg = Localizer.Str(@"Missing required attributes ({0})");
                    errMsg = string.Format(errMsg, string.Join(", ", missingAttributes));
                    RecordMarkerError(state, marker, errMsg + markerSlot);
                }

                // find attributes that don't start with x- and aren't defined for the character style
                // also, link attributes of link-href, link-title and link-name are valid on any style
                // for figures, the standard attributes are already stripped out, so can skip the check for attributes on the CharTag (which will be null)
                string[] unknownAttributes =
                    namedAttributes.Where(
                            na => !na.Name.InternalValue.StartsWith("x-", StringComparison.OrdinalIgnoreCase))
                        .Where(na => !linkAttributes.Contains(na.Name.InternalValue))
                        .Where(na => tag.Attributes.All(a => a.Name != na.Name))
                        .Select(na => na.Name.InternalValue).ToArray();
                if (unknownAttributes.Length > 0)
                {
                    string errMsg = Localizer.Str(@"Unknown attributes ({0})");
                    errMsg = string.Format(errMsg, string.Join(", ", unknownAttributes));
                    RecordMarkerError(state, marker, errMsg + markerSlot);
                }
            }

            private bool HasNonDefaultAttributes(ScrTag tag, NamedAttribute[] namedAttributes)
            {
                if (namedAttributes.Length == 0)
                    return false;

                if (namedAttributes.Length > 1)
                    return true;

                return namedAttributes[0].Name != tag.DefaultAttribute;
            }

            private void CheckWordlistErrors(UsfmParserState state, string marker, NamedAttribute[] attributes)
            {
                // FB 47540 - an \w inside antoher \w can cause this to be null.
                if (innerTextBuilder.Value == null)
                    return;

                // code checking glossary was removed for this stand alone checking tool

                innerTextBuilder = new KeyValuePair<MarkerLevel, StringBuilder>();
            }

            private void CheckGlossaryCitationFormErrors(UsfmParserState state, string marker)
            {
                // removed glossary checking from this stand alone marker check
                innerTextBuilder = new KeyValuePair<MarkerLevel, StringBuilder>();
            }

            private void CheckRubyGlossing(UsfmParserState state, string marker, NamedAttribute[] attributes)
            {
                var baseText = innerTextBuilder.Value?.ToString();
                if (string.IsNullOrEmpty(baseText))
                    return;
                string[] baseSequences = CharacterSequences(baseText).ToArray();
                var glossText = attributes?.FirstOrDefault(a => a.Name == AttributeName.Gloss)?.Value;
                // empty gloss text will result in a missing gloss attribute error, so just returning rather than creating 2 errors
                if (string.IsNullOrEmpty(glossText))
                    return;
                string[] glosses = UsfmToken.ParseRubyGlosses(glossText, false);
                if (baseSequences.Length > glosses.Length && glosses.Length != 1)
                    RecordMarkerError(state, marker,
                        Localizer.Str(@"Fewer ruby glosses than base text characters") + markerSlot);
                else if (baseSequences.Length < glosses.Length)
                    RecordMarkerError(state, marker,
                        Localizer.Str(@"More ruby glosses than base text characters") + markerSlot);
            }

            private void HandleWordlistOrGlossaryCitationFormInnerText(UsfmParserState state, string text)
            {
                if (innerTextBuilder.Key != null)
                    innerTextBuilder.Value.Append(text);
            }


            private void RecordMarkerError(UsfmParserState state, string marker, string message)
            {
                recordError(state.VerseRef, "\\" + marker, state.VerseOffset,
                    GetErrorMessage(message, marker));
            }

            private string GetErrorMessage(string message, string markerText)
            {
                if (!stylesheetIsDefault)
                    message += " (" + scrStylesheet.Name + ")";
                return "#" + string.Format(message,
                    (isRightToLeft ? StringUtils.rtlMarker.ToString() : "") + "\\" + markerText);
            }

            private void ValidateParagraphTypeTag(UsfmParserState state, string marker)
            {
                ReportPendingVerseNoParaError();
                if (marker == "id" && paraStack.Count != 0)
                {
                    // \id marker can only appear at the beginning of the book
                    UsfmParserState idMarkerState = previousTextState ?? state;
                    string text = previousText ?? "";
                    int offset = idMarkerState.VerseOffset + text.Length;
                    // we can't actully select the \id marker because it changes the verse reference and is
                    // not even considered as a match)
                    recordError(new VerseRef(idMarkerState.VerseRef), "", offset,
                        GetErrorMessage(notHereMessage, marker));
                }
                else if (marker == "nb" && !prevMarkerWasChapter)
                {
                    // \nb marker can only appear immediately following a chapter marker
                    recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                        GetErrorMessage(notHereMessage, marker));
                }

                // Validate paragraph
                if (!IsParagraphTagValid(paraStack, scrStylesheet.GetTag(marker), true))
                {
                    recordError(new VerseRef(state.VerseRef), "\\" + marker, state.VerseOffset,
                        GetErrorMessage(notHereMessage, marker));
                }
                prevMarkerWasChapter = false;
            }

            private void ValidateCharacterTypeTag(UsfmParserState state, string marker)
            {
                prevMarkerWasChapter = false;
                if (!Canon.IsCanonical(state.VerseRef.BookNum)) // only validate order/occursUnder in Canonical
                    return;

                // Determine if nested styles are used correctly within a cross reference quote
                bool skipCharStyle = false;
                int index = state.Stack.Count - 2;
                UsfmParserElement elem = index >= 0 ? state.Stack[index] : null;
                while (elem != null && elem.Type != UsfmElementTypes.Note)
                {
                    if (elem.Marker == "xq")
                    {
                        skipCharStyle = true;
                        break;
                    }
                    index--;
                    elem = index >= 0 ? state.Stack[index] : null;
                }

                if (skipCharStyle)
                    return; // allow any embedded character styles within cross reference quote


                string contextMarker = null;
                if (state.NoteTag != null)
                    contextMarker = state.NoteTag.Marker;
                else if (state.ParaTag != null)
                    contextMarker = state.ParaTag.Marker;

                ValidateOccursUnder(state, marker, contextMarker, true);
            }

            private void ValidateOccursUnder(UsfmParserState state, string marker, string contextMarker, bool includeMarkerInError)
            {
                ScrTag tag = scrStylesheet.GetTag(marker);
                var occursUnder = tag.OccursUnderList;
                if (occursUnder.Count > 0 && (contextMarker == null || !occursUnder.Contains(contextMarker)))
                {
                    recordError(new VerseRef(state.VerseRef), includeMarkerInError ? "\\" + marker : "", state.VerseOffset,
                        GetErrorMessage(notHereMessage, marker));
                }
            }

            internal void ReportPendingVerseNoParaError()
            {
                if (!pendingVerseNoParaError.IsDefault)
                {
                    recordError(pendingVerseNoParaError, pendingVerseNoParaErrorMarker, pendingVerseNoParaErrorOffset,
                                 "#" + verseNoParaMessage);
                    pendingVerseNoParaError = new VerseRef();
                }
            }

            internal void ReportOpenMilestoneErrors()
            {
                foreach (var kvp in openMilestones)
                {
                    recordError(kvp.Value.Item1, kvp.Key, kvp.Value.Item2, "#" + missingMilestoneEnd + " \\" + kvp.Key);
                }
            }

            private static bool MarkerRequiresClose(string markerWithoutPlus, ScrTag scrTag)
            {
                return markerWithoutPlus == "fig" || scrTag.OccursUnderList.Contains("NEST");
            }

            private IEnumerable<string> CharacterSequences(string text)
            {
                string key = "";

                for (int i = 0; i < text.Length; i++)
                {
                    char cc = text[i];


                    UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(cc);

                    if (cat == UnicodeCategory.Surrogate && i < text.Length - 1)
                    {
                        string s = text.Substring(i, 2);
                        i++;
                        if (key != "") yield return key;
                        key = "";
                        yield return s;
                        continue;
                    }

                    if (cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.NonSpacingMark)
                    {
                        if (key == " ")
                        {
                            yield return " "; // Don't allow diacritic to apply to a space
                            key = "";
                        }

                        key += cc;
                    }
                    else
                    {
                        if (key != "")
                        {
                            yield return key;
                            key = "";
                        }

                        if (cc == ' ')
                            yield return " ";
                        else
                            key += cc;
                    }
                }

                if (key != "") yield return key;   // Diacritics without a base character

            }

            /// <summary>
            /// Determines if a paragraph tag is valid. To validate a series, start with 
            /// an empty stack and call repeatedly with each paragraph style.
            /// </summary>
            /// <param name="stack">stack to use (will be modified)</param>
            /// <param name="tag">tag to check</param>
            /// <param name="addTag">true to add tag to stack, false to check only</param>
            /// <returns>true if valid</returns>
            private static bool IsParagraphTagValid(List<ScrTag> stack, ScrTag tag, bool addTag)
            {
                // If stack empty, add and return success
                if (stack.Count == 0)
                {
                    if (addTag)
                        stack.Add(tag);
                    return true;
                }

                var occursUnderList = tag.OccursUnderList;
                if (occursUnderList.Count == 0)
                    return true;

                // Go backwards up stack looking for suitable occurs under
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    // If allowable occurs under
                    if (occursUnderList.Contains(stack[i].Marker))
                    {
                        // If rank of next is less or equal
                        if ((stack.Count - 1 == i)
                            || tag.Rank == 0    // no rank requirement for this tag
                            || (stack[i + 1].Rank <= tag.Rank))
                        {
                            if (addTag)
                            {
                                // Clear rest of stack
                                if (stack.Count - 1 > i)
                                    stack.RemoveRange(i + 1, stack.Count - i - 1);

                                // Add tag and return success
                                stack.Add(tag);
                            }
                            return true;
                        }
                    }
                }
                return false;
            }

            #endregion
        }
        #endregion

    }

    public sealed class ChapterNumber
    {
        private static readonly Regex chapterNumberRegex = new Regex(@"^(\w)+", RegexOptions.Compiled);
        public int ChapterValue;

        private readonly string chapterStr;

        public ChapterNumber(string chapterText)
        {
            Match match = chapterNumberRegex.Match(chapterText);
            chapterStr = match.Value.Trim();
            if (!match.Success)
                ChapterValue = 0;
            else
                int.TryParse(chapterStr, out ChapterValue);
        }

        public override string ToString()
        {
            if (ChapterValue == 0)
                return "(" + chapterStr + ")";
            return chapterStr;
        }
    }

}
