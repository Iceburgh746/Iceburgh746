using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UVConsole
{
    internal sealed class ViewerFrame
    {
        public byte Type;
        public byte Flags;
        public byte[] Payload;
    }

    internal sealed class RfLogRow
    {
        public uint Frequency10Hz;
        public uint Sequence;
        public ushort DurationSeconds;
        public ushort Channel;
        public byte Flags;
        public byte Meter;
        public byte Battery;
        public string ChannelName;
        public bool IsTx { get { return (Flags & 0x01) != 0; } }
        public bool IsSession { get { return (Flags & 0x08) != 0; } }
    }

    internal static class ViewerProtocol
    {
        public const int BaudRate = 38400;
        public const int Width = 128;
        public const int Height = 64;
        public const int FrameSize = 1024;
        public const byte TypeScreenshot = 0x01;
        public const byte TypeDiff = 0x02;
        public const byte TypeKey = 0x03;
        public const byte TypeKeyLong = 0x04;
        public const byte TypeRfLog = 0x05;
        public const byte TypeRfLogHistory = 0x06;
        public const byte FlagDeepSleep = 0x01;
        public const byte FlagLedRed = 0x02;
        public const byte FlagLedGreen = 0x04;

        public static readonly byte[] RebootPacket = new byte[]
        {
            0xAB,0xCD,0x04,0x00,0xCB,0x69,0x14,0xE6,0x5B,0xEB,0xDC,0xBA
        };

        public static byte[] MakeKeepAlive(bool restartRfLog)
        {
            byte features = 0x01 | 0x02;
            if (restartRfLog) features |= 0x80;
            return new byte[] { 0x55,0xAA,TypeRfLog,features };
        }

        public static byte[] MakeViewerKeepAlive()
        {
            return new byte[] { 0x55,0xAA,0x00,0x00 };
        }

        public static byte[] MakeKey(byte keyCode, bool longPress)
        {
            return new byte[] { 0xAA,0x55,longPress ? TypeKeyLong : TypeKey,keyCode };
        }

        public static bool TryTakeFrame(List<byte> buffer, out ViewerFrame frame)
        {
            frame = null;
            while (buffer.Count >= 2)
            {
                int start = -1;
                byte flags = 0;
                bool marker = false;

                for (int i = 0; i < buffer.Count - 1; i++)
                {
                    int header = i;
                    byte b = buffer[i];
                    if (b == 0xFF || (b & 0xF0) == 0xF0)
                    {
                        if (i + 2 >= buffer.Count) return false;
                        if (buffer[i + 1] == 0xAA && buffer[i + 2] == 0x55)
                        {
                            marker = true;
                            flags = b == 0xFF ? (byte)0 : (byte)(b & 0x0F);
                            header = i + 1;
                            start = i;
                            break;
                        }
                    }
                    if (buffer[i] == 0xAA && buffer[i + 1] == 0x55)
                    {
                        start = i;
                        marker = false;
                        flags = 0;
                        break;
                    }
                }

                if (start < 0)
                {
                    bool keep = buffer.Count > 0 && (buffer[buffer.Count - 1] == 0xAA || buffer[buffer.Count - 1] == 0xFF || (buffer[buffer.Count - 1] & 0xF0) == 0xF0);
                    byte tail = keep ? buffer[buffer.Count - 1] : (byte)0;
                    buffer.Clear();
                    if (keep) buffer.Add(tail);
                    return false;
                }

                if (start > 0) buffer.RemoveRange(0, start);
                int headerStart = marker ? 1 : 0;
                if (buffer.Count < headerStart + 5) return false;
                if (buffer[headerStart] != 0xAA || buffer[headerStart + 1] != 0x55)
                {
                    buffer.RemoveAt(0);
                    continue;
                }

                byte type = buffer[headerStart + 2];
                int size = (buffer[headerStart + 3] << 8) | buffer[headerStart + 4];
                if (size > 8192)
                {
                    buffer.RemoveAt(0);
                    continue;
                }

                int total = headerStart + 5 + size + 1;
                if (buffer.Count < total) return false;
                if (buffer[total - 1] != 0x0A)
                {
                    buffer.RemoveAt(0);
                    continue;
                }

                byte[] payload = new byte[size];
                if (size > 0) buffer.CopyTo(headerStart + 5, payload, 0, size);
                buffer.RemoveRange(0, total);
                frame = new ViewerFrame { Type = type, Flags = flags, Payload = payload };
                return true;
            }
            return false;
        }

        public static void ApplyDisplayFrame(byte[] framebuffer, ViewerFrame frame)
        {
            if (frame.Type == TypeScreenshot && frame.Payload.Length == FrameSize)
            {
                Buffer.BlockCopy(frame.Payload, 0, framebuffer, 0, FrameSize);
                return;
            }
            if (frame.Type == TypeDiff && (frame.Payload.Length % 9) == 0)
            {
                int p = 0;
                while (p + 9 <= frame.Payload.Length)
                {
                    int index = frame.Payload[p++];
                    if (index >= 128) break;
                    int dst = index * 8;
                    for (int j = 0; j < 8; j++) framebuffer[dst + j] = frame.Payload[p + j];
                    p += 8;
                }
            }
        }

        public static bool GetPixel(byte[] framebuffer, int x, int y)
        {
            int bitIndex = y * Width + x;
            int byteIndex = bitIndex >> 3;
            int bit = bitIndex & 7;
            return ((framebuffer[byteIndex] >> bit) & 1) != 0;
        }
    }

    internal static class RfLogProtocol
    {
        public const int StatusPacketSize = 4;
        public const int ChannelNameLength = 10;
        public const int RowSize = 25;
        public const int RowCount = 64;
        public const int PacketSize = StatusPacketSize + RowSize * (RowCount + 1);
        public const int HistoryPacketSize = RowSize * RowCount;
        public const byte PacketVersion = 2;
        public const ushort ChannelNone = 0xFFFF;
        public const byte BatteryUnknown = 0xFF;
        public const int BatteryOffset = 600;
        public static readonly string[] PowerLabels = new string[] { "USER","LOW1","LOW2","LOW3","LOW4","LOW5","MID","HIGH" };

        public static bool TryParseMain(byte[] payload, out byte statusFlags, out RfLogRow live, out List<RfLogRow> rows)
        {
            statusFlags = 0;
            live = null;
            rows = new List<RfLogRow>();
            if (payload == null || (payload.Length != PacketSize && payload.Length != StatusPacketSize)) return false;
            if (payload[0] != PacketVersion) return false;
            statusFlags = payload[1];
            if (payload.Length == StatusPacketSize) return true;
            int rowCount = Math.Min(payload[2], (byte)RowCount);
            live = ParseRow(payload, StatusPacketSize);
            if (live.Frequency10Hz == 0) live = null;
            int offset = StatusPacketSize + RowSize;
            for (int i = 0; i < rowCount; i++, offset += RowSize)
            {
                RfLogRow r = ParseRow(payload, offset);
                if (r.Frequency10Hz > 0 || r.IsSession) rows.Add(r);
            }
            return true;
        }

        public static List<RfLogRow> ParseHistory(byte[] payload)
        {
            List<RfLogRow> rows = new List<RfLogRow>();
            if (payload == null || payload.Length != HistoryPacketSize) return rows;
            for (int i = 0, off = 0; i < RowCount; i++, off += RowSize)
            {
                RfLogRow r = ParseRow(payload, off);
                if (r.Frequency10Hz > 0 || r.IsSession) rows.Add(r);
            }
            return rows;
        }

        private static RfLogRow ParseRow(byte[] p, int o)
        {
            RfLogRow row = new RfLogRow();
            row.Frequency10Hz = ReadU32(p, o);
            row.Sequence = ReadU32(p, o + 4);
            row.DurationSeconds = ReadU16(p, o + 8);
            row.Channel = ReadU16(p, o + 10);
            row.Flags = p[o + 12];
            row.Meter = p[o + 13];
            row.Battery = p[o + 14];
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ChannelNameLength; i++)
            {
                byte c = p[o + 15 + i];
                if (c == 0) break;
                if (c >= 32 && c <= 126) sb.Append((char)c);
            }
            row.ChannelName = sb.ToString().Trim();
            return row;
        }

        public static string FormatFrequency(uint frequency10Hz)
        {
            if (frequency10Hz == 0) return "-";
            return (frequency10Hz / 100000.0).ToString("0.00000");
        }

        public static string FormatMeter(RfLogRow row)
        {
            if (row.Meter == 0xFF) return "-";
            if (row.IsTx)
            {
                return row.Meter < PowerLabels.Length ? PowerLabels[row.Meter] : "P" + row.Meter;
            }
            int m = Math.Max(1, (int)row.Meter);
            return m > 9 ? "S9+" + (m - 9).ToString("00") : "S" + m;
        }

        public static string FormatBattery(byte b)
        {
            if (b == BatteryUnknown) return "-";
            return ((BatteryOffset + b) / 100.0).ToString("0.00") + " V";
        }

        public static string Csv(IEnumerable<RfLogRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('\uFEFF');
            sb.AppendLine("sequence,event,frequency_mhz,frequency_10hz,duration_seconds,channel,channel_name,signal_or_power,battery_volts");
            List<RfLogRow> list = new List<RfLogRow>(rows);
            list.Sort(delegate(RfLogRow a, RfLogRow b) { return a.Sequence.CompareTo(b.Sequence); });
            foreach (RfLogRow r in list)
            {
                bool session = r.IsSession;
                string evt = session ? "POWER_ON" : (r.IsTx ? "TX" : "RX");
                string channel = session || r.Channel == ChannelNone ? "" : (r.Channel + 1).ToString();
                string batt = session || r.Battery == BatteryUnknown ? "" : ((BatteryOffset + r.Battery) / 100.0).ToString("0.00");
                string[] v = new string[]
                {
                    r.Sequence.ToString(), evt,
                    session || r.Frequency10Hz == 0 ? "" : (r.Frequency10Hz / 100000.0).ToString("0.00000"),
                    session ? "" : r.Frequency10Hz.ToString(),
                    session ? "" : r.DurationSeconds.ToString(),
                    channel,
                    session ? "" : r.ChannelName,
                    session ? "" : FormatMeter(r),
                    batt
                };
                for (int i = 0; i < v.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(CsvCell(v[i]));
                }
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        private static string CsvCell(string s)
        {
            if (s == null) s = "";
            if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@' || s[0] == '\t' || s[0] == '\r')) s = "'" + s;
            if (s.IndexOfAny(new char[] { ',', '"', '\r', '\n' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static ushort ReadU16(byte[] p, int o) { return (ushort)(p[o] | (p[o + 1] << 8)); }
        private static uint ReadU32(byte[] p, int o) { return (uint)(p[o] | (p[o + 1] << 8) | (p[o + 2] << 16) | (p[o + 3] << 24)); }
    }

    internal sealed class MaintenanceMessage
    {
        public ushort Type;
        public byte[] Data;
    }

    internal static class MaintenanceProtocol
    {
        public const int BaudRate = 38400;
        public const ushort NotifyDevInfo = 0x0518;
        public const ushort NotifyBlVer = 0x0530;
        public const ushort ProgFw = 0x0519;
        public const ushort ProgFwResp = 0x051A;
        public const ushort DevInfoReq = 0x0514;
        public const ushort DevInfoResp = 0x0515;
        public const ushort ReadEeprom = 0x051B;
        public const ushort ReadEepromResp = 0x051C;
        public const ushort WriteEeprom = 0x051D;
        public const ushort WriteEepromResp = 0x051E;
        public const ushort Reboot = 0x05DD;
        public const int CalibrationSize = 512;
        public const int ChunkSize = 16;
        public const int LogoOffset = 0xC000;
        public const int LogoBitmapSize = 1024;
        public const int LogoHeaderSize = 8;
        public const int LogoPaddedSize = 1040;
        public static readonly byte[] LogoMagic = Encoding.ASCII.GetBytes("F4HWNLGO");

        private static readonly byte[] Obfus = new byte[]
        {
            0x16,0x6c,0x14,0xe6,0x2e,0x91,0x0d,0x40,0x21,0x35,0xd5,0x40,0x13,0x03,0xe9,0x80
        };

        public static byte[] CreateMessage(ushort type, int dataLen)
        {
            byte[] msg = new byte[4 + dataLen];
            WriteU16(msg, 0, type);
            WriteU16(msg, 2, (ushort)dataLen);
            return msg;
        }

        public static byte[] MakePacket(byte[] msg)
        {
            int msgLen = msg.Length;
            if ((msgLen & 1) != 0) msgLen++;
            byte[] buf = new byte[8 + msgLen];
            WriteU16(buf, 0, 0xCDAB);
            WriteU16(buf, 2, (ushort)msgLen);
            Buffer.BlockCopy(msg, 0, buf, 4, msg.Length);
            ushort crc = Crc16(buf, 4, msgLen);
            WriteU16(buf, 4 + msgLen, crc);
            WriteU16(buf, 6 + msgLen, 0xBADC);
            Obfuscate(buf, 4, 2 + msgLen);
            return buf;
        }

        public static bool TryTakeMessage(List<byte> buffer, out MaintenanceMessage message)
        {
            message = null;
            if (buffer.Count < 8) return false;
            int begin = -1;
            for (int i = 0; i < buffer.Count - 1; i++)
            {
                if (buffer[i] == 0xAB && buffer[i + 1] == 0xCD) { begin = i; break; }
            }
            if (begin < 0)
            {
                bool keep = buffer.Count > 0 && buffer[buffer.Count - 1] == 0xAB;
                byte tail = keep ? buffer[buffer.Count - 1] : (byte)0;
                buffer.Clear(); if (keep) buffer.Add(tail);
                return false;
            }
            if (begin > 0) buffer.RemoveRange(0, begin);
            if (buffer.Count < 8) return false;
            int msgLen = buffer[2] | (buffer[3] << 8);
            int packEnd = 6 + msgLen;
            if (buffer.Count < packEnd + 2) return false;
            if (buffer[packEnd] != 0xDC || buffer[packEnd + 1] != 0xBA)
            {
                buffer.RemoveRange(0, Math.Min(2, buffer.Count));
                return false;
            }
            byte[] body = new byte[msgLen + 2];
            buffer.CopyTo(4, body, 0, body.Length);
            Obfuscate(body, 0, body.Length);
            ushort receivedCrc = ReadU16(body, msgLen);
            ushort calc = Crc16(body, 0, msgLen);
            if (receivedCrc != calc)
            {
                buffer.RemoveRange(0, packEnd + 2);
                return false;
            }
            ushort type = ReadU16(body, 0);
            int declared = ReadU16(body, 2);
            int dataLen = Math.Min(Math.Max(0, declared), Math.Max(0, msgLen - 4));
            byte[] data = new byte[dataLen];
            if (dataLen > 0) Buffer.BlockCopy(body, 4, data, 0, dataLen);
            buffer.RemoveRange(0, packEnd + 2);
            message = new MaintenanceMessage { Type = type, Data = data };
            return true;
        }

        public static ushort Crc16(byte[] buf, int offset, int size)
        {
            int crc = 0;
            for (int i = 0; i < size; i++)
            {
                crc ^= (buf[offset + i] & 0xFF) << 8;
                for (int j = 0; j < 8; j++) crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) & 0xFFFF : (crc << 1) & 0xFFFF;
            }
            return (ushort)crc;
        }

        private static void Obfuscate(byte[] buf, int off, int size)
        {
            for (int i = 0; i < size; i++) buf[off + i] ^= Obfus[i % Obfus.Length];
        }

        public static ushort ReadU16(byte[] p, int o) { return (ushort)(p[o] | (p[o + 1] << 8)); }
        public static uint ReadU32(byte[] p, int o) { return (uint)(p[o] | (p[o + 1] << 8) | (p[o + 2] << 16) | (p[o + 3] << 24)); }
        public static void WriteU16(byte[] p, int o, ushort v) { p[o] = (byte)v; p[o + 1] = (byte)(v >> 8); }
        public static void WriteU32(byte[] p, int o, uint v) { p[o] = (byte)v; p[o + 1] = (byte)(v >> 8); p[o + 2] = (byte)(v >> 16); p[o + 3] = (byte)(v >> 24); }
    }
}
