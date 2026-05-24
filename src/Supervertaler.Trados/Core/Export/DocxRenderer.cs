using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Supervertaler.Trados.Core.Export
{
    /// <summary>
    /// Renders bilingual data as a Word (.docx) document using
    /// DocumentFormat.OpenXml. Two shapes:
    ///
    /// 1. <see cref="ExportLayout.Table"/> — a 5-column table matching
    ///    the Supervertaler Workbench's "Bilingual Table" format
    ///    (columns: #, Source, Target, Status, Notes). This is the
    ///    canonical round-trippable shape — files exported from the
    ///    Trados plugin can be re-imported by Supervertaler Workbench
    ///    and vice versa, as long as the structure is preserved.
    ///
    /// 2. Stacked layouts — segment number, source paragraph, target
    ///    paragraph (or target above source), one segment after the
    ///    other. Each segment has an invisible bookmark naming it
    ///    "SV_seg_N" so a future DOCX re-importer can match segments
    ///    by ID even if surrounding paragraphs are reformatted.
    ///
    /// Note: OpenXml WordprocessingML is verbose; keep formatting
    /// minimal here. The DOCX is a deliverable, not a place to
    /// experiment with rich typography.
    /// </summary>
    public class DocxRenderer : IExportRenderer
    {
        public void Render(List<ExportSegment> segments, ExportOptions options, string outputPath)
        {
            using (var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                AppendHeader(body, options, segments.Count);

                switch (options.Layout)
                {
                    case ExportLayout.Table:
                        AppendBilingualTable(body, segments, options);
                        break;
                    case ExportLayout.StackedTargetTop:
                        AppendStacked(body, segments, options, targetFirst: true);
                        break;
                    case ExportLayout.StackedSourceTop:
                    default:
                        AppendStacked(body, segments, options, targetFirst: false);
                        break;
                }

                // Landscape page setup (better for long segments in table form).
                AppendSectionProperties(body, options.Layout == ExportLayout.Table);

                mainPart.Document.Save();
            }
        }

        // ─── Header block ─────────────────────────────────────────────

        private static void AppendHeader(Body body, ExportOptions opts, int total)
        {
            // Title.
            body.AppendChild(MakeParagraph("Supervertaler Bilingual Review",
                bold: true, fontSize: "36", color: "0066CC", alignment: "center"));

            // Project info lines.
            body.AppendChild(MakeKeyValueLine("Project: ", opts.ProjectName));
            body.AppendChild(MakeKeyValueLine("Source file: ", opts.SourceFileName));
            body.AppendChild(MakeKeyValueLine("Languages: ",
                opts.SourceLanguageDisplay + " → " + opts.TargetLanguageDisplay));
            body.AppendChild(MakeKeyValueLine("Segments: ", total.ToString(CultureInfo.InvariantCulture)));

            // Notice / re-import warning. Workbench-compatible wording.
            var notice = new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines() { Before = "240", After = "240" }),
                MakeRun("Important: ", bold: true, color: "B46400"),
                MakeRun(
                    "Do not change segment numbers (#) or source text. " +
                    "This file can be re-imported into Supervertaler after proofreading.",
                    italic: true));
            body.AppendChild(notice);
        }

        // ─── Table layout (Supervertaler Bilingual Table) ─────────────

        private static void AppendBilingualTable(Body body, List<ExportSegment> segments, ExportOptions opts)
        {
            var table = new Table();

            // Table-wide properties: borders + auto-width.
            var tblProps = new TableProperties(
                new TableBorders(
                    new TopBorder() { Val = BorderValues.Single, Size = 4, Color = "888888" },
                    new BottomBorder() { Val = BorderValues.Single, Size = 4, Color = "888888" },
                    new LeftBorder() { Val = BorderValues.Single, Size = 4, Color = "888888" },
                    new RightBorder() { Val = BorderValues.Single, Size = 4, Color = "888888" },
                    new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" },
                    new InsideVerticalBorder() { Val = BorderValues.Single, Size = 4, Color = "BBBBBB" }),
                new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct });
            table.AppendChild(tblProps);

            // Header row.
            var header = new TableRow(new TableRowProperties(new TableHeader()));
            header.AppendChild(MakeHeaderCell("#", widthPct: 4));
            header.AppendChild(MakeHeaderCell(opts.SourceLanguageDisplay, widthPct: 36));
            header.AppendChild(MakeHeaderCell(opts.TargetLanguageDisplay, widthPct: 36));
            header.AppendChild(MakeHeaderCell("Status", widthPct: 10));
            header.AppendChild(MakeHeaderCell("Notes", widthPct: 14));
            table.AppendChild(header);

            // Body rows.
            foreach (var seg in segments)
            {
                var row = new TableRow();
                row.AppendChild(MakeBodyCell(seg.Number.ToString(CultureInfo.InvariantCulture), alignment: "right"));
                row.AppendChild(MakeBodyCellWithBookmark(seg.SourceText, seg.Number));
                row.AppendChild(MakeBodyCell(seg.TargetText ?? ""));
                row.AppendChild(MakeBodyCell(seg.Status ?? ""));
                row.AppendChild(MakeBodyCell(seg.Notes ?? ""));
                table.AppendChild(row);
            }

            body.AppendChild(table);
        }

        // ─── Stacked layouts ──────────────────────────────────────────

        private static void AppendStacked(Body body, List<ExportSegment> segments,
            ExportOptions opts, bool targetFirst)
        {
            foreach (var seg in segments)
            {
                // Segment header line — also doubles as anchor for re-import.
                body.AppendChild(MakeSegmentHeading(seg.Number));

                if (targetFirst)
                {
                    AppendStackedTarget(body, seg, opts);
                    AppendStackedSource(body, seg, opts);
                }
                else
                {
                    AppendStackedSource(body, seg, opts);
                    AppendStackedTarget(body, seg, opts);
                }

                if (!string.IsNullOrEmpty(seg.Status))
                {
                    body.AppendChild(new Paragraph(
                        new ParagraphProperties(new SpacingBetweenLines() { After = "120" }),
                        MakeRun("Status: ", bold: true, fontSize: "16", color: "888888"),
                        MakeRun(seg.Status, fontSize: "16", color: "888888")));
                }

                // Visual separator between segments.
                body.AppendChild(new Paragraph(
                    new ParagraphProperties(
                        new ParagraphBorders(
                            new TopBorder() { Val = BorderValues.Single, Size = 4, Color = "DDDDDD", Space = 1 })),
                    MakeRun("")));
            }
        }

        private static void AppendStackedSource(Body body, ExportSegment seg, ExportOptions opts)
        {
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines() { After = "60" }),
                MakeRun(opts.SourceLanguageDisplay + ":", bold: true, color: "555555")));
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines() { After = "180" }),
                MakeRun(seg.SourceText ?? "")));
        }

        private static void AppendStackedTarget(Body body, ExportSegment seg, ExportOptions opts)
        {
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines() { After = "60" }),
                MakeRun(opts.TargetLanguageDisplay + ":", bold: true, color: "555555")));
            body.AppendChild(new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines() { After = "180" }),
                MakeRun(seg.TargetText ?? "", color: "0A4D8E")));
        }

        // ─── Section / page setup ─────────────────────────────────────

        private static void AppendSectionProperties(Body body, bool landscape)
        {
            var sectPr = new SectionProperties();
            // Page size: landscape A4 if landscape, otherwise portrait Letter-ish default.
            // Numbers are in twentieths of a point (twips).
            // A4 landscape: 16838 × 11906; A4 portrait: 11906 × 16838.
            if (landscape)
            {
                sectPr.AppendChild(new PageSize() { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape });
                sectPr.AppendChild(new PageMargin() { Top = 720, Bottom = 720, Left = 720, Right = 720, Header = 720, Footer = 720, Gutter = 0 });
            }
            else
            {
                sectPr.AppendChild(new PageSize() { Width = 11906, Height = 16838, Orient = PageOrientationValues.Portrait });
                sectPr.AppendChild(new PageMargin() { Top = 1440, Bottom = 1440, Left = 1440, Right = 1440, Header = 720, Footer = 720, Gutter = 0 });
            }
            body.AppendChild(sectPr);
        }

        // ─── Run / cell helpers ───────────────────────────────────────

        private static Run MakeRun(string text, bool bold = false, bool italic = false,
            string fontSize = null, string color = null)
        {
            var props = new RunProperties();
            if (bold) props.AppendChild(new Bold());
            if (italic) props.AppendChild(new Italic());
            if (fontSize != null) props.AppendChild(new FontSize() { Val = fontSize });
            if (color != null) props.AppendChild(new Color() { Val = color });
            props.AppendChild(new RunFonts() { Ascii = "Segoe UI", HighAnsi = "Segoe UI" });

            var run = new Run();
            run.AppendChild(props);

            // Split on newlines so Word renders soft returns rather than literal "\n".
            if (text == null) text = "";
            text = text.Replace("\r\n", "\n");
            var parts = text.Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) run.AppendChild(new Break());
                if (parts[i].Length > 0)
                {
                    var t = new Text(parts[i]);
                    t.Space = SpaceProcessingModeValues.Preserve;
                    run.AppendChild(t);
                }
            }
            return run;
        }

        private static Paragraph MakeParagraph(string text, bool bold = false, bool italic = false,
            string fontSize = null, string color = null, string alignment = null)
        {
            var p = new Paragraph();
            if (alignment != null)
            {
                var pp = new ParagraphProperties();
                pp.AppendChild(new Justification() { Val = ParseAlignment(alignment) });
                p.AppendChild(pp);
            }
            p.AppendChild(MakeRun(text, bold: bold, italic: italic, fontSize: fontSize, color: color));
            return p;
        }

        private static JustificationValues ParseAlignment(string alignment)
        {
            switch (alignment)
            {
                case "center": return JustificationValues.Center;
                case "right":  return JustificationValues.Right;
                default:       return JustificationValues.Left;
            }
        }

        private static Paragraph MakeKeyValueLine(string key, string value)
        {
            return new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines() { After = "60" }),
                MakeRun(key, bold: true),
                MakeRun(value ?? ""));
        }

        private static Paragraph MakeSegmentHeading(int number)
        {
            // Bookmarks let a future DOCX re-importer locate segments by ID
            // even if the visible "Segment N" heading is reflowed.
            var p = new Paragraph();
            p.AppendChild(new ParagraphProperties(
                new SpacingBetweenLines() { Before = "200", After = "60" }));

            var bookmarkName = "SV_seg_" + number.ToString(CultureInfo.InvariantCulture);
            p.AppendChild(new BookmarkStart() { Id = number.ToString(CultureInfo.InvariantCulture), Name = bookmarkName });
            p.AppendChild(MakeRun(
                "Segment " + number.ToString(CultureInfo.InvariantCulture),
                bold: true, fontSize: "22", color: "0066CC"));
            p.AppendChild(new BookmarkEnd() { Id = number.ToString(CultureInfo.InvariantCulture) });
            return p;
        }

        private static TableCell MakeHeaderCell(string text, int widthPct)
        {
            var tcp = new TableCellProperties(
                new TableCellWidth() { Width = (widthPct * 50).ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Pct },
                new Shading() { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "F0F4F8" });
            var p = new Paragraph(MakeRun(text, bold: true, color: "333333"));
            var cell = new TableCell();
            cell.AppendChild(tcp);
            cell.AppendChild(p);
            return cell;
        }

        private static TableCell MakeBodyCell(string text, string alignment = "left")
        {
            var p = new Paragraph();
            if (alignment != "left")
            {
                var pp = new ParagraphProperties();
                pp.AppendChild(new Justification() { Val = ParseAlignment(alignment) });
                p.AppendChild(pp);
            }
            p.AppendChild(MakeRun(text ?? ""));
            var cell = new TableCell();
            cell.AppendChild(p);
            return cell;
        }

        /// <summary>Source-text cell variant that anchors a bookmark so the
        /// DOCX importer can locate the segment row even if cells are
        /// reordered. The bookmark name follows the same SV_seg_N
        /// convention as the stacked layout.</summary>
        private static TableCell MakeBodyCellWithBookmark(string text, int number)
        {
            var bookmarkName = "SV_seg_" + number.ToString(CultureInfo.InvariantCulture);
            var p = new Paragraph();
            p.AppendChild(new BookmarkStart() { Id = number.ToString(CultureInfo.InvariantCulture), Name = bookmarkName });
            p.AppendChild(MakeRun(text ?? ""));
            p.AppendChild(new BookmarkEnd() { Id = number.ToString(CultureInfo.InvariantCulture) });
            var cell = new TableCell();
            cell.AppendChild(p);
            return cell;
        }
    }
}
