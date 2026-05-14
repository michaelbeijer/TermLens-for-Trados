using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Supervertaler.Trados.Controls;

namespace Supervertaler.Trados.Core
{
    /// <summary>
    /// Host-agnostic controller for SuperSearch. Owns the single
    /// <see cref="SuperSearchControl"/> instance and all search / replace /
    /// navigate logic, so the UI can be hosted either by the standalone
    /// <c>SuperSearchViewPart</c> or as a tab inside the Supervertaler
    /// Assistant panel. Exposed as a process-wide singleton via
    /// <see cref="Shared"/> so both hosts share one control (and therefore
    /// one set of results, which survives a tab switch).
    /// </summary>
    public class SuperSearchController
    {
        private static SuperSearchController _shared;

        /// <summary>
        /// The shared controller instance. First access creates the control
        /// and wires it to the EditorController. Both hosts call this; whichever
        /// runs first does the wiring, the other just re-parents the control.
        /// </summary>
        public static SuperSearchController Shared =>
            _shared ?? (_shared = new SuperSearchController());

        private readonly SuperSearchControl _control;

        /// <summary>The SuperSearch UI control. Re-parent this into whichever host is active.</summary>
        public SuperSearchControl Control => _control;

        private EditorController _editorController;
        private IStudioDocument _activeDocument;

        // Cached project SDLXLIFF file list (refreshed on document change)
        private List<string> _projectFiles;
        private string _lastProjectRoot;

        // Search cancellation
        private CancellationTokenSource _searchCts;

        // Last search results (for replace operations)
        private List<SearchResult> _lastResults;

        private SuperSearchController()
        {
            _control = new SuperSearchControl();

            // Wire UI events
            _control.SearchRequested += OnSearchRequested;
            _control.StopRequested += OnStopRequested;
            _control.NavigateRequested += OnNavigateRequested;
            _control.ReplaceRequested += OnReplaceRequested;
            _control.ReplaceAllRequested += OnReplaceAllRequested;
            _control.HelpRequested += (s, e) => HelpSystem.OpenHelp(HelpSystem.Topics.SuperSearch);

            _editorController = SdlTradosStudio.Application.GetController<EditorController>();
            if (_editorController != null)
            {
                _editorController.ActiveDocumentChanged += OnActiveDocumentChanged;

                if (_editorController.ActiveDocument != null)
                {
                    _activeDocument = _editorController.ActiveDocument;
                    RefreshProjectFiles();
                }
            }
        }

        // ─── Document Events ─────────────────────────────────────

        private void OnActiveDocumentChanged(object sender, DocumentEventArgs e)
        {
            _activeDocument = _editorController?.ActiveDocument;
            RefreshProjectFiles();
        }

        private void RefreshProjectFiles()
        {
            if (_activeDocument == null) return;

            try
            {
                var activeFile = _activeDocument.ActiveFile;
                if (activeFile == null) return;

                var filePath = activeFile.LocalFilePath;
                if (string.IsNullOrEmpty(filePath)) return;

                // Find project root
                var dir = Path.GetDirectoryName(filePath);
                string projectRoot = null;
                while (!string.IsNullOrEmpty(dir))
                {
                    try
                    {
                        if (Directory.GetFiles(dir, "*.sdlproj", SearchOption.TopDirectoryOnly).Length > 0)
                        {
                            projectRoot = dir;
                            break;
                        }
                    }
                    catch { }

                    var parent = Path.GetDirectoryName(dir);
                    if (parent == dir) break;
                    dir = parent;
                }

                // Only re-scan if project root changed
                if (projectRoot != null && projectRoot != _lastProjectRoot)
                {
                    _lastProjectRoot = projectRoot;
                    _projectFiles = XliffSearcher.FindProjectXliffFiles(filePath);
                    SafeInvoke(() =>
                    {
                        _control.SetProjectFiles(_projectFiles);
                        _control.SetStatus(
                            $"Project: {Path.GetFileName(projectRoot)} — {_projectFiles.Count} file(s)");
                    });
                }
            }
            catch
            {
                _projectFiles = null;
            }
        }

