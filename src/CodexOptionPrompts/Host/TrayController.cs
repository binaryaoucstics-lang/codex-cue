using System;
using System.Drawing;
using System.Windows.Forms;

namespace CodexOptionPrompts.Host {
    public sealed class TrayController : IDisposable {
        private readonly NotifyIcon icon;

        public TrayController() {
            ToolStripMenuItem open = new ToolStripMenuItem("Open");
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            open.Click += delegate { Raise(OpenRequested); };
            exit.Click += delegate { Raise(ExitRequested); };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(open);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            icon = new NotifyIcon {
                Text = "Codex Option Prompts",
                Icon = SystemIcons.Application,
                ContextMenuStrip = menu,
                Visible = true
            };
            icon.DoubleClick += delegate { Raise(OpenRequested); };
        }

        public event EventHandler OpenRequested;
        public event EventHandler ExitRequested;

        public void Dispose() {
            icon.Visible = false;
            if (icon.ContextMenuStrip != null) icon.ContextMenuStrip.Dispose();
            icon.Dispose();
        }

        private void Raise(EventHandler handler) {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
