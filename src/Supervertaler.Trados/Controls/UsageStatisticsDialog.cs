using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Supervertaler.Trados.Controls
{
    /// <summary>
    /// One-time opt-in dialog for anonymous usage statistics.
    /// Shown once after install/update; the user's choice is saved and can be
    /// changed at any time in Settings.
    ///
    /// DialogResult.Yes  = user opted in
    /// DialogResult.No   = user declined
    /// </summary>
    internal sealed class UsageStatisticsDialog : Form
    {
        private const string HelpUrl =
            "https://supervertaler.gitbook.io/trados/settings/usage-statistics";

        public UsageStatisticsDialog()
        {
            SuspendLayout();

            Text = "Supervertaler for Trados";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            HelpButton = true;
            ClientSize = new Size(460, 260);
            Font = new Font("Segoe UI", 9F);

            HelpButtonClicked += (s, e) =>
            {
                e.Cancel = true; // prevent the cursor from changing to ?
                try { Process.Start(new ProcessStartInfo(HelpUrl) { UseShellExecute = true }); }
                catch { }
            };

            var lblTitle = new Label
            {
                Text = "Help improve Supervertaler",
                Location = new Point(20, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30)
            };

            var lblBody = new Label
            {
                Text = "Would you like to share anonymous usage statistics to help " +
                       "improve the plugin?\n\n" +
                       "Only the following is sent — once per session, on startup:\n" +
                       "  •  Plugin version\n" +
                       "  •  OS and Trados Studio version\n" +
                       "  •  System locale\n\n" +
                       "No personal data, translation content, or termbase info is " +
                       "ever collected. You can change this at any time in Settings.",
                Location = new Point(20, 46),
                Size = new Size(420, 140),
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            var lnkLearnMore = new LinkLabel
            {
                Text = "Learn more about what is collected",
                Location = new Point(20, 188),
                AutoSize = true,
                LinkColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.FromArgb(37, 99, 235)
            };
            lnkLearnMore.LinkClicked += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(HelpUrl) { UseShellExecute = true }); }
                catch { }
            };

            var btnYes = new Button
            {
                Text = "Yes, share statistics",
                DialogResult = DialogResult.Yes,
                Location = new Point(170, 224),
                Size = new Size(140, 32),
                FlatStyle = FlatStyle.System
            };

            var btnNo = new Button
            {
                Text = "No thanks",
                DialogResult = DialogResult.No,
                Location = new Point(320, 224),
                Size = new Size(120, 32),
                FlatStyle = FlatStyle.System
            };

            AcceptButton = btnYes;
            CancelButton = btnNo;

            Controls.AddRange(new Control[] { lblTitle, lblBody, lnkLearnMore, btnYes, btnNo });

            ResumeLayout(false);
        }
    }
}
