using System;
using System.Collections.Generic;
using System.Text;

namespace CodexOptionPrompts.Install {
    public static class CodexConfigEditor {
        private const string Section = "[plugins.\"codex-option-prompts@personal\".mcp_servers.codex_option_prompts]";

        public static string EnableDirectQuestions(string source) {
            string text = source ?? "";
            string newline = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            List<string> lines = new List<string>(text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            int start = FindSection(lines);
            if (start < 0) {
                while (lines.Count > 0 && String.IsNullOrWhiteSpace(lines[lines.Count - 1])) lines.RemoveAt(lines.Count - 1);
                if (lines.Count > 0) lines.Add("");
                lines.Add(Section);
                lines.Add("enabled = true");
                lines.Add("default_tools_approval_mode = \"approve\"");
                lines.Add("");
                return String.Join(newline, lines.ToArray());
            }

            int end = FindNextSection(lines, start + 1);
            bool enabled = false;
            bool approval = false;
            for (int index = start + 1; index < end; index++) {
                string trimmed = lines[index].TrimStart();
                if (trimmed.StartsWith("enabled", StringComparison.Ordinal) && IsAssignment(trimmed, "enabled")) {
                    lines[index] = "enabled = true";
                    enabled = true;
                } else if (trimmed.StartsWith("default_tools_approval_mode", StringComparison.Ordinal) &&
                    IsAssignment(trimmed, "default_tools_approval_mode")) {
                    lines[index] = "default_tools_approval_mode = \"approve\"";
                    approval = true;
                }
            }
            int insert = start + 1;
            if (!enabled) { lines.Insert(insert++, "enabled = true"); end++; }
            if (!approval) lines.Insert(insert, "default_tools_approval_mode = \"approve\"");
            return String.Join(newline, lines.ToArray());
        }

        private static int FindSection(IList<string> lines) {
            for (int index = 0; index < lines.Count; index++) {
                if (String.Equals(lines[index].Trim(), Section, StringComparison.Ordinal)) return index;
            }
            return -1;
        }

        private static int FindNextSection(IList<string> lines, int start) {
            for (int index = start; index < lines.Count; index++) {
                string value = lines[index].Trim();
                if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal)) return index;
            }
            return lines.Count;
        }

        private static bool IsAssignment(string line, string key) {
            int separator = line.IndexOf('=');
            return separator >= 0 && String.Equals(line.Substring(0, separator).Trim(), key, StringComparison.Ordinal);
        }
    }
}
