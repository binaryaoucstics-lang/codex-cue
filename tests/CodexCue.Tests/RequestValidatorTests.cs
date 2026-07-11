using CodexCue.Core;

namespace CodexCue.Tests {
    internal static class RequestValidatorTests {
        public static void Register(TestRegistry tests) {
            tests.Add("RequestValidator rejects duplicate question ids", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("invalid-duplicate-id.json"));
                ValidationResult result = RequestValidator.Validate(request);
                Assert.False(result.IsValid);
                Assert.Equal("INVALID_REQUEST", result.Code);
            });

            tests.Add("RequestValidator rejects more than 16 KiB of text", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.Questions[0].Prompt = new string('x', 16 * 1024 + 1);
                Assert.False(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestValidator auto resolution requires all recommendations", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.AutoResolutionMs = 60000;
                foreach (OptionChoice choice in request.Questions[1].Options) choice.Recommended = false;
                Assert.False(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestValidator accepts dynamic option counts", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                for (int index = 0; index < 20; index++) {
                    request.Questions[0].Options.Add(new OptionChoice { Id = "extra-" + index, Label = "Extra " + index });
                }
                Assert.True(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestValidator requires choice or Other", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.Questions[0].Options.Clear();
                request.Questions[0].AllowOther = false;
                Assert.False(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestValidator rejects blank question prompts", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.Questions[0].Prompt = "  ";
                Assert.False(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestValidator rejects blank and duplicate option identities", delegate {
                OptionRequest blankId = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                blankId.Questions[0].Options[0].Id = " ";
                Assert.False(RequestValidator.Validate(blankId).IsValid);

                OptionRequest blankLabel = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                blankLabel.Questions[0].Options[0].Label = "";
                Assert.False(RequestValidator.Validate(blankLabel).IsValid);

                OptionRequest duplicate = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                duplicate.Questions[0].Options[1].Id = duplicate.Questions[0].Options[0].Id;
                Assert.False(RequestValidator.Validate(duplicate).IsValid);
            });

            tests.Add("RequestValidator rejects multiple recommendations for single choice", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.Questions[0].Options[0].Recommended = true;
                request.Questions[0].Options[1].Recommended = true;
                Assert.False(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestValidator allows multiple recommendations for multiple choice", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.Questions[1].Options[0].Recommended = true;
                request.Questions[1].Options[1].Recommended = true;
                Assert.True(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestValidator protects every caller text field", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.Questions[0].Options[0].Description = new string('x', 16 * 1024 + 1);
                Assert.False(RequestValidator.Validate(request).IsValid);
            });

            tests.Add("RequestValidator enforces auto resolution timing bounds", delegate {
                OptionRequest tooShort = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                tooShort.AutoResolutionMs = 59999;
                Assert.False(RequestValidator.Validate(tooShort).IsValid);

                OptionRequest minimum = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                minimum.AutoResolutionMs = 60000;
                Assert.True(RequestValidator.Validate(minimum).IsValid);

                OptionRequest maximum = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                maximum.AutoResolutionMs = 240000;
                Assert.True(RequestValidator.Validate(maximum).IsValid);

                OptionRequest tooLong = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                tooLong.AutoResolutionMs = 240001;
                Assert.False(RequestValidator.Validate(tooLong).IsValid);
            });

            tests.Add("RequestValidator requires a positive maximum wait", delegate {
                OptionRequest request = RequestNormalizer.Normalize(Fixtures.Object("new-multi-question.json"));
                request.MaxWaitMs = 0;
                Assert.False(RequestValidator.Validate(request).IsValid);
            });
        }
    }
}