        // ─── Search ──────────────────────────────────────────────

        private async void OnSearchRequested(object sender, SearchRequestEventArgs e)
        {
            // Cancel any in-progress search
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var ct = _searchCts.Token;

            if (_projectFiles == null || _projectFiles.Count == 0)
            {
                RefreshProjectFiles();
                if (_projectFiles == null || _projectFiles.Count == 0)
                {
                    SafeInvoke(() =>
                    {
                        _control.SetStatus("No project files found. Open a file in the editor first.");
                        _control.SetSearching(false);
                    });
                    return;
                }
            }

            // Use the file selection from the control (respects user's file filter)
            var files = _control.GetSelectedFiles();
            if (files.Count == 0)
            {
                SafeInvoke(() =>
                {
                    _control.SetStatus("No files selected. Click 'Files' to select files to search.");
                    _control.SetSearching(false);
                });
                return;
            }

            SafeInvoke(() =>
            {
                _control.SetSearching(true);
                _control.SetStatus("Searching...");
            });

            var sw = Stopwatch.StartNew();

            try
            {
                var results = await Task.Run(() =>
                    XliffSearcher.Search(
                        files, e.Query, e.Scope,
                        e.CaseSensitive, e.UseRegex, e.WholeWord,
                        (done, total) => SafeInvoke(() =>
                            _control.SetStatus($"Searching... ({done}/{total} files)")),
                        ct),
                    ct);

                sw.Stop();
                _lastResults = results;

                SafeInvoke(() =>
                {
                    _control.SetResults(results);
                    _control.SetStatus(
                        $"{results.Count} result(s) in {files.Count} file(s) — {sw.ElapsedMilliseconds} ms");
                    _control.SetSearching(false);
                });
            }
            catch (OperationCanceledException)
            {
                SafeInvoke(() =>
                {
                    _control.SetStatus("Search cancelled.");
                    _control.SetSearching(false);
                });
            }
            catch (Exception ex)
            {
                SafeInvoke(() =>
                {
                    _control.SetStatus($"Search error: {ex.Message}");
                    _control.SetSearching(false);
                });
            }
        }

        private void OnStopRequested(object sender, EventArgs e)
        {
            _searchCts?.Cancel();
        }

        // ─── Navigate ────────────────────────────────────────────

        private void OnNavigateRequested(object sender, NavigateToSegmentEventArgs e)
        {
            // Must run on the UI thread – same pattern as AiAssistantViewPart.OnNavigateToSegment
            SafeInvoke(() =>
            {
                if (_activeDocument == null || _editorController == null)
                {
                    _control.SetStatus("No active document.");
                    return;
                }

                var result = _control.GetSelectedResult();
                if (result == null) return;

                var activeFilePath = GetActiveFilePath();
                var isSameFile = activeFilePath != null &&
                    string.Equals(activeFilePath, result.FilePath, StringComparison.OrdinalIgnoreCase);

                if (!isSameFile)
                {
                    _control.SetStatus(
                        $"Open \"{result.FileName}\" in the editor first, then double-click to navigate.");
                    return;
                }

                try
                {
                    _activeDocument.SetActiveSegmentPair(
                        result.ParagraphUnitId, result.SegmentId, true);

                    // Give focus back to the editor so the navigation is visible
                    try { _editorController.Activate(); }
                    catch { /* Activate may not be available */ }

                    _control.SetStatus(
                        $"Navigated to segment #{result.SegmentNumber} in {result.FileName}");
                }
                catch (Exception ex)
                {
                    _control.SetStatus($"Navigation failed: {ex.Message}");
                }
            });
        }

        private string GetActiveFilePath()
        {
            try
            {
                return _activeDocument?.ActiveFile?.LocalFilePath;
            }
            catch
            {
                return null;
            }
        }

        // ─── Replace ─────────────────────────────────────────────

