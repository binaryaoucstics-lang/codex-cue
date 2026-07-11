using CodexCue.Tests;

namespace CodexCue.UiTests {
    internal static class WizardUiTests {
        public static void Register(TestRegistry tests) {
            tests.Add("SettingsUi displays and changes option count", delegate {
                using (UiDriver ui = UiDriver.StartSettings()) {
                    ui.WaitForWindow("SettingsWindow", 3000);
                    Assert.True(ui.IsFullyVisible("OptionCountValue"));
                    int initial = System.Int32.Parse(ui.Name("OptionCountValue"));
                    if (initial < 6) {
                        ui.Click("IncreaseOptionCount");
                        Assert.Equal(initial + 1, System.Int32.Parse(ui.Name("OptionCountValue")));
                    } else {
                        ui.Click("DecreaseOptionCount");
                        Assert.Equal(initial - 1, System.Int32.Parse(ui.Name("OptionCountValue")));
                    }
                    ui.Click("SettingsCancelButton");
                    Assert.True(ui.WaitForExit(1000));
                }
            });
            tests.Add("WizardUi shows four options without scrolling", delegate {
                using (UiDriver ui = UiDriver.StartManyOptions()) {
                    ui.WaitForWindow("PromptWindow", 3000);
                    Assert.True(ui.IsFullyVisible("Option_installer"));
                    Assert.True(ui.IsFullyVisible("Option_portable"));
                    Assert.True(ui.IsFullyVisible("Option_weekend"));
                    Assert.True(ui.IsFullyVisible("Option_explore"));
                    Assert.Equal(0, ui.VisibleScrollBarCount());
                }
            });
            tests.Add("WizardUi demo completes single and multiple answers", delegate {
                using (UiDriver ui = UiDriver.Start("--demo --automation")) {
                    ui.WaitForWindow("PromptWindow", 3000);
                    ui.Click("Option_installer");
                    ui.Click("NextButton");
                    ui.Click("Option_windows");
                    ui.Click("Option_portable");
                    ui.SetText("OtherText", "Custom note");
                    ui.Click("NextButton");
                    ui.Click("SubmitButton");
                    Assert.True(ui.WaitForExit(1000));
                }
            });

            tests.Add("WizardUi exposes accessible names and visual focus order", delegate {
                using (UiDriver ui = UiDriver.Start("--demo --automation")) {
                    ui.WaitForWindow("PromptWindow", 3000);
                    Assert.True(ui.IsTopmost("PromptWindow"));
                    Assert.Equal("第 1 题，共 2 题", ui.Name("ProgressText"));
                    Assert.Equal("本机安装 + 可分发安装包", ui.Name("Option_installer"));
                    Assert.True(ui.TabOrderIs("Option_installer", "Option_portable", "OtherText", "CancelButton", "NextButton"));
                }
            });

            tests.Add("WizardUi required validation remains on current question", delegate {
                using (UiDriver ui = UiDriver.Start("--demo --automation")) {
                    ui.WaitForWindow("PromptWindow", 3000);
                    ui.Click("NextButton");
                    Assert.True(ui.Name("ValidationError").Length > 0);
                    Assert.Equal("第 1 题，共 2 题", ui.Name("ProgressText"));
                }
            });

            tests.Add("WizardUi number Enter and Alt arrows navigate", delegate {
                using (UiDriver ui = UiDriver.Start("--demo --automation")) {
                    ui.WaitForWindow("PromptWindow", 3000);
                    ui.Focus("Option_installer");
                    ui.SendKeys("1");
                    ui.SendKeys("{ENTER}");
                    Assert.Equal("第 2 题，共 2 题", ui.Name("ProgressText"));
                    ui.SendKeys("%{LEFT}");
                    Assert.Equal("第 1 题，共 2 题", ui.Name("ProgressText"));
                    ui.SendKeys("%{RIGHT}");
                    Assert.Equal("第 2 题，共 2 题", ui.Name("ProgressText"));
                }
            });

            tests.Add("WizardUi direction and space operate native choices", delegate {
                using (UiDriver ui = UiDriver.Start("--demo --automation")) {
                    ui.WaitForWindow("PromptWindow", 3000);
                    ui.Focus("Option_installer");
                    ui.SendKeys("{RIGHT}");
                    Assert.True(ui.WaitUntilFocused("Option_portable", 1500));
                    ui.SendKeys(" ");
                    Assert.True(ui.WaitUntilSelected("Option_portable", 1500));
                    ui.SendKeys("{ENTER}");
                    ui.Focus("Option_windows");
                    ui.SendKeys(" ");
                    Assert.True(ui.WaitUntilSelected("Option_windows", 1500));
                    ui.Focus("Option_portable");
                    ui.SendKeys(" ");
                    Assert.True(ui.WaitUntilSelected("Option_portable", 1500));
                }
            });

            tests.Add("WizardUi Escape cancels the current demo", delegate {
                using (UiDriver escape = UiDriver.Start("--demo --automation")) {
                    escape.WaitForWindow("PromptWindow", 3000);
                    escape.Focus("Option_installer");
                    escape.SendKeys("{ESC}");
                    Assert.True(escape.WaitForExit(1000));
                }
            });

            tests.Add("WizardUi Alt F4 cancels the current demo", delegate {
                using (UiDriver altF4 = UiDriver.Start("--demo --automation")) {
                    altF4.WaitForWindow("PromptWindow", 3000);
                    altF4.Focus("Option_installer");
                    altF4.SendAltF4();
                    if (!altF4.WaitForExit(1000)) {
                        throw new System.Exception("Alt+F4 did not exit; prompt=" + altF4.Exists("PromptWindow") + ", confirm=" + altF4.Exists("ConfirmWindow"));
                    }
                }
            });

            tests.Add("WizardUi cancellation confirmation keeps or discards answers", delegate {
                using (UiDriver ui = UiDriver.Start("--demo --automation")) {
                    ui.WaitForWindow("PromptWindow", 3000);
                    ui.Click("Option_installer");
                    ui.Click("CancelButton");
                    ui.WaitForWindow("ConfirmWindow", 1000);
                    ui.Click("KeepButton");
                    Assert.True(ui.Exists("PromptWindow"));
                    ui.Click("CancelButton");
                    ui.WaitForWindow("ConfirmWindow", 1000);
                    ui.Click("DiscardButton");
                    Assert.True(ui.WaitForExit(1000));
                }
            });

            tests.Add("WizardUi review edit preserves later answers", delegate {
                using (UiDriver ui = UiDriver.Start("--demo --automation")) {
                    ui.WaitForWindow("PromptWindow", 3000);
                    ui.Click("Option_installer");
                    ui.Click("NextButton");
                    ui.Click("Option_windows");
                    ui.Click("NextButton");
                    ui.Click("Edit_publish-mode");
                    ui.Click("Option_portable");
                    ui.Click("NextButton");
                    Assert.True(ui.IsSelected("Option_windows"));
                    ui.Click("NextButton");
                    ui.Click("SubmitButton");
                    Assert.True(ui.WaitForExit(1000));
                }
            });
        }
    }
}
