using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi.Interfaces;
using TermLens.Core;
using TermLens.Models;

namespace TermLens.Controls
{
    /// <summary>
    /// Main TermLens panel control. Renders the source segment as a flowing
    /// word-by-word display with terminology translations underneath matched terms.
    /// Port of Supervertaler's TermLensWidget.
    /// </summary>
    public class TermLensControl : UserControl, IUIControl
    {
        private readonly FlowLayoutPanel _flowPanel;
        private readonly Label _statusLabel;
        private readonly Panel _headerPanel;
        private readonly Label _headerLabel;

        private TermMatcher _matcher;
        private TermbaseReader _reader;
        private string _currentDbPath;

        /// <summary>
        /// Fired when the user clicks a translation to insert it into the target segment.
        /// </summary>
        public event EventHandler<TermInsertEventArgs> TermInsertRequested;

        /// <summary>
        /// Fired when the user clicks the gear/settings button in the header.
        /// </summary>
        public event EventHandler SettingsRequested;

        public TermLensControl()
        {
            SuspendLayout();

            BackColor = Color.White;

            // Header bar
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24,
                BackColor = Color.FromArgb(245, 245, 245),
                Padding = new Padding(6, 3, 6, 3)
            };

            _headerLabel = new Label
            {
                Text = "TermLens",
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            _headerPanel.Controls.Add(_headerLabel);

            // Gear button (right side of header)
            var btnSettings = new Button
            {
                Text = "\u2699",  // ⚙ gear character
                Dock = DockStyle.Right,
                Width = 24,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(100, 100, 100),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 220, 220);
            btnSettings.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            _headerPanel.Controls.Add(btnSettings);

            // Status label (right of header, left of gear button)
            _statusLabel = new Label
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(120, 120, 120),
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 4, 0)
            };
            _headerPanel.Controls.Add(_statusLabel);

            // Main flow panel for term blocks
            _flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                Padding = new Padding(4),
                BackColor = Color.White,
                FlowDirection = FlowDirection.LeftToRight
            };

            Controls.Add(_flowPanel);
            Controls.Add(_headerPanel);

            _matcher = new TermMatcher();

            ResumeLayout(false);
        }

        /// <summary>
        /// Loads a Supervertaler termbase database.
        /// </summary>
        public bool LoadTermbase(string dbPath)
        {
            if (dbPath == _currentDbPath && _reader != null)
                return true;

            _reader?.Dispose();
            _reader = new TermbaseReader(dbPath);

            if (!_reader.Open())
            {
                _statusLabel.Text = "Failed to open termbase";
                return false;
            }

            // Build in-memory index for fast matching
            var index = _reader.LoadAllTerms();
            _matcher.LoadIndex(index);

            var termbases = _reader.GetTermbases();
            int totalTerms = termbases.Sum(tb => tb.TermCount);
            _statusLabel.Text = $"{termbases.Count} termbases, {totalTerms} terms";
            _currentDbPath = dbPath;

            return true;
        }

        /// <summary>
        /// Updates the display with a new source segment.
        /// Call this when the active segment changes in Trados Studio.
        /// </summary>
        public void UpdateSegment(string sourceText)
        {
            _flowPanel.SuspendLayout();
            _flowPanel.Controls.Clear();

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                _statusLabel.Text = "";
                _flowPanel.ResumeLayout(true);
                return;
            }

            var tokens = _matcher.Tokenize(sourceText);

            int matchCount = 0;
            int wordCount = 0;
            int shortcutIndex = 0;

            foreach (var token in tokens)
            {
                if (token.IsLineBreak)
                {
                    // Force a line break in the flow layout
                    _flowPanel.SetFlowBreak(
                        _flowPanel.Controls.Count > 0
                            ? _flowPanel.Controls[_flowPanel.Controls.Count - 1]
                            : null,
                        true);
                    continue;
                }

                wordCount++;

                if (token.HasMatch)
                {
                    var block = new TermBlock(token.Text, token.Matches, shortcutIndex)
                    {
                        Font = Font,
                        Margin = new Padding(2, 1, 2, 1)
                    };

                    block.TermInsertRequested += (s, args) => TermInsertRequested?.Invoke(s, args);
                    _flowPanel.Controls.Add(block);

                    matchCount++;
                    shortcutIndex++;
                }
                else
                {
                    var label = new WordLabel(token.Text)
                    {
                        Font = Font,
                        Margin = new Padding(2, 4, 2, 4)
                    };
                    _flowPanel.Controls.Add(label);
                }
            }

            _statusLabel.Text = matchCount > 0
                ? $"\u2713 Found {matchCount} terms in {wordCount} words"
                : $"{wordCount} words, no matches";

            _flowPanel.ResumeLayout(true);
        }

        /// <summary>
        /// Clears the display.
        /// </summary>
        public void Clear()
        {
            _flowPanel.Controls.Clear();
            _statusLabel.Text = "";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
