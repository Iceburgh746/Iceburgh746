using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UVConsole
{
    internal sealed class RadioSession : IDisposable
    {
        private readonly object sync = new object();
        private readonly List<byte> rx = new List<byte>();
        private SerialPort port;
        private System.Threading.Timer keepAlive;
        private Thread readerThread;
        private volatile bool readerStop;
        private bool viewerMode;
        private bool rfRestart = true;
        private int keepAliveTick;
        private long totalBytesReceived;
        private long totalViewerFrames;
        private DateTime lastViewerFrameUtc = DateTime.MinValue;

        public event Action<ViewerFrame> ViewerFrameReceived;
        public event Action BytesReceived;
        public event Action<string> Faulted;

        public bool IsOpen { get { lock (sync) { return port != null && port.IsOpen; } } }
        public string PortName { get { lock (sync) { return port == null ? "" : port.PortName; } } }
        public long TotalBytesReceived { get { lock (sync) { return totalBytesReceived; } } }
        public long TotalViewerFrames { get { lock (sync) { return totalViewerFrames; } } }
        public DateTime LastViewerFrameUtc { get { lock (sync) { return lastViewerFrameUtc; } } }

        public void Open(string portName, bool viewer)
        {
            Close();
            SerialPort sp = new SerialPort(portName, 38400, Parity.None, 8, StopBits.One);
            sp.Handshake = Handshake.None;
            sp.ReadTimeout = 500;
            sp.WriteTimeout = 1000;
            sp.ReadBufferSize = 65536;
            sp.WriteBufferSize = 4096;
            sp.ReceivedBytesThreshold = 1;
            // F4HWN's USB CDC viewer transmit path is gated by DTR.
            // Assert DTR for viewer mode; on K2-style USB/UART cables the
            // DTR conductor is normally unused, so this remains harmless.
            sp.DtrEnable = viewer;
            sp.RtsEnable = false;
            sp.Open();
            if (viewer)
            {
                try { sp.DtrEnable = true; } catch { }
            }
            lock (sync)
            {
                port = sp;
                viewerMode = viewer;
                rx.Clear();
                rfRestart = true;
                keepAliveTick = 0;
                totalBytesReceived = 0;
                totalViewerFrames = 0;
                lastViewerFrameUtc = DateTime.MinValue;
                readerStop = false;
            }

            readerThread = new Thread(ReaderLoop);
            readerThread.IsBackground = true;
            readerThread.Name = "UVConsole.SerialReader";
            readerThread.Start();

            if (viewer)
            {
                ForceViewerSync();
                keepAlive = new System.Threading.Timer(delegate { SendKeepAlive(); }, null, 200, 200);
            }
        }

        private void SendKeepAlive()
        {
            try
            {
                bool restart;
                lock (sync)
                {
                    if (!viewerMode || port == null || !port.IsOpen) return;
                    restart = rfRestart;
                    keepAliveTick++;
                    rfRestart = false;
                }

                // Match UV Studio/K5Viewer exactly: every 200 ms send the
                // base viewer ping, immediately followed by the feature ping.
                // The first feature ping includes the RF-log restart bit.
                Write(ViewerProtocol.MakeViewerKeepAlive());
                Write(ViewerProtocol.MakeKeepAlive(restart));
            }
            catch (Exception ex)
            {
                bool stillOpen;
                lock (sync) stillOpen = viewerMode && port != null && port.IsOpen;
                if (!stillOpen) return;
                Action<string> f = Faulted;
                if (f != null) f(ex.Message);
            }
        }

        private void ReaderLoop()
        {
            while (!readerStop)
            {
                SerialPort p;
                lock (sync) p = port;
                if (p == null || !p.IsOpen) break;

                try
                {
                    // Web Serial's reader waits for incoming bytes rather than
                    // polling a byte-count property. Do the same here: a blocking
                    // SerialPort.Read with a short timeout is more reliable across
                    // USB CDC and USB/UART drivers than BytesToRead polling.
                    byte[] data = new byte[8192];
                    int got = p.Read(data, 0, data.Length);
                    if (got <= 0) continue;

                    lock (sync)
                    {
                        for (int i = 0; i < got; i++) rx.Add(data[i]);
                        totalBytesReceived += got;
                    }

                    bool viewer;
                    lock (sync) viewer = viewerMode;
                    if (viewer) ProcessViewerFrames();

                    Action b = BytesReceived;
                    if (b != null) b();
                }
                catch (TimeoutException)
                {
                    // Normal on a quiet link.
                }
                catch (InvalidOperationException)
                {
                    if (!readerStop) NotifyFault("Serial port closed unexpectedly.");
                    break;
                }
                catch (IOException ex)
                {
                    if (!readerStop) NotifyFault(ex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    if (!readerStop) NotifyFault(ex.Message);
                    break;
                }
            }
        }

        private void ProcessViewerFrames()
        {
            while (true)
            {
                ViewerFrame frame;
                lock (sync)
                {
                    if (!ViewerProtocol.TryTakeFrame(rx, out frame)) break;
                    if (frame.Type == ViewerProtocol.TypeScreenshot || frame.Type == ViewerProtocol.TypeDiff)
                    {
                        totalViewerFrames++;
                        lastViewerFrameUtc = DateTime.UtcNow;
                    }
                }

                Action<ViewerFrame> h = ViewerFrameReceived;
                if (h != null) h(frame);
            }
        }

        private void NotifyFault(string message)
        {
            Action<string> f = Faulted;
            if (f != null) f(message);
        }

        public void Write(byte[] data)
        {
            lock (sync)
            {
                if (port == null || !port.IsOpen) throw new InvalidOperationException("Serial port is not connected.");
                port.Write(data, 0, data.Length);
            }
        }

        public MaintenanceMessage WaitMaintenanceMessage(int timeoutMs, params ushort[] wanted)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                while (true)
                {
                    MaintenanceMessage m;
                    lock (sync)
                    {
                        if (!MaintenanceProtocol.TryTakeMessage(rx, out m)) break;
                    }
                    if (wanted == null || wanted.Length == 0) return m;
                    for (int i = 0; i < wanted.Length; i++) if (m.Type == wanted[i]) return m;
                }
                Thread.Sleep(10);
            }
            return null;
        }

        public void ClearReceive()
        {
            lock (sync) rx.Clear();
            try
            {
                SerialPort p;
                lock (sync) p = port;
                if (p != null && p.IsOpen) p.DiscardInBuffer();
            }
            catch { }
        }

        public int BufferedCount { get { lock (sync) return rx.Count; } }

        public void SendKey(byte key, bool longPress)
        {
            Write(ViewerProtocol.MakeKey(key, longPress));
        }

        public void SendViewerReboot()
        {
            Write(ViewerProtocol.RebootPacket);
        }

        public void ForceViewerSync()
        {
            bool canSync;
            lock (sync)
            {
                canSync = viewerMode && port != null && port.IsOpen;
                rfRestart = true;
                keepAliveTick = 0;
            }
            if (!canSync) return;

            // Exact UV Studio resync sequence: base keepalive, then the
            // feature keepalive with restart/history/RF-log flags.
            Write(ViewerProtocol.MakeViewerKeepAlive());
            Write(ViewerProtocol.MakeKeepAlive(true));
            lock (sync) rfRestart = false;
        }

        public void Close()
        {
            System.Threading.Timer t = keepAlive;
            keepAlive = null;
            if (t != null) { try { t.Dispose(); } catch { } }

            readerStop = true;
            SerialPort p = null;
            Thread r = null;
            lock (sync)
            {
                p = port;
                port = null;
                r = readerThread;
                readerThread = null;
                rx.Clear();
                viewerMode = false;
            }

            if (p != null)
            {
                try { if (p.IsOpen) p.Close(); } catch { }
                try { p.Dispose(); } catch { }
            }

            if (r != null && r != Thread.CurrentThread)
            {
                try { r.Join(350); } catch { }
            }
        }

        public void Dispose() { Close(); }
    }

    internal sealed class MaintenanceService
    {
        private readonly RadioSession session;
        private readonly Action<string> log;
        private readonly Action<int> progress;

        public MaintenanceService(RadioSession session, Action<string> logger, Action<int> progressHandler)
        {
            this.session = session;
            this.log = logger;
            this.progress = progressHandler;
        }

        private void Log(string s) { if (log != null) log(s); }
        private void Progress(int p) { if (progress != null) progress(Math.Max(0, Math.Min(100, p))); }

        private void Send(byte[] message) { session.Write(MaintenanceProtocol.MakePacket(message)); }

        public string RequestDeviceInfo(out uint timestamp, out int calibrationOffset)
        {
            timestamp = CurrentTimestamp();
            calibrationOffset = 0x1E00;
            byte[] msg = MaintenanceProtocol.CreateMessage(MaintenanceProtocol.DevInfoReq, 4);
            MaintenanceProtocol.WriteU32(msg, 4, timestamp);
            Send(msg);
            MaintenanceMessage resp = session.WaitMaintenanceMessage(5000, MaintenanceProtocol.DevInfoResp);
            if (resp == null) throw new TimeoutException("No normal-mode device-information response received.");
            string info = AsciiPrefix(resp.Data);
            Version v;
            int idx = info.IndexOf('v');
            if (idx >= 0 && Version.TryParse(ExtractVersion(info.Substring(idx + 1)), out v) && v.Major >= 5) calibrationOffset = 0xB000;
            return info.Length == 0 ? "Device responded (binary info)" : info;
        }

        public byte[] DumpCalibration()
        {
            uint ts; int offset;
            string info = RequestDeviceInfo(out ts, out offset);
            Log("Device: " + info);
            Log("Calibration base: 0x" + offset.ToString("X4"));
            byte[] result = new byte[MaintenanceProtocol.CalibrationSize];
            for (int i = 0; i < result.Length; i += MaintenanceProtocol.ChunkSize)
            {
                byte[] msg = MaintenanceProtocol.CreateMessage(MaintenanceProtocol.ReadEeprom, 8);
                MaintenanceProtocol.WriteU16(msg, 4, (ushort)(offset + i));
                MaintenanceProtocol.WriteU16(msg, 6, MaintenanceProtocol.ChunkSize);
                MaintenanceProtocol.WriteU32(msg, 8, ts);
                Send(msg);
                MaintenanceMessage resp = session.WaitMaintenanceMessage(3000, MaintenanceProtocol.ReadEepromResp);
                if (resp == null || resp.Data.Length < 20) throw new IOException("EEPROM read timeout at 0x" + (offset + i).ToString("X4"));
                ushort ro = MaintenanceProtocol.ReadU16(resp.Data, 0);
                int size = resp.Data[2];
                if (ro != offset + i || size != MaintenanceProtocol.ChunkSize) throw new IOException("Unexpected EEPROM response.");
                Buffer.BlockCopy(resp.Data, 4, result, i, MaintenanceProtocol.ChunkSize);
                Progress((i + MaintenanceProtocol.ChunkSize) * 100 / result.Length);
            }
            Log("Calibration dump complete.");
            return result;
        }

        public void RestoreCalibration(byte[] data)
        {
            if (data == null || data.Length != MaintenanceProtocol.CalibrationSize) throw new ArgumentException("Calibration file must be exactly 512 bytes.");
            uint ts; int offset;
            string info = RequestDeviceInfo(out ts, out offset);
            Log("Device: " + info);
            for (int i = 0; i < data.Length; i += MaintenanceProtocol.ChunkSize)
            {
                byte[] msg = MaintenanceProtocol.CreateMessage(MaintenanceProtocol.WriteEeprom, 24);
                MaintenanceProtocol.WriteU16(msg, 4, (ushort)(offset + i));
                MaintenanceProtocol.WriteU16(msg, 6, MaintenanceProtocol.ChunkSize);
                msg[7] = 1;
                MaintenanceProtocol.WriteU32(msg, 8, ts);
                Buffer.BlockCopy(data, i, msg, 12, MaintenanceProtocol.ChunkSize);
                Send(msg);
                MaintenanceMessage resp = session.WaitMaintenanceMessage(3000, MaintenanceProtocol.WriteEepromResp);
                if (resp == null || resp.Data.Length < 2 || MaintenanceProtocol.ReadU16(resp.Data, 0) != offset + i) throw new IOException("EEPROM write timeout at 0x" + (offset + i).ToString("X4"));
                Progress((i + MaintenanceProtocol.ChunkSize) * 100 / data.Length);
            }
            Send(MaintenanceProtocol.CreateMessage(MaintenanceProtocol.Reboot, 0));
            Log("Calibration restore complete; reboot requested.");
        }

        public byte[] DumpLogo()
        {
            uint ts; int ignored;
            string info = RequestDeviceInfo(out ts, out ignored);
            Log("Device: " + info);
            byte[] result = new byte[MaintenanceProtocol.LogoPaddedSize];
            for (int i = 0; i < result.Length; i += MaintenanceProtocol.ChunkSize)
            {
                byte[] msg = MaintenanceProtocol.CreateMessage(MaintenanceProtocol.ReadEeprom, 8);
                MaintenanceProtocol.WriteU16(msg, 4, (ushort)(MaintenanceProtocol.LogoOffset + i));
                MaintenanceProtocol.WriteU16(msg, 6, MaintenanceProtocol.ChunkSize);
                MaintenanceProtocol.WriteU32(msg, 8, ts);
                Send(msg);
                MaintenanceMessage resp = session.WaitMaintenanceMessage(3000, MaintenanceProtocol.ReadEepromResp);
                if (resp == null || resp.Data.Length < 20) throw new IOException("Logo read timeout.");
                Buffer.BlockCopy(resp.Data, 4, result, i, MaintenanceProtocol.ChunkSize);
                Progress((i + MaintenanceProtocol.ChunkSize) * 100 / result.Length);
            }
            return result;
        }

        public void UploadLogo(byte[] bitmap)
        {
            if (bitmap == null || bitmap.Length != MaintenanceProtocol.LogoBitmapSize) throw new ArgumentException("Logo bitmap must be 1024 bytes.");
            uint ts; int ignored;
            string info = RequestDeviceInfo(out ts, out ignored);
            Log("Device: " + info);
            byte[] payload = new byte[MaintenanceProtocol.LogoPaddedSize];
            for (int i = 0; i < payload.Length; i++) payload[i] = 0xFF;
            Buffer.BlockCopy(MaintenanceProtocol.LogoMagic, 0, payload, 0, MaintenanceProtocol.LogoMagic.Length);
            Buffer.BlockCopy(bitmap, 0, payload, MaintenanceProtocol.LogoHeaderSize, bitmap.Length);
            for (int i = 0; i < payload.Length; i += MaintenanceProtocol.ChunkSize)
            {
                int off = MaintenanceProtocol.LogoOffset + i;
                byte[] msg = MaintenanceProtocol.CreateMessage(MaintenanceProtocol.WriteEeprom, 24);
                MaintenanceProtocol.WriteU16(msg, 4, (ushort)off);
                MaintenanceProtocol.WriteU16(msg, 6, MaintenanceProtocol.ChunkSize);
                msg[7] = 1;
                MaintenanceProtocol.WriteU32(msg, 8, ts);
                Buffer.BlockCopy(payload, i, msg, 12, MaintenanceProtocol.ChunkSize);
                Send(msg);
                MaintenanceMessage resp = session.WaitMaintenanceMessage(3000, MaintenanceProtocol.WriteEepromResp);
                if (resp == null || resp.Data.Length < 2 || MaintenanceProtocol.ReadU16(resp.Data, 0) != off) throw new IOException("Logo write timeout.");
                Progress((i + MaintenanceProtocol.ChunkSize) * 100 / payload.Length);
            }
            Send(MaintenanceProtocol.CreateMessage(MaintenanceProtocol.Reboot, 0));
            Log("Boot logo uploaded; reboot requested.");
        }

        public void FlashFirmware(byte[] firmware)
        {
            if (firmware == null || firmware.Length == 0) throw new ArgumentException("Firmware image is empty.");
            Log("Waiting for DFU bootloader announcements...");
            string bl; byte[] uid;
            WaitForBootloader(out uid, out bl);
            Log("Bootloader: " + bl);
            Version version;
            if (Version.TryParse(bl, out version) && version < new Version(7, 0, 7)) throw new InvalidOperationException("Bootloader " + bl + " is older than required 7.00.07.");
            Handshake(bl);
            int pageCount = (firmware.Length + 255) / 256;
            uint stamp = CurrentTimestamp();
            for (int page = 0; page < pageCount; page++)
            {
                bool ok = false;
                for (int retry = 0; retry < 4 && !ok; retry++)
                {
                    byte[] msg = MaintenanceProtocol.CreateMessage(MaintenanceProtocol.ProgFw, 268);
                    MaintenanceProtocol.WriteU32(msg, 4, stamp);
                    MaintenanceProtocol.WriteU16(msg, 8, (ushort)page);
                    MaintenanceProtocol.WriteU16(msg, 10, (ushort)pageCount);
                    int src = page * 256;
                    int len = Math.Min(256, firmware.Length - src);
                    Buffer.BlockCopy(firmware, src, msg, 16, len);
                    Send(msg);
                    MaintenanceMessage resp = session.WaitMaintenanceMessage(3000, MaintenanceProtocol.ProgFwResp);
                    if (resp == null || resp.Data.Length < 8) continue;
                    ushort responsePage = MaintenanceProtocol.ReadU16(resp.Data, 4);
                    ushort err = MaintenanceProtocol.ReadU16(resp.Data, 6);
                    if (responsePage == page && err == 0) ok = true;
                    else Log("Page " + page + " response error " + err + "; retry " + (retry + 1));
                }
                if (!ok) throw new IOException("Firmware page " + page + " failed after retries.");
                Progress((page + 1) * 100 / pageCount);
                if ((page + 1) % 10 == 0 || page == pageCount - 1) Log("Page " + (page + 1) + "/" + pageCount + " OK");
            }
            Log("Firmware programming complete.");
        }

        private void WaitForBootloader(out byte[] uid, out string blVersion)
        {
            uid = null; blVersion = null;
            DateTime deadline = DateTime.UtcNow.AddSeconds(6);
            int valid = 0;
            while (DateTime.UtcNow < deadline)
            {
                MaintenanceMessage m = session.WaitMaintenanceMessage(700, MaintenanceProtocol.NotifyDevInfo);
                if (m == null) continue;
                valid++;
                if (valid >= 5 && m.Data.Length >= 16)
                {
                    uid = new byte[16]; Buffer.BlockCopy(m.Data, 0, uid, 0, 16);
                    int end = Math.Min(32, m.Data.Length); int nul = 16;
                    while (nul < end && m.Data[nul] != 0) nul++;
                    blVersion = Encoding.ASCII.GetString(m.Data, 16, Math.Max(0, nul - 16));
                    return;
                }
            }
            throw new TimeoutException("No compatible DFU bootloader detected. Power off, hold PTT, power on, release PTT, then reconnect the cable.");
        }

        private void Handshake(string blVersion)
        {
            int sent = 0;
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (sent < 3 && DateTime.UtcNow < deadline)
            {
                MaintenanceMessage m = session.WaitMaintenanceMessage(1000, MaintenanceProtocol.NotifyDevInfo);
                if (m == null) continue;
                byte[] msg = MaintenanceProtocol.CreateMessage(MaintenanceProtocol.NotifyBlVer, 4);
                byte[] v = Encoding.ASCII.GetBytes(blVersion.Length > 4 ? blVersion.Substring(0, 4) : blVersion);
                Buffer.BlockCopy(v, 0, msg, 4, Math.Min(4, v.Length));
                Send(msg);
                sent++;
                Thread.Sleep(50);
            }
            if (sent < 3) throw new TimeoutException("DFU handshake did not complete.");
            Thread.Sleep(200);
        }


        private static uint CurrentTimestamp()
        {
            long ms = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            return unchecked((uint)ms);
        }

        private static string AsciiPrefix(byte[] data)
        {
            if (data == null) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                byte c = data[i];
                if (c == 0 || c == 0xFF) break;
                if (c >= 32 && c < 127) sb.Append((char)c);
            }
            return sb.ToString();
        }

        private static string ExtractVersion(string s)
        {
            StringBuilder b = new StringBuilder();
            foreach (char c in s)
            {
                if ((c >= '0' && c <= '9') || c == '.') b.Append(c); else if (b.Length > 0) break;
            }
            return b.ToString();
        }
    }
}
