using System.Drawing;
using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi.Interfaces;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// Top-level container for the Supervertaler ViewPart.
    /// Hosts a TabControl with tabs for each feature: TermLens (glossary),
    /// AI Assistant, Batch Translate, etc.
    /// </summary>
    public class MainPanelControl : UserControl, IUIControl
    {
        private readonly TabControl _tabControl;

        public MainPanelControl(TermLensControl termLensControl)
        {
            SuspendLayout();

            BackColor = Color.White;

            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f),
                Padding = new Point(6, 3),
            };

            // TermLens tab (glossary)
            var termLensPage = new TabPage("TermLens");
            termLensControl.Dock = DockStyle.Fill;
            termLensPage.Controls.Add(termLensControl);
            _tabControl.TabPages.Add(termLensPage);

            // Placeholder tabs for upcoming features
            var aiAssistantPage = new TabPage("AI Assistant");
            aiAssistantPage.Controls.Add(CreatePlaceholderLabel("AI Assistant \u2014 coming soon"));
            _tabControl.TabPages.Add(aiAssistantPage);

            var batchPage = new TabPage("Batch Translate");
            batchPage.Controls.Add(CreatePlaceholderLabel("Batch Translate \u2014 coming soon"));
            _tabControl.TabPages.Add(batchPage);

            Controls.Add(_tabControl);

            ResumeLayout(false);
        }

        private static Label CreatePlaceholderLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 10f, FontStyle.Italic),
            };
        }
    }
}
