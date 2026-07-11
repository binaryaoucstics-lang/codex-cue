using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CodexCue.Core;
using CodexCue.Mcp;

namespace CodexCue.Tests {
    internal static class StdioTransportTests {
        public static void Register(TestRegistry tests) {
            tests.Add("Transport reads jsonl and content length messages", delegate {
                Assert.Equal(2, TransportFixtures.ReadJsonlAndHeaderMessages().Count);
            });

            tests.Add("Transport preserves first detected framing for responses", delegate {
                MemoryStream input = TransportFixtures.MixedInput();
                MemoryStream output = new MemoryStream();
                StdioTransport transport = new StdioTransport(input, output);
                transport.ReadMessage();
                transport.ReadMessage();
                transport.WriteMessage("{\"ok\":true}");
                string written = Encoding.UTF8.GetString(output.ToArray());
                Assert.True(written.StartsWith("{\"ok\":true}"));
                Assert.False(written.StartsWith("Content-Length"));
            });

            tests.Add("Transport rejects content length above one MiB", delegate {
                string headers = "Content-Length: " + (RequestValidator.MaximumMessageBytes + 1) + "\r\n\r\n";
                StdioTransport transport = new StdioTransport(
                    new MemoryStream(Encoding.ASCII.GetBytes(headers)), new MemoryStream());
                Assert.Throws<InvalidDataException>(delegate { transport.ReadMessage(); });
            });
        }
    }
}