        private void OnReplaceRequested(object sender, ReplaceRequestEventArgs e)
        {
            if (e.SelectedResult == null) return;
            if (_activeDocument == null) return;

            var result = e.SelectedResult;

            // The segment must be in the active file
            var activeFilePath = GetActiveFilePath();
            if (activeFilePath == null ||
                !string.Equals(activeFilePath, result.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                SafeInvoke(() => _control.SetStatus(
                    "Navigate to the segment first (double-click the row). Replace works on the active file."));
                return;
            }

            try
            {
                // Navigate to segment
                _activeDocument.SetActiveSegmentPair(result.ParagraphUnitId, result.SegmentId, true);

                // Get the active segment pair and use ProcessSegmentPair to modify it
                var activePair = _activeDocument.ActiveSegmentPair;
                if (activePair == null)
                {
                    SafeInvoke(() => _control.SetStatus("Could not access the active segment."));
                    return;
                }

                var outcome = ReplaceInActiveSegmentPair(
                    activePair, e.SearchText, e.ReplaceText,
                    e.CaseSensitive, e.UseRegex, e.WholeWord, out var newTarget);

                switch (outcome)
                {
                    case ActiveReplaceOutcome.NoMatch:
                        SafeInvoke(() => _control.SetStatus("No match found in target text."));
                        return;
                    case ActiveReplaceOutcome.SpansInlineTags:
                        SafeInvoke(() => _control.SetStatus(
                            "Match spans inline tags – skipped to preserve formatting. Edit the segment manually."));
                        return;
                    case ActiveReplaceOutcome.Error:
                        SafeInvoke(() => _control.SetStatus("Replace failed – the segment couldn't be modified."));
                        return;
                }

                result.TargetText = newTarget;
                SafeInvoke(() =>
                {
                    _control.SetResults(_lastResults);
                    _control.SetStatus("Replaced in 1 segment.");
                });
            }
            catch (Exception ex)
            {
                SafeInvoke(() => _control.SetStatus($"Replace error: {ex.Message}"));
            }
        }

        private void OnReplaceAllRequested(object sender, ReplaceRequestEventArgs e)
        {
            if (_lastResults == null || _lastResults.Count == 0) return;

            // Count target matches
            var targetMatches = _lastResults.Where(r =>
                IsTargetMatch(r.TargetText, e.SearchText, e.CaseSensitive, e.UseRegex, e.WholeWord)).ToList();

            if (targetMatches.Count == 0)
            {
                SafeInvoke(() => _control.SetStatus("No matches found in target text."));
                return;
            }

            // Group by file
            var fileGroups = targetMatches.GroupBy(r => r.FilePath).ToList();

            var activeFilePath = GetActiveFilePath();
            var hasNonActiveFiles = fileGroups.Any(g =>
                activeFilePath == null ||
                !string.Equals(g.Key, activeFilePath, StringComparison.OrdinalIgnoreCase));

            var msg = $"Replace {targetMatches.Count} occurrence(s) in {fileGroups.Count} file(s)?\n\n";

            if (hasNonActiveFiles)
            {
                msg += "WARNING: This will modify SDLXLIFF files directly on disk for files " +
                       "not currently open in the editor. These changes CANNOT be undone.\n\n" +
                       "Changes in the active file go through the Trados API and can be undone.\n\n" +
                       "Save your project before proceeding.";
            }
            else
            {
                msg += "All changes go through the Trados API and can be undone with Ctrl+Z.";
            }

            var dialogResult = MessageBox.Show(msg, "SuperSearch — Replace All",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (dialogResult != DialogResult.OK) return;

            // Second confirmation for irreversible disk modifications
            if (hasNonActiveFiles)
            {
                var confirm = MessageBox.Show(
                    "Are you sure? Changes to files on disk cannot be undone.\n\n" +
                    "Make sure you have saved your project or have a backup.",
                    "SuperSearch — Final Confirmation",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button2); // default to "No"
                if (confirm != DialogResult.Yes) return;
            }

            int replacedCount = 0;
            int errorCount = 0;
            int skippedTagSpan = 0;

            foreach (var group in fileGroups)
            {
                var filePath = group.Key;
                var isActiveFile = activeFilePath != null &&
                    string.Equals(filePath, activeFilePath, StringComparison.OrdinalIgnoreCase);

                if (isActiveFile && _activeDocument != null)
                {
                    // Replace via Trados API for the active file
                    foreach (var result in group)
                    {
                        try
                        {
                            _activeDocument.SetActiveSegmentPair(result.ParagraphUnitId, result.SegmentId, true);
                            var pair = _activeDocument.ActiveSegmentPair;
                            if (pair == null) { errorCount++; continue; }

                            var outcome = ReplaceInActiveSegmentPair(
                                pair, e.SearchText, e.ReplaceText,
                                e.CaseSensitive, e.UseRegex, e.WholeWord, out var newTarget);

                            if (outcome == ActiveReplaceOutcome.Replaced)
                            {
                                result.TargetText = newTarget;
                                replacedCount++;
                            }
                            else if (outcome == ActiveReplaceOutcome.SpansInlineTags)
                            {
                                skippedTagSpan++;
                            }
                            else if (outcome == ActiveReplaceOutcome.Error)
                            {
                                errorCount++;
                            }
                        }
                        catch { errorCount++; }
                    }
                }
                else
                {
                    // Replace directly in the SDLXLIFF file on disk
                    try
                    {
                        int count = ReplaceInXliffFile(filePath, group.ToList(),
                            e.SearchText, e.ReplaceText, e.CaseSensitive, e.UseRegex, e.WholeWord,
                            out var tagSpanInFile);
                        replacedCount += count;
                        skippedTagSpan += tagSpanInFile;
                    }
                    catch { errorCount += group.Count(); }
                }
            }

            SafeInvoke(() =>
            {
                _control.SetResults(_lastResults);
                var statusMsg = $"Replaced {replacedCount} segment(s)";
                if (skippedTagSpan > 0) statusMsg += $", skipped {skippedTagSpan} (match spans inline tags)";
                if (errorCount > 0) statusMsg += $" ({errorCount} error(s))";
                if (fileGroups.Any(g => !string.Equals(g.Key, activeFilePath, StringComparison.OrdinalIgnoreCase)))
                    statusMsg += ". Non-active files were modified on disk — reopen to see changes.";
                _control.SetStatus(statusMsg);
            });
        }

        /// <summary>
        /// Replaces target text directly in an SDLXLIFF file on disk.
        /// Used for files not currently open in the editor.
        /// </summary>
        private int ReplaceInXliffFile(string filePath, List<SearchResult> results,
            string searchText, string replaceText, bool caseSensitive, bool useRegex, bool wholeWord,
            out int tagSpanSkipped)
        {
            tagSpanSkipped = 0;
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.Load(filePath);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            var root = doc.DocumentElement;
            var xliffNs = root?.NamespaceURI ?? "";
            if (!string.IsNullOrEmpty(xliffNs))
                nsMgr.AddNamespace("x", xliffNs);

            var prefix = string.IsNullOrEmpty(xliffNs) ? "" : "x:";
            int count = 0;

            foreach (var result in results)
            {
                var unit = doc.SelectSingleNode(
                    $"//{prefix}trans-unit[@id='{result.ParagraphUnitId}']", nsMgr);
                if (unit == null) continue;

                var targetNode = unit.SelectSingleNode($"{prefix}target", nsMgr);
                if (targetNode == null) continue;

                // Find the specific segment marker
                XmlNode segNode = null;
                if (!string.IsNullOrEmpty(result.SegmentId))
                {
                    segNode = targetNode.SelectSingleNode(
                        $".//{prefix}mrk[@mtype='seg'][@mid='{result.SegmentId}']", nsMgr);
                }

                var node = segNode ?? targetNode;
                var currentText = node.InnerText;
                var newText = PerformReplace(currentText, searchText, replaceText, caseSensitive, useRegex, wholeWord);

                if (newText != currentText)
                {
                    if (node.ChildNodes.Count == 1 && node.FirstChild is XmlText)
                    {
                        node.FirstChild.Value = newText;
                        result.TargetText = newText;
                        count++;
                    }
                    else
                    {
                        // The match was found in InnerText (which concatenates
                        // text across child nodes), but the segment's text is
                        // split across XmlText siblings separated by inline-tag
                        // elements. Per-text-node replace only changes nodes
                        // whose individual value contains the match. Verify
                        // every match hit a single text node before counting
                        // and saving – pre-v4.19.56 we'd always count++ and
                        // save the file even if no text-node value changed,
                        // making Replace All silently lie about its work.
                        ReplaceTextInNodes(node, searchText, replaceText, caseSensitive, useRegex, wholeWord);
                        if (node.InnerText == newText)
                        {
                            result.TargetText = newText;
                            count++;
                        }
                        else
                        {
                            tagSpanSkipped++;
                        }
                    }
                }
            }

            if (count > 0)
                doc.Save(filePath);

            return count;
        }

        private void ReplaceTextInNodes(XmlNode parent, string searchText, string replaceText,
            bool caseSensitive, bool useRegex, bool wholeWord)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child is XmlText textNode)
                {
                    var newVal = PerformReplace(textNode.Value, searchText, replaceText, caseSensitive, useRegex, wholeWord);
                    if (newVal != textNode.Value)
                        textNode.Value = newVal;
                }
                else if (child.HasChildNodes)
                {
                    ReplaceTextInNodes(child, searchText, replaceText, caseSensitive, useRegex, wholeWord);
                }
            }
        }

