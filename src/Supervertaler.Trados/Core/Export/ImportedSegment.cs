namespace Supervertaler.Trados.Core.Export
{
    /// <summary>One segment as read back from a round-tripped DOCX or Markdown.
    /// Maps to a row in the export manifest via <see cref="Number"/>.</summary>
    public class ImportedSegment
    {
        public int Number { get; set; }
        public string SourceText { get; set; }
        public string TargetText { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
    }
}
