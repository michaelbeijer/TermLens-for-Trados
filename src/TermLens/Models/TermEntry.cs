using System.Collections.Generic;

namespace TermLens.Models
{
    /// <summary>
    /// A single term pair from a Supervertaler termbase.
    /// </summary>
    public class TermEntry
    {
        public long Id { get; set; }
        public string SourceTerm { get; set; }
        public string TargetTerm { get; set; }
        public string SourceLang { get; set; }
        public string TargetLang { get; set; }
        public long TermbaseId { get; set; }
        public string TermbaseName { get; set; }
        public bool IsProjectTermbase { get; set; }
        public int Ranking { get; set; }
        public string Definition { get; set; }
        public string Domain { get; set; }
        public string Notes { get; set; }
        public bool Forbidden { get; set; }
        public bool CaseSensitive { get; set; }
        public List<string> TargetSynonyms { get; set; } = new List<string>();
    }

    /// <summary>
    /// A matched term found in the current source segment, ready for display.
    /// </summary>
    public class TermMatch
    {
        public TermEntry Entry { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string MatchedText { get; set; }
    }

    /// <summary>
    /// A token in the source segment — either a matched term or a plain word.
    /// </summary>
    public class SegmentToken
    {
        public string Text { get; set; }
        public bool IsLineBreak { get; set; }
        public List<TermEntry> Matches { get; set; } = new List<TermEntry>();
        public bool HasMatch => Matches.Count > 0;
    }

    /// <summary>
    /// Metadata about a loaded termbase.
    /// </summary>
    public class TermbaseInfo
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string SourceLang { get; set; }
        public string TargetLang { get; set; }
        public bool IsProjectTermbase { get; set; }
        public int Ranking { get; set; }
        public int TermCount { get; set; }
    }
}
