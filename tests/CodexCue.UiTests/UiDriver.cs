using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using CodexCue.Tests;

namespace CodexCue.UiTests {
    internal sealed class UiDriver : IDisposable {
        private const byte VirtualKeyAlt = 0x12;
        private const byte VirtualKeyF4 = 0x73;
        private const uint KeyUp = 0x0002;
        private readonly Process process;
        private readonly double captureScale;

        private UiDriver(Process process, double captureScale) {
            this.process = process;
            this.captureScale = captureScale;
        }

        public static UiDriver Start(string arguments) { return Start(arguments, 1.0); }

        public static UiDriver Start(string arguments, double scale) {
            return Start(arguments, scale, false, false, false);
        }

        public static UiDriver StartManyOptions() { return Start("--demo --automation", 1.0, false, true, false); }

        public static UiDriver StartSettings() { return Start("--demo --automation", 1.0, false, false, true); }

        public static UiDriver StartReferenceCapture(double scale) {
            return Start("--demo --automation", scale, true, false, false);
        }

        private static UiDriver Start(string arguments, double scale, bool referenceCapture, bool manyOptions, bool settingsDemo) {
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexCue.exe");
            ProcessStartInfo start = new ProcessStartInfo {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            start.EnvironmentVariables["CODEX_CUE_AUTOMATION_SCALE"] = scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (referenceCapture) start.EnvironmentVariables["CODEX_CUE_REFERENCE_CAPTURE"] = "1";
            if (manyOptions) start.EnvironmentVariables["CODEX_CUE_MANY_OPTIONS"] = "1";
            if (settingsDemo) start.EnvironmentVariables["CODEX_CUE_SETTINGS_DEMO"] = "1";
            Process process = Process.Start(start);
            if (process == null) throw new InvalidOperationException("Demo process did not start.");
            return new UiDriver(process, scale);
        }

        public AutomationElement WaitForWindow(string automationId, int timeoutMs) {
            AutomationElement result = WaitFor(automationId, timeoutMs);
            if (result == null) throw new Exception("Window not found: " + automationId);
            return result;
        }

        public void Click(string automationId) {
            AutomationElement element = Require(automationId);
            object pattern;
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern)) {
                ((InvokePattern)pattern).Invoke();
            } else if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern)) {
                ((SelectionItemPattern)pattern).Select();
            } else if (element.TryGetCurrentPattern(TogglePattern.Pattern, out pattern)) {
                ((TogglePattern)pattern).Toggle();
            } else throw new Exception("Element cannot be clicked: " + automationId);
            Thread.Sleep(140);
        }

        public void SetText(string automationId, string value) {
            AutomationElement element = Require(automationId);
            object pattern;
            if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) throw new Exception("Element has no value pattern: " + automationId);
            ((ValuePattern)pattern).SetValue(value);
            Thread.Sleep(140);
        }

        public string Name(string automationId) { return Require(automationId).Current.Name; }

        public bool IsTopmost(string automationId) {
            AutomationElement element = Require(automationId);
            object pattern;
            if (!element.TryGetCurrentPattern(WindowPattern.Pattern, out pattern)) throw new Exception("Element has no window pattern: " + automationId);
            return ((WindowPattern)pattern).Current.IsTopmost;
        }

        public void Focus(string automationId) {
            AutomationElement window = Require("PromptWindow");
            IntPtr handle = new IntPtr(window.Current.NativeWindowHandle);
            if (handle != IntPtr.Zero) SetForegroundWindow(handle);
            AutomationElement element = Require(automationId);
            element.SetFocus();
            if (!SpinWait.SpinUntil(delegate { return element.Current.HasKeyboardFocus; }, 1000)) {
                throw new Exception("Element did not receive keyboard focus: " + automationId);
            }
            Thread.Sleep(100);
        }

        public void SendKeys(string keys) {
            AutomationElement window = Require("PromptWindow");
            IntPtr handle = new IntPtr(window.Current.NativeWindowHandle);
            if (handle != IntPtr.Zero) SetForegroundWindow(handle);
            System.Windows.Forms.SendKeys.SendWait(keys);
            Thread.Sleep(150);
        }

        public bool IsSelected(string automationId) {
            AutomationElement element = Require(automationId);
            object pattern;
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern)) {
                return ((SelectionItemPattern)pattern).Current.IsSelected;
            }
            if (element.TryGetCurrentPattern(TogglePattern.Pattern, out pattern)) {
                return ((TogglePattern)pattern).Current.ToggleState == ToggleState.On;
            }
            throw new Exception("Element has no selectable pattern: " + automationId);
        }

        public bool IsFocused(string automationId) { return Require(automationId).Current.HasKeyboardFocus; }

        public bool WaitUntilFocused(string automationId, int timeoutMs) {
            return SpinWait.SpinUntil(delegate {
                try { return IsFocused(automationId); }
                catch (Exception) { return false; }
            }, timeoutMs);
        }

        public bool WaitUntilSelected(string automationId, int timeoutMs) {
            return SpinWait.SpinUntil(delegate {
                try { return IsSelected(automationId); }
                catch (Exception) { return false; }
            }, timeoutMs);
        }

        public bool Exists(string automationId) { return WaitFor(automationId, 150) != null; }

        public bool IsFullyVisible(string automationId) {
            AutomationElement element = Require(automationId);
            AutomationElement window = RequireWindowRoot();
            return !element.Current.IsOffscreen && window.Current.BoundingRectangle.Contains(element.Current.BoundingRectangle);
        }

        public int VisibleScrollBarCount() {
            AutomationElement window = Require("PromptWindow");
            AutomationElementCollection bars = window.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ScrollBar));
            int visible = 0;
            foreach (AutomationElement bar in bars) if (!bar.Current.IsOffscreen) visible++;
            return visible;
        }

        public void SendAltF4() {
            AutomationElement window = Require("PromptWindow");
            IntPtr handle = new IntPtr(window.Current.NativeWindowHandle);
            if (handle == IntPtr.Zero || !SetForegroundWindow(handle)) throw new Exception("Could not focus the prompt window.");
            Thread.Sleep(60);
            KeyEvent(VirtualKeyAlt, 0, 0, UIntPtr.Zero);
            KeyEvent(VirtualKeyF4, 0, 0, UIntPtr.Zero);
            KeyEvent(VirtualKeyF4, 0, KeyUp, UIntPtr.Zero);
            KeyEvent(VirtualKeyAlt, 0, KeyUp, UIntPtr.Zero);
            Thread.Sleep(100);
        }

        public bool TabOrderIs(params string[] expected) {
            AutomationElement window = Require("PromptWindow");
            AutomationElementCollection elements = window.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.IsKeyboardFocusableProperty, true));
            List<string> ids = new List<string>();
            foreach (AutomationElement element in elements) {
                string id = element.Current.AutomationId;
                if (!String.IsNullOrEmpty(id)) ids.Add(id);
            }
            int cursor = 0;
            foreach (string id in expected) {
                while (cursor < ids.Count && ids[cursor] != id) cursor++;
                if (cursor >= ids.Count) return false;
                cursor++;
            }
            return true;
        }

        public bool WaitForExit(int timeoutMs) { return process.WaitForExit(timeoutMs); }

        public void Capture(string path) {
            AutomationElement window = RequireWindowRoot();
            try { window.SetFocus(); } catch (InvalidOperationException) { }
            Thread.Sleep(150);
            System.Windows.Rect bounds = window.Current.BoundingRectangle;
            int width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb)) {
                for (int attempt = 0; attempt < 3; attempt++) {
                    using (Graphics graphics = Graphics.FromImage(bitmap)) {
                        graphics.CopyFromScreen((int)bounds.Left, (int)bounds.Top, 0, 0, new Size(width, height));
                    }
                    if (!HasUnexpectedBlackSurface(bitmap, bounds.Width / 736.0)) break;
                    Thread.Sleep(450);
                }
                double currentScale = bounds.Width / 736.0;
                double factor = currentScale <= 0 ? 1.0 : captureScale / currentScale;
                int targetWidth = Math.Max(1, (int)Math.Round(width * factor));
                int targetHeight = Math.Max(1, (int)Math.Round(height * factor));
                if (targetWidth == width && targetHeight == height) bitmap.Save(path, ImageFormat.Png);
                else {
                    using (Bitmap normalized = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppArgb)) {
                        using (Graphics graphics = Graphics.FromImage(normalized)) {
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.DrawImage(bitmap, new Rectangle(0, 0, targetWidth, targetHeight));
                        }
                        normalized.Save(path, ImageFormat.Png);
                    }
                }
            }
        }

        private static bool HasUnexpectedBlackSurface(Bitmap bitmap, double scale) {
            int margin = Math.Max(1, (int)Math.Round(20 * scale));
            int black = 0;
            int samples = 0;
            for (int y = margin; y < bitmap.Height - margin; y += 8) {
                for (int x = margin; x < bitmap.Width - margin; x += 8) {
                    Color color = bitmap.GetPixel(x, y);
                    if (color.R < 8 && color.G < 8 && color.B < 8) black++;
                    samples++;
                }
            }
            return samples > 0 && black > samples / 10;
        }

        public void Dispose() {
            if (!process.HasExited) {
                process.Kill();
                process.WaitForExit(1000);
            }
            process.Dispose();
        }

        private AutomationElement Require(string automationId) {
            AutomationElement result = WaitFor(automationId, 3000);
            if (result == null) throw new Exception("Element not found: " + automationId);
            return result;
        }

        private AutomationElement RequireWindowRoot() {
            process.Refresh();
            IntPtr handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero) throw new Exception("Process has no main window.");
            AutomationElement root = AutomationElement.FromHandle(handle);
            if (root == null) throw new Exception("Main window automation root is unavailable.");
            return root;
        }

        private AutomationElement WaitFor(string automationId, int timeoutMs) {
            Stopwatch timer = Stopwatch.StartNew();
            Condition condition = new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id),
                new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            while (timer.ElapsedMilliseconds < timeoutMs) {
                if (process.HasExited) return null;
                try {
                    process.Refresh();
                    IntPtr handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero) {
                        AutomationElement root = AutomationElement.FromHandle(handle);
                        if (root != null) {
                            if (String.Equals(root.Current.AutomationId, automationId, StringComparison.Ordinal)) return root;
                            AutomationElement result = root.FindFirst(TreeScope.Descendants, condition);
                            if (result != null) return result;
                        }
                    }
                } catch (COMException) { }
                Thread.Sleep(40);
            }
            return null;
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        private static extern void KeyEvent(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    }

    internal static class WizardUiCaptures {
        public static void CaptureSettings(string path) {
            string directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using (UiDriver ui = UiDriver.StartSettings()) {
                ui.WaitForWindow("SettingsWindow", 2500);
                ui.Capture(path);
            }
        }

        public static void Capture(string directory) {
            Directory.CreateDirectory(directory);
            CaptureOne(directory, "wizard-100.png", 1.0);
            CaptureOne(directory, "wizard-150.png", 1.5);
            CaptureOne(directory, "wizard-200.png", 2.0);
        }

        private static void CaptureOne(string directory, string name, double scale) {
            using (UiDriver ui = UiDriver.StartReferenceCapture(scale)) {
                ui.WaitForWindow("PromptWindow", 2500);
                ui.Capture(Path.Combine(directory, name));
            }
        }
    }
}
