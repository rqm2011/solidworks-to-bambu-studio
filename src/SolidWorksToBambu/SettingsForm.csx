using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SolidWorksToBambu
{
    internal sealed class SettingsForm : Form
    {
        private readonly TextBox _pathTextBox;
        private readonly CheckBox _keepFilesCheckBox;
        private readonly NumericUpDown _cleanupDays;

        public PluginSettings Settings { get; private set; }

        public SettingsForm(PluginSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            Settings = settings;
            Text = "SolidWorks → Bambu Studio 设置";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(700, 235);

            Label pathLabel = new Label
            {
                Text = "Bambu Studio 程序路径：",
                AutoSize = true,
                Location = new Point(16, 20)
            };

            _pathTextBox = new TextBox
            {
                Location = new Point(19, 45),
                Width = 570,
                Text = string.IsNullOrWhiteSpace(settings.BambuStudioPath)
                    ? (BambuStudioLocator.Find(null) ?? string.Empty)
                    : settings.BambuStudioPath
            };

            Button browseButton = new Button
            {
                Text = "浏览…",
                Location = new Point(599, 43),
                Size = new Size(84, 28)
            };
            browseButton.Click += BrowseButton_Click;

            _keepFilesCheckBox = new CheckBox
            {
                Text = "保留插件生成的临时 STL 文件",
                AutoSize = true,
                Checked = settings.KeepExportFiles,
                Location = new Point(19, 91)
            };
            _keepFilesCheckBox.CheckedChanged += KeepFilesCheckBox_CheckedChanged;

            Label cleanupLabel = new Label
            {
                Text = "不保留时，自动清理早于",
                AutoSize = true,
                Location = new Point(19, 129)
            };

            _cleanupDays = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 30,
                Value = Math.Max(1, Math.Min(30, settings.CleanupDays)),
                Location = new Point(174, 126),
                Width = 58
            };

            Label daysLabel = new Label
            {
                Text = "天的缓存（建议 7 天，防止 Bambu Studio 尚未读完文件）",
                AutoSize = true,
                Location = new Point(239, 129)
            };

            Button okButton = new Button
            {
                Text = "保存",
                DialogResult = DialogResult.None,
                Location = new Point(509, 185),
                Size = new Size(84, 31)
            };
            okButton.Click += OkButton_Click;

            Button cancelButton = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(599, 185),
                Size = new Size(84, 31)
            };

            Controls.Add(pathLabel);
            Controls.Add(_pathTextBox);
            Controls.Add(browseButton);
            Controls.Add(_keepFilesCheckBox);
            Controls.Add(cleanupLabel);
            Controls.Add(_cleanupDays);
            Controls.Add(daysLabel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
            UpdateCleanupControls();
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择 Bambu Studio 程序";
                dialog.Filter = "Bambu Studio (bambu-studio.exe;BambuStudio.exe)|bambu-studio.exe;BambuStudio.exe|程序 (*.exe)|*.exe";
                dialog.CheckFileExists = true;
                if (File.Exists(_pathTextBox.Text))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(_pathTextBox.Text);
                    dialog.FileName = Path.GetFileName(_pathTextBox.Text);
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _pathTextBox.Text = dialog.FileName;
                }
            }
        }

        private void KeepFilesCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCleanupControls();
        }

        private void UpdateCleanupControls()
        {
            _cleanupDays.Enabled = !_keepFilesCheckBox.Checked;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            string path = _pathTextBox.Text.Trim().Trim('"');
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "请选择有效的 Bambu Studio 可执行文件。", "路径无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Settings.BambuStudioPath = Path.GetFullPath(path);
            Settings.KeepExportFiles = _keepFilesCheckBox.Checked;
            Settings.CleanupDays = (int)_cleanupDays.Value;
            try
            {
                Settings.Save();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error("保存设置失败", ex);
                MessageBox.Show(this, "保存设置失败：" + ex.Message, "SolidWorks → Bambu Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
