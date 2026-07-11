using System.Globalization;
using System.Threading;
using CodexOptionPrompts.Localization;

namespace CodexOptionPrompts.Tests {
    internal static class LocalizationTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Localization shell strings follow Chinese and English UI culture", delegate {
                CultureInfo original = Thread.CurrentThread.CurrentUICulture;
                try {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                    Assert.Equal("Next", Strings.Next);
                    Assert.Equal("Question 1 of 2", Strings.Progress(1, 2));
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                    Assert.Equal("下一步", Strings.Next);
                    Assert.Equal("第 1 题，共 2 题", Strings.Progress(1, 2));
                } finally {
                    Thread.CurrentThread.CurrentUICulture = original;
                }
            });
        }
    }
}
