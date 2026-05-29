using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Supervertaler.Trados.Models;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Clipboard Mode: formats segments for manual copy/paste to external web-based LLMs
    /// (ChatGPT, Claude, Gemini, etc.) and parses responses back.
    /// Reuses existing prompt building, tag serialisation, and response parsing infrastructure.
    /// </summary>
    public static class ClipboardRelay
    {
        // ─── Translate: format for clipboard ─────────────────────────

        /// <summary>
        /// Builds the full clipboard text for translation: system prompt + numbered
        /// bilingual segment blocks. The user pastes this into any web-based LLM.
        /// </summary>
        public static string FormatForTranslation(
            List<BatchSegment> segments,
            string sourceLang,
            string targetLang,
            string customPromptContent = null,
            List<TermEntry> termbaseTerms = null,
            string customSystemPrompt = null,
            List<string> documentSegments = null,
            int maxDocumentSegments = 500,
            bool includeTermMetadata = true)
        {
            var sb = new StringBuilder(segments.Count * 300 + 4096);

            // Build system prompt (reuses existing infrastructure)
            var systemPrompt = TranslationPrompt.BuildSystemPrompt(
                sourceLang, targetLang,
                customPromptContent, termbaseTerms, customSystemPrompt,
                documentSegments, maxDocumentSegments, includeTermMetadata);

            sb.AppendLine(systemPrompt);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Use short language labels for per-segment lines to save tokens.
            // The full names (with region) are already stated in the system prompt.
            var srcLabel = LanguageUtils.GetBaseLanguageName(sourceLang);
            var tgtLabel = LanguageUtils.GetBaseLanguageName(targetLang);

            // Instructions for the bilingual format. These are deliberately
            // emphatic: some web LLMs (notably DeepSeek's web chat) otherwise
            // reformat the reply into a bare list and drop the "Segment N"
            // headers, which makes the result impossible to re-import.
            sb.Append("Translate the following segments from ").Append(sourceLang)
              .Append(" into ").Append(targetLang).AppendLine(".");
            sb.AppendLine();
            sb.AppendLine("OUTPUT FORMAT — follow EXACTLY; this is critical:");
            sb.AppendLine("- Reproduce every block in the SAME structure shown below, in the SAME order.");
            sb.AppendLine("- Keep the literal \"Segment <n>\" header line for EVERY segment, with the SAME "
                + "number. Do NOT renumber, merge, split, omit, or reorder segments.");
            sb.AppendLine("- Keep the \"" + srcLabel + ":\" line unchanged and put your translation on the \""
                + tgtLabel + ":\" line.");
            sb.AppendLine("- Do NOT reformat the output into a plain list, a table, or prose, and do NOT drop "
                + "the segment numbers. The result is parsed by its \"Segment <n>\" headers to import it back "
                + "into the CAT tool, so losing them breaks re-import.");
            sb.AppendLine("- Do NOT add commentary, explanations, or notes.");
            sb.AppendLine("- Preserve ALL tag placeholders (<t1>, </t1>, <t2/>, etc.) exactly as they appear.");
            sb.AppendLine();

            // Numbered bilingual segments
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                var status = GetSegmentStatus(seg);

                sb.Append("Segment ").Append(i + 1);
                if (!string.IsNullOrEmpty(status))
                    sb.Append(" [").Append(status).Append("]");
                sb.AppendLine(":");

                sb.Append(srcLabel).Append(": ").AppendLine(seg.SourceText);
                sb.Append(tgtLabel).Append(": ");

                // Include existing target for fuzzy/translated segments
                if (!string.IsNullOrWhiteSpace(seg.ExistingTarget))
                    sb.AppendLine(seg.ExistingTarget);
                else
                    sb.AppendLine();

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        // ─── Proofread: format for clipboard ─────────────────────────

        /// <summary>
        /// Builds the full clipboard text for proofreading: system prompt + numbered
        /// bilingual segment blocks with both source and target filled in.
        /// </summary>
        public static string FormatForProofreading(
            List<BatchSegment> segments,
            string sourceLang,
            string targetLang,
            string customPromptContent = null,
            List<TermEntry> termbaseTerms = null,
            string customSystemPrompt = null,
            List<(string source, string target)> documentSegments = null,
            bool includeTermMetadata = true)
        {
            var sb = new StringBuilder(segments.Count * 400 + 4096);

            // Build system prompt using proofreading base. Document context is
            // bilingual (source + target) and untruncated — matches the API path so
            // a "what gets sent to the AI" preview is faithful for both modes.
            var systemPrompt = ProofreadingPrompt.BuildSystemPrompt(
                sourceLang, targetLang,
                termbaseTerms, customPromptContent,
                documentSegments, includeTermMetadata);

            sb.AppendLine(systemPrompt);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Instructions
            sb.AppendLine("Review the following translated segments.");
            sb.AppendLine("For each segment, respond in this format:");
            sb.AppendLine();
            sb.AppendLine("Segment N: OK");
            sb.AppendLine("  (if the translation is correct)");
            sb.AppendLine();
            sb.AppendLine("Segment N: ISSUE");
            sb.AppendLine("  Problem: [description of the issue]");
            sb.AppendLine("  Suggestion: [corrected translation]");
            sb.AppendLine();

            // Use short language labels for per-segment lines to save tokens
            var srcLabel = LanguageUtils.GetBaseLanguageName(sourceLang);
            var tgtLabel = LanguageUtils.GetBaseLanguageName(targetLang);

            // Numbered bilingual segments with both source and target
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];

                sb.Append("Segment ").Append(i + 1).AppendLine(":");
                sb.Append(srcLabel).Append(": ").AppendLine(seg.SourceText);
                sb.Append(tgtLabel).Append(": ").AppendLine(seg.ExistingTarget ?? "");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        // ─── Translate: parse clipboard response ─────────────────────

        /// <summary>
        /// Parses the bilingual response from the LLM. Expects the format:
        ///   Segment 1:
        ///   {sourceLang}: ...
        ///   {targetLang}: translated text
        ///
        /// Falls back to the existing numbered-list parser if the bilingual
        /// format is not detected.
        /// </summary>
        public static List<ParsedTranslation> ParseTranslationResponse(
            string response, int expectedCount, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(response))
                return new List<ParsedTranslation>();

            // Try bilingual format first
            var results = ParseBilingualResponse(response, targetLang);

            // Fall back to simple numbered list (1. translation)
            if (results.Count == 0)
                results = TranslationPrompt.ParseBatchResponse(response, expectedCount);

            return results;
        }

        /// <summary>
        /// Parses the bilingual segment format:
        ///   Segment N [status]:
        ///   {lang}: source text
        ///   {lang}: target text
        /// </summary>
        private static List<ParsedTranslation> ParseBilingualResponse(string response, string targetLang)
        {
            var results = new List<ParsedTranslation>();

            // Match "Segment N" headers (with optional status annotation)
            var segmentPattern = new Regex(
                @"^Segment\s+(\d+)\s*(?:\[.*?\])?\s*:",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            var matches = segmentPattern.Matches(response);
            if (matches.Count == 0)
                return results;

            // Build a target language prefix pattern that matches both the short
            // label ("English:") and the full label ("English (United Kingdom):").
            var baseLang = LanguageUtils.GetBaseLanguageName(targetLang);
            var targetPrefix = new Regex(
                @"^\s*" + Regex.Escape(baseLang) + @"(?:\s*\([^)]*\))?\s*:\s*(.*)",
                RegexOptions.IgnoreCase);

            for (int i = 0; i < matches.Count; i++)
            {
                var segNum = int.Parse(matches[i].Groups[1].Value);

                // Extract the block between this segment header and the next
                int blockStart = matches[i].Index + matches[i].Length;
                int blockEnd = (i + 1 < matches.Count)
                    ? matches[i + 1].Index
                    : response.Length;

                var block = response.Substring(blockStart, blockEnd - blockStart);
                var lines = block.Split(new[] { '\n' }, StringSplitOptions.None);

                // Find the target language line and collect continuation lines
                var targetText = new StringBuilder();
                bool foundTarget = false;

                foreach (var line in lines)
                {
                    var targetMatch = targetPrefix.Match(line);
                    if (targetMatch.Success)
                    {
                        foundTarget = true;
                        targetText.Append(targetMatch.Groups[1].Value.Trim());
                        continue;
                    }

                    if (foundTarget)
                    {
                        // Stop at empty line or next language label
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                            break;
                        // Check if this is a language label (e.g., "Dutch: ...")
                        if (Regex.IsMatch(line, @"^\s*\w[\w\s]*:\s"))
                            break;
                        // Continuation line
                        targetText.AppendLine();
                        targetText.Append(trimmed);
                    }
                }

                if (foundTarget)
                {
                    var translation = targetText.ToString().Trim();
                    if (!string.IsNullOrEmpty(translation))
                    {
                        results.Add(new ParsedTranslation
                        {
                            Number = segNum,
                            Translation = translation
                        });
                    }
                }
            }

            return results;
        }

        // ─── Helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Determines the status annotation for a segment (new, fuzzy, draft, translated).
        /// </summary>
        private static string GetSegmentStatus(BatchSegment seg)
        {
            if (string.IsNullOrWhiteSpace(seg.ExistingTarget))
                return "new";

            var pair = seg.SegmentPairRef as ISegmentPair;
            if (pair == null)
                return "draft";

            try
            {
                var origin = pair.Properties?.TranslationOrigin;
                if (origin == null)
                    return "draft";

                var originType = origin.OriginType ?? "";
                var matchPct = origin.MatchPercent;

                // TM fuzzy or auto-propagated
                if (originType.Equals("tm", StringComparison.OrdinalIgnoreCase)
                    || originType.Equals("auto-propagated", StringComparison.OrdinalIgnoreCase))
                {
                    if (matchPct >= 100)
                        return "translated, 100%";
                    return "fuzzy, " + matchPct + "%";
                }

                // Machine translation
                if (originType.Equals("mt", StringComparison.OrdinalIgnoreCase)
                    || originType.Equals("nmt", StringComparison.OrdinalIgnoreCase)
                    || originType.Equals("adaptive-mt", StringComparison.OrdinalIgnoreCase))
                    return "machine translated";

                // Interactive (human-edited)
                if (originType.Equals("interactive", StringComparison.OrdinalIgnoreCase))
                    return "translated";

                return "draft";
            }
            catch
            {
                return "draft";
            }
        }
    }
}
