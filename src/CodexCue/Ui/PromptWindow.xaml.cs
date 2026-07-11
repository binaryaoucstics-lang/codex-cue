using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CodexCue.Core;
using CodexCue.Localization;

namespace CodexCue.Ui {
    public partial class PromptWindow : Window {
        private readonly WizardViewModel viewModel;
        private readonly IList<ToggleButton> optionButtons = new List<ToggleButton>();
        private bool updating;
        private bool closeRequested;
        private int renderedIndex = -1;
        private bool renderedReview;
        private bool fittingWindow;

        public PromptWindow(WizardViewModel viewModel) {
            if (viewModel == null) throw new ArgumentNullException("viewModel");
            this.viewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.Completed += OnResolved;
            viewModel.Cancelled += OnCancelled;
            Loaded += OnLoaded;
            ApplyShellStrings();
            ApplyHighContrastShell();
            RenderCurrentState(true);
        }

        public WizardViewModel ViewModel { get { return viewModel; } }

        public void ForceClose() {
            closeRequested = true;
            Close();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            FitWindowToContent();
            UpdateProgress(true);
            StartGradientAnimation();
            if (optionButtons.Count > 0) optionButtons[0].Focus();
        }

        private void ApplyShellStrings() {
            string cancelLabel = String.IsNullOrWhiteSpace(viewModel.State.Request.CancelLabel)
                ? Strings.Cancel : viewModel.State.Request.CancelLabel;
            CancelButton.Content = cancelLabel;
            AutomationProperties.SetName(CancelButton, cancelLabel);
            BackButton.Content = Strings.Back;
            AutomationProperties.SetName(BackButton, Strings.Back);
            ReviewTitle.Text = Strings.ReviewTitle;
            ReviewDescription.Text = Strings.ReviewDescription;
        }

        private void ApplyHighContrastShell() {
            if (!SystemParameters.HighContrast) return;
            OuterBorder.Effect = null;
            OuterBorder.Background = SystemColors.WindowBrush;
            OuterBorder.BorderBrush = SystemColors.WindowTextBrush;
            OuterBorder.BorderThickness = new Thickness(1);
            Surface.Background = SystemColors.WindowBrush;
            FooterBorder.Background = SystemColors.WindowBrush;
            FooterBorder.BorderBrush = SystemColors.WindowTextBrush;
            ProgressTrack.Background = SystemColors.ControlBrush;
            ProgressFill.Background = SystemColors.HighlightBrush;
            ProgressText.Foreground = SystemColors.WindowTextBrush;
            QuestionPrompt.Foreground = SystemColors.WindowTextBrush;
            QuestionDescription.Foreground = SystemColors.GrayTextBrush;
            ReviewTitle.Foreground = SystemColors.WindowTextBrush;
            ReviewDescription.Foreground = SystemColors.GrayTextBrush;
            OtherPlaceholder.Foreground = SystemColors.GrayTextBrush;
            ValidationError.Foreground = SystemColors.WindowTextBrush;
            OtherTextBox.Style = null;
            OtherTextBox.Height = 74;
            OtherTextBox.FontFamily = (FontFamily)FindResource("ShellFont");
            OtherTextBox.FontSize = 20;
            OtherTextBox.Foreground = SystemColors.WindowTextBrush;
            OtherTextBox.Background = SystemColors.WindowBrush;
            OtherTextBox.BorderBrush = SystemColors.WindowTextBrush;
            CancelButton.Style = null;
            BackButton.Style = null;
            NextButton.Style = null;
            CancelButton.Height = BackButton.Height = NextButton.Height = 62;
        }

