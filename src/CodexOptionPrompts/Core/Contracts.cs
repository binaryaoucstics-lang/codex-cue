using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodexOptionPrompts.Core {
    public enum SelectionMode { Invalid, Single, Multiple }
    public enum ReviewMode { Invalid, Auto, Always, Never }

    public sealed class OptionRequest {
        public OptionRequest() {
            Questions = new List<OptionQuestion>();
            CompatibilityWarnings = new List<string>();
            ReviewMode = ReviewMode.Auto;
            MaxWaitMs = 900000;
            CreatedAt = DateTime.UtcNow;
        }

        public string SessionId { get; set; }
        public string Title { get; set; }
        public IList<OptionQuestion> Questions { get; set; }
        public ReviewMode ReviewMode { get; set; }
        public int? AutoResolutionMs { get; set; }
        public int MaxWaitMs { get; set; }
        public bool IsLegacy { get; set; }
        public bool SuppressUi { get; set; }
        public IList<string> CompatibilityWarnings { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class OptionQuestion {
        public OptionQuestion() {
            Mode = SelectionMode.Single;
            Required = true;
            AllowOther = true;
            Options = new List<OptionChoice>();
        }

        public string Id { get; set; }
        public string Prompt { get; set; }
        public string Description { get; set; }
        public SelectionMode Mode { get; set; }
        public bool Required { get; set; }
        public bool AllowOther { get; set; }
        public string OtherLabel { get; set; }
        public IList<OptionChoice> Options { get; set; }
    }

    public sealed class OptionChoice {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public bool Recommended { get; set; }
    }

    public sealed class QuestionAnswer {
        public QuestionAnswer() { SelectedOptionIds = new List<string>(); }
        public string QuestionId { get; set; }
        public IList<string> SelectedOptionIds { get; set; }
        public string OtherText { get; set; }
    }

    public sealed class OptionResult {
        public OptionResult() {
            Answers = new List<QuestionAnswer>();
            CompatibilityWarnings = new List<string>();
        }

        public string Status { get; set; }
        public string SessionId { get; set; }
        public IList<QuestionAnswer> Answers { get; set; }
        public string Source { get; set; }
        public string Resolution { get; set; }
        public int ProtocolVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ResolvedAt { get; set; }
        public IList<string> CompatibilityWarnings { get; set; }
        public string SelectedOptionId { get; set; }
        public OptionChoice SelectedOption { get; set; }
    }

    public sealed class WizardCompletedEventArgs : EventArgs {
        public WizardCompletedEventArgs(IList<QuestionAnswer> answers) { Answers = answers; }
        public IList<QuestionAnswer> Answers { get; private set; }
    }

    public sealed class PromptHostStatus {
        public string ApplicationVersion { get; set; }
        public int ProtocolVersion { get; set; }
        public bool IsRunning { get; set; }
        public int ActiveCount { get; set; }
        public int QueuedCount { get; set; }
    }

    public sealed class ValidationResult {
        public bool IsValid { get; private set; }
        public string Code { get; private set; }
        public string Message { get; private set; }

        public static ValidationResult Valid() {
            return new ValidationResult { IsValid = true };
        }

        public static ValidationResult Invalid(string code, string message) {
            return new ValidationResult { IsValid = false, Code = code, Message = message };
        }
    }

    public interface IPromptClient {
        Task<OptionResult> AskAsync(OptionRequest request, CancellationToken cancellationToken);
        Task<PromptHostStatus> GetStatusAsync(CancellationToken cancellationToken);
    }

    public interface IClock { DateTime UtcNow { get; } }

    public sealed class SystemClock : IClock {
        public DateTime UtcNow { get { return DateTime.UtcNow; } }
    }
}
