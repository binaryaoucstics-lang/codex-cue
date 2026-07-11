using System;
using System.IO;
using System.Reflection;
using System.Threading;
using CodexCue.Core;
using CodexCue.Host;
using CodexCue.Hooks;
using CodexCue.Install;
using CodexCue.Ipc;
using CodexCue.Mcp;
using CodexCue.Settings;
using CodexCue.Ui;

namespace CodexCue {
    public partial class App : System.Windows.Application {
        private Mutex hostMutex;
        private HostController hostController;
        private PipeServer pipeServer;
        private TrayController trayController;
        private PromptWindow activeWindow;
        private string activeSessionId;
        private bool demoShutdownScheduled;

        protected override void OnStartup(System.Windows.StartupEventArgs e) {
            base.OnStartup(e);
            AccentTheme.Apply(CueSettingsStore.Current().Load().AccentColor);
            CodexCue.Application.AppModeOptions options;
            try {
                options = CodexCue.Application.AppModeParser.Parse(e.Args);
            } catch (Exception error) {
                Console.Error.WriteLine(error.Message);
                Shutdown(2);
                return;
            }

            if (options.Mode == CodexCue.Application.AppMode.Mcp) {
                RunMcp();
                Shutdown(0);
            } else if (options.Mode == CodexCue.Application.AppMode.Hook) {
                RunHook(options.HookEvent);
                Shutdown(0);
            } else if (options.Mode == CodexCue.Application.AppMode.Host) {
                StartHost();
            } else if (options.Mode == CodexCue.Application.AppMode.Demo) {
                if (String.Equals(Environment.GetEnvironmentVariable("CODEX_CUE_SETTINGS_DEMO"), "1", StringComparison.Ordinal)) StartSettingsDemo();
                else StartDemo(options.Automation);
            } else if (options.Mode == CodexCue.Application.AppMode.InstallPlugin) {
                RunInstall(options);
            } else if (options.Mode == CodexCue.Application.AppMode.UninstallPlugin) {
                RunUninstall(options);
            }
        }

        protected override void OnExit(System.Windows.ExitEventArgs e) {
            StopHost();
            base.OnExit(e);
        }