        // ─── Text Helpers ────────────────────────────────────────

        /// <summary>
        /// Outcome of an in-editor replace. <see cref="Replaced"/> means the
        /// segment was actually modified; <see cref="SpansInlineTags"/> means
        /// the search string straddled a tag boundary so we refused to apply
        /// a destructive flatten-and-rewrite (and the caller should report
        /// "skipped, would lose formatting" rather than counting a success).
        /// </summary>
        private enum ActiveReplaceOutcome { Replaced, NoMatch, SpansInlineTags, Error }

        /// <summary>
        /// Replaces text in the active segment pair while preserving inline
        /// tags. Pre-v4.19.56 the replace path read <c>pair.Target.ToString()</c>,
        /// did a string replace, then cleared the target and re-added a single
        /// cloned <see cref="IText"/> – which destroyed every tag pair,
        /// placeholder, and formatting span the segment originally contained.
        ///
        /// This helper instead walks the existing target's <see cref="IText"/>
        /// children and replaces each one's text in-place, so structure is
        /// preserved. If the search string straddles a tag boundary (no single
        /// IText contains the full match), the per-IText replace produces a
        /// flat result that doesn't match what a flat replace would produce –
        /// in that case we refuse to apply rather than try to be clever, and
        /// return <see cref="ActiveReplaceOutcome.SpansInlineTags"/> so the
        /// caller can surface a clear "match spans tags – skipped" message.
        /// </summary>
        private ActiveReplaceOutcome ReplaceInActiveSegmentPair(
            ISegmentPair pair, string searchText, string replaceText,
            bool caseSensitive, bool useRegex, bool wholeWord, out string newFlatTarget)
        {
            newFlatTarget = null;
            if (pair == null || _activeDocument == null) return ActiveReplaceOutcome.Error;

            var currentTarget = pair.Target?.ToString() ?? "";
            var expected = PerformReplace(currentTarget, searchText, replaceText, caseSensitive, useRegex, wholeWord);
            if (expected == currentTarget) return ActiveReplaceOutcome.NoMatch;

            // Pre-flight: simulate per-IText replacement and see if the
            // concatenated result matches the flat-replace expectation.
            // pair.Target.ToString() concatenates IText content depth-first;
            // if a per-IText replace can't reproduce the same flat output,
            // the match must straddle a tag boundary.
            var iTexts = new List<IText>();
            CollectTextsDepthFirst(pair.Target, iTexts);

            if (iTexts.Count == 0) return ActiveReplaceOutcome.SpansInlineTags;

            var simulated = string.Concat(iTexts.Select(t =>
                PerformReplace(t.Properties.Text ?? "", searchText, replaceText, caseSensitive, useRegex, wholeWord)));

            if (simulated != expected) return ActiveReplaceOutcome.SpansInlineTags;

            // Safe to apply.
            try
            {
                _activeDocument.ProcessSegmentPair(pair, "Supervertaler", (sp, cancel) =>
                {
                    var liveTexts = new List<IText>();
                    CollectTextsDepthFirst(sp.Target, liveTexts);
                    foreach (var t in liveTexts)
                    {
                        var oldVal = t.Properties.Text ?? "";
                        var newVal = PerformReplace(oldVal, searchText, replaceText, caseSensitive, useRegex, wholeWord);
                        if (!string.Equals(oldVal, newVal, StringComparison.Ordinal))
                            t.Properties.Text = newVal;
                    }
                });
                newFlatTarget = expected;
                return ActiveReplaceOutcome.Replaced;
            }
            catch
            {
                return ActiveReplaceOutcome.Error;
            }
        }

