using System;
using System.Collections.Generic;

namespace CodexOptionPrompts.Core {
    public sealed class WizardState {
        private readonly IDictionary<string, QuestionAnswer> answers;

        public WizardState(OptionRequest request) {
            if (request == null) throw new ArgumentNullException("request");
            if (request.Questions == null || request.Questions.Count == 0) {
                throw new ArgumentException("A wizard requires at least one question.", "request");
            }

            Request = request;
            answers = new Dictionary<string, QuestionAnswer>(StringComparer.Ordinal);
            foreach (OptionQuestion question in request.Questions) {
                answers.Add(question.Id, new QuestionAnswer { QuestionId = question.Id });
            }
        }

        public OptionRequest Request { get; private set; }
        public int CurrentIndex { get; private set; }
        public bool IsReview { get; private set; }

        public OptionQuestion CurrentQuestion {
            get { return IsReview ? null : Request.Questions[CurrentIndex]; }
        }

        public bool CanContinue {
            get { return IsReview ? CanSubmit : IsAnswered(Request.Questions[CurrentIndex]); }
        }

        public bool CanSubmit {
            get {
                foreach (OptionQuestion question in Request.Questions) {
                    if (!IsAnswered(question)) return false;
                }
                return true;
            }
        }

        public bool ShouldReview {
            get {
                return Request.ReviewMode == ReviewMode.Always ||
                    (Request.ReviewMode == ReviewMode.Auto && Request.Questions.Count > 1);
            }
        }

        public QuestionAnswer Answer(string questionId) {
            QuestionAnswer answer;
            if (!answers.TryGetValue(questionId, out answer)) {
                throw new ArgumentException("Unknown question ID.", "questionId");
            }
            return answer;
        }

        public void Select(string questionId, string optionId) {
            OptionQuestion question = FindQuestion(questionId);
            EnsureOption(question, optionId);
            QuestionAnswer answer = Answer(questionId);

            if (question.Mode == SelectionMode.Single) {
                answer.SelectedOptionIds.Clear();
                answer.SelectedOptionIds.Add(optionId);
                answer.OtherText = null;
                return;
            }

            if (answer.SelectedOptionIds.Contains(optionId)) answer.SelectedOptionIds.Remove(optionId);
            else answer.SelectedOptionIds.Add(optionId);
            SortSelectedOptions(question, answer);
        }

        public void SetOther(string questionId, string value) {
            OptionQuestion question = FindQuestion(questionId);
            if (!question.AllowOther) throw new InvalidOperationException("Other input is disabled for this question.");
            QuestionAnswer answer = Answer(questionId);
            answer.OtherText = String.IsNullOrWhiteSpace(value) ? null : value;
            if (question.Mode == SelectionMode.Single && answer.OtherText != null) {
                answer.SelectedOptionIds.Clear();
            }
        }

        public bool MoveNext() {
            if (IsReview || !CanContinue) return false;
            if (CurrentIndex < Request.Questions.Count - 1) {
                CurrentIndex++;
                return true;
            }
            if (ShouldReview) return OpenReview();
            return false;
        }

        public bool MovePrevious() {
            if (IsReview) {
                IsReview = false;
                CurrentIndex = Request.Questions.Count - 1;
                return true;
            }
            if (CurrentIndex == 0) return false;
            CurrentIndex--;
            return true;
        }

        public bool OpenReview() {
            if (!ShouldReview || !CanSubmit) return false;
            IsReview = true;
            return true;
        }

        public void Edit(string questionId) {
            OptionQuestion question = FindQuestion(questionId);
            CurrentIndex = Request.Questions.IndexOf(question);
            IsReview = false;
        }

        public IList<QuestionAnswer> BuildAnswers() {
            List<QuestionAnswer> result = new List<QuestionAnswer>();
            foreach (OptionQuestion question in Request.Questions) {
                QuestionAnswer source = Answer(question.Id);
                QuestionAnswer copy = new QuestionAnswer {
                    QuestionId = question.Id,
                    OtherText = source.OtherText
                };
                foreach (OptionChoice option in question.Options) {
                    if (source.SelectedOptionIds.Contains(option.Id)) copy.SelectedOptionIds.Add(option.Id);
                }
                result.Add(copy);
            }
            return result;
        }

        private bool IsAnswered(OptionQuestion question) {
            if (!question.Required) return true;
            QuestionAnswer answer = Answer(question.Id);
            return answer.SelectedOptionIds.Count > 0 || !String.IsNullOrWhiteSpace(answer.OtherText);
        }

        private OptionQuestion FindQuestion(string questionId) {
            foreach (OptionQuestion question in Request.Questions) {
                if (String.Equals(question.Id, questionId, StringComparison.Ordinal)) return question;
            }
            throw new ArgumentException("Unknown question ID.", "questionId");
        }

        private static void EnsureOption(OptionQuestion question, string optionId) {
            foreach (OptionChoice option in question.Options) {
                if (String.Equals(option.Id, optionId, StringComparison.Ordinal)) return;
            }
            throw new ArgumentException("Unknown option ID.", "optionId");
        }

        private static void SortSelectedOptions(OptionQuestion question, QuestionAnswer answer) {
            List<string> ordered = new List<string>();
            foreach (OptionChoice option in question.Options) {
                if (answer.SelectedOptionIds.Contains(option.Id)) ordered.Add(option.Id);
            }
            answer.SelectedOptionIds = ordered;
        }
    }
}
