using System;
using System.Globalization;

namespace CodexOptionPrompts.Localization {
    public static class Strings {
        private static bool Chinese {
            get { return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase); }
        }

        public static string Cancel { get { return Chinese ? "取消" : "Cancel"; } }
        public static string Back { get { return Chinese ? "上一步" : "Back"; } }
        public static string Next { get { return Chinese ? "下一步" : "Next"; } }
        public static string ReviewAnswers { get { return Chinese ? "检查答案" : "Review"; } }
        public static string SubmitAnswers { get { return Chinese ? "提交答案" : "Submit"; } }
        public static string OtherPlaceholder { get { return Chinese ? "其他：输入自定义答案..." : "Other: enter a custom answer..."; } }
        public static string CustomAnswerHint { get { return Chinese ? "输入自定义答案..." : "Enter a custom answer..."; } }
        public static string ListSeparator { get { return Chinese ? "、" : ", "; } }
        public static string AnswerRequired { get { return Chinese ? "请选择一个选项或填写自己的答案。" : "Choose an option or enter your own answer."; } }
        public static string CompleteBeforeReview { get { return Chinese ? "请先完成所有必答题。" : "Complete all required questions before review."; } }
        public static string CompleteBeforeSubmit { get { return Chinese ? "请先完成所有必答题再提交。" : "Complete all required questions before submitting."; } }
        public static string ReviewTitle { get { return Chinese ? "检查你的答案" : "Review your answers"; } }
        public static string ReviewDescription { get { return Chinese ? "确认无误后提交，也可以返回修改。" : "Submit when everything looks right, or go back to make changes."; } }
        public static string Edit { get { return Chinese ? "修改" : "Edit"; } }
        public static string NoAnswer { get { return Chinese ? "未回答" : "Not answered"; } }
        public static string ConfirmCancelTitle { get { return Chinese ? "取消这次选择？" : "Cancel this prompt?"; } }
        public static string ConfirmCancelBody { get { return Chinese ? "已经填写的内容不会提交给 Codex。" : "Your current answers will not be submitted to Codex."; } }
        public static string KeepAnswering { get { return Chinese ? "继续填写" : "Keep answering"; } }
        public static string Discard { get { return Chinese ? "放弃" : "Discard"; } }

        public static string Progress(int current, int total) {
            return Chinese
                ? String.Format(CultureInfo.CurrentCulture, "第 {0} 题，共 {1} 题", current, total)
                : String.Format(CultureInfo.CurrentCulture, "Question {0} of {1}", current, total);
        }
    }
}
