using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using CodexCue.Settings;

namespace CodexCue.Ui {
    public partial class SettingsWindow : Window {
        private readonly CueSettingsStore store;
        private int optionCount;

        public SettingsWindow(CueSettingsStore store) {
            if (store == null) throw new ArgumentNullException("store");
            this.store = store;
            InitializeComponent();
            CueSettings value = store.Load();
            CompletionEnabled.IsChecked = value.CompletionSuggestionsEnabled;
            SkipNext.IsChecked = value.SkipNextCompletion;
            optionCount = value.CompletionOptionCount;
            RefreshOptionCount();
            RefreshEnabledState();
        }

        private void OnDecreaseClick(object sender, RoutedEventArgs e) {
            if (optionCount > 1) optionCount--;
            RefreshOptionCount();
        }

        private void OnIncreaseClick(object sender, RoutedEventArgs e) {
            if (optionCount < 6) optionCount++;
            RefreshOptionCount();
        }

        private void RefreshOptionCount() {
            string text = optionCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            OptionCountText.Text = text;
            AutomationProperties.SetName(OptionCountText, text);
        }

        private void OnEnabledChanged(object sender, RoutedEventArgs e) { RefreshEnabledState(); }

        private void RefreshEnabledState() {
            if (CountCard == null || SkipNext == null) return;
            bool enabled = CompletionEnabled.IsChecked == true;
            CountCard.IsEnabled = enabled;
            SkipNext.IsEnabled = enabled;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e) {
            bool enabled = CompletionEnabled.IsChecked == true;
            store.Save(new CueSettings {
                CompletionSuggestionsEnabled = enabled,
                CompletionOptionCount = optionCount,
                SkipNextCompletion = enabled && SkipNext.IsChecked == true
            });
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) { Close(); }

        private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        }
    }
}
