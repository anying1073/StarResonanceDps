using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace StarResonanceDpsAnalysis.WinForm
{

    public static class ProtoPrettyPrinter
    {
        public static string PrettyPrintProto(string hexInput, bool forceRecurse = false)
        {
            var data = ParseHex(hexInput);
            return PrettyPrintProto(data, forceRecurse);
        }

        public static string PrettyPrintProto(byte[] data, bool forceRecurse = false)
        {
            int pos = 0;
            var sb = new StringBuilder();
            PrintMessage(data, ref pos, data.Length, 0, sb, forceRecurse);
            return sb.ToString();
        }

        static void PrintMessage(byte[] data, ref int pos, int end, int indent, StringBuilder sb, bool forceRecurse)
        {
            while (pos < end)
            {
                ulong key = ReadVarint64(data, ref pos, out var keyRaw);
                int field = (int)(key >> 3);
                int wire = (int)(key & 0x07);

                Indent(indent, sb);
                sb.Append($"Field #{field}: {ToHexDash(keyRaw)} ");

                switch (wire)
                {
                    case 0:
                        {
                            ulong v = ReadVarint64(data, ref pos, out var raw);
                            long zig = ZigZag(v);
                            long asSigned = unchecked((long)v);
                            sb.AppendLine($"Varint = {v}, Hex = {ToHexDash(raw)}, ZigZag = {zig}, Signed = {asSigned}");
                            break;
                        }
                    case 1:
                        {
                            if (pos + 8 > end) { sb.AppendLine("<fixed64 truncated>"); return; }
                            ulong u = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(data, pos, 8));
                            long i = unchecked((long)u);
                            double d = BitConverter.ToDouble(data, pos);
                            sb.AppendLine($"Fixed64 Hex = {ToHexDash(new ReadOnlySpan<byte>(data, pos, 8))}, Int64 = {i}, Double = {d}");
                            pos += 8; break;
                        }
                    case 2:
                        {
                            ulong len = ReadVarint64(data, ref pos, out var lenRaw);
                            int ilen = checked((int)len);
                            if (pos + ilen > end) { sb.AppendLine($"String/Bytes Length = {ilen}, Hex = {ToHexDash(lenRaw)} <truncated>"); pos = end; break; }

                            var seg = new ReadOnlySpan<byte>(data, pos, ilen);
                            var utf8Preview = TryUtf8Preview(seg, 120);
                            bool looksMsg = forceRecurse || LooksLikeSubMessage(seg);

                            sb.Append($"String Length = {ilen}, Hex = {ToHexDash(lenRaw)}, ");
                            sb.AppendLine(utf8Preview != null ? $"UTF8 = \"{utf8Preview}\"" : "UTF8 = <binary>");

                            if (looksMsg)
                            {
                                Indent(indent, sb); sb.AppendLine("As sub-object :");
                                int subPos = 0; var sub = seg.ToArray();
                                PrintMessage(sub, ref subPos, sub.Length, indent + 1, sb, forceRecurse);
                            }
                            else
                            {
                                Indent(indent, sb);
                                sb.AppendLine($"Bytes[{ilen}] = {ToHexDash(seg.Slice(0, Math.Min(64, ilen)))}{(ilen > 64 ? " ..." : "")}");
                            }

                            pos += ilen; break;
                        }
                    case 5:
                        {
                            if (pos + 4 > end) { sb.AppendLine("<fixed32 truncated>"); return; }
                            uint u = BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(data, pos, 4));
                            int i = unchecked((int)u);
                            float f = BitConverter.ToSingle(data, pos);
                            sb.AppendLine($"Fixed32 Hex = {ToHexDash(new ReadOnlySpan<byte>(data, pos, 4))}, Int32 = {i}, Float = {f}");
                            pos += 4; break;
                        }
                    default:
                        sb.AppendLine($"<unsupported wire type {wire}>"); return;
                }
            }
        }

        // ===== Heuristics =====
        static bool LooksLikeSubMessage(ReadOnlySpan<byte> seg)
        {
            try
            {
                int p = 0;
                int end = seg.Length;
                while (p < end)
                {
                    // 读 key
                    ReadVarint64(seg, ref p, out _);
                    int wire = (int)(_lastVarint & 0x07);
                    switch (wire)
                    {
                        case 0: // varint
                            ReadVarint64(seg, ref p, out _);
                            break;
                        case 1: // fixed64
                            if (p + 8 > end) return false;
                            p += 8;
                            break;
                        case 2: // length-delimited
                            {
                                ReadVarint64(seg, ref p, out _);
                                int len = checked((int)_lastVarint);
                                if (p + len > end) return false;
                                p += len;
                                break;
                            }
                        case 5: // fixed32
                            if (p + 4 > end) return false;
                            p += 4;
                            break;
                        default:
                            return false;
                    }
                }
                return p == end;
            }
            catch
            {
                return false;
            }
        }

        static string TryUtf8Preview(ReadOnlySpan<byte> seg, int maxChars)
        {
            try
            {
                var enc = Encoding.GetEncoding("utf-8", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                string s = enc.GetString(seg);
                // 过滤不可打印控制字符（保留 \r\n\t）
                var sb = new StringBuilder(s.Length);
                foreach (var ch in s)
                {
                    if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
                        sb.Append('�'); // 不打印的控制字符用替代符
                    else
                        sb.Append(ch);
                    if (sb.Length >= maxChars) break;
                }
                return sb.ToString();
            }
            catch
            {
                return null; // 不是合法 UTF-8
            }
        }

        static double PrintableRatio(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int printable = 0;
            foreach (var ch in s)
            {
                if (!char.IsControl(ch) || ch == '\r' || ch == '\n' || ch == '\t') printable++;
            }
            return printable / (double)s.Length;
        }

        static string AsciiPreview(ReadOnlySpan<byte> seg, int max)
        {
            var sb = new StringBuilder();
            int n = Math.Min(max, seg.Length);
            for (int i = 0; i < n; i++)
            {
                byte b = seg[i];
                char ch = (b >= 32 && b < 127) ? (char)b : '.';
                sb.Append(ch);
            }
            if (seg.Length > max) sb.Append("...");
            return sb.ToString();
        }

        // ===== Varint readers with raw capture =====
        static ulong _lastVarint;

        static ulong ReadVarint64(byte[] data, ref int pos, out ReadOnlySpan<byte> rawBytes)
        {
            int start = pos;
            ulong result = 0;
            int shift = 0;
            while (pos < data.Length)
            {
                byte b = data[pos++];
                result |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
                if (shift >= 64) throw new FormatException("Varint too long");
            }
            rawBytes = new ReadOnlySpan<byte>(data, start, pos - start);
            _lastVarint = result;
            return result;
        }

        static ulong ReadVarint64(ReadOnlySpan<byte> data, ref int pos, out ReadOnlySpan<byte> rawBytes)
        {
            int start = pos;
            ulong result = 0;
            int shift = 0;
            while (pos < data.Length)
            {
                byte b = data[pos++];
                result |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
                if (shift >= 64) throw new FormatException("Varint too long");
            }
            rawBytes = data.Slice(start, pos - start);
            _lastVarint = result;
            return result;
        }

        // ===== Utils =====
        static string ToHexDash(ReadOnlySpan<byte> bytes)
            => string.Join("-", bytes.ToArray().Select(b => b.ToString("X2")));

        static byte[] ParseHex(string hex)
        {
            var tokens = hex
                .Replace("\r", " ").Replace("\n", " ")
                .Split(new[] { ' ', '\t', ',', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? t.Substring(2) : t);

            return tokens.Select(t => byte.Parse(t, NumberStyles.HexNumber)).ToArray();
        }

        static long ZigZag(ulong v) => (long)((v >> 1) ^ (ulong)-(long)(v & 1));
        static void Indent(int n, StringBuilder sb) => sb.Append(new string(' ', n * 2));
    }
}