using System;

namespace Supervertaler.Trados.Core.Export
{
    /// <summary>
    /// One row of bilingual data, format-agnostic. The renderers consume a
    /// <see cref="System.Collections.Generic.List{T}"/> of these and produce
    /// DOCX/Markdown/HTML; the importer reverses the flow.
    ///
    /// Notes on identity:
    /// - <see cref="Number"/> is the human-visible export number (1..N) written
    ///   into the file itself. Proofreaders can see it; renames break the
    ///   re-import alignment.
    /// - <see cref="ParagraphUnitId"/> + <see cref="SegmentId"/> together form
    ///   the unambiguous Trados segment key. They are NOT written into the
    ///   user-visible file content; they live in the sidecar manifest so even
    ///   if a proofreader accidentally reorders rows we can still match by
    ///   the manifest's (Number → Pu/Seg id) mapping.
    /// </summary>
    public class ExportSegment
    {
        /// <summary>1-based export number, matches the column header "#" in the DOCX table.</summary>
        public int Number { get; set; }

        /// <summary>Stable Trados ParagraphUnit id (string GUID-like).</summary>
        public string ParagraphUnitId { get; set; }

        /// <summary>Stable Trados Segment id (string GUID-like, unique within its paragraph unit).</summary>
        public string SegmentId { get; set; }

        /// <summary>Plain source text with tag placeholders left in (e.g. &lt;t1&gt;...&lt;/t1&gt;).</summary>
        public string SourceText { get; set; }

        /// <summary>Plain target text. Empty for untranslated segments.</summary>
        public string TargetText { get; set; }

        /// <summary>
        /// Trados confirmation level snapshot at export time — "Draft",
        /// "Translated", "Approved", "Rejected", etc. Surfaced in the
        /// "Status" column. Empty when not applicable.
        /// </summary>
        public string Status { get; set; }

        /// <summary>Optional free-form notes column, blank on initial export.</summary>
        public string Notes { get; set; }

        /// <summary>SHA-256 prefix of the source text at export time. Used by the
        /// importer to detect source tampering before applying changes.</summary>
        public string SourceHash { get; set; }
    }
}
