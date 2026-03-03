using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TermLens.Core;

namespace TermLens.Settings
{
    /// <summary>
    /// Settings dialog for the TermLens plugin.
    /// Allows the user to select a Supervertaler termbase (.db) file and configure options.
    /// </summary>
    public class TermLensSettingsForm : Form
    {
        private readonly TermLensSettings _settings;

        // Controls
        private TextBox _txtTermbasePath;
        private Button _btnBrowse;
        private Label _lblTermbaseInfo;
        private CheckBox _chkAutoLoad;
        private Button _btnOK;
        private Button _btnCancel;

        public TermLensSettingsForm(TermLensSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            BuildUI();
            PopulateFromSettings();
        }

        private void BuildUI()
        {
            Text = "TermLens Settings";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 210);
            BackColor = Color.White;

            // === Termbase section ===
            var lblSection = new Label
            {
                Text = "Termbase",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50),
                Location = new Point(16, 16),
                AutoSize = true
            };

            var lblPath = new Label
            {
                Text = "Termbase file (.db):",
                Location = new Point(16, 42),
                AutoSize = true,
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            _txtTermbasePath = new TextBox
            {
                Location = new Point(16, 60),
                Width = 420,
                ReadOnly = true,
                BackColor = Color.FromArgb(250, 250, 250),
                ForeColor = Color.FromArgb(40, 40, 40)
            };

            _btnBrowse = new Button
            {
                Text = "Browse...",
                Location = new Point(444, 58),
                Width = 62,
                Height = _txtTermbasePath.Height + 2,
                FlatStyle = FlatStyle.System
            };
            _btnBrowse.Click += OnBrowseClick;

            _lblTermbaseInfo = new Label
            {
                Location = new Point(16, 86),
                AutoSize = false,
                Width = 490,
                Height = 32,
                ForeColor = Color.FromArgb(100, 100, 100),
                Font = new Font("Segoe UI", 8f)
            };

            // === Options section ===
            var sep = new Label
            {
                Location = new Point(16, 124),
                Width = 490,
                Height = 1,
                BorderStyle = BorderStyle.Fixed3D
            };

            _chkAutoLoad = new CheckBox
            {
                Text = "Automatically load termbase when Trados Studio starts",
                Location = new Point(16, 134),
                AutoSize = true,
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            // === OK / Cancel ===
            _btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(ClientSize.Width - 170, ClientSize.Height - 40),
                Width = 75,
                FlatStyle = FlatStyle.System
            };
            _btnOK.Click += OnOKClick;

            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(ClientSize.Width - 88, ClientSize.Height - 40),
                Width = 75,
                FlatStyle = FlatStyle.System
            };

            AcceptButton = _btnOK;
            CancelButton = _btnCancel;

            Controls.AddRange(new Control[]
            {
                lblSection, lblPath, _txtTermbasePath, _btnBrowse,
                _lblTermbaseInfo, sep, _chkAutoLoad,
                _btnOK, _btnCancel
            });
        }

        private void PopulateFromSettings()
        {
            _txtTermbasePath.Text = _settings.TermbasePath ?? "";
            _chkAutoLoad.Checked = _settings.AutoLoadOnStartup;
            UpdateTermbaseInfo(_settings.TermbasePath);
        }

        private void OnBrowseClick(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select Supervertaler Termbase";
                dlg.Filter = "Supervertaler Termbase (*.db)|*.db|All files (*.*)|*.*";
                dlg.FilterIndex = 1;

                // Start in the directory of the currently configured file, if it exists
                var current = _txtTermbasePath.Text;
                if (!string.IsNullOrEmpty(current) && File.Exists(current))
                    dlg.InitialDirectory = Path.GetDirectoryName(current);

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _txtTermbasePath.Text = dlg.FileName;
                    UpdateTermbaseInfo(dlg.FileName);
                }
            }
        }

        private void UpdateTermbaseInfo(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _lblTermbaseInfo.Text = string.IsNullOrEmpty(path)
                    ? "No termbase selected."
                    : "File not found.";
                _lblTermbaseInfo.ForeColor = Color.FromArgb(160, 160, 160);
                return;
            }

            try
            {
                using (var reader = new TermbaseReader(path))
                {
                    if (!reader.Open())
                    {
                        _lblTermbaseInfo.Text = $"Could not open: {reader.LastError}";
                        _lblTermbaseInfo.ForeColor = Color.FromArgb(180, 60, 60);
                        return;
                    }

                    var termbases = reader.GetTermbases();
                    int total = 0;
                    foreach (var tb in termbases) total += tb.TermCount;

                    _lblTermbaseInfo.Text = termbases.Count == 1
                        ? $"\u2713  {termbases[0].Name}  \u2014  {total:N0} terms  ({termbases[0].SourceLang} \u2192 {termbases[0].TargetLang})"
                        : $"\u2713  {termbases.Count} termbases, {total:N0} terms total";

                    _lblTermbaseInfo.ForeColor = Color.FromArgb(30, 130, 60);
                }
            }
            catch
            {
                _lblTermbaseInfo.Text = "Error reading termbase.";
                _lblTermbaseInfo.ForeColor = Color.FromArgb(180, 60, 60);
            }
        }

        private void OnOKClick(object sender, EventArgs e)
        {
            _settings.TermbasePath = _txtTermbasePath.Text.Trim();
            _settings.AutoLoadOnStartup = _chkAutoLoad.Checked;
            _settings.Save();
        }
    }
}
