using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using CodexCue.Core;
using CodexCue.Localization;

namespace CodexCue.Ui {
    public sealed class WizardViewModel : INotifyPropertyChanged {
        private readonly WizardState state;
        private bool resolved;
        private string errorText;

        public WizardViewModel(WizardState state) {
            if (state == null) throw new ArgumentNullException("state");
            this.state = state;
            SelectCommand = new RelayCommand(delegate(object value) { Select(Convert.ToString(value)); });
            NextCommand = new RelayCommand(delegate { Next(); });
            BackCommand = new RelayCommand(delegate { Back(); });
            CancelCommand = new RelayCommand(delegate { Cancel(); });
            SubmitCommand = new RelayCommand(delegate { Submit(); });
            EditCommand = new RelayCommand(delegate(object value) { Edit(Convert.ToString(value)); });
            OpenReviewCommand = new RelayCommand(delegate { OpenReview(); });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<WizardCompletedEventArgs> Completed;
        public event EventHandler Cancelled;

        public ICommand SelectCommand { get; private set; }
        public ICommand NextCommand { get; private set; }
        public ICommand BackCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand SubmitCommand { get; private set; }
        public ICommand EditCommand { get; private set; }
        public ICommand OpenReviewCommand { get; private set; }

        public WizardState State { get { return state; } }
        public OptionQuestion CurrentQuestion { get { return state.CurrentQuestion; } }
        public QuestionAnswer CurrentAnswer {
            get { return CurrentQuestion == null ? null : state.Answer(CurrentQuestion.Id); }
        }
        public IList<QuestionAnswer> ReviewAnswers { get { return state.BuildAnswers(); } }
        public int CurrentIndex { get { return state.CurrentIndex; } }
        public int QuestionCount { get { return state.Request.Questions.Count; } }
        public bool IsReview { get { return state.IsReview; } }
        public bool CanContinue { get { return state.CanContinue; } }
        public bool CanGoBack { get { return IsReview || CurrentIndex > 0; } }

        public double ProgressFraction {
            get { return QuestionCount == 0 ? 0.0 : (CurrentIndex + 1.0) / QuestionCount; }
        }

        public string OtherText {
            get { return CurrentAnswer == null ? null : CurrentAnswer.OtherText; }
            set {
                if (CurrentQuestion == null) return;
                state.SetOther(CurrentQuestion.Id, value);
                errorText = null;
                RaiseTransitionProperties();
            }
        }

        public string ErrorText { get { return errorText; } }
        public string BackLabel { get { return Strings.Back; } }
        public string NextLabel {
            get {
                if (IsReview) return Strings.SubmitAnswers;
                if (CurrentIndex < QuestionCount - 1) return Strings.Next;
                return state.ShouldReview ? Strings.ReviewAnswers : Strings.SubmitAnswers;
            }
        }

        public bool IsOptionSelected(string optionId) {
            return CurrentAnswer != null && CurrentAnswer.SelectedOptionIds.Contains(optionId);
        }

        public void Select(string optionId) {
            if (CurrentQuestion == null) return;
            state.Select(CurrentQuestion.Id, optionId);
            errorText = null;
            RaiseTransitionProperties();
        }

        public void Next() {
            if (resolved || IsReview) {
                if (IsReview) Submit();
                return;
            }
            if (!state.CanContinue) {
                errorText = Strings.AnswerRequired;
                RaiseTransitionProperties();
                return;
            }

            errorText = null;
            bool isLast = CurrentIndex == QuestionCount - 1;
            if (!isLast) state.MoveNext();
            else if (state.ShouldReview) state.OpenReview();
            else Complete();
            RaiseTransitionProperties();
        }

        public void Back() {
            if (resolved) return;
            errorText = null;
            state.MovePrevious();
            RaiseTransitionProperties();
        }

        public void OpenReview() {
            if (resolved) return;
            if (!state.OpenReview()) errorText = Strings.CompleteBeforeReview;
            else errorText = null;
            RaiseTransitionProperties();
        }

        public void Edit(string questionId) {
            if (resolved) return;
            state.Edit(questionId);
            errorText = null;
            RaiseTransitionProperties();
        }

        public void Submit() {
            if (resolved) return;
            if (!state.CanSubmit) {
                errorText = Strings.CompleteBeforeSubmit;
                RaiseTransitionProperties();
                return;
            }
            Complete();
        }

        public void Cancel() {
            if (resolved) return;
            resolved = true;
            EventHandler handler = Cancelled;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void Complete() {
            if (resolved) return;
            resolved = true;
            EventHandler<WizardCompletedEventArgs> handler = Completed;
            if (handler != null) handler(this, new WizardCompletedEventArgs(state.BuildAnswers()));
        }

        private void RaiseTransitionProperties() {
            RaisePropertyChanged("CurrentQuestion");
            RaisePropertyChanged("CurrentAnswer");
            RaisePropertyChanged("ReviewAnswers");
            RaisePropertyChanged("CurrentIndex");
            RaisePropertyChanged("QuestionCount");
            RaisePropertyChanged("ProgressFraction");
            RaisePropertyChanged("IsReview");
            RaisePropertyChanged("CanContinue");
            RaisePropertyChanged("CanGoBack");
            RaisePropertyChanged("OtherText");
            RaisePropertyChanged("ErrorText");
            RaisePropertyChanged("BackLabel");
            RaisePropertyChanged("NextLabel");
            RaiseCommands();
        }

        private void RaisePropertyChanged(string propertyName) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RaiseCommands() {
            RaiseCommand(SelectCommand);
            RaiseCommand(NextCommand);
            RaiseCommand(BackCommand);
            RaiseCommand(CancelCommand);
            RaiseCommand(SubmitCommand);
            RaiseCommand(EditCommand);
            RaiseCommand(OpenReviewCommand);
        }

        private static void RaiseCommand(ICommand command) {
            RelayCommand relay = command as RelayCommand;
            if (relay != null) relay.RaiseCanExecuteChanged();
        }
    }
}
