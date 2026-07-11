using System.Windows;
using System.Windows.Input;
using CodexOptionPrompts.Localization;

namespace CodexOptionPrompts.Ui {
    public partial class ConfirmWindow : Window {
        public ConfirmWindow() {
            InitializeComponent();
            ConfirmTitle.Text = Strings.ConfirmCancelTitle;
            ConfirmBody.Text = Strings.ConfirmCancelBody;
            KeepButton.Content = Strings.KeepAnswering;
            DiscardButton.Content = Strings.Discard;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnKeep(object sender, RoutedEventArgs e) { DialogResult = false; }
        private void OnDiscard(object sender, RoutedEventArgs e) { DialogResult = true; }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
            else if (e.Key == Key.Enter) { DialogResult = true; e.Handled = true; }
        }
    }
}
