using System.Collections.Generic;
using CodexCue.Core;

namespace CodexCue.Tests {
    internal static class RequestNormalizerTests {
        public static void Register(TestRegistry tests) {
            tests.Add("RequestNormalizer accepts custom cancel label", delegate {
                IDictionary<string, object> input = Fixtures.Object("new-multi-question.json");
                input["cancelLabel"] = "Skip";
                OptionRequest request = RequestNormalizer.Normalize(input);
                Assert.Equal("Skip", request.CancelLabel);
                OptionRequest roundTrip = CodexCue.Ipc.PipeProtocol.RequestFromPayload(CodexCue.Ipc.PipeProtocol.RequestPayload(request));
                Assert.Equal("Skip", roundTrip.CancelLabel);
            });
            tests.Add("RequestNormalizer new request defaults single required and other", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                Assert.Equal(2, request.Questions.Count);
                Assert.Equal(SelectionMode.Single, request.Questions[0].Mode);
                Assert.True(request.Questions[0].Required);
                Assert.True(request.Questions[0].AllowOther);
                Assert.Equal(ReviewMode.Auto, request.ReviewMode);
                Assert.Equal(900000, request.MaxWaitMs);
                Assert.Equal(null, request.AutoResolutionMs);
            });

            tests.Add("RequestNormalizer legacy request becomes one question", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("legacy-single-question.json"));
                Assert.True(request.IsLegacy);
                Assert.Equal(1, request.Questions.Count);
                Assert.Equal("legacy-question", request.Questions[0].Id);
                Assert.Equal("simple", request.Questions[0].Options[0].Id);
                Assert.True(request.Questions[0].Options[0].Recommended);
                Assert.True(request.CompatibilityWarnings.Contains("browser_mode_removed"));
            });

            tests.Add("RequestNormalizer legacy none mode suppresses desktop UI", delegate {
                IDictionary<string, object> input = Fixtures.Object("legacy-single-question.json");
                input["uiMode"] = "none";

                OptionRequest request = RequestNormalizer.Normalize(input);

                Assert.True(request.SuppressUi);
                IDictionary<string, object> payload = CodexCue.Ipc.PipeProtocol.RequestPayload(request);
                OptionRequest roundTrip = CodexCue.Ipc.PipeProtocol.RequestFromPayload(payload);
                Assert.True(roundTrip.SuppressUi);
            });

            tests.Add("RequestNormalizer string options keep stable ids and labels", delegate {
                IDictionary<string, object> input = Fixtures.Object("new-multi-question.json");
                object[] questions = (object[])input["questions"];
                IDictionary<string, object> first = (IDictionary<string, object>)questions[0];
                first["options"] = new object[] { "first", "second" };

                OptionRequest request = RequestNormalizer.Normalize(input);

                Assert.Equal("first", request.Questions[0].Options[0].Id);
                Assert.Equal("first", request.Questions[0].Options[0].Label);
                Assert.Equal("second", request.Questions[0].Options[1].Id);
                Assert.Equal("second", request.Questions[0].Options[1].Label);
            });

            tests.Add("RequestNormalizer accepts open ended question alias", delegate {
                IDictionary<string, object> question = new Dictionary<string, object>();
                question["id"] = "details";
                question["question"] = "What should Codex know?";
                question["options"] = new object[0];
                question["allowOther"] = true;
                IDictionary<string, object> input = new Dictionary<string, object>();
                input["questions"] = new object[] { question };

                OptionRequest request = RequestNormalizer.Normalize(input);

                Assert.Equal("What should Codex know?", request.Questions[0].Prompt);
                Assert.Equal(0, request.Questions[0].Options.Count);
                Assert.True(request.Questions[0].AllowOther);
                Assert.True(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestNormalizer preserves invalid selection mode for validation", delegate {
                IDictionary<string, object> input = Fixtures.Object("new-multi-question.json");
                object[] questions = (object[])input["questions"];
                ((IDictionary<string, object>)questions[0])["mode"] = "ranked";

                OptionRequest request = RequestNormalizer.Normalize(input);

                Assert.False(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestNormalizer preserves invalid review mode for validation", delegate {
                IDictionary<string, object> input = Fixtures.Object("new-multi-question.json");
                input["reviewMode"] = "sometimes";

                OptionRequest request = RequestNormalizer.Normalize(input);

                Assert.False(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("ResultFactory legacy submitted result preserves old fields", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("legacy-single-question.json"));
                QuestionAnswer answer = new QuestionAnswer {
                    QuestionId = "legacy-question",
                    SelectedOptionIds = new List<string> { "simple" },
                    OtherText = null
                };
                OptionResult result = ResultFactory.Submitted(request, new List<QuestionAnswer> { answer }, "user");
                Assert.Equal("submitted", result.Status);
                Assert.Equal("simple", result.SelectedOptionId);
                Assert.Equal("simple", result.SelectedOption.Id);
                Assert.Equal("desktop-wpf", result.Source);
            });

            tests.Add("ResultFactory cancelled and timed out expose no partial answers", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                Assert.Equal(0, ResultFactory.Cancelled(request).Answers.Count);
                Assert.Equal(0, ResultFactory.TimedOut(request).Answers.Count);
            });

            tests.Add("ResultFactory auto submit selects recommendations in question order", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.Questions[0].Options[0].Recommended = false;
                request.Questions[0].Options[1].Recommended = true;
                request.Questions[1].Options[0].Recommended = true;
                request.Questions[1].Options[1].Recommended = true;

                OptionResult result = ResultFactory.AutoSubmitted(request);

                Assert.Equal("submitted", result.Status);
                Assert.Equal("auto", result.Resolution);
                Assert.Equal(request.Questions[0].Id, result.Answers[0].QuestionId);
                Assert.Equal(request.Questions[0].Options[1].Id, result.Answers[0].SelectedOptionIds[0]);
                Assert.Equal(2, result.Answers[1].SelectedOptionIds.Count);
            });
        }
    }
}
