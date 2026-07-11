using CodexCue.Core;

namespace CodexCue.Tests {
    internal static class WizardStateTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Wizard single option and other clear each other", delegate {
                WizardState state = WizardFixtures.SingleQuestion();
                state.Select("q1", "a");
                state.SetOther("q1", "custom");
                Assert.Equal(0, state.Answer("q1").SelectedOptionIds.Count);
                state.Select("q1", "b");
                Assert.Equal(null, state.Answer("q1").OtherText);
            });

            tests.Add("Wizard multiple options coexist with other", delegate {
                WizardState state = WizardFixtures.MultipleQuestion();
                state.Select("q1", "a");
                state.Select("q1", "b");
                state.SetOther("q1", "custom");
                Assert.Equal(2, state.Answer("q1").SelectedOptionIds.Count);
                Assert.Equal("custom", state.Answer("q1").OtherText);
            });

            tests.Add("Wizard multiple select toggles membership and preserves option order", delegate {
                WizardState state = WizardFixtures.MultipleQuestion();
                state.Select("q1", "b");
                state.Select("q1", "a");
                Assert.Equal("a", state.BuildAnswers()[0].SelectedOptionIds[0]);
                Assert.Equal("b", state.BuildAnswers()[0].SelectedOptionIds[1]);
                state.Select("q1", "b");
                Assert.Equal(1, state.Answer("q1").SelectedOptionIds.Count);
                Assert.Equal("a", state.Answer("q1").SelectedOptionIds[0]);
            });

            tests.Add("Wizard required whitespace other cannot continue", delegate {
                WizardState state = WizardFixtures.SingleQuestion();
                state.SetOther("q1", "   ");
                Assert.False(state.CanContinue);
                Assert.Equal(null, state.Answer("q1").OtherText);
            });

            tests.Add("Wizard preserves nonblank other text exactly", delegate {
                WizardState state = WizardFixtures.SingleQuestion();
                state.SetOther("q1", "  custom answer  ");
                Assert.Equal("  custom answer  ", state.Answer("q1").OtherText);
            });

            tests.Add("Wizard multi question auto review can edit answer", delegate {
                WizardState state = WizardFixtures.TwoRequiredQuestions();
                WizardFixtures.AnswerAll(state);
                state.OpenReview();
                state.Edit("q1");
                Assert.False(state.IsReview);
                Assert.Equal(0, state.CurrentIndex);
            });

            tests.Add("Wizard build answers preserves request question order", delegate {
                WizardState state = WizardFixtures.TwoRequiredQuestions();
                state.Select("q2", "b");
                state.Select("q1", "a");
                Assert.Equal("q1", state.BuildAnswers()[0].QuestionId);
                Assert.Equal("q2", state.BuildAnswers()[1].QuestionId);
            });
        }
    }
}
