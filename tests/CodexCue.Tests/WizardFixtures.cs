using CodexCue.Core;

namespace CodexCue.Tests {
    internal static class WizardFixtures {
        public static WizardState SingleQuestion() {
            return new WizardState(Request(SelectionMode.Single, ReviewMode.Auto));
        }

        public static WizardState MultipleQuestion() {
            return new WizardState(Request(SelectionMode.Multiple, ReviewMode.Auto));
        }

        public static WizardState TwoRequiredQuestions() {
            OptionRequest request = Request(SelectionMode.Single, ReviewMode.Auto);
            request.Questions.Add(Question("q2", SelectionMode.Multiple));
            return new WizardState(request);
        }

        public static void AnswerAll(WizardState state) {
            foreach (OptionQuestion question in state.Request.Questions) {
                state.Select(question.Id, "a");
            }
        }

        private static OptionRequest Request(SelectionMode mode, ReviewMode reviewMode) {
            OptionRequest request = new OptionRequest { ReviewMode = reviewMode };
            request.Questions.Add(Question("q1", mode));
            return request;
        }

        private static OptionQuestion Question(string id, SelectionMode mode) {
            OptionQuestion question = new OptionQuestion {
                Id = id,
                Prompt = "Question " + id,
                Mode = mode,
                Required = true,
                AllowOther = true
            };
            question.Options.Add(new OptionChoice { Id = "a", Label = "A" });
            question.Options.Add(new OptionChoice { Id = "b", Label = "B" });
            return question;
        }
    }
}