        private static void RunMcp() {
            PipeIdentity identity = PipeNames.Current(1);
            StdioTransport transport = new StdioTransport(Console.OpenStandardInput(), Console.OpenStandardOutput());
            McpServer server = new McpServer(new PipePromptClient(identity, new HostLauncher(identity)), new JsonCodec());
            server.RunAsync(transport, Console.Error, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static void RunHook(string hookEvent) {
            HookContextWriter.Write(hookEvent, Console.In, Console.Out);
        }

        private void RunInstall(CodexCue.Application.AppModeOptions options) {
            try {
                InstallPaths paths = String.IsNullOrWhiteSpace(options.TestHome) ? InstallPaths.Current() : InstallPaths.ForHome(options.TestHome);
                string program = Assembly.GetExecutingAssembly().Location;
                string directory = Path.GetDirectoryName(program);
                string stagedPlugin = Path.Combine(directory, "plugin", "codex-cue");
                if (!Directory.Exists(stagedPlugin)) stagedPlugin = Path.Combine(directory, "plugins", "codex-cue");
                new InstallerEntry(paths).Install(new InstallRequest {
                    StagedProgramPath = program,
                    StagedPluginPath = stagedPlugin,
                    RefreshCodex = String.IsNullOrWhiteSpace(options.TestHome)
                });
                if (String.IsNullOrWhiteSpace(options.TestHome)) {
                    StopLegacyHosts();
                    string installedProgram = Path.Combine(paths.ProgramDirectory, "CodexCue.exe");
                    StartupRegistration.Enable(installedProgram);
                    StartupRegistration.StartHost(installedProgram);
                }
                Shutdown(0);
            } catch (InstallException error) {
                Console.Error.WriteLine(error.Code + ": " + error.Message);
                Shutdown(1);
            }
        }

        private void RunUninstall(CodexCue.Application.AppModeOptions options) {
            try {
                InstallPaths paths = String.IsNullOrWhiteSpace(options.TestHome) ? InstallPaths.Current() : InstallPaths.ForHome(options.TestHome);
                new InstallerEntry(paths).Uninstall(new UninstallRequest { RestoreBackup = true });
                if (String.IsNullOrWhiteSpace(options.TestHome)) {
                    StartupRegistration.Disable(Path.Combine(paths.ProgramDirectory, "CodexCue.exe"));
                }
                Shutdown(0);
            } catch (InstallException error) {
                Console.Error.WriteLine(error.Code + ": " + error.Message);
                Shutdown(1);
            }
        }

        private sealed class InstallerEntry : PluginInstaller {
            public InstallerEntry(InstallPaths paths) : base(paths) { }
        }

        private void StartHost() {
            PipeIdentity identity = PipeNames.Current(1);
            bool created;
            hostMutex = new Mutex(true, identity.MutexName, out created);
            if (!created) {
                hostMutex.Dispose();
                hostMutex = null;
                Shutdown(0);
                return;
            }

            hostController = new HostController();
            hostController.PromptRequested += OnPromptRequested;
            hostController.PromptResolved += OnPromptResolved;
            pipeServer = new PipeServer(identity, hostController);
            pipeServer.Start();
            trayController = new TrayController();
            trayController.OpenRequested += delegate {
                Dispatcher.BeginInvoke(new Action(delegate {
                    if (activeWindow != null) { activeWindow.Show(); activeWindow.Activate(); }
                }));
            };
            trayController.SettingsRequested += delegate {
                Dispatcher.BeginInvoke(new Action(delegate {
                    SettingsWindow window = new SettingsWindow(CueSettingsStore.Current());
                    window.Owner = activeWindow;
                    window.Topmost = true;
                    window.ShowDialog();
                }));
            };
            trayController.SkipNextRequested += delegate {
                CueSettingsStore store = CueSettingsStore.Current();
                CueSettings settings = store.Load();
                settings.SkipNextCompletion = true;
                store.Save(settings);
            };
            trayController.ExitRequested += delegate { Shutdown(0); };
        }

        private void OnPromptRequested(object sender, PromptRequestedEventArgs e) {
            Dispatcher.BeginInvoke(new Action<PendingPrompt>(ShowPendingPrompt), e.Pending);
        }

        private void OnPromptResolved(object sender, PromptResolvedEventArgs e) {
            Dispatcher.BeginInvoke(new Action<string>(CloseResolvedPrompt), e.SessionId);
        }

        private void CloseResolvedPrompt(string sessionId) {
            if (activeWindow == null || !String.Equals(activeSessionId, sessionId, StringComparison.Ordinal)) return;
            PromptWindow window = activeWindow;
            activeWindow = null;
            activeSessionId = null;
            window.ForceClose();
        }

        private void ShowPendingPrompt(PendingPrompt pending) {
            if (activeWindow != null) activeWindow.ForceClose();
            WizardViewModel viewModel = new WizardViewModel(new WizardState(pending.Request));
            viewModel.Completed += delegate(object sender, WizardCompletedEventArgs args) {
                hostController.Submit(pending.SessionId, ResultFactory.Submitted(pending.Request, args.Answers, "user"));
            };
            viewModel.Cancelled += delegate { hostController.Cancel(pending.SessionId); };
            viewModel.Skipped += delegate { hostController.Submit(pending.SessionId, ResultFactory.Skipped(pending.Request)); };
            PromptWindow window = new PromptWindow(viewModel);
            activeWindow = window;
            activeSessionId = pending.SessionId;
            window.Topmost = true;
            window.Closed += delegate {
                if (Object.ReferenceEquals(activeWindow, window)) {
                    activeWindow = null;
                    activeSessionId = null;
                }
            };
            window.Show();
            window.Activate();
        }

        private void StartDemo(bool automation) {
            ShutdownMode = System.Windows.ShutdownMode.OnLastWindowClose;
            bool referenceCapture = String.Equals(
                Environment.GetEnvironmentVariable("CODEX_CUE_REFERENCE_CAPTURE"), "1", StringComparison.Ordinal);
            OptionRequest request = DemoRequest(referenceCapture);
            WizardState state = new WizardState(request);
            if (referenceCapture) {
                state.Select("capture-intro", "continue");
                state.MoveNext();
                state.Select("publish-mode", "installer");
            }
            WizardViewModel viewModel = new WizardViewModel(state);
            viewModel.Completed += delegate { ScheduleDemoShutdown(); };
            viewModel.Cancelled += delegate { ScheduleDemoShutdown(); };
            viewModel.Skipped += delegate { ScheduleDemoShutdown(); };
            activeWindow = new PromptWindow(viewModel);
            activeWindow.Topmost = automation;
            activeWindow.Closed += delegate { activeWindow = null; ScheduleDemoShutdown(); };
            MainWindow = activeWindow;
            activeWindow.Show();
        }

        private void StartSettingsDemo() {
            ShutdownMode = System.Windows.ShutdownMode.OnLastWindowClose;
            SettingsWindow window = new SettingsWindow(CueSettingsStore.Current());
            window.Topmost = true;
            window.ShowInTaskbar = true;
            MainWindow = window;
            window.Show();
        }

        private void ScheduleDemoShutdown() {
            if (demoShutdownScheduled) return;
            demoShutdownScheduled = true;
            Dispatcher.BeginInvoke(new Action(delegate { Shutdown(0); }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static OptionRequest DemoRequest(bool referenceCapture) {
            OptionRequest request = new OptionRequest { SessionId = "demo", ReviewMode = ReviewMode.Auto };
            if (referenceCapture) {
                OptionQuestion intro = new OptionQuestion {
                    Id = "capture-intro", Prompt = "Capture introduction", AllowOther = false
                };
                intro.Options.Add(new OptionChoice { Id = "continue", Label = "Continue" });
                request.Questions.Add(intro);
            }
            OptionQuestion publish = new OptionQuestion {
                Id = "publish-mode",
                Prompt = "选择这项功能的发布方式",
                Description = "请选择一项，也可以填写自己的方案。",
                Mode = SelectionMode.Single,
                Required = true,
                AllowOther = true
            };
            publish.Options.Add(new OptionChoice {
                Id = "installer",
                Label = "本机安装 + 可分发安装包",
                Description = "适合当前电脑测试，也方便分享给其他用户。",
                Recommended = true
            });
            publish.Options.Add(new OptionChoice {
                Id = "portable",
                Label = "仅提供便携版",
                Description = "无需安装，但不会自动配置插件。"
            });
            if (String.Equals(Environment.GetEnvironmentVariable("CODEX_CUE_MANY_OPTIONS"), "1", StringComparison.Ordinal)) {
                publish.Options.Add(new OptionChoice { Id = "weekend", Label = "朋友聚会", Description = "约朋友吃饭、聊天或玩乐。" });
                publish.Options.Add(new OptionChoice { Id = "explore", Label = "探索新地", Description = "去一个没去过的地方走走。" });
            }
            request.Questions.Add(publish);

            OptionQuestion targets = new OptionQuestion {
                Id = "targets",
                Prompt = "选择要包含的发布目标",
                Description = "可以选择多项，也可以补充自己的方案。",
                Mode = SelectionMode.Multiple,
                Required = true,
                AllowOther = true
            };
            targets.Options.Add(new OptionChoice { Id = "windows", Label = "Windows 本机安装版" });
            targets.Options.Add(new OptionChoice { Id = "portable", Label = "可分发便携包" });
            request.Questions.Add(targets);
            if (referenceCapture) {
                request.Questions.Add(CapturePlaceholder("capture-review", "Review"));
                request.Questions.Add(CapturePlaceholder("capture-finish", "Finish"));
            }
            return request;
        }

        private static OptionQuestion CapturePlaceholder(string id, string label) {
            OptionQuestion question = new OptionQuestion { Id = id, Prompt = label, AllowOther = false };
            question.Options.Add(new OptionChoice { Id = "continue", Label = "Continue" });
            return question;
        }

        private void StopHost() {
            if (activeWindow != null) { activeWindow.ForceClose(); activeWindow = null; activeSessionId = null; }
            if (trayController != null) { trayController.Dispose(); trayController = null; }
            if (pipeServer != null) { pipeServer.Dispose(); pipeServer = null; }
            if (hostController != null) { hostController.Dispose(); hostController = null; }
            if (hostMutex != null) {
                try { hostMutex.ReleaseMutex(); } catch (ApplicationException) { }
                hostMutex.Dispose();
                hostMutex = null;
            }
        }

        private static void StopLegacyHosts() {
            foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcessesByName("CodexOptionPrompts")) {
                try { process.Kill(); process.WaitForExit(2000); } catch (InvalidOperationException) { } finally { process.Dispose(); }
            }
        }
    }
}
