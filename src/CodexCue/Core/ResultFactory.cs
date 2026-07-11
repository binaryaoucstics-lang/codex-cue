using System;
using System.Collections.Generic;

namespace CodexCue.Core {
    public static class ResultFactory {
        public static OptionResult AutoSubmitted(OptionRequest request) {
            if (request == null) throw new ArgumentNullException("request");
            List<QuestionAnswer> answers = new List<QuestionAnswer>();
            foreach (OptionQuestion question in request.Questions) {
                QuestionAnswer answer = new QuestionAnswer { QuestionId = question.Id };
                foreach (OptionChoice choice in question.Options) {
                    if (choice.Recommended) answer.SelectedOptionIds.Add(choice.Id);
                }
                answers.Add(answer);
            }
            return Submitted(request, answers, "auto");
        }

        public static OptionResult Submitted(OptionRequest request, IList<QuestionAnswer> answers, string resolution) {
            OptionResult result = Create(request, "submitted", resolution);
            result.Answers = answers == null ? new List<QuestionAnswer>() : new List<QuestionAnswer>(answers);
            if (request.IsLegacy && result.Answers.Count > 0 && result.Answers[0].SelectedOptionIds.Count > 0) {
                result.SelectedOptionId = result.Answers[0].SelectedOptionIds[0];
                foreach (OptionChoice choice in request.Questions[0].Options) {
                    if (String.Equals(choice.Id, result.SelectedOptionId, StringComparison.Ordinal)) {
                        result.SelectedOption = choice;
                        break;
                    }
                }
            }
            return result;
        }

        public static OptionResult Cancelled(OptionRequest request) {
            return Create(request, "cancelled", "user");
        }

        public static OptionResult Skipped(OptionRequest request) {
            return Create(request, "skipped", "user");
        }

        public static OptionResult TimedOut(OptionRequest request) {
            return Create(request, "timed_out", "auto");
        }

        private static OptionResult Create(OptionRequest request, string status, string resolution) {
            return new OptionResult {
                Status = status,
                SessionId = request.SessionId,
                Source = "desktop-wpf",
                Resolution = resolution,
                ProtocolVersion = 1,
                CreatedAt = request.CreatedAt,
                ResolvedAt = DateTime.UtcNow,
                CompatibilityWarnings = new List<string>(request.CompatibilityWarnings)
            };
        }
    }
}