        private static void CollectTextsDepthFirst(IAbstractMarkupDataContainer container, List<IText> sink)
        {
            if (container == null) return;
            foreach (var item in container)
            {
                if (item is IText t)
                    sink.Add(t);
                else if (item is IAbstractMarkupDataContainer inner)
                    CollectTextsDepthFirst(inner, sink);
            }
        }

        /// <summary>
        /// Finds the first IText node in a segment (used as a template for cloning).
        /// Same pattern as SegmentTagHandler.FindFirstText.
        /// </summary>
        private static IText FindFirstText(ISegment segment)
        {
            if (segment == null) return null;
            foreach (var item in segment)
            {
                if (item is IText text)
                    return text;
            }
            return null;
        }

        private static string PerformReplace(string text, string search, string replace,
            bool caseSensitive, bool useRegex, bool wholeWord)
        {
            if (useRegex)
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                try { return Regex.Replace(text, search, replace, options); }
                catch { return text; }
            }
            if (wholeWord)
            {
                // Whole-word literal replace: \b boundaries. Escape $ in the
                // replacement so it stays literal (Regex.Replace treats $ specially).
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                try
                {
                    return Regex.Replace(text, @"\b" + Regex.Escape(search) + @"\b",
                        (replace ?? "").Replace("$", "$$"), options);
                }
                catch { return text; }
            }
            return ReplaceString(text, search, replace, caseSensitive);
        }

        private static string ReplaceString(string text, string search, string replace, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
                return text;

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var result = new System.Text.StringBuilder();
            int pos = 0;

            while (pos < text.Length)
            {
                int idx = text.IndexOf(search, pos, comparison);
                if (idx < 0)
                {
                    result.Append(text, pos, text.Length - pos);
                    break;
                }
                result.Append(text, pos, idx - pos);
                result.Append(replace);
                pos = idx + search.Length;
            }

            return result.ToString();
        }

        private static bool IsTargetMatch(string targetText, string search,
            bool caseSensitive, bool useRegex, bool wholeWord)
        {
            if (string.IsNullOrEmpty(targetText)) return false;

            if (useRegex)
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                try { return Regex.IsMatch(targetText, search, options); }
                catch { return false; }
            }

            if (wholeWord)
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                try { return Regex.IsMatch(targetText, @"\b" + Regex.Escape(search) + @"\b", options); }
                catch { return false; }
            }

            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return targetText.IndexOf(search, comparison) >= 0;
        }

        // ─── Helpers ─────────────────────────────────────────────

        private void SafeInvoke(Action action)
        {
            try
            {
                if (_control.InvokeRequired)
                    _control.BeginInvoke(action);
                else
                    action();
            }
            catch { }
        }
    }
}
