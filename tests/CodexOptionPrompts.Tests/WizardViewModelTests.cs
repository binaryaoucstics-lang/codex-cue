using System;
using System.Collections.Generic;
using CodexOptionPrompts.Core;
using CodexOptionPrompts.Ui;

namespace CodexOptionPrompts.Tests {
    internal static class WizardViewModelTests {
        public static void Register(TestRegistry tests) {
            tests.Add("WizardViewModel reports progress and enters automatic review", delegate {
                WizardViewModel viewModel = new WizardViewModel(WizardFixtures.TwoRequiredQuestions());
                Assert.Equal(0, viewModel.CurrentIndex);
                Assert.Equal(2, viewModel.QuestionCount);
                Assert.Equal(0.5, viewModel.ProgressFraction);

                viewModel.Select("a");
                viewModel.Next();
                Assert.Equal(1, viewModel.CurrentIndex);
                Assert.Equal(1.0, viewModel.ProgressFraction);
                viewModel.Select("a");
                viewModel.Next();
                Assert.True(viewModel.IsReview);
            });

            tests.Add("WizardViewModel blocks required question and exposes error", delegate {
                WizardViewModel viewModel = new WizardViewModel(WizardFixtures.SingleQuestion());
                viewModel.Next();
                Assert.Equal(0, viewModel.CurrentIndex);
                Assert.True(!String.IsNullOrEmpty(viewModel.ErrorText));
            });

            tests.Add("WizardViewModel completes single question without review", delegate {
                WizardViewModel viewModel = new WizardViewModel(WizardFixtures.SingleQuestion());
                IList<QuestionAnswer> completed = null;
                viewModel.Completed += delegate(object sender, WizardCompletedEventArgs args) { completed = args.Answers; };
                viewModel.Select("b");
                viewModel.Next();
                Assert.Equal(1, completed.Count);
                Assert.Equal("b", completed[0].SelectedOptionIds[0]);
            });

            tests.Add("WizardViewModel review edit and submit preserve answers", delegate {
                WizardViewModel viewModel = new WizardViewModel(WizardFixtures.TwoRequiredQuestions());
                IList<QuestionAnswer> completed = null;
                viewModel.Completed += delegate(object sender, WizardCompletedEventArgs args) { completed = args.Answers; };
                viewModel.Select("a");
                viewModel.Next();
                viewModel.Select("b");
                viewModel.Next();
                viewModel.Edit("q1");
                viewModel.Select("b");
                viewModel.OpenReview();
                viewModel.Submit();
                Assert.Equal("b", completed[0].SelectedOptionIds[0]);
                Assert.Equal("b", completed[1].SelectedOptionIds[0]);
            });

            tests.Add("WizardViewModel cancellation raises typed event", delegate {
                WizardViewModel viewModel = new WizardViewModel(WizardFixtures.SingleQuestion());
                bool cancelled = false;
                viewModel.Cancelled += delegate { cancelled = true; };
                viewModel.Cancel();
                Assert.True(cancelled);
            });

            tests.Add("WizardViewModel notifies transition properties", delegate {
                WizardViewModel viewModel = new WizardViewModel(WizardFixtures.TwoRequiredQuestions());
                List<string> changed = new List<string>();
                viewModel.PropertyChanged += delegate(object sender, System.ComponentModel.PropertyChangedEventArgs args) {
                    changed.Add(args.PropertyName);
                };
                viewModel.Select("a");
                viewModel.Next();
                Assert.True(changed.Contains("CurrentQuestion"));
                Assert.True(changed.Contains("CurrentIndex"));
                Assert.True(changed.Contains("ProgressFraction"));
                Assert.True(changed.Contains("ErrorText"));
                Assert.True(changed.Contains("IsReview"));
                Assert.True(changed.Contains("NextLabel"));
            });
        }
    }
}
