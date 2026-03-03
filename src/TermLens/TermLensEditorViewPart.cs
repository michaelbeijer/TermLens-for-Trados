using System;
using System.IO;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.Desktop.IntegrationApi.Interfaces;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using TermLens.Controls;
using TermLens.Settings;

namespace TermLens
{
    /// <summary>
    /// Trados Studio editor ViewPart that docks the TermLens panel below the editor.
    /// Listens to segment changes and updates the terminology display accordingly.
    /// </summary>
    [ViewPart(
        Id = "TermLensEditorViewPart",
        Name = "TermLens",
        Description = "Inline terminology display \u2014 shows source text with translations underneath matched terms",
        Icon = "TermLensIcon"
    )]
    [ViewPartLayout(typeof(EditorController), Dock = DockType.Bottom)]
    public class TermLensEditorViewPart : AbstractViewPartController
    {
        private static readonly Lazy<TermLensControl> _control =
            new Lazy<TermLensControl>(() => new TermLensControl());

        private EditorController _editorController;
        private IStudioDocument _activeDocument;
        private TermLensSettings _settings;

        protected override IUIControl GetContentControl()
        {
            return _control.Value;
        }

        protected override void Initialize()
        {
            // Load persisted settings
            _settings = TermLensSettings.Load();

            _editorController = SdlTradosStudio.Application.GetController<EditorController>();

            if (_editorController != null)
            {
                _editorController.ActiveDocumentChanged += OnActiveDocumentChanged;

                // If a document is already open, wire up to it immediately
                if (_editorController.ActiveDocument != null)
                {
                    _activeDocument = _editorController.ActiveDocument;
                    _activeDocument.ActiveSegmentChanged += OnActiveSegmentChanged;
                }
            }

            // Wire up term insertion — when user clicks a translation in the panel
            _control.Value.TermInsertRequested += OnTermInsertRequested;

            // Wire up the gear/settings button
            _control.Value.SettingsRequested += OnSettingsRequested;

            // Load termbase: prefer saved setting, fall back to auto-detect
            LoadTermbase();

            // Display the current segment immediately (even without a termbase, show all words)
            UpdateFromActiveSegment();
        }

        private void LoadTermbase()
        {
            // 1. Use the saved termbase path if set and the file exists
            if (!string.IsNullOrEmpty(_settings.TermbasePath) && File.Exists(_settings.TermbasePath))
            {
                _control.Value.LoadTermbase(_settings.TermbasePath);
                return;
            }

            // 2. Fallback: auto-detect Supervertaler's default locations
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Supervertaler_Data", "resources", "supervertaler.db"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Supervertaler", "resources", "supervertaler.db"),
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    _control.Value.LoadTermbase(path);
                    return;
                }
            }
        }

        private void OnSettingsRequested(object sender, EventArgs e)
        {
            SafeInvoke(() =>
            {
                using (var form = new TermLensSettingsForm(_settings))
                {
                    // Find a parent window handle for proper dialog parenting
                    var parent = _control.Value.FindForm();
                    var result = parent != null
                        ? form.ShowDialog(parent)
                        : form.ShowDialog();

                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        // Settings already saved inside the form's OK handler.
                        // Reload the termbase with the (possibly changed) path.
                        LoadTermbase();
                        UpdateFromActiveSegment();
                    }
                }
            });
        }

        private void OnActiveDocumentChanged(object sender, DocumentEventArgs e)
        {
            if (_activeDocument != null)
                _activeDocument.ActiveSegmentChanged -= OnActiveSegmentChanged;

            _activeDocument = _editorController?.ActiveDocument;

            if (_activeDocument != null)
            {
                _activeDocument.ActiveSegmentChanged += OnActiveSegmentChanged;
                UpdateFromActiveSegment();
            }
            else
            {
                SafeInvoke(() => _control.Value.Clear());
            }
        }

        private void OnActiveSegmentChanged(object sender, EventArgs e)
        {
            UpdateFromActiveSegment();
        }

        private void UpdateFromActiveSegment()
        {
            if (_activeDocument?.ActiveSegmentPair == null)
            {
                SafeInvoke(() => _control.Value.Clear());
                return;
            }

            try
            {
                var sourceSegment = _activeDocument.ActiveSegmentPair.Source;
                var sourceText = sourceSegment?.ToString() ?? "";
                SafeInvoke(() => _control.Value.UpdateSegment(sourceText));
            }
            catch (Exception)
            {
                // Silently handle — segment may not be available during transitions
            }
        }

        private void SafeInvoke(Action action)
        {
            var ctrl = _control.Value;
            if (ctrl.InvokeRequired)
                ctrl.BeginInvoke(action);
            else
                action();
        }

        private void OnTermInsertRequested(object sender, TermInsertEventArgs e)
        {
            if (_activeDocument == null || string.IsNullOrEmpty(e.TargetTerm))
                return;

            try
            {
                _activeDocument.Selection.Target.Replace(e.TargetTerm, "TermLens");
            }
            catch (Exception)
            {
                // Silently handle — editor may not allow insertion at this moment
            }
        }

        public override void Dispose()
        {
            if (_activeDocument != null)
                _activeDocument.ActiveSegmentChanged -= OnActiveSegmentChanged;

            if (_editorController != null)
                _editorController.ActiveDocumentChanged -= OnActiveDocumentChanged;

            base.Dispose();
        }
    }
}
