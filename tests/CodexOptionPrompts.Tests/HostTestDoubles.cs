using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using CodexOptionPrompts.Core;
using CodexOptionPrompts.Host;
using CodexOptionPrompts.Ipc;

namespace CodexOptionPrompts.Tests {
    internal static class HostFixtures {
        public static PendingPrompt Pending(string sessionId) {
            return new PendingPrompt(Request(sessionId));
        }

        public static OptionRequest Request() { return Request("fixture-request"); }

        public static OptionRequest Request(string sessionId) {
            OptionRequest request = new OptionRequest { SessionId = sessionId, Title = "Fixture" };
            OptionQuestion question = new OptionQuestion { Id = "q1", Prompt = "Choose", AllowOther = true };
            question.Options.Add(new OptionChoice { Id = "a", Label = "A" });
            request.Questions.Add(question);
            return request;
        }

        public static OptionResult Submitted(string sessionId) {
            QuestionAnswer answer = new QuestionAnswer { QuestionId = "q1" };
            answer.SelectedOptionIds.Add("a");
            return new OptionResult {
                Status = "submitted",
                SessionId = sessionId,
                Answers = new List<QuestionAnswer> { answer },
                Source = "desktop-wpf",
                Resolution = "user",
                ProtocolVersion = 1,
                CreatedAt = DateTime.UtcNow,
                ResolvedAt = DateTime.UtcNow
            };
        }

        public static MemoryStream OversizedFrame() {
            int length = RequestValidator.MaximumMessageBytes + 1;
            byte[] prefix = new byte[] {
                (byte)(length & 0xff),
                (byte)((length >> 8) & 0xff),
                (byte)((length >> 16) & 0xff),
                (byte)((length >> 24) & 0xff)
            };
            return new MemoryStream(prefix);
        }
    }

    internal sealed class PipeIntegrationFixture : IDisposable {
        private readonly HostController controller;
        private readonly PipeServer server;

        private PipeIntegrationFixture(PipeIdentity identity, HostController controller, PipeServer server) {
            this.controller = controller;
            this.server = server;
            Client = new PipePromptClient(identity, HostLauncher.ForExistingPipe(identity));
        }

        public PipePromptClient Client { get; private set; }
        public HostController Controller { get { return controller; } }

        public bool ServerSecurityContainsOnlyCurrentUser {
            get {
                SecurityIdentifier current = WindowsIdentity.GetCurrent().User;
                AuthorizationRuleCollection rules = server.Security.GetAccessRules(true, false, typeof(SecurityIdentifier));
                if (rules.Count != 1) return false;
                PipeAccessRule rule = rules[0] as PipeAccessRule;
                return rule != null && rule.AccessControlType == AccessControlType.Allow &&
                    current.Equals(rule.IdentityReference) && !rule.IsInherited;
            }
        }

        public static PipeIntegrationFixture Start() {
            return Start(true);
        }

        public static PipeIntegrationFixture StartManual() {
            return Start(false);
        }

        private static PipeIntegrationFixture Start(bool completeAutomatically) {
            string sid = WindowsIdentity.GetCurrent().User.Value + "-" + Guid.NewGuid().ToString("N");
            PipeIdentity identity = PipeNames.ForSid(sid, 1);
            HostController controller = new HostController();
            if (completeAutomatically) {
                controller.PromptRequested += delegate(object sender, PromptRequestedEventArgs args) {
                    controller.Submit(args.Pending.SessionId, HostFixtures.Submitted(args.Pending.SessionId));
                };
            }
            PipeServer server = new PipeServer(identity, controller);
            server.Start();
            return new PipeIntegrationFixture(identity, controller, server);
        }

        public void Dispose() {
            server.Dispose();
            controller.Dispose();
        }
    }
}
