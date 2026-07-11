using System;
using System.Drawing;
using System.Windows.Forms;
using CodexCue.Settings;

namespace CodexCue.Ui {
    public sealed class SettingsDialog : Form {
        private readonly CueSettingsStore store;
        private readonly CheckBox enabled;
        private readonly NumericUpDown optionCount;
        private readonly CheckBox skipNext;

        public SettingsDialog(CueSettingsStore store) {
            if (store == null) throw new ArgumentNullException("store");
            this.store = store;
            CueSettings value = store.Load();

            Text = "Codex Cue 设置";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 210);
            Font = new Font("Microsoft YaHei UI", 9F);

            enabled = new CheckBox {
                Text = "任务完成时显示下一步选项",
                Checked = value.CompletionSuggestionsEnabled,
                AutoSize = true,
                Location = new Point(24, 24)
            };
            Controls.Add(enabled);

            Label countLabel = new Label {
                Text = "每次显示的选项数量：",
                AutoSize = true,
                Location = new Point(42, 67)
            };
            Controls.Add(countLabel);
            optionCount = new NumericUpDown {
                Minimum = 1,
                Maximum = 6,
                Value = value.CompletionOptionCount,
                Location = new Point(210, 63),
                Width = 64
            };
            Controls.Add(optionCount);

            skipNext = new CheckBox {
                Text = "跳过下一次任务完成建议（仅一次）",
                Checked = value.SkipNextCompletion,
                AutoSize = true,
                Location = new Point(42, 105)
            };
            Controls.Add(skipNext);

            Button cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(82, 32), Location = new Point(220, 154) };
            Button save = new Button { Text = "保存", Size = new Size(82, 32), Location = new Point(310, 154) };
            save.Click += SaveClicked;
            Controls.Add(cancel);
            Controls.Add(save);
            CancelButton = cancel;
            AcceptButton = save;

            enabled.CheckedChanged += delegate {
                optionCount.Enabled = enabled.Checked;
                skipNext.Enabled = enabled.Checked;
            };
            optionCount.Enabled = enabled.Checked;
            skipNext.Enabled = enabled.Checked;
        }

        private void SaveClicked(object sender, EventArgs e) {
            store.Save(new CueSettings {
                CompletionSuggestionsEnabled = enabled.Checked,
                CompletionOptionCount = Decimal.ToInt32(optionCount.Value),
                SkipNextCompletion = enabled.Checked && skipNext.Checked
            });
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