        private void RenderCurrentState(bool force) {
            if (!force && renderedIndex == viewModel.CurrentIndex && renderedReview == viewModel.IsReview) {
                RefreshAnswerControls();
                RefreshChrome();
                return;
            }
            renderedIndex = viewModel.CurrentIndex;
            renderedReview = viewModel.IsReview;
            if (viewModel.IsReview) RenderReview();
            else RenderQuestion();
            RefreshChrome();
            ContentScroller.ScrollToTop();
            if (IsLoaded) Dispatcher.BeginInvoke(new Action(FitWindowToContent), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void FitWindowToContent() {
            if (fittingWindow) return;
            fittingWindow = true;
            try {
                Rect workArea = SystemParameters.WorkArea;
                double maximumHeight = Math.Max(MinHeight, workArea.Height - 24);
                double contentWidth = Math.Max(320, Width - 132);
                FrameworkElement content = viewModel.IsReview ? (FrameworkElement)ReviewPanel : QuestionPanel;
                content.Measure(new Size(contentWidth, Double.PositiveInfinity));

                // Header (49), footer (114), outer border margins (40), and scroller margins (72).
                double desiredHeight = Math.Ceiling(content.DesiredSize.Height + 275);
                double targetHeight = Math.Max(MinHeight, Math.Min(maximumHeight, desiredHeight));
                bool contentFits = desiredHeight <= maximumHeight + 0.5;
                ContentScroller.VerticalScrollBarVisibility = contentFits
                    ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
                Height = targetHeight;
                Top = workArea.Top + Math.Max(0, (workArea.Height - targetHeight) / 2);
            } finally { fittingWindow = false; }
        }

        private void RenderQuestion() {
            ReviewPanel.Visibility = Visibility.Collapsed;
            QuestionPanel.Visibility = Visibility.Visible;
            OptionQuestion question = viewModel.CurrentQuestion;
            QuestionPrompt.Text = question.Prompt;
            AutomationProperties.SetName(QuestionPrompt, question.Prompt);
            QuestionDescription.Text = question.Description;
            QuestionDescription.Visibility = String.IsNullOrEmpty(question.Description) ? Visibility.Collapsed : Visibility.Visible;
            OptionsPanel.Children.Clear();
            optionButtons.Clear();
            bool compact = question.Options.Count > 2;
            ApplyQuestionDensity(compact);

            int index = 0;
            foreach (OptionChoice option in question.Options) {
                ToggleButton button = CreateOptionButton(question, option, index++, compact);
                optionButtons.Add(button);
                OptionsPanel.Children.Add(button);
            }

            OtherContainer.Visibility = question.AllowOther ? Visibility.Visible : Visibility.Collapsed;
            string placeholder = String.IsNullOrWhiteSpace(question.OtherLabel)
                ? Strings.OtherPlaceholder
                : question.OtherLabel + (Strings.ListSeparator == "、" ? "：" : ": ") + Strings.CustomAnswerHint;
            OtherPlaceholder.Text = placeholder;
            AutomationProperties.SetName(OtherTextBox, placeholder);
            RefreshAnswerControls();
        }

        private void ApplyQuestionDensity(bool compact) {
            ContentScroller.Margin = compact ? new Thickness(46, 32, 46, 18) : new Thickness(46, 44, 46, 28);
            OptionsPanel.Margin = compact ? new Thickness(0, 24, 0, 0) : new Thickness(0, 34, 0, 0);
            OtherContainer.Height = compact ? 64 : 74;
            OtherTextBox.Height = compact ? 64 : 74;
        }

        private ToggleButton CreateOptionButton(OptionQuestion question, OptionChoice option, int index, bool compact) {
            ToggleButton button;
            if (question.Mode == CodexCue.Core.SelectionMode.Single) {
                button = new RadioButton { GroupName = "Question_" + question.Id };
            } else button = new CheckBox();
            button.Style = (Style)FindResource("OptionToggleStyle");
            button.Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 20);
            if (compact) {
                button.Padding = new Thickness(20, 16, 20, 16);
                button.MinHeight = 78;
            }
            button.Tag = option.Id;
            button.TabIndex = 10 + index;
            AutomationProperties.SetAutomationId(button, "Option_" + option.Id);
            AutomationProperties.SetName(button, option.Label);

            if (SystemParameters.HighContrast) {
                button.Style = null;
                button.Foreground = SystemColors.WindowTextBrush;
                button.Background = SystemColors.WindowBrush;
                button.BorderBrush = SystemColors.WindowTextBrush;
                button.Padding = new Thickness(14);
            }

            Brush labelBrush = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : (Brush)FindResource("InkBrush");
            Brush descriptionBrush = SystemParameters.HighContrast ? SystemColors.GrayTextBrush : (Brush)FindResource("MutedBrush");
            StackPanel content = new StackPanel();
            content.Children.Add(new TextBlock {
                Text = option.Label,
                FontFamily = (FontFamily)FindResource("ShellFont"),
                FontSize = compact ? 22 : 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = labelBrush,
                TextWrapping = TextWrapping.Wrap
            });
            if (!String.IsNullOrEmpty(option.Description)) {
                content.Children.Add(new TextBlock {
                    Text = option.Description,
                    Margin = new Thickness(0, compact ? 6 : 9, 0, 0),
                    FontFamily = (FontFamily)FindResource("ShellFont"),
                    FontSize = compact ? 18 : 20,
                    LineHeight = compact ? 24 : 28,
                    Foreground = descriptionBrush,
                    TextWrapping = TextWrapping.Wrap
                });
                button.MinHeight = compact ? 84 : 94;
            }
            button.Content = content;
            button.Checked += OnOptionToggled;
            if (question.Mode == CodexCue.Core.SelectionMode.Multiple) button.Unchecked += OnOptionToggled;
            return button;
        }

        private void RenderReview() {
            ContentScroller.Margin = new Thickness(46, 44, 46, 28);
            QuestionPanel.Visibility = Visibility.Collapsed;
            ReviewPanel.Visibility = Visibility.Visible;
            ReviewItems.Children.Clear();
            IList<QuestionAnswer> answers = viewModel.ReviewAnswers;
            foreach (OptionQuestion question in viewModel.State.Request.Questions) {
                QuestionAnswer answer = answers.First(item => item.QuestionId == question.Id);
                ReviewItems.Children.Add(CreateReviewCard(question, answer));
            }
        }

        private UIElement CreateReviewCard(OptionQuestion question, QuestionAnswer answer) {
            Brush labelBrush = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : (Brush)FindResource("InkBrush");
            Brush descriptionBrush = SystemParameters.HighContrast ? SystemColors.GrayTextBrush : (Brush)FindResource("MutedBrush");
            Grid content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel text = new StackPanel();
            text.Children.Add(new TextBlock {
                Text = question.Prompt,
                FontFamily = (FontFamily)FindResource("ShellFont"),
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = labelBrush,
                TextWrapping = TextWrapping.Wrap
            });
            text.Children.Add(new TextBlock {
                Text = AnswerText(question, answer),
                Margin = new Thickness(0, 8, 0, 0),
                FontFamily = (FontFamily)FindResource("ShellFont"),
                FontSize = 16,
                Foreground = descriptionBrush,
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(text);

            Button edit = new Button {
                Content = Strings.Edit,
                Tag = question.Id,
                Style = (Style)FindResource("TextButtonStyle"),
                Height = 42,
                MinWidth = 68,
                Margin = new Thickness(14, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(edit, 1);
            AutomationProperties.SetAutomationId(edit, "Edit_" + question.Id);
            AutomationProperties.SetName(edit, Strings.Edit + " " + question.Prompt);
            edit.Click += OnEditClick;
            if (SystemParameters.HighContrast) edit.Style = null;
            content.Children.Add(edit);

            return new Border {
                Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(Color.FromRgb(250, 251, 253)),
                BorderBrush = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : (Brush)FindResource("LineBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(21, 18, 18, 18),
                Margin = new Thickness(0, 0, 0, 14),
                Child = content
            };
        }

        private static string AnswerText(OptionQuestion question, QuestionAnswer answer) {
            List<string> values = new List<string>();
            foreach (OptionChoice option in question.Options) {
                if (answer.SelectedOptionIds.Contains(option.Id)) values.Add(option.Label);
            }
            if (!String.IsNullOrWhiteSpace(answer.OtherText)) values.Add(answer.OtherText);
            return values.Count == 0 ? Strings.NoAnswer : String.Join(Strings.ListSeparator, values.ToArray());
        }

        private void RefreshAnswerControls() {
            if (viewModel.IsReview || viewModel.CurrentQuestion == null) return;
            updating = true;
            try {
                foreach (ToggleButton button in optionButtons) {
                    button.IsChecked = viewModel.IsOptionSelected(Convert.ToString(button.Tag));
                }
                string value = viewModel.OtherText ?? "";
                if (!String.Equals(OtherTextBox.Text, value, StringComparison.Ordinal)) OtherTextBox.Text = value;
                OtherPlaceholder.Visibility = String.IsNullOrEmpty(OtherTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            } finally { updating = false; }
        }

        private void RefreshChrome() {
            int current = Math.Min(viewModel.CurrentIndex + 1, viewModel.QuestionCount);
            ProgressText.Text = current + " / " + viewModel.QuestionCount;
            AutomationProperties.SetName(ProgressText, Strings.Progress(current, viewModel.QuestionCount));
            ValidationError.Text = viewModel.ErrorText ?? "";
            AutomationProperties.SetName(ValidationError, ValidationError.Text);
            BackButton.Visibility = viewModel.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
            BackButton.IsTabStop = viewModel.CanGoBack;
            string nextLabel = viewModel.NextLabel;
            NextButton.Content = nextLabel;
            AutomationProperties.SetName(NextButton, nextLabel);
            AutomationProperties.SetAutomationId(NextButton, viewModel.IsReview ? "SubmitButton" : "NextButton");
            UpdateProgress(true);
        }

        private void UpdateProgress(bool animate) {
            if (ProgressTrack.ActualWidth <= 0) return;
            double fraction = viewModel.IsReview ? 1.0 : viewModel.ProgressFraction;
            double target = Math.Max(0, Math.Min(ProgressTrack.ActualWidth, ProgressTrack.ActualWidth * fraction));
            if (!animate || !SystemParameters.ClientAreaAnimation) {
                ProgressFill.BeginAnimation(WidthProperty, null);
                ProgressFill.Width = target;
                return;
            }
            DoubleAnimation animation = new DoubleAnimation {
                To = target,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void StartGradientAnimation() {
            if (!SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast) return;
            DoubleAnimation animation = new DoubleAnimation {
                From = -0.08,
                To = 0.08,
                Duration = TimeSpan.FromSeconds(2.6),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            GradientTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "CurrentQuestion" || e.PropertyName == "CurrentIndex" || e.PropertyName == "IsReview") {
                RenderCurrentState(false);
            } else {
                RefreshAnswerControls();
                RefreshChrome();
            }
        }

        private void OnOptionToggled(object sender, RoutedEventArgs e) {
            if (updating) return;
            ToggleButton button = (ToggleButton)sender;
            string optionId = Convert.ToString(button.Tag);
            bool selected = viewModel.IsOptionSelected(optionId);
            if (button.IsChecked == true && !selected) viewModel.Select(optionId);
            else if (button.IsChecked != true && selected) viewModel.Select(optionId);
        }

        private void OnOtherTextChanged(object sender, TextChangedEventArgs e) {
            if (updating || viewModel.IsReview || viewModel.CurrentQuestion == null) return;
            viewModel.OtherText = OtherTextBox.Text;
            OtherPlaceholder.Visibility = String.IsNullOrEmpty(OtherTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnNextClick(object sender, RoutedEventArgs e) { viewModel.Next(); }
        private void OnBackClick(object sender, RoutedEventArgs e) { viewModel.Back(); }
        private void OnCancelClick(object sender, RoutedEventArgs e) { RequestCancel(); }

        private void OnEditClick(object sender, RoutedEventArgs e) {
            Button button = (Button)sender;
            viewModel.Edit(Convert.ToString(button.Tag));
        }

        private bool RequestCancel() {
            if (HasInput()) {
                ConfirmWindow confirm = new ConfirmWindow { Owner = this };
                bool? result = confirm.ShowDialog();
                if (result != true) return false;
            }
            closeRequested = true;
            viewModel.Cancel();
            return true;
        }

        private bool HasInput() {
            foreach (QuestionAnswer answer in viewModel.State.BuildAnswers()) {
                if (answer.SelectedOptionIds.Count > 0 || !String.IsNullOrWhiteSpace(answer.OtherText)) return true;
            }
            return false;
        }

        private void OnResolved(object sender, WizardCompletedEventArgs e) {
            closeRequested = true;
            Close();
        }

        private void OnCancelled(object sender, EventArgs e) {
            closeRequested = true;
            Close();
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            if (closeRequested) return;
            e.Cancel = !RequestCancel();
        }

        private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs e) {
            Surface.Clip = new RectangleGeometry(new Rect(0, 0, Surface.ActualWidth, Surface.ActualHeight), 18, 18);
            UpdateProgress(false);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                RequestCancel();
                e.Handled = true;
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) {
                if (e.SystemKey == Key.F4) { RequestCancel(); e.Handled = true; return; }
                if (e.SystemKey == Key.Left) { viewModel.Back(); e.Handled = true; return; }
                if (e.SystemKey == Key.Right) { viewModel.Next(); e.Handled = true; return; }
            }
            if (e.Key == Key.Left || e.Key == Key.Up || e.Key == Key.Right || e.Key == Key.Down) {
                ToggleButton focused = Keyboard.FocusedElement as ToggleButton;
                int index = focused == null ? -1 : optionButtons.IndexOf(focused);
                if (index >= 0) {
                    int direction = (e.Key == Key.Left || e.Key == Key.Up) ? -1 : 1;
                    int target = Math.Max(0, Math.Min(optionButtons.Count - 1, index + direction));
                    optionButtons[target].Focus();
                    e.Handled = true;
                    return;
                }
            }
            if (e.Key == Key.Enter) {
                viewModel.Next();
                e.Handled = true;
                return;
            }
            int number = NumberKey(e.Key);
            if (number > 0 && number <= optionButtons.Count) {
                ToggleButton button = optionButtons[number - 1];
                button.Focus();
                viewModel.Select(Convert.ToString(button.Tag));
                e.Handled = true;
            }
        }

        private static int NumberKey(Key key) {
            if (key >= Key.D1 && key <= Key.D9) return (int)key - (int)Key.D0;
            if (key >= Key.NumPad1 && key <= Key.NumPad9) return (int)key - (int)Key.NumPad0;
            return 0;
        }
    }
}
