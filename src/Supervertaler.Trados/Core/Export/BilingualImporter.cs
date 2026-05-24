using System;
using System.Collections.Generic;
using System.IO;

namespace Supervertaler.Trados.Core.Export
{
    /// <summary>
    /// Reads back a round-tripped DOCX or Markdown bilingual file, joins each
    /// row to the originating Trados segment via the sidecar manifest, and
    /// produces a list of pending diffs.
    ///
    /// The class is pure: it doesn't write to Trados itself. The caller (the
    /// ViewPart) applies confirmed diffs via <c>ProcessSegmentPair</c>.
    /// </summary>
    public class BilingualImporter
    {
        /// <summary>Build the diff list. Caller supplies "current target"
        /// lookups via <paramref name="currentTargetLookup"/> — given a
        /// (paragraphUnitId, segmentId) pair return the current target text,
        /// or <c>null</c> if no such segment exists in the active document.
        /// Lock/confirmation status is supplied via
        /// <paramref name="isWriteable"/> — return false to mark a segment
        /// as Locked.</summary>
        public BilingualImportResult Build(
            string importedFilePath,
            ExportManifest manifest,
            Func<string, string, string> currentTargetLookup,
            Func<string, string, bool> isWriteable,
            Func<string, string, string> currentSourceLookup = null)
        {
            var ext = Path.GetExtension(importedFilePath).ToLowerInvariant();
            List<ImportedSegment> imported;
            if (ext == ".docx")
                imported = new DocxImporter().Parse(importedFilePath);
            else
                imported = new MarkdownImporter().Parse(importedFilePath);

            var result = new BilingualImportResult { Manifest = manifest };

            // Index manifest segments by number for O(1) lookup.
            var manifestByNumber = new Dictionary<int, ExportManifestSegment>();
            foreach (var m in manifest.Segments)
                manifestByNumber[m.Number] = m;

            foreach (var row in imported)
            {
                ExportManifestSegment m;
                if (!manifestByNumber.TryGetValue(row.Number, out m))
                {
                    result.Diffs.Add(new ImportSegmentDiff
                    {
                        Number = row.Number,
                        NewTarget = row.TargetText,
                        Kind = ImportChangeKind.SegmentMissing,
                        Detail = "No manifest entry for segment #" + row.Number
                    });
                    continue;
                }

                var diff = new ImportSegmentDiff
                {
                    Number = row.Number,
                    ParagraphUnitId = m.ParagraphUnitId,
                    SegmentId = m.SegmentId,
                    NewTarget = row.TargetText ?? "",
                    Notes = row.Notes,
                    Status = row.Status
                };

                // Source-text tamper check, if the imported file kept the
                // source column (table layout) and a source lookup was given.
                if (currentSourceLookup != null && !string.IsNullOrEmpty(row.SourceText) && !string.IsNullOrEmpty(m.SourceHash))
                {
                    var currentHash = BilingualExporter.HashPrefix(row.SourceText);
                    if (!string.Equals(currentHash, m.SourceHash, StringComparison.Ordinal))
                    {
                        diff.Kind = ImportChangeKind.SourceMismatch;
                        diff.Detail = "Source text has been changed in the round-tripped file";
                        result.Diffs.Add(diff);
                        continue;
                    }
                }

                var currentTarget = currentTargetLookup?.Invoke(m.ParagraphUnitId, m.SegmentId);
                if (currentTarget == null)
                {
                    diff.Kind = ImportChangeKind.SegmentMissing;
                    diff.Detail = "Segment not present in the active Trados document";
                    result.Diffs.Add(diff);
                    continue;
                }
                diff.OldTarget = currentTarget;

                if (string.Equals(NormaliseForCompare(currentTarget), NormaliseForCompare(row.TargetText ?? ""),
                        StringComparison.Ordinal))
                {
                    diff.Kind = ImportChangeKind.Unchanged;
                    result.Diffs.Add(diff);
                    continue;
                }

                if (isWriteable != null && !isWriteable(m.ParagraphUnitId, m.SegmentId))
                {
                    diff.Kind = ImportChangeKind.Locked;
                    diff.Detail = "Segment is locked or rejected; needs explicit override";
                    result.Diffs.Add(diff);
                    continue;
                }

                diff.Kind = ImportChangeKind.Changed;
                diff.Apply = true; // default: include changed segments in the writeback
                result.Diffs.Add(diff);
            }

            return result;
        }

        private static string NormaliseForCompare(string s)
        {
            if (s == null) return "";
            // Normalise newlines + collapse trailing whitespace per line.
            s = s.Replace("\r\n", "\n");
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimEnd();
            return string.Join("\n", lines).Trim();
        }
    }
}
