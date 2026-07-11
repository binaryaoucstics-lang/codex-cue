using System;
using System.IO;
using System.Text;
using CodexCue.Core;

namespace CodexCue.Mcp {
    public enum StdioFraming { Unknown, JsonLines, ContentLength }

    public sealed class StdioTransport {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly Stream input;
        private readonly Stream output;
        private readonly object writeLock = new object();

        public StdioTransport(Stream input, Stream output) {
            if (input == null) throw new ArgumentNullException("input");
            if (output == null) throw new ArgumentNullException("output");
            this.input = input;
            this.output = output;
        }

        public StdioFraming Framing { get; private set; }

        public string ReadMessage() {
            string firstLine;
            do {
                firstLine = ReadLine();
                if (firstLine == null) return null;
            } while (firstLine.Length == 0);

            if (firstLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) {
                SetFraming(StdioFraming.ContentLength);
                int contentLength = ParseContentLength(firstLine);
                while (true) {
                    string header = ReadLine();
                    if (header == null) throw new EndOfStreamException("Unexpected end of MCP headers.");
                    if (header.Length == 0) break;
                    if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) {
                        contentLength = ParseContentLength(header);
                    }
                }
                if (contentLength < 0 || contentLength > RequestValidator.MaximumMessageBytes) {
                    throw new InvalidDataException("MCP message exceeds 1 MiB.");
                }
                byte[] payload = ReadExactly(contentLength);
                return Utf8.GetString(payload);
            }

            SetFraming(StdioFraming.JsonLines);
            if (Utf8.GetByteCount(firstLine) > RequestValidator.MaximumMessageBytes) {
                throw new InvalidDataException("MCP message exceeds 1 MiB.");
            }
            return firstLine;
        }

        public void WriteMessage(string json) {
            if (json == null) throw new ArgumentNullException("json");
            byte[] payload = Utf8.GetBytes(json);
            if (payload.Length > RequestValidator.MaximumMessageBytes) {
                throw new InvalidDataException("MCP response exceeds 1 MiB.");
            }

            lock (writeLock) {
                if (Framing == StdioFraming.ContentLength) {
                    byte[] header = Encoding.ASCII.GetBytes("Content-Length: " + payload.Length + "\r\n\r\n");
                    output.Write(header, 0, header.Length);
                    output.Write(payload, 0, payload.Length);
                } else {
                    output.Write(payload, 0, payload.Length);
                    output.WriteByte((byte)'\n');
                }
                output.Flush();
            }
        }

        private void SetFraming(StdioFraming detected) {
            if (Framing == StdioFraming.Unknown) Framing = detected;
        }

        private static int ParseContentLength(string header) {
            int separator = header.IndexOf(':');
            int value;
            if (separator < 0 || !Int32.TryParse(header.Substring(separator + 1).Trim(), out value)) {
                throw new InvalidDataException("Invalid Content-Length header.");
            }
            return value;
        }

        private string ReadLine() {
            MemoryStream bytes = new MemoryStream();
            while (true) {
                int value = input.ReadByte();
                if (value < 0) {
                    if (bytes.Length == 0) return null;
                    break;
                }
                if (value == '\n') break;
                if (value != '\r') bytes.WriteByte((byte)value);
                if (bytes.Length > RequestValidator.MaximumMessageBytes) {
                    throw new InvalidDataException("MCP line exceeds 1 MiB.");
                }
            }
            return Utf8.GetString(bytes.ToArray());
        }

        private byte[] ReadExactly(int count) {
            byte[] result = new byte[count];
            int offset = 0;
            while (offset < count) {
                int read = input.Read(result, offset, count - offset);
                if (read == 0) throw new EndOfStreamException("Unexpected end of MCP payload.");
                offset += read;
            }
            return result;
        }
    }
}
