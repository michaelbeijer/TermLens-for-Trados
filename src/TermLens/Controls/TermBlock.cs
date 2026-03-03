using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TermLens.Models;

namespace TermLens.Controls
{
    /// <summary>
    /// Displays a single source word/phrase with its translation(s) underneath.
    /// Port of Supervertaler's TermBlock widget.
    ///
    /// Layout:
    ///   ┌──────────────────────┐
    ///   │  source_text         │
    ///   │  target_translation  │
    ///   │  [+N] shortcut badge │
    ///   └──────────────────────┘
    /// </summary>
    public class TermBlock : Control
    {
        // Colors matching Supervertaler's scheme
        private static readonly Color ProjectBg = ColorTranslator.FromHtml("#FFE5F0");
        private static readonly Color ProjectHover = ColorTranslator.FromHtml("#FFD0E8");
        private static readonly Color RegularBg = ColorTranslator.FromHtml("#D6EBFF");
        private static readonly Color RegularHover = ColorTranslator.FromHtml("#BBDEFB");
        private static readonly Color SeparatorColor = Color.FromArgb(180, 180, 180);

        private bool _isHovered;
        private readonly List<TermEntry> _entries;
        private readonly string _sourceText;
        private readonly int _shortcutIndex; // -1 = no shortcut

        public event EventHandler<TermInsertEventArgs> TermInsertRequested;

        public TermBlock(string sourceText, List<TermEntry> entries, int shortcutIndex = -1)
        {
            _sourceText = sourceText;
            _entries = entries ?? new List<TermEntry>();
            _shortcutIndex = shortcutIndex;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Cursor = Cursors.Hand;
            CalculateSize();
        }

        public bool IsProjectTermbase => _entries.Count > 0 && _entries[0].IsProjectTermbase;
        public TermEntry PrimaryEntry => _entries.Count > 0 ? _entries[0] : null;

        private void CalculateSize()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            {
                var sourceSize = g.MeasureString(_sourceText, SourceFont);
                var targetText = PrimaryEntry?.TargetTerm ?? "";
                var targetSize = g.MeasureString(targetText, TargetFont);

                int badgeWidth = 0;
                if (_shortcutIndex >= 0)
                    badgeWidth = 18;

                int extraCount = _entries.Count - 1;
                int extraWidth = 0;
                if (extraCount > 0)
                    extraWidth = (int)g.MeasureString($"+{extraCount}", BadgeFont).Width + 4;

                int width = (int)Math.Max(sourceSize.Width, targetSize.Width + badgeWidth + extraWidth) + 10;
                int height = (int)(sourceSize.Height + targetSize.Height) + 8;

                Size = new Size(width, Math.Max(height, 28));
            }
        }

        private Font SourceFont => new Font(Font.FontFamily, Font.Size, FontStyle.Regular);
        private Font TargetFont => new Font(Font.FontFamily, Font.Size, FontStyle.Regular);
        private Font BadgeFont => new Font(Font.FontFamily, Font.Size - 1, FontStyle.Bold);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bgColor = IsProjectTermbase
                ? (_isHovered ? ProjectHover : ProjectBg)
                : (_isHovered ? RegularHover : RegularBg);

            // Background with rounded corners
            using (var brush = new SolidBrush(bgColor))
            using (var path = RoundedRect(ClientRectangle, 3))
            {
                g.FillPath(brush, path);
            }

            // Thin separator line at top
            using (var pen = new Pen(SeparatorColor, 1))
            {
                g.DrawLine(pen, 3, 1, Width - 4, 1);
            }

            // Source text
            float y = 3;
            using (var brush = new SolidBrush(Color.FromArgb(60, 60, 60)))
            {
                g.DrawString(_sourceText, SourceFont, brush, 4, y);
                y += g.MeasureString(_sourceText, SourceFont).Height;
            }

            // Target translation
            var targetText = PrimaryEntry?.TargetTerm ?? "";
            float targetX = 4;

            // Shortcut badge
            if (_shortcutIndex >= 0)
            {
                var badgeText = (_shortcutIndex + 1).ToString();
                var badgeSize = g.MeasureString(badgeText, BadgeFont);
                var badgeRect = new RectangleF(targetX, y, badgeSize.Width + 4, badgeSize.Height);

                using (var brush = new SolidBrush(Color.FromArgb(100, 100, 100)))
                using (var bgBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                {
                    g.FillRectangle(bgBrush, badgeRect);
                    g.DrawString(badgeText, BadgeFont, brush, targetX + 2, y);
                }
                targetX += badgeSize.Width + 6;
            }

            using (var brush = new SolidBrush(Color.FromArgb(20, 20, 20)))
            {
                g.DrawString(targetText, TargetFont, brush, targetX, y);
                targetX += g.MeasureString(targetText, TargetFont).Width + 2;
            }

            // "+N" indicator for multiple translations
            int extraCount = _entries.Count - 1;
            if (extraCount > 0)
            {
                var extraText = $"+{extraCount}";
                using (var brush = new SolidBrush(Color.FromArgb(120, 120, 120)))
                {
                    g.DrawString(extraText, BadgeFont, brush, targetX, y);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();

            // Show tooltip with all translations and metadata
            if (_entries.Count > 0)
            {
                var lines = new List<string>();
                foreach (var entry in _entries)
                {
                    var line = $"{entry.SourceTerm} \u2192 {entry.TargetTerm}";
                    if (!string.IsNullOrEmpty(entry.TermbaseName))
                        line += $" [{entry.TermbaseName}]";
                    lines.Add(line);

                    foreach (var syn in entry.TargetSynonyms)
                        lines.Add($"  \u2022 {syn}");

                    if (!string.IsNullOrEmpty(entry.Definition))
                        lines.Add($"  Def: {entry.Definition}");
                }

                var tip = new ToolTip { AutoPopDelay = 10000 };
                tip.SetToolTip(this, string.Join("\n", lines));
            }

            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            if (PrimaryEntry != null)
            {
                TermInsertRequested?.Invoke(this, new TermInsertEventArgs
                {
                    TargetTerm = PrimaryEntry.TargetTerm,
                    Entry = PrimaryEntry
                });
            }
            base.OnClick(e);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Displays a plain (unmatched) word in the segment flow.
    /// </summary>
    public class WordLabel : Label
    {
        public WordLabel(string text)
        {
            Text = text;
            AutoSize = true;
            ForeColor = Color.FromArgb(100, 100, 100);
            Padding = new Padding(2, 4, 2, 4);
            Margin = new Padding(1, 0, 1, 0);
        }
    }

    public class TermInsertEventArgs : EventArgs
    {
        public string TargetTerm { get; set; }
        public TermEntry Entry { get; set; }
    }
}
