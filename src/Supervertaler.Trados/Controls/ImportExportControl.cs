using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Supervertaler.Trados.Core;
using Supervertaler.Trados.Core.Export;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// Tab UI for the new "Import / Export" feature in the Supervertaler
    /// Assistant pane. Lets the translator:
    ///
    /// - Export the active Trados document's bilingual segments to one of
    ///   three formats (DOCX, Markdown, HTML) in one of three layouts
    ///   (Supervertaler Bilingual Table, stacked source-on-top, stacked
    ///   target-on-top).
    /// - Round-trip a previously-exported DOCX or Markdown file back into
    ///   the active document, updating target segments where the file
    ///   contains edits.
    /// - Browse recent exports.
    ///
    /// The control is dumb-by-design — it fires events for export / import /
    /// open-folder / open-file and the hosting <c>AiAssistantViewPart</c>
    /// does the Trados SDK plumbing.
    /// </summary>
    public class ImportExportControl : UserControl
    {
        // Config controls.
        private ComboBox _cmbFormat;
        private ComboBox _cmbLayout;
        private Label _lblSegmentCount;

        // Action buttons.
        private Button _btnExport;
        private Button _btnImport;

        // History list.
        private Label _lblHistoryHeading;
        private ListView _lvHistory;
        private Button _btnOpenFile;
        private Button _btnOpenFolder;
        private Button _btnReImportSelected;

        // Log.
        private Label _lblLog;
        private TextBox _txtLog;

        // State.
        private int _totalSegments;

        // ─── Public events ────────────────────────────────────────────

        /// <summary>Fired when the user clicks the Export button.
        /// The handler is responsible for collecting segments from Trados,
        /// invoking <see cref="BilingualExporter"/>, and recording the
        /// result via <see cref="AddHistoryEntry"/>.</summary>
        public event EventHandler<ExportRequestedEventArgs> ExportRequested;

        /// <summary>Fired when the user picks a file to re-import (either via
        /// the "Re-import…" button or the history pane). The handler runs
        /// <see cref="BilingualImporter"/> and applies the resulting diffs to
        /// the active Trados document.</summary>
        public event EventHandler<ImportRequestedEventArgs> ImportRequested;

        /// <summary>Fired when the user clicks "Open file" on a history entry.</summary>
        public event EventHandler<string> OpenFileRequested;

        /// <summary>Fired when the user clicks "Open folder" on a history entry.</summary>
        public event EventHandler<string> OpenFolderRequested;

        // ─── Construction ─────────────────────────────────────────────

        public ImportExportControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            SuspendLayout();
            BackColor = Color.White;
            Font = new Font("Segoe UI", UiScale.FontSize(8.5f));

            var bodyFont = new Font("Segoe UI", UiScale.FontSize(8.5f));
            var labelColor = Color.FromArgb(60, 60, 60);
            int leftMargin = UiScale.Pixels(12);
            int y = UiScale.Pixels(10);

            // ─── Header ──────────────────────────────────────────────
            var lblHeader = new Label
            {
                Text = "Import / Export",
                Location = new Point(leftMargin, y),
                AutoSize = true,
                Font = new Font("Segoe UI", UiScale.FontSize(11f), FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 20, 20)
            };
            Controls.Add(lblHeader);
            y += UiScale.Pixels(28);

            var lblBlurb = new Label
            {
                Text = "Export bilingual files for proofreaders or clients, then re-import edits back into Trados.",
                Location = new Point(leftMargin, y),
                AutoSize = false,
                Width = UiScale.Pixels(540),
                Height = UiScale.Pixels(32),
                Font = bodyFont,
                ForeColor = labelColor
            };
            Controls.Add(lblBlurb);
            y += UiScale.Pixels(36);

            // ─── Format row ──────────────────────────────────────────
            var lblFormat = new Label
            {
                Text = "Format:",
                Location = new Point(leftMargin, y + UiScale.Pixels(4)),
                AutoSize = false,
                Width = UiScale.Pixels(70),
                Font = bodyFont,
                ForeColor = labelColor
            };
            Controls.Add(lblFormat);

            _cmbFormat = new ComboBox
            {
                Location = new Point(leftMargin + UiScale.Pixels(72), y),
                Width = UiScale.Pixels(220),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = bodyFont
            };
            _cmbFormat.Items.Add("Word document (.docx)");
            _cmbFormat.Items.Add("Markdown (.md)");
            _cmbFormat.Items.Add("HTML report (.html, read-only)");
            _cmbFormat.SelectedIndex = 0;
            Controls.Add(_cmbFormat);
            y += UiScale.Pixels(30);

            // ─── Layout row ──────────────────────────────────────────
            var lblLayout = new Label
            {
                Text = "Layout:",
                Location = new Point(leftMargin, y + UiScale.Pixels(4)),
                AutoSize = false,
                Width = UiScale.Pixels(70),
                Font = bodyFont,
                ForeColor = labelColor
            };
            Controls.Add(lblLayout);

            _cmbLayout = new ComboBox
            {
                Location = new Point(leftMargin + UiScale.Pixels(72), y),
                Width = UiScale.Pixels(330),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = bodyFont
            };
            _cmbLayout.Items.Add("Supervertaler Bilingual Table (5 columns, round-trippable)");
            _cmbLayout.Items.Add("Stacked — source on top, target below");
            _cmbLayout.Items.Add("Stacked — target on top, source below");
            _cmbLayout.SelectedIndex = 0;
            Controls.Add(_cmbLayout);
            y += UiScale.Pixels(32);

            // ─── Segment count + action buttons ──────────────────────
            _lblSegmentCount = new Label
            {
                Text = "Segments: 0",
                Location = new Point(leftMargin, y + UiScale.Pixels(4)),
                AutoSize = true,
                Font = bodyFont,
                ForeColor = labelColor
            };
            Controls.Add(_lblSegmentCount);

            _btnExport = new Button
            {
                Text = "📤  Export",
                Location = new Point(leftMargin + UiScale.Pixels(150), y),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                MinimumSize = new Size(UiScale.Pixels(110), UiScale.Pixels(28)),
                FlatStyle = FlatStyle.System,
                Font = bodyFont
            };
            _btnExport.Click += (s, e) => RaiseExport();
            Controls.Add(_btnExport);

            _btnImport = new Button
            {
                Text = "📥  Re-import…",
                Location = new Point(_btnExport.Right + UiScale.Pixels(8), y),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                MinimumSize = new Size(UiScale.Pixels(140), UiScale.Pixels(28)),
                FlatStyle = FlatStyle.System,
                Font = bodyFont
            };
            _btnImport.Click += (s, e) => OnImportButton();
            Controls.Add(_btnImport);
            y += UiScale.Pixels(40);

            // ─── History heading + list ──────────────────────────────
            _lblHistoryHeading = new Label
            {
                Text = "Recent exports",
                Location = new Point(leftMargin, y),
                AutoSize = true,
                Font = new Font("Segoe UI", UiScale.FontSize(9f), FontStyle.Bold),
                ForeColor = labelColor
            };
            Controls.Add(_lblHistoryHeading);
            y += UiScale.Pixels(22);

            _lvHistory = new ListView
            {
                Location = new Point(leftMargin, y),
                Width = UiScale.Pixels(540),
                Height = UiScale.Pixels(150),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = bodyFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _lvHistory.Columns.Add("When", UiScale.Pixels(130));
            _lvHistory.Columns.Add("Format", UiScale.Pixels(70));
            _lvHistory.Columns.Add("File", UiScale.Pixels(330));
            Controls.Add(_lvHistory);
            y += _lvHistory.Height + UiScale.Pixels(6);

            _btnOpenFile = new Button
            {
                Text = "Open file",
                Location = new Point(leftMargin, y),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                MinimumSize = new Size(UiScale.Pixels(90), UiScale.Pixels(26)),
                FlatStyle = FlatStyle.System,
                Font = bodyFont
            };
            _btnOpenFile.Click += (s, e) => OnOpenFileClicked();
            Controls.Add(_btnOpenFile);

            _btnOpenFolder = new Button
            {
                Text = "Open folder",
                Location = new Point(_btnOpenFile.Right + UiScale.Pixels(8), y),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                MinimumSize = new Size(UiScale.Pixels(100), UiScale.Pixels(26)),
                FlatStyle = FlatStyle.System,
                Font = bodyFont
            };
            _btnOpenFolder.Click += (s, e) => OnOpenFolderClicked();
            Controls.Add(_btnOpenFolder);

            _btnReImportSelected = new Button
            {
                Text = "Re-import this",
                Location = new Point(_btnOpenFolder.Right + UiScale.Pixels(8), y),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowOnly,
                MinimumSize = new Size(UiScale.Pixels(110), UiScale.Pixels(26)),
                FlatStyle = FlatStyle.System,
                Font = bodyFont
            };
            _btnReImportSelected.Click += (s, e) => OnReImportSelectedClicked();
            Controls.Add(_btnReImportSelected);
            y += UiScale.Pixels(34);

            // ─── Log ─────────────────────────────────────────────────
            _lblLog = new Label
            {
                Text = "Log:",
                Location = new Point(leftMargin, y),
                AutoSize = true,
                Font = bodyFont,
                ForeColor = labelColor
            };
            Controls.Add(_lblLog);
            y += UiScale.Pixels(18);

            _txtLog = new TextBox
            {
                Location = new Point(leftMargin, y),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", UiScale.FontSize(8.5f)),
                BackColor = Color.FromArgb(248, 248, 248),
                ForeColor = Color.FromArgb(60, 60, 60),
                BorderStyle = BorderStyle.FixedSingle,
                WordWrap = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(_txtLog);

            ResumeLayout(false);

            Resize += OnResize;
            OnResize(this, EventArgs.Empty);
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (_txtLog == null) return;
            int rightPad = UiScale.Pixels(24);
            int wAvail = Math.Max(UiScale.Pixels(100), Width - rightPad);

            _lvHistory.Width = wAvail;
            _txtLog.Width = wAvail;
            _txtLog.Height = Math.Max(UiScale.Pixels(60), Height - _txtLog.Top - UiScale.Pixels(8));
        }

        // ─── Public API ───────────────────────────────────────────────

        public void UpdateSegmentCount(int total)
        {
            _totalSegments = total;
            SafeInvoke(() => _lblSegmentCount.Text = "Segments: " + total);
        }

        public void AppendLog(string message, bool isError = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            SafeInvoke(() =>
            {
                _txtLog.AppendText((isError ? "[ERROR] " : "") + message + Environment.NewLine);
            });
        }

        public void ClearLog()
        {
            SafeInvoke(() => _txtLog.Clear());
        }

        public void SetBusy(bool busy)
        {
            SafeInvoke(() =>
            {
                _btnExport.Enabled = !busy;
                _btnImport.Enabled = !busy;
                _btnReImportSelected.Enabled = !busy;
                Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            });
        }

        /// <summary>Add a row to the recent-exports list.</summary>
        public void AddHistoryEntry(DateTime whenLocal, string format, string filePath)
        {
            SafeInvoke(() =>
            {
                var item = new ListViewItem(whenLocal.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(format);
                item.SubItems.Add(filePath);
                item.Tag = filePath;
                _lvHistory.Items.Insert(0, item);

                // Keep history bounded.
                while (_lvHistory.Items.Count > 30)
                    _lvHistory.Items.RemoveAt(_lvHistory.Items.Count - 1);
            });
        }

        public void LoadHistoryEntries(IEnumerable<HistoryEntry> entries)
        {
            SafeInvoke(() =>
            {
                _lvHistory.Items.Clear();
                foreach (var e in entries)
                {
                    var item = new ListViewItem(e.WhenLocal.ToString("yyyy-MM-dd HH:mm"));
                    item.SubItems.Add(e.Format);
                    item.SubItems.Add(e.FilePath);
                    item.Tag = e.FilePath;
                    _lvHistory.Items.Add(item);
                }
            });
        }

        // ─── Event raisers ────────────────────────────────────────────

        private void RaiseExport()
        {
            var opts = new ExportOptions
            {
                Format = SelectedFormat(),
                Layout = SelectedLayout()
            };

            var args = new ExportRequestedEventArgs(opts);
            ExportRequested?.Invoke(this, args);
        }

        private void OnImportButton()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Supported (*.docx;*.md;*.markdown)|*.docx;*.md;*.markdown|Word documents (*.docx)|*.docx|Markdown (*.md;*.markdown)|*.md;*.markdown|All files|*.*";
                dlg.Title = "Choose a Supervertaler bilingual file to re-import";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    ImportRequested?.Invoke(this, new ImportRequestedEventArgs(dlg.FileName));
                }
            }
        }

        private void OnOpenFileClicked()
        {
            var path = SelectedHistoryPath();
            if (string.IsNullOrEmpty(path)) return;
            OpenFileRequested?.Invoke(this, path);
        }

        private void OnOpenFolderClicked()
        {
            var path = SelectedHistoryPath();
            if (string.IsNullOrEmpty(path)) return;
            OpenFolderRequested?.Invoke(this, path);
        }

        private void OnReImportSelectedClicked()
        {
            var path = SelectedHistoryPath();
            if (string.IsNullOrEmpty(path)) return;
            ImportRequested?.Invoke(this, new ImportRequestedEventArgs(path));
        }

        private string SelectedHistoryPath()
        {
            if (_lvHistory.SelectedItems.Count == 0) return null;
            return _lvHistory.SelectedItems[0].Tag as string;
        }

        // ─── Selection mapping ────────────────────────────────────────

        private ExportFormat SelectedFormat()
        {
            switch (_cmbFormat.SelectedIndex)
            {
                case 0: return ExportFormat.Docx;
                case 1: return ExportFormat.Markdown;
                case 2: return ExportFormat.Html;
                default: return ExportFormat.Docx;
            }
        }

        private ExportLayout SelectedLayout()
        {
            switch (_cmbLayout.SelectedIndex)
            {
                case 0: return ExportLayout.Table;
                case 1: return ExportLayout.StackedSourceTop;
                case 2: return ExportLayout.StackedTargetTop;
                default: return ExportLayout.Table;
            }
        }

        private void SafeInvoke(Action a)
        {
            if (a == null) return;
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(a); } catch { } }
            else a();
        }

        // ─── Nested helper types ──────────────────────────────────────

        public class HistoryEntry
        {
            public DateTime WhenLocal { get; set; }
            public string Format { get; set; }
            public string FilePath { get; set; }
        }
    }

    public class ExportRequestedEventArgs : EventArgs
    {
        public ExportOptions Options { get; }
        public ExportRequestedEventArgs(ExportOptions options) { Options = options; }
    }

    public class ImportRequestedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public ImportRequestedEventArgs(string filePath) { FilePath = filePath; }
    }
}
