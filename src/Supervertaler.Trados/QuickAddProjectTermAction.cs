using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Sdl.TranslationStudioAutomation.IntegrationApi.Presentation.DefaultLocations;
using Supervertaler.Trados.Core;
using Supervertaler.Trados.Settings;

namespace Supervertaler.Trados
{
    /// <summary>
    /// Keyboard-only action: "Quick add Term to Project Glossary".
    /// Responds to Alt+Up. Extracts selected source/target text and inserts
    /// the term directly into the project glossary, bypassing the AddTermDialog.
    /// </summary>
    [Action("TermLens_QuickAddProjectTerm", typeof(EditorController),
        Name = "Quick add Term to project glossary",
        Description = "Quickly add the selected source/target text to the project glossary (no dialog)")]
    [ActionLayout(
        typeof(TranslationStudioDefaultContextMenus.EditorDocumentContextMenuLocation), 7,
        DisplayType.Default, "", false)]
    [Shortcut(Keys.Alt | Keys.Up)]
    public class QuickAddProjectTermAction : AbstractAction
    {
        protected override void Execute()
        {
            try
            {
                var editorController = SdlTradosStudio.Application.GetController<EditorController>();
                var doc = editorController?.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("No document is open.",
                        "TermLens", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var settings = TermLensSettings.Load();

                // Validate project glossary is configured
                if (settings.ProjectTermbaseId < 0)
                {
                    MessageBox.Show(
                        "No project glossary is configured.\n\n" +
                        "Open TermLens settings (gear icon) and check the \u201cProject\u201d column " +
                        "for the glossary that should receive project-specific terms.",
                        "TermLens \u2014 Quick Add to Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Validate termbase path
                if (string.IsNullOrEmpty(settings.TermbasePath) || !File.Exists(settings.TermbasePath))
                {
                    MessageBox.Show(
                        "Termbase file not found. Please check the TermLens settings.",
                        "TermLens \u2014 Quick Add to Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get text from source and target segments
                string sourceText = "";
                string targetText = "";

                try
                {
                    // Try to get selected text first, fall back to full segment
                    if (doc.ActiveSegmentPair?.Source != null)
                        sourceText = doc.ActiveSegmentPair.Source.ToString() ?? "";
                    if (doc.ActiveSegmentPair?.Target != null)
                        targetText = doc.ActiveSegmentPair.Target.ToString() ?? "";

                    // If there is an active selection, prefer it
                    var selection = doc.Selection;
                    if (selection != null)
                    {
                        try
                        {
                            if (selection.Source != null)
                            {
                                var srcSel = selection.Source.ToString();
                                if (!string.IsNullOrWhiteSpace(srcSel))
                                    sourceText = srcSel;
                            }
                        }
                        catch { /* Selection may not be available */ }

                        try
                        {
                            if (selection.Target != null)
                            {
                                var tgtSel = selection.Target.ToString();
                                if (!string.IsNullOrWhiteSpace(tgtSel))
                                    targetText = tgtSel;
                            }
                        }
                        catch { /* Selection may not be available */ }
                    }
                }
                catch
                {
                    // Fall back to full segment text
                    if (doc.ActiveSegmentPair?.Source != null)
                        sourceText = doc.ActiveSegmentPair.Source.ToString() ?? "";
                    if (doc.ActiveSegmentPair?.Target != null)
                        targetText = doc.ActiveSegmentPair.Target.ToString() ?? "";
                }

                sourceText = sourceText.Trim();
                targetText = targetText.Trim();

                // Validate we have text to work with
                if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(targetText))
                {
                    MessageBox.Show(
                        "Both source and target text are required.\n\n" +
                        "Make sure you have an active segment with text in both " +
                        "the source and target columns.",
                        "TermLens \u2014 Quick Add to Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get project termbase metadata
                Models.TermbaseInfo projectTermbase = null;
                using (var reader = new TermbaseReader(settings.TermbasePath))
                {
                    if (reader.Open())
                        projectTermbase = reader.GetTermbaseById(settings.ProjectTermbaseId);
                }

                if (projectTermbase == null)
                {
                    MessageBox.Show(
                        "The configured project glossary was not found in the database.\n" +
                        "Please check the TermLens settings.",
                        "TermLens \u2014 Quick Add to Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Insert the term directly — no dialog
                try
                {
                    var newId = TermbaseReader.InsertTerm(
                        settings.TermbasePath,
                        settings.ProjectTermbaseId,
                        sourceText,
                        targetText,
                        projectTermbase.SourceLang,
                        projectTermbase.TargetLang,
                        ""); // No definition for quick-add

                    if (newId > 0)
                    {
                        // Incremental index update — no full DB reload
                        var entry = new Models.TermEntry
                        {
                            Id = newId,
                            SourceTerm = sourceText,
                            TargetTerm = targetText,
                            SourceLang = projectTermbase.SourceLang,
                            TargetLang = projectTermbase.TargetLang,
                            TermbaseId = projectTermbase.Id,
                            TermbaseName = projectTermbase.Name,
                            IsProjectTermbase = projectTermbase.IsProjectTermbase,
                            Ranking = projectTermbase.Ranking,
                            Definition = "",
                            Domain = "",
                            Notes = "",
                            Forbidden = false,
                            CaseSensitive = false,
                            TargetSynonyms = new List<string>()
                        };
                        TermLensEditorViewPart.NotifyTermInserted(
                            new List<Models.TermEntry> { entry });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to add term: {ex.Message}\n\n" +
                        "The database may be locked by another application.",
                        "TermLens \u2014 Quick Add to Project",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}",
                    "TermLens", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
