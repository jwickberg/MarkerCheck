using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MarkerCheck
{
    /// <summary>
    /// Stylesheet represented on disk by an .STY file
    /// <remarks>Thread-safe</remarks>
    /// </summary>
    public class ScrStylesheet
    {
        #region Constants/Member variables
        private readonly string path;
        private readonly string name;
        private readonly List<string> stylesheetErrors;
        private readonly List<string> altStylesheetErrors;
        protected readonly Dictionary<string, int> tagIndexDictionary = new Dictionary<string, int>();
        protected readonly List<ScrTag> tags = new List<ScrTag>();

        private readonly object syncRoot = new object();
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a stylesheet for the specified project
        /// </summary>
        /// <param name="styleSheetRelFileName">optional stylesheet to force to load (path relative 
        /// to the project's directory). Note: custom styles will not override forced stylesheets.</param>
        public ScrStylesheet(string styleSheetRelFileName)
        {
            path = styleSheetRelFileName;
            name = Path.GetFileName(path);

			IEnumerable<string> mainFileLines = File.ReadAllLines(styleSheetRelFileName);

            ParseStylesheets(mainFileLines,null,
                out stylesheetErrors, out altStylesheetErrors);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets a list of all the tags in the stylesheet
        /// </summary>
        public IEnumerable<ScrTag> Tags
        {
            get { return tags; }
        }

        public IEnumerable<string> MainStylesheetErrors
        {
            get { return stylesheetErrors; }
        }

        public IEnumerable<string> AltStylesheetErrors
        {
            get { return altStylesheetErrors; }
        }

        public string Name
        {
            get { return name; }
        }

        internal int TagCount
        {
            get { return tags.Count; }
        }

        internal ScrTag this[int index]
        {
            get { return tags[index]; }
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Creates a new stylesheet that is a union of the specified stylesheets. For any markers/tags that
        /// are defined in two or more stylesheets, this method will take the definition defined in the latter stylesheet.
        /// No merging of style properties are done. Also, this method does make "deep" copies of the
        /// styles since some stylesheets will be modified and we don't want to change the original stylesheet.
        /// </summary>
        public static ScrStylesheet MergeStylesheets(params ScrStylesheet[] stylesheets)
        {
            ScrStylesheet newStyleSheet = new ScrStylesheet(string.Join("-", stylesheets.Select(ss => ss.name)));

            foreach (ScrTag tag in stylesheets.SelectMany(ss => ss.Tags))
                newStyleSheet.AddTagInternal(tag.Clone());
            
            return newStyleSheet;
        }

        public int GetTagIndex(string marker)
        {
            lock (syncRoot)
            {
                int index;
                if (tagIndexDictionary.TryGetValue(marker, out index))
                    return index;
                
                // Create tag
                ScrTag tag = CreateTag(marker);
                tag.StyleType = ScrStyleType.scUnknownStyle;
                tag.Color = new RgbColor(255, 0, 0);
                return tagIndexDictionary[tag.Marker];
            }
        }

        /// <summary>
        /// Gets the specified tag entry, creating a default one if doesn't exist.
        /// </summary>
        public ScrTag GetTag(string marker)
        {
            lock (syncRoot)
            {
                return tags[GetTagIndex(marker)];
            }
        }

        /// <summary>
        /// Create a style corresponding to the list of markers given
        /// </summary>
        /// <param name="markers"></param>
        /// <returns></returns>
        public ScrTag NestedStyle(string[] markers)
        {
            ScrTag tag = GetTag(markers[0]).Clone();
            if (tag == null)
                return null;

            for (int i = 1; i < markers.GetLength(0); ++i)
            {
                ScrTag nestedTag = GetTag(markers[i]);
                if (nestedTag == null)
                    return null;

                tag.Marker += "X" + markers[i];
                tag.AddNestedFormatting(nestedTag);
            }

            tag.Endmarker = tag.Marker + "*";
            tag.Description = null;
            tag.Name = tag.Marker;

            return tag;
        }

        #endregion

        #region Private helper methods
        /// <summary>
        /// Adds the specified tag to stylesheet.
        /// </summary>
        protected void AddTagInternal(ScrTag tag)
        {
            int existingTagIndex;
            if (tagIndexDictionary.TryGetValue(tag.Marker, out existingTagIndex))
                tags[existingTagIndex] = tag;
            else
            {
                tags.Add(tag);
                tagIndexDictionary[tag.Marker] = tagIndexDictionary.Count;
            }
        }

        private void ParseStylesheets(IEnumerable<string> mainStyleSheetLines, IEnumerable<string> alternateStyleSheetLines,
            out List<string> mainErrors, out List<string> altErrors)
        {
            altErrors = null;
            mainErrors = null;
            try
            {
                mainErrors = Parse(mainStyleSheetLines);

                if (alternateStyleSheetLines != null)
                    altErrors = Parse(alternateStyleSheetLines);
            }
            catch (Exception ex)
            {
                Console.WriteLine(Localizer.Str("Error in stylesheet:") + path + "\r\n" + ex.Message);
            }
        }

        private List<string> Parse(IEnumerable<string> fileLines)
        {
            List<StylesheetEntry> entries = SplitStylesheet(fileLines);

            HashSet<string> foundStyles = new HashSet<string>();
            List<string> errors = new List<string>();
            bool foundMarker = false;
            for (int i = 0; i < entries.Count; ++i)
            {
                StylesheetEntry entry = entries[i];

                if (entry.Marker != "marker")
                    continue;
                string[] parts = entry.Text.Split();
                if (parts.Length > 1 && parts[1] == "-")
                {
                    // If the entry looks like "\marker xy -" remove the tag and its end tag if any
                    foundMarker = true;
                    RemoveTag(parts[0]);
                    RemoveTag(parts[0] + "*");
                    continue;
                }

                foundMarker = true;
                ScrTag tag = CreateTag(entry.Text);
                List<string> tagErrors;
                ScrTag endTag = ScrTag.ParseSTYMarkerEntry(tag, entries, i + 1, out tagErrors);

                errors.AddRange(tagErrors);

                if (endTag != null && !tagIndexDictionary.ContainsKey(endTag.Marker))
                {
                    tags.Add(endTag);
                    tagIndexDictionary[endTag.Marker] = tags.Count - 1;
                }

                if (foundStyles.Contains(entry.Text))
                {
                    errors.Add(ScrTag.GetMessage(entry.LineNumber,
                        string.Format(Localizer.Str("Duplicate style definition '{0}'"), entry.Text)));
                }

                foundStyles.Add(entry.Text);
            }

            if (!foundMarker)
            {
                errors.Add(ScrTag.GetMessage(1, Localizer.Str("No styles defined")));
                return errors;
            }

            return errors.Count == 0 ? null : errors;
        }

        private void RemoveTag(string tag)
        {
            int tagInd;
            if (!tagIndexDictionary.TryGetValue(tag, out tagInd))
                return;

            tagIndexDictionary.Remove(tag);
            tags.RemoveAt(tagInd);

            foreach (string tag2 in tagIndexDictionary.Keys.ToList())
                if (tagIndexDictionary[tag2] >= tagInd)
                    tagIndexDictionary[tag2] = tagIndexDictionary[tag2] - 1;
        }

        private ScrTag CreateTag(string marker)
        {
            // If tag already exists update with addtl info (normally from custom.sty)
            int tagIndex;
            if (tagIndexDictionary.TryGetValue(marker, out tagIndex))
            {
                return tags[tagIndex];
            }

            ScrTag tag = new ScrTag(marker);

            // Sigh.
            // COM based stylesheet support assumes that for all markers except c & v
            // the default is that they ARE publishable.
            if (marker != "c" && marker != "v")
                tag.TextProperties = TextProperties.scPublishable;

            tags.Add(tag);
            tagIndexDictionary[tag.Marker] = tags.Count - 1;

            return tag;
        }

        private List<StylesheetEntry> SplitStylesheet(IEnumerable<string> fileLines)
        {
            List<StylesheetEntry> entries = new List<StylesheetEntry>();
            int lineNumber = 0;
            // Lines that are not compatible with USFM 2 are started with #!, so these two characters are stripped from the beginning of lines.
            foreach (string line in fileLines.Select(l => l.StartsWith("#!", StringComparison.Ordinal) ? l.Substring(2) : l).Select(l => l.Split('#')[0].Trim()))
            {
                lineNumber++;
                if (line == "")
                    continue;

                if (!line.StartsWith("\\", StringComparison.Ordinal))
                {
                    Console.WriteLine("Line in {0} does not start with backslash: {1}", path, line);
                    continue;
                }

                string[] parts = line.Split(new[]{' '}, 2);
                entries.Add(new StylesheetEntry(parts[0].Substring(1).ToLowerInvariant(), 
                    (parts.Length > 1) ? parts[1].Trim() : "", lineNumber));
            }

            return entries;
        }
        #endregion
    }

    #region StylesheetEntry class
    /// <summary>
    /// Single entry in a style sheet
    /// </summary>
    internal sealed class StylesheetEntry
    {
        /// <summary>marker, lower case</summary>
        public readonly string Marker;

        /// <summary>text of marker, trimmed</summary>
        public readonly string Text;

        /// <summary>line number of entry</summary>
        public readonly int LineNumber;

        public StylesheetEntry(string marker, string text, int lineNumer)
        {
            Marker = marker;
            Text = text;
            LineNumber = lineNumer;
        }

        public override string ToString()
        {
            return LineNumber + ": \\" + Marker + " " + Text;
        }
    }
    #endregion
}
