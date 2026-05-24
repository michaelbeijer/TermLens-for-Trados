namespace Supervertaler.Trados.Core.Export
{
    public enum ExportFormat
    {
        Docx,
        Markdown,
        Html
    }

    public enum ExportLayout
    {
        /// <summary>5-column table (#, Source, Target, Status, Notes).
        /// This is the canonical Supervertaler Bilingual Table format and
        /// the only one that the DOCX importer round-trips.</summary>
        Table,

        /// <summary>Source paragraph above target paragraph, segment by segment.</summary>
        StackedSourceTop,

        /// <summary>Target paragraph above source paragraph, segment by segment.</summary>
        StackedTargetTop
    }

    public class ExportOptions
    {
        public ExportFormat Format { get; set; } = ExportFormat.Docx;
        public ExportLayout Layout { get; set; } = ExportLayout.Table;

        /// <summary>Display name of the source language (e.g. "English (US)").</summary>
        public string SourceLanguageDisplay { get; set; } = "Source";

        /// <summary>Display name of the target language (e.g. "Dutch (Belgium)").</summary>
        public string TargetLanguageDisplay { get; set; } = "Target";

        /// <summary>Used in the document title + filename.</summary>
        public string ProjectName { get; set; } = "Untitled";

        /// <summary>Used in the manifest only — identifies which file these segments belong to.</summary>
        public string SourceFileName { get; set; } = "";

        /// <summary>Plugin version that produced the export, written into the manifest.</summary>
        public string ToolVersion { get; set; } = "";
    }
}
