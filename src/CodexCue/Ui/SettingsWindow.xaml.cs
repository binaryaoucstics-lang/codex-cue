using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CodexCue.Settings;

namespace CodexCue.Ui {
    public partial class SettingsWindow : Window {
        private readonly CueSettingsStore store;
        private readonly IList<ToggleButton> accentButtons = new List<ToggleButton>();
        private int optionCount;
        private string selectedAccent;
        private string originalAccent;
        private bool saved;

        public SettingsWindow(CueSettingsStore store) {
            if (store == null) throw new ArgumentNullException("store");
            this.store = store;
            InitializeComponent();
            CueSettings value = store.Load();
            CompletionEnabled.IsChecked = value.CompletionSuggestionsEnabled;
            SkipNext.IsChecked = value.SkipNextCompletion;
            optionCount = value.CompletionOptionCount;
            selectedAccent = originalAccent = value.AccentColor;
            BuildAccentOptions();
            RefreshOptionCount();
            RefreshEnabledState();
        }

        private void BuildAccentOptions() {
            foreach (AccentDefinition accent in AccentTheme.All) {
                ToggleButton button = new ToggleButton {
                    Tag = accent.Id,
                    Background = new SolidColorBrush(accent.Primary),
                    Content = String.Equals(accent.Id, selectedAccent, StringComparison.Ordinal) ? "✓" : "",
                    IsChecked = String.Equals(accent.Id, selectedAccent, StringComparison.Ordinal),
                    ToolTip = accent.Label,
                    Style = (Style)FindResource("AccentSwatchStyle")
                };
                AutomationProperties.SetAutomationId(button, "Accent_" + accent.Id);
                AutomationProperties.SetName(button, accent.Label);
                button.Click += OnAccentClick;
                accentButtons.Add(button);
                AccentOptions.Children.Add(button);
            }
        }

        private void OnAccentClick(object sender, RoutedEventArgs e) {
            selectedAccent = Convert.ToString(((ToggleButton)sender).Tag);
            foreach (ToggleButton button in accentButtons) {
                bool selected = String.Equals(Convert.ToString(button.Tag), selectedAccent, StringComparison.Ordinal);
                button.IsChecked = selected;
                button.Content = selected ? "✓" : "";
            }
            AccentTheme.Apply(selectedAccent);
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
                SkipNextCompletion = enabled && SkipNext.IsChecked == true,
                AccentColor = selectedAccent
            });
            saved = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) { Close(); }

        private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            if (!saved) AccentTheme.Apply(originalAccent);
        }
    }
}
