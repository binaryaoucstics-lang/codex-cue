using System;
using System.Collections.Generic;

namespace CodexOptionPrompts.Core {
    public static class RequestNormalizer {
        public static OptionRequest Normalize(IDictionary<string, object> input) {
            if (input == null) throw new ArgumentNullException("input");
            object questionsValue;
            return input.TryGetValue("questions", out questionsValue)
                ? NormalizeQuestions(input, questionsValue)
                : NormalizeLegacy(input);
        }

        private static OptionRequest NormalizeQuestions(IDictionary<string, object> input, object questionsValue) {
            OptionRequest request = CreateBase(input);
            object[] questions = AsArray(questionsValue);
            for (int index = 0; index < questions.Length; index++) {
                IDictionary<string, object> value = AsObject(questions[index]);
                OptionQuestion question = new OptionQuestion {
                    Id = StringValue(value, "id", ""),
                    Prompt = StringValue(value, "prompt", StringValue(value, "question", "")),
                    Description = StringValue(value, "description", ""),
                    Required = BoolValue(value, "required", true),
                    AllowOther = BoolValue(value, "allowOther", true),
                    OtherLabel = StringValue(value, "otherLabel", "")
                };
                string mode = StringValue(value, "mode", "single");
                if (String.Equals(mode, "single", StringComparison.OrdinalIgnoreCase)) question.Mode = SelectionMode.Single;
                else if (String.Equals(mode, "multiple", StringComparison.OrdinalIgnoreCase)) question.Mode = SelectionMode.Multiple;
                else question.Mode = SelectionMode.Invalid;
                object optionsValue;
                if (value.TryGetValue("options", out optionsValue)) AddOptions(question, optionsValue, null);
                request.Questions.Add(question);
            }
            return request;
        }

        private static OptionRequest NormalizeLegacy(IDictionary<string, object> input) {
            OptionRequest request = CreateBase(input);
            request.IsLegacy = true;
            OptionQuestion question = new OptionQuestion {
                Id = "legacy-question",
                Prompt = StringValue(input, "question", ""),
                Description = ""
            };
            object optionsValue;
            if (input.TryGetValue("options", out optionsValue)) {
                AddOptions(question, optionsValue, StringValue(input, "recommendedOptionId", ""));
            }
            request.Questions.Add(question);
            return request;
        }

        private static OptionRequest CreateBase(IDictionary<string, object> input) {
            OptionRequest request = new OptionRequest {
                SessionId = StringValue(input, "sessionId", Guid.NewGuid().ToString("N")),
                Title = StringValue(input, "title", StringValue(input, "header", "")),
                MaxWaitMs = IntValue(input, "maxWaitMs", 900000),
                AutoResolutionMs = NullableIntValue(input, "autoResolutionMs")
            };
            string review = StringValue(input, "reviewMode", "auto");
            if (String.Equals(review, "always", StringComparison.OrdinalIgnoreCase)) request.ReviewMode = ReviewMode.Always;
            else if (String.Equals(review, "never", StringComparison.OrdinalIgnoreCase)) request.ReviewMode = ReviewMode.Never;
            else if (!String.Equals(review, "auto", StringComparison.OrdinalIgnoreCase)) request.ReviewMode = ReviewMode.Invalid;
            string uiMode = StringValue(input, "uiMode", "");
            if (String.Equals(uiMode, "none", StringComparison.OrdinalIgnoreCase)) request.SuppressUi = true;
            else if (String.Equals(uiMode, "browser", StringComparison.OrdinalIgnoreCase)) {
                request.CompatibilityWarnings.Add("browser_mode_removed");
            }
            return request;
        }

        private static void AddOptions(OptionQuestion question, object value, string recommendedId) {
            object[] options = AsArray(value);
            for (int index = 0; index < options.Length; index++) {
                string text = options[index] as string;
                OptionChoice choice;
                if (text != null) {
                    choice = new OptionChoice { Id = text, Label = text };
                } else {
                    IDictionary<string, object> item = AsObject(options[index]);
                    string label = StringValue(item, "label", "");
                    choice = new OptionChoice {
                        Id = StringValue(item, "id", label),
                        Label = label,
                        Description = StringValue(item, "description", ""),
                        Recommended = BoolValue(item, "recommended", false)
                    };
                }
                if (!String.IsNullOrEmpty(recommendedId) && String.Equals(choice.Id, recommendedId, StringComparison.Ordinal)) {
                    choice.Recommended = true;
                }
                question.Options.Add(choice);
            }
        }

        private static IDictionary<string, object> AsObject(object value) {
            IDictionary<string, object> result = value as IDictionary<string, object>;
            if (result == null) throw new ArgumentException("Expected a JSON object.");
            return result;
        }

        private static object[] AsArray(object value) {
            object[] result = value as object[];
            if (result == null) throw new ArgumentException("Expected a JSON array.");
            return result;
        }

        private static string StringValue(IDictionary<string, object> values, string key, string defaultValue) {
            object value;
            return values.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : defaultValue;
        }

        private static bool BoolValue(IDictionary<string, object> values, string key, bool defaultValue) {
            object value;
            return values.TryGetValue(key, out value) && value != null ? Convert.ToBoolean(value) : defaultValue;
        }

        private static int IntValue(IDictionary<string, object> values, string key, int defaultValue) {
            object value;
            return values.TryGetValue(key, out value) && value != null ? Convert.ToInt32(value) : defaultValue;
        }

        private static int? NullableIntValue(IDictionary<string, object> values, string key) {
            object value;
            return values.TryGetValue(key, out value) && value != null ? (int?)Convert.ToInt32(value) : null;
        }
    }
}
