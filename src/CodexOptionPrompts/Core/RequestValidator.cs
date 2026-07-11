using System;
using System.Collections.Generic;

namespace CodexOptionPrompts.Core {
    public static class RequestValidator {
        public const int MaximumMessageBytes = 1024 * 1024;
        public const int MaximumTextCharacters = 16 * 1024;
        public const int MaximumJsonDepth = 64;
        public const int MinimumAutoResolutionMs = 60000;
        public const int MaximumAutoResolutionMs = 240000;

        public static ValidationResult Validate(OptionRequest request) {
            if (request == null || request.Questions == null || request.Questions.Count == 0) {
                return Invalid("At least one question is required.");
            }
            if (request.ReviewMode == ReviewMode.Invalid) return Invalid("Review mode is invalid.");
            if (request.MaxWaitMs <= 0) return Invalid("Maximum wait must be positive.");
            if (request.AutoResolutionMs.HasValue &&
                (request.AutoResolutionMs.Value < MinimumAutoResolutionMs ||
                 request.AutoResolutionMs.Value > MaximumAutoResolutionMs)) {
                return Invalid("Auto resolution must be between 60,000 and 240,000 milliseconds.");
            }
            if (TooLong(request.Title)) return Invalid("Request text exceeds 16 KiB.");

            HashSet<string> questionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OptionQuestion question in request.Questions) {
                if (String.IsNullOrWhiteSpace(question.Id) || !questionIds.Add(question.Id)) {
                    return Invalid("Question IDs must be non-empty and unique.");
                }
                if (question.Mode == SelectionMode.Invalid) return Invalid("Question mode is invalid.");
                if (String.IsNullOrWhiteSpace(question.Prompt)) return Invalid("Question prompt must not be blank.");
                if (TooLong(question.Prompt) || TooLong(question.Description) || TooLong(question.OtherLabel)) {
                    return Invalid("Question text exceeds 16 KiB.");
                }
                if ((question.Options == null || question.Options.Count == 0) && !question.AllowOther) {
                    return Invalid("A question requires a choice or Other input.");
                }

                HashSet<string> optionIds = new HashSet<string>(StringComparer.Ordinal);
                int recommendationCount = 0;
                if (question.Options != null) {
                    foreach (OptionChoice choice in question.Options) {
                        if (choice == null || String.IsNullOrWhiteSpace(choice.Id) || !optionIds.Add(choice.Id)) {
                            return Invalid("Option IDs must be non-empty and unique within a question.");
                        }
                        if (String.IsNullOrWhiteSpace(choice.Label)) return Invalid("Option labels must not be blank.");
                        if (TooLong(choice.Id) || TooLong(choice.Label) || TooLong(choice.Description)) {
                            return Invalid("Option text exceeds 16 KiB.");
                        }
                        if (choice.Recommended) recommendationCount++;
                    }
                }
                if (question.Mode == SelectionMode.Single && recommendationCount > 1) {
                    return Invalid("A single-choice question can have at most one recommendation.");
                }

                if (request.AutoResolutionMs.HasValue && question.Required) {
                    if (recommendationCount == 0) return Invalid("Auto resolution requires recommendations for every required question.");
                }
            }
            return ValidationResult.Valid();
        }

        private static bool TooLong(string value) {
            return value != null && value.Length > MaximumTextCharacters;
        }

        private static ValidationResult Invalid(string message) {
            return ValidationResult.Invalid("INVALID_REQUEST", message);
        }
    }
}
