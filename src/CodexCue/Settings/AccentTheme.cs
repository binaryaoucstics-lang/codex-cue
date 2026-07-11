using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace CodexCue.Settings {
    public sealed class AccentDefinition {
        public AccentDefinition(string id, string label, string primary, string start, string end, string selected) {
            Id = id; Label = label;
            Primary = (Color)ColorConverter.ConvertFromString(primary);
            Start = (Color)ColorConverter.ConvertFromString(start);
            End = (Color)ColorConverter.ConvertFromString(end);
            Selected = (Color)ColorConverter.ConvertFromString(selected);
        }

        public string Id { get; private set; }
        public string Label { get; private set; }
        public Color Primary { get; private set; }
        public Color Start { get; private set; }
        public Color End { get; private set; }
        public Color Selected { get; private set; }
    }

    public static class AccentTheme {
        private static readonly IList<AccentDefinition> accents = new List<AccentDefinition> {
            new AccentDefinition("blue", "蓝色", "#2563EB", "#1D4ED8", "#60A5FA", "#F5F8FF"),
            new AccentDefinition("indigo", "靛青", "#4F46E5", "#4338CA", "#818CF8", "#F5F3FF"),
            new AccentDefinition("violet", "紫罗兰", "#7C3AED", "#6D28D9", "#A78BFA", "#F7F3FF"),
            new AccentDefinition("rose", "玫红", "#E11D48", "#BE123C", "#FB7185", "#FFF1F4"),
            new AccentDefinition("orange", "橙色", "#EA580C", "#C2410C", "#FB923C", "#FFF7ED"),
            new AccentDefinition("emerald", "翡翠绿", "#059669", "#047857", "#34D399", "#ECFDF5"),
            new AccentDefinition("teal", "青绿色", "#0D9488", "#0F766E", "#2DD4BF", "#F0FDFA"),
            new AccentDefinition("slate", "石板灰", "#475569", "#334155", "#94A3B8", "#F8FAFC")
        };

        public static IList<AccentDefinition> All { get { return accents; } }

        public static AccentDefinition Find(string id) {
            foreach (AccentDefinition accent in accents) {
                if (String.Equals(accent.Id, id, StringComparison.OrdinalIgnoreCase)) return accent;
            }
            return accents[0];
        }

        public static void Apply(string id) {
            if (System.Windows.Application.Current == null) return;
            AccentDefinition accent = Find(id);
            System.Windows.Application.Current.Resources["PrimaryBrush"] = FrozenBrush(accent.Primary);
            System.Windows.Application.Current.Resources["SelectedBrush"] = FrozenBrush(accent.Selected);
            System.Windows.Application.Current.Resources["AccentStartColor"] = accent.Start;
            System.Windows.Application.Current.Resources["AccentPrimaryColor"] = accent.Primary;
            System.Windows.Application.Current.Resources["AccentEndColor"] = accent.End;
        }

        private static SolidColorBrush FrozenBrush(Color color) {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
