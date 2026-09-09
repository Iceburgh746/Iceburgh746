using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UVConsole
{
    internal sealed class MainForm : Form
    {
        private readonly Color Bg = Color.FromArgb(10,13,16);
        private readonly Color PanelBg = Color.FromArgb(19,24,28);
        private readonly Color PanelBg2 = Color.FromArgb(25,31,36);
        private readonly Color Edge = Color.FromArgb(48,65,72);
        private readonly Color Accent = Color.FromArgb(55,211,237);
        private readonly Color TextMain = Color.FromArgb(226,238,241);
        private readonly Color TextDim = Color.FromArgb(132,154,162);

        private readonly RadioSession session = new RadioSession();
        private readonly byte[] framebuffer = new byte[ViewerProtocol.FrameSize];
        private readonly Dictionary<uint,RfLogRow> rfRows = new Dictionary<uint,RfLogRow>();
        private RfLogRow rfLive;
        private byte rfStatus;

        private ComboBox portCombo;
        private Button connectButton;
        private LedLamp connectionLed, redLed, greenLed;
        private Label connectionLabel, fpsLabel, deepSleepLabel, radioStateLabel, screenStatusLabel, screenStatsLabel;
        private RadioDisplayControl display;
        private ActivityScope scope;
        private TabControl mainTabs;
        private DataGridView rfGrid;
        private Label rfSummary;
        private ProgressBar maintenanceProgress;
        private TextBox maintenanceLog;
        private Label firmwareLabel, calibrationLabel, logoLabel;
        private byte[] firmwareData, calibrationData, logoBitmap;
        private string firmwarePath;
        private Bitmap logoSource;
        private TrackBar logoThreshold;
        private CheckBox logoInvert;
        private PictureBox logoPreview;
        private bool k1Model = true;
        private Button modelK1Button, modelK5Button, upButton, downButton, syncScreenButton;
        private int frameCounter;
        private DateTime fpsStart = DateTime.UtcNow;
        private bool operationBusy;
        private readonly List<Button> radioKeyButtons = new List<Button>();
        private System.Windows.Forms.Timer viewerWatchdog;
        private DateTime lastAutoSyncUtc = DateTime.MinValue;
        private TextBox dashboardLog;
        private bool displayFrozen;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        public MainForm()
        {
            Text = "UV Console by WRCX 212";
            BackColor = Bg;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1536, 1024);
            MinimumSize = new Size(1536, 1024);
            MaximumSize = new Size(1536, 1024);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            KeyPreview = true;
            KeyDown += MainKeyDown;
            Shown += delegate
            {
                try
                {
                    BringToFront();
                    Activate();
                    AppendMaintenanceLog("Main console window shown.");
                }
                catch { }
            };
            BuildUi();
            WireSession();
            StartViewerWatchdog();
            RefreshPorts();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (operationBusy)
            {
                DialogResult r = MessageBox.Show(this, "A radio maintenance operation is active. Closing now could interrupt it.\n\nDo you still want to exit?", "Operation active", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
            }
            if (viewerWatchdog != null) { viewerWatchdog.Stop(); viewerWatchdog.Dispose(); viewerWatchdog = null; }
            session.Close();
            base.OnFormClosing(e);
        }

        private void BuildUi()
        {
            // Build the secondary tool pages so all proven v0.9.4 maintenance
            // functions remain available, but keep them off the faceplate.
            mainTabs = new TabControl { Visible = false, Size = new Size(1,1), Location = new Point(-20,-20) };
            mainTabs.TabPages.Add(new TabPage("LIVE"));
            mainTabs.TabPages.Add(BuildRfTab());
            mainTabs.TabPages.Add(BuildMaintenanceTab());
            mainTabs.TabPages.Add(BuildAboutTab());
            Controls.Add(mainTabs);
            mainTabs.SelectedIndex = 0;

            // The supplied reference image is the actual native faceplate.
            // Parenting it directly to the Form prevents TabControl borders,
            // DPI padding, or normal Windows chrome from changing its geometry.
            Panel face = BuildLiveTab();
            face.Dock = DockStyle.Fill;
            face.Margin = new Padding(0);
            face.Padding = new Padding(0);
            Controls.Add(face);
            face.BringToFront();
        }

        private Control BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Color.FromArgb(14,18,21) };
            header.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Color.FromArgb(38,100,118))) e.Graphics.DrawLine(p, 0, header.Height-1, header.Width, header.Height-1);
            };
            Label title = new Label { AutoSize = true, Text = "UV CONSOLE", ForeColor = TextMain, Font = new Font("Segoe UI Semibold", 20f), Location = new Point(22, 13) };
            Label sub = new Label { AutoSize = true, Text = "BY WRCX 212  /  NATIVE WINDOWS QUANSHENG RADIO CONSOLE", ForeColor = Accent, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), Location = new Point(25, 53) };
            header.Controls.Add(title); header.Controls.Add(sub);

            FlowLayoutPanel right = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 550, Padding = new Padding(8,20,18,0), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            Label pl = SmallLabel("PORT"); pl.Margin = new Padding(0,9,6,0);
            portCombo = new ComboBox { Width = 105, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = PanelBg2, ForeColor = TextMain, FlatStyle = FlatStyle.Flat, Margin = new Padding(0,4,7,0) };
            Button refresh = TechButton("REFRESH", 74); refresh.Click += delegate { RefreshPorts(); };
            connectButton = TechButton("CONNECT", 100); connectButton.Click += delegate { ToggleViewerConnection(); };
            connectionLed = new LedLamp { LampColor = Color.FromArgb(51,255,151), Margin = new Padding(12,9,2,0) };
            connectionLabel = new Label { AutoSize = true, Text = "OFFLINE", ForeColor = TextDim, Font = new Font("Segoe UI",8.2f,FontStyle.Bold), Margin = new Padding(3,10,0,0) };
            right.Controls.Add(pl); right.Controls.Add(portCombo); right.Controls.Add(refresh); right.Controls.Add(connectButton); right.Controls.Add(connectionLed); right.Controls.Add(connectionLabel);
            header.Controls.Add(right);
            return header;
        }

        private Panel BuildLiveTab()
        {
            Panel tab = new Panel();
            tab.BackColor = Bg;
            tab.Padding = new Padding(0);
            tab.Margin = new Padding(0);
            tab.Visible = true;
            tab.Enabled = true;
            tab.BackgroundImage = LoadFaceplateImage();
            tab.BackgroundImageLayout = ImageLayout.None;
            tab.AutoScroll = false;

            // Preserve the exact renderer from the last confirmed-working build.
            // The faceplate opening itself is not 2:1, so use a host panel for
            // letterbox margins and place a true 2:1 live LCD inside it.
            Panel lcdHost = new Panel {
                Location = new Point(394, 261), Size = new Size(429, 308),
                // The faceplate opening is taller than the radio's true 2:1 LCD.
                // Treat the unused area as a recessed bezel, not as LCD glass.
                BackColor = Color.FromArgb(18, 31, 44), Margin = new Padding(0), Padding = new Padding(0)
            };
            display = new RadioDisplayControl {
                // Use almost the full available width. 424x212 is exactly 2:1.
                // Center it vertically inside the taller cosmetic opening.
                Location = new Point(2, 48), Size = new Size(424, 212),
                ShowBezel = false, ForceFill = true, SmoothMode = true, Glow = false,
                ScreenForeground = Color.FromArgb(29, 58, 111),
                ScreenBackground = Color.FromArgb(205, 235, 247)
            };
            lcdHost.Controls.Add(display);
            tab.Controls.Add(lcdHost);
            lcdHost.BringToFront();

            // The actual COM selector sits directly over the painted selector.
            portCombo = new ComboBox {
                Location = new Point(37, 241), Size = new Size(229, 30),
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(47, 52, 54), ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };
            tab.Controls.Add(portCombo);

            // Dynamic connection state covers the mockup's static word "Connected".
            Panel connState = new Panel { Location = new Point(35, 278), Size = new Size(208, 31), BackColor = Color.FromArgb(31, 35, 36) };
            connectionLed = new LedLamp { Location = new Point(3, 6), LampColor = Color.FromArgb(46, 245, 92) };
            connectionLabel = new Label { Location = new Point(29, 4), Size = new Size(170, 22), Text = "OFFLINE", BackColor = Color.Transparent, ForeColor = Color.FromArgb(175,180,182), Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            connState.Controls.Add(connectionLed); connState.Controls.Add(connectionLabel); tab.Controls.Add(connState);

            // Correct the illustrative baud value in the concept artwork. The
            // proven viewer transport is 38400 and remains unchanged.
            Label baud = new Label { Location = new Point(83, 314), Size = new Size(96, 29), Text = "38400", BackColor = Color.FromArgb(51,55,57), ForeColor = Color.White, Font = new Font("Segoe UI",9.5f), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10,0,0,0) };
            tab.Controls.Add(baud);

            // Dummy controls retained for the known-good v0.9.4 state-management code.
            connectButton = new Button { Visible = false, Text = "CONNECT" };
            redLed = new LedLamp { Visible = false }; greenLed = new LedLamp { Visible = false };
            deepSleepLabel = new Label { Visible = false, Text = "AWAKE" };
            fpsLabel = new Label { Visible = false, Text = "0 FPS" };
            radioStateLabel = new Label { Visible = false, Text = "WAITING FOR RADIO" };
            syncScreenButton = new Button { Visible = false, Enabled = false };
            modelK1Button = new Button { Visible = false }; modelK5Button = new Button { Visible = false };
            upButton = new Button { Visible = false }; downButton = new Button { Visible = false };
            scope = new ActivityScope { Visible = false, Size = new Size(1,1) };
            tab.Controls.Add(connectButton); tab.Controls.Add(redLed); tab.Controls.Add(greenLed);
            tab.Controls.Add(deepSleepLabel); tab.Controls.Add(fpsLabel); tab.Controls.Add(radioStateLabel);
            tab.Controls.Add(syncScreenButton); tab.Controls.Add(modelK1Button); tab.Controls.Add(modelK5Button); tab.Controls.Add(upButton); tab.Controls.Add(downButton); tab.Controls.Add(scope);

            // Live screen diagnostics overwrite only the changing values in the
            // lower-left SCREEN panel, preserving the faceplate artwork.
            screenStatusLabel = new Label { Location = new Point(66, 646), Size = new Size(225, 25), BackColor = Color.FromArgb(28,32,33), ForeColor = Color.FromArgb(80,255,105), Font = new Font("Segoe UI Semibold",9f,FontStyle.Bold), Text = "Disconnected", TextAlign = ContentAlignment.MiddleLeft };
            screenStatsLabel = new Label { Location = new Point(126, 679), Size = new Size(180, 58), BackColor = Color.FromArgb(28,32,33), ForeColor = Color.White, Font = new Font("Consolas",9f), Text = "RX 0 bytes\r\n0 frames" };
            tab.Controls.Add(screenStatusLabel); tab.Controls.Add(screenStatsLabel);

            dashboardLog = new TextBox { Location = new Point(31, 824), Size = new Size(290, 135), Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(3,14,8), ForeColor = Color.FromArgb(58,255,79), Font = new Font("Consolas",8.8f), ScrollBars = ScrollBars.Vertical, Text = "[READY] UV Console faceplate loaded.\r\n" };
            tab.Controls.Add(dashboardLog);

            // Invisible hit areas preserve the exact artwork while making the
            // controls real. No imitation WinForms button chrome is painted.
            AddHot(tab, new Rectangle(281,241,35,31), delegate { RefreshPorts(); });
            AddHot(tab, new Rectangle(39,369,79,44), delegate { if (!session.IsOpen) ToggleViewerConnection(); });
            AddHot(tab, new Rectangle(133,382,116,31), delegate { if (session.IsOpen) ToggleViewerConnection(); });
            AddHot(tab, new Rectangle(38,750,135,30), delegate { ForceScreenSync(); });
            AddHot(tab, new Rectangle(184,750,136,30), delegate { SaveScreenshot(); });
            AddHot(tab, new Rectangle(857,286,125,31), delegate { display.SmoothMode = true; });
            AddHot(tab, new Rectangle(857,322,125,31), delegate { display.SmoothMode = false; });
            AddHot(tab, new Rectangle(856,414,114,36), delegate { display.Invert = !display.Invert; });
            AddHot(tab, new Rectangle(856,500,114,34), delegate { displayFrozen = !displayFrozen; AppendMaintenanceLog(displayFrozen ? "Display frozen." : "Display resumed."); });
            AddHot(tab, new Rectangle(856,540,114,34), delegate { byte[] blank = new byte[ViewerProtocol.FrameSize]; lock(framebuffer) Buffer.BlockCopy(blank,0,framebuffer,0,blank.Length); display.SetFrame(blank); });

            // Keypad hit areas: visually they are the exact buttons in the supplied image.
            AddFaceKey(tab, new Rectangle(1007,247,74,52), 0x0A); // MENU
            AddFaceKey(tab, new Rectangle(1089,247,74,52), 0x0B); // up/left key family
            AddFaceKey(tab, new Rectangle(1172,247,76,52), 0x0D); // EXIT
            AddFaceKey(tab, new Rectangle(1007,307,74,52), 0x0B);
            AddFaceKey(tab, new Rectangle(1089,307,74,52), 0x0A); // OK = menu/select
            AddFaceKey(tab, new Rectangle(1172,307,76,52), 0x0C);
            AddFaceKey(tab, new Rectangle(1007,367,74,52), 0x01);
            AddFaceKey(tab, new Rectangle(1089,367,74,52), 0x02);
            AddFaceKey(tab, new Rectangle(1172,367,76,52), 0x03);
            AddFaceKey(tab, new Rectangle(1007,427,74,52), 0x04);
            AddFaceKey(tab, new Rectangle(1089,427,74,52), 0x05);
            AddFaceKey(tab, new Rectangle(1172,427,76,52), 0x06);
            AddFaceKey(tab, new Rectangle(1007,487,74,52), 0x07);
            AddFaceKey(tab, new Rectangle(1089,487,74,52), 0x08);
            AddFaceKey(tab, new Rectangle(1172,487,76,52), 0x09);
            AddFaceKey(tab, new Rectangle(1007,547,74,37), 0x0E);
            AddFaceKey(tab, new Rectangle(1089,547,74,37), 0x00);
            AddFaceKey(tab, new Rectangle(1172,547,76,37), 0x0F);

            // Quick actions that correspond to functions this native console really supports.
            AddHot(tab, new Rectangle(1284,399,216,34), async delegate { await DumpCalibrationAsync(); });
            AddHot(tab, new Rectangle(1284,439,216,34), delegate { ChooseCalibration(); });
            AddHot(tab, new Rectangle(1284,479,216,34), async delegate { await DumpCalibrationAsync(); });
            AddHot(tab, new Rectangle(1284,519,216,34), async delegate { await RestoreCalibrationAsync(); });
            AddHot(tab, new Rectangle(1284,559,216,34), async delegate { await DumpLogoAsync(); });
            AddHot(tab, new Rectangle(1284,599,216,34), delegate { ChooseLogoImage(); });

            // Top navigation and other mockup-only controls remain clickable but
            // explicitly report when the firmware protocol has no safe native action.
            AddHot(tab, new Rectangle(26,157,86,39), delegate { });
            AddHot(tab, new Rectangle(118,157,82,39), delegate { ShowProtocolNotice("RADIO", "Use the live display and virtual keypad to operate the radio directly."); });
            AddHot(tab, new Rectangle(205,157,85,39), delegate { ShowProtocolNotice("MEMORY", "Direct channel-memory writing is intentionally not guessed. Use a compatible CHIRP/CPS workflow for channel programming."); });
            AddHot(tab, new Rectangle(297,157,84,39), delegate { ShowProtocolNotice("SCAN", "Use the radio's own scan command from the keypad; the console mirrors the result live."); });
            AddHot(tab, new Rectangle(388,157,84,39), delegate { ShowProtocolNotice("VFO", "Enter frequencies with the virtual keypad while watching the live radio display."); });
            AddHot(tab, new Rectangle(478,157,82,39), delegate { ShowProtocolNotice("FM", "FM controls follow the radio firmware menu through the virtual keypad."); });
            AddHot(tab, new Rectangle(565,157,87,39), delegate { ShowProtocolNotice("SETTINGS", "Radio settings are operated through the live firmware UI. Undocumented EEPROM writes are not performed."); });
            AddHot(tab, new Rectangle(658,157,87,39), delegate { ShowProtocolNotice("TOOLS", "Calibration, boot-logo, firmware and diagnostics actions are available from the QUICK ACTIONS column on this faceplate."); });
            AddHot(tab, new Rectangle(752,157,121,39), delegate { ChooseFirmware(); });
            AddHot(tab, new Rectangle(879,157,92,39), delegate { MessageBox.Show(this, "UV Console by WRCX 212\r\nReference-faceplate edition\r\nViewer transport: 38400 baud, DTR ON", "About", MessageBoxButtons.OK, MessageBoxIcon.Information); });

            // Window controls and drag surface match the artwork's own chrome.
            AddHot(tab, new Rectangle(1420,10,34,34), delegate { WindowState = FormWindowState.Minimized; });
            AddHot(tab, new Rectangle(1457,10,34,34), delegate { WindowState = (WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized; });
            AddHot(tab, new Rectangle(1494,10,34,34), delegate { Close(); });
            FaceplateHotspot drag = new FaceplateHotspot { Location = new Point(110,0), Size = new Size(1280,145), Cursor = Cursors.SizeAll };
            drag.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0); } };
            tab.Controls.Add(drag);

            // Clear-log hotspot.
            AddHot(tab, new Rectangle(205,941,115,26), delegate { dashboardLog.Clear(); });

            return tab;
        }

        private Image LoadFaceplateImage()
        {
            try
            {
                Assembly a = Assembly.GetExecutingAssembly();
                using (Stream st = a.GetManifestResourceStream("UVConsole.ConsoleFaceplate.png"))
                {
                    if (st != null) { using (Bitmap temp = new Bitmap(st)) return new Bitmap(temp); }
                }
            }
            catch { }
            return new Bitmap(1536,1024);
        }

        private FaceplateHotspot AddHot(Control host, Rectangle bounds, EventHandler click)
        {
            FaceplateHotspot h = new FaceplateHotspot { Location = bounds.Location, Size = bounds.Size, ShowHover = false };
            if (click != null) h.Click += click;
            host.Controls.Add(h); h.BringToFront(); return h;
        }

        private FaceplateHotspot AddFaceKey(Control host, Rectangle bounds, byte code)
        {
            FaceplateHotspot h = AddHot(host, bounds, null);
            h.Tag = code; radioKeyButtons.Add(new Button { Tag = code, Visible = false });
            bool down = false; bool longSent = false;
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer(); timer.Interval = 500;
            timer.Tick += delegate { timer.Stop(); if (down && session.IsOpen) { longSent = true; SendKey(code,true); } };
            h.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button != MouseButtons.Left || !session.IsOpen) return; down=true; longSent=false; timer.Start(); };
            h.MouseUp += delegate(object sender, MouseEventArgs e) { if (e.Button != MouseButtons.Left || !down) return; timer.Stop(); down=false; if (!longSent) SendKey(code,false); longSent=false; };
            h.MouseLeave += delegate { if (down) { timer.Stop(); down=false; longSent=false; } };
            return h;
        }

        private void ShowProtocolNotice(string title, string text)
        {
            MessageBox.Show(this, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuildKeypad(Panel host)
        {
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            host.Controls.Add(layout);

            Label heading = SectionLabel("VIRTUAL FRONT PANEL — ALL SERIAL KEYS"); heading.Dock = DockStyle.Fill;
            layout.Controls.Add(heading, 0, 0);

            FlowLayoutPanel modelBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0,2,0,0) };
            modelK1Button = TechButton("UV-K1", 82); modelK5Button = TechButton("UV-K5 V3", 92);
            modelK1Button.Click += delegate { k1Model = true; ApplyModel(); };
            modelK5Button.Click += delegate { k1Model = false; ApplyModel(); };
            modelBar.Controls.Add(modelK1Button); modelBar.Controls.Add(modelK5Button);
            Label hint = new Label { AutoSize = true, Text = "Hold 0.5 s = LONG", ForeColor = TextDim, Font = new Font("Segoe UI", 7.7f, FontStyle.Bold), Margin = new Padding(10,10,0,0) };
            modelBar.Controls.Add(hint);
            layout.Controls.Add(modelBar, 0, 1);

            TableLayoutPanel pad = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5, Padding = new Padding(3,8,3,3) };
            for (int i = 0; i < 4; i++) pad.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            for (int i = 0; i < 5; i++) pad.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

            Button ptt = TechButton("PTT\r\nN/A", 70); ptt.Dock = DockStyle.Fill; ptt.Margin = new Padding(5); ptt.Enabled = false; ptt.Tag = "PTT_UNAVAILABLE";
            pad.Controls.Add(ptt, 0, 0);
            AddKey(pad, "SIDE 1\r\nF1", 0x12, 1, 0);
            AddKey(pad, "SIDE 2\r\nF2", 0x11, 2, 0);
            Button reboot = TechButton("REBOOT", 70); reboot.Dock = DockStyle.Fill; reboot.Margin = new Padding(5); reboot.Click += delegate { if (session.IsOpen) { try { session.SendViewerReboot(); AppendMaintenanceLog("Viewer reboot command sent."); } catch (Exception ex) { ShowError(ex); } } };
            pad.Controls.Add(reboot, 3, 0);

            AddKey(pad, "MENU", 0x0A, 0, 1);
            upButton = AddKey(pad, "◀", 0x0B, 1, 1);
            downButton = AddKey(pad, "▶", 0x0C, 2, 1);
            AddKey(pad, "EXIT", 0x0D, 3, 1);

            AddKey(pad, "1", 0x01, 0, 2); AddKey(pad, "2", 0x02, 1, 2); AddKey(pad, "3", 0x03, 2, 2); AddKey(pad, "F / #", 0x0F, 3, 2);
            AddKey(pad, "4", 0x04, 0, 3); AddKey(pad, "5", 0x05, 1, 3); AddKey(pad, "6", 0x06, 2, 3); AddKey(pad, "*", 0x0E, 3, 3);
            AddKey(pad, "7", 0x07, 0, 4); AddKey(pad, "8", 0x08, 1, 4); AddKey(pad, "9", 0x09, 2, 4); AddKey(pad, "0", 0x00, 3, 4);
            layout.Controls.Add(pad, 0, 2);
            ApplyModel();
        }

        private Button AddKey(TableLayoutPanel pad, string text, byte code, int col, int row)
        {
            Button b = TechButton(text, 70); b.Dock = DockStyle.Fill; b.Margin = new Padding(5); b.Tag = code; b.Enabled = false;
            radioKeyButtons.Add(b);
            bool down = false;
            bool longSent = false;
            Timer longTimer = new Timer(); longTimer.Interval = 500;
            longTimer.Tick += delegate
            {
                longTimer.Stop();
                if (!down || longSent) return;
                longSent = true;
                b.BackColor = Color.FromArgb(85,77,28);
                SendKey(code, true);
            };
            b.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || !b.Enabled) return;
                down = true; longSent = false; b.Capture = true; b.BackColor = Color.FromArgb(39,108,121); longTimer.Start();
            };
            b.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left || !down) return;
                longTimer.Stop(); down = false; b.Capture = false; b.BackColor = PanelBg2;
                if (!longSent) SendKey(code, false);
                longSent = false;
            };
            b.MouseCaptureChanged += delegate
            {
                if (!down || b.Capture) return;
                longTimer.Stop(); down = false; longSent = false; b.BackColor = PanelBg2;
            };
            b.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if ((e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space) || down || !b.Enabled) return;
                e.SuppressKeyPress = true; down = true; longSent = false; b.BackColor = Color.FromArgb(39,108,121); longTimer.Start();
            };
            b.KeyUp += delegate(object sender, KeyEventArgs e)
            {
                if ((e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space) || !down) return;
                e.SuppressKeyPress = true; longTimer.Stop(); down = false; b.BackColor = PanelBg2;
                if (!longSent) SendKey(code, false);
                longSent = false;
            };
            pad.Controls.Add(b, col, row); return b;
        }

        private TabPage BuildRfTab()
        {
            TabPage tab=MakeTab("RF LOG"); Panel root=Card(); root.Dock=DockStyle.Fill; root.Padding=new Padding(18); tab.Controls.Add(root);
            FlowLayoutPanel top=new FlowLayoutPanel { Dock=DockStyle.Top,Height=45,FlowDirection=FlowDirection.LeftToRight,WrapContents=false };
            top.Controls.Add(SectionLabel("RADIO RF ACTIVITY LOG"));
            Button export=TechButton("EXPORT CSV",105); export.Click += delegate { ExportRfCsv(); };
            Button clear=TechButton("CLEAR VIEW",100); clear.Click += delegate { rfRows.Clear();rfLive=null;RefreshRfGrid(); };
            top.Controls.Add(export);top.Controls.Add(clear); root.Controls.Add(top);
            rfSummary=new Label { Dock=DockStyle.Top,Height=32,ForeColor=Accent,Text="No RF-log data yet. Compatible Fusion firmware will populate this automatically.",Padding=new Padding(3,7,0,0) };root.Controls.Add(rfSummary);
            rfGrid=new DataGridView { Dock=DockStyle.Fill,BackgroundColor=Color.FromArgb(8,12,14),BorderStyle=BorderStyle.None,GridColor=Color.FromArgb(40,52,57),ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,EnableHeadersVisualStyles=false };
            rfGrid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(25,34,39);rfGrid.ColumnHeadersDefaultCellStyle.ForeColor=Accent;rfGrid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI Semibold",8.5f);rfGrid.DefaultCellStyle.BackColor=Color.FromArgb(13,18,21);rfGrid.DefaultCellStyle.ForeColor=TextMain;rfGrid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(27,76,86);rfGrid.DefaultCellStyle.SelectionForeColor=Color.White;
            rfGrid.Columns.Add("event","EVENT");rfGrid.Columns.Add("frequency","FREQUENCY MHz");rfGrid.Columns.Add("channel","CHANNEL");rfGrid.Columns.Add("duration","DURATION");rfGrid.Columns.Add("meter","SIGNAL / POWER");rfGrid.Columns.Add("battery","BATTERY");rfGrid.Columns.Add("sequence","SEQ");
            root.Controls.Add(rfGrid);rfGrid.BringToFront();top.BringToFront();rfSummary.BringToFront(); return tab;
        }

        private TabPage BuildMaintenanceTab()
        {
            TabPage tab=MakeTab("MAINTENANCE");
            TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(16),BackColor=Bg};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));tab.Controls.Add(root);
            Panel controls=Card();controls.Dock=DockStyle.Fill;controls.Padding=new Padding(14);Panel logPanel=Card();logPanel.Dock=DockStyle.Fill;logPanel.Padding=new Padding(14);root.Controls.Add(controls,0,0);root.Controls.Add(logPanel,1,0);
            TabControl mt=new TabControl{Dock=DockStyle.Fill,Appearance=TabAppearance.FlatButtons,ItemSize=new Size(125,34),SizeMode=TabSizeMode.Fixed,DrawMode=TabDrawMode.OwnerDrawFixed};mt.DrawItem+=DrawTab;controls.Controls.Add(mt);
            mt.TabPages.Add(BuildFlashPage());mt.TabPages.Add(BuildCalibrationPage());mt.TabPages.Add(BuildLogoPage());
            Label l=SectionLabel("OPERATION LOG");l.Dock=DockStyle.Top;l.Height=36;logPanel.Controls.Add(l);
            maintenanceProgress=new ProgressBar{Dock=DockStyle.Bottom,Height=18,Style=ProgressBarStyle.Continuous};logPanel.Controls.Add(maintenanceProgress);
            maintenanceLog=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.FromArgb(6,10,12),ForeColor=Color.FromArgb(152,231,200),BorderStyle=BorderStyle.FixedSingle,Font=new Font("Consolas",9f),Text="UV Console by WRCX 212 maintenance console ready.\r\n"};logPanel.Controls.Add(maintenanceLog);maintenanceLog.BringToFront();l.BringToFront();
            return tab;
        }

        private TabPage BuildFlashPage()
        {
            TabPage p=MakeTab("FLASH"); p.Padding=new Padding(16);
            Label h=SectionLabel("FIRMWARE FLASH / DFU");h.Dock=DockStyle.Top;h.Height=34;p.Controls.Add(h);
            Label info=InfoLabel("Flash mode requires the radio's DFU bootloader. Power OFF → hold PTT → power ON → release PTT → reconnect the cable. The native flasher validates bootloader version ≥ 7.00.07 and retries failed pages.");info.Dock=DockStyle.Top;info.Height=84;p.Controls.Add(info);
            firmwareLabel=FileLabel("No firmware image selected");firmwareLabel.Dock=DockStyle.Top;p.Controls.Add(firmwareLabel);
            Button choose=TechButton("CHOOSE .BIN",130);choose.Dock=DockStyle.Top;choose.Height=38;choose.Click+=delegate{ChooseFirmware();};p.Controls.Add(choose);
            Button flash=ActionButton("FLASH FIRMWARE");flash.Dock=DockStyle.Top;flash.Height=52;flash.Click+=async delegate{await FlashFirmwareAsync();};p.Controls.Add(flash);
            return p;
        }

        private TabPage BuildCalibrationPage()
        {
            TabPage p=MakeTab("CALIBRATION");p.Padding=new Padding(16);
            Label h=SectionLabel("CALIBRATION BACKUP / RESTORE");h.Dock=DockStyle.Top;h.Height=34;p.Controls.Add(h);
            Label info=InfoLabel("Normal radio mode. Backup reads the 512-byte calibration region. Firmware v5+ uses 0xB000; earlier versions use 0x1E00. Restore is write-critical—keep power and cable stable.");info.Dock=DockStyle.Top;info.Height=78;p.Controls.Add(info);
            calibrationLabel=FileLabel("No calibration.dat selected");calibrationLabel.Dock=DockStyle.Top;p.Controls.Add(calibrationLabel);
            Button choose=TechButton("LOAD CALIBRATION.DAT",180);choose.Dock=DockStyle.Top;choose.Height=38;choose.Click+=delegate{ChooseCalibration();};p.Controls.Add(choose);
            Button dump=ActionButton("BACK UP FROM RADIO");dump.Dock=DockStyle.Top;dump.Height=48;dump.Click+=async delegate{await DumpCalibrationAsync();};p.Controls.Add(dump);
            Button restore=ActionButton("RESTORE TO RADIO");restore.Dock=DockStyle.Top;restore.Height=48;restore.Click+=async delegate{await RestoreCalibrationAsync();};p.Controls.Add(restore);
            return p;
        }

        private TabPage BuildLogoPage()
        {
            TabPage p=MakeTab("BOOT LOGO");p.Padding=new Padding(16);
            Label h=SectionLabel("128 × 64 BOOT LOGO LAB");h.Dock=DockStyle.Top;h.Height=34;p.Controls.Add(h);
            logoLabel=FileLabel("No source image selected");logoLabel.Dock=DockStyle.Top;p.Controls.Add(logoLabel);
            Button choose=TechButton("CHOOSE IMAGE",140);choose.Dock=DockStyle.Top;choose.Height=36;choose.Click+=delegate{ChooseLogoImage();};p.Controls.Add(choose);
            FlowLayoutPanel settings=new FlowLayoutPanel{Dock=DockStyle.Top,Height=55,WrapContents=false,Padding=new Padding(0,8,0,0)};
            settings.Controls.Add(SmallLabel("THRESHOLD"));logoThreshold=new TrackBar{Minimum=0,Maximum=255,Value=128,TickFrequency=32,Width=180,Height=40};logoThreshold.ValueChanged+=delegate{UpdateLogoPreview();};settings.Controls.Add(logoThreshold);logoInvert=new CheckBox{Text="INVERT",ForeColor=TextMain,AutoSize=true,Margin=new Padding(16,8,0,0)};logoInvert.CheckedChanged+=delegate{UpdateLogoPreview();};settings.Controls.Add(logoInvert);p.Controls.Add(settings);
            logoPreview=new PictureBox{Dock=DockStyle.Top,Height=150,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle};p.Controls.Add(logoPreview);
            Button upload=ActionButton("UPLOAD BOOT LOGO");upload.Dock=DockStyle.Top;upload.Height=44;upload.Click+=async delegate{await UploadLogoAsync();};p.Controls.Add(upload);
            Button dump=TechButton("DUMP LOGO FROM RADIO",190);dump.Dock=DockStyle.Top;dump.Height=40;dump.Click+=async delegate{await DumpLogoAsync();};p.Controls.Add(dump);
            return p;
        }

        private TabPage BuildAboutTab()
        {
            TabPage p=MakeTab("ABOUT");p.Padding=new Padding(24);Panel card=Card();card.Dock=DockStyle.Fill;card.Padding=new Padding(24);p.Controls.Add(card);
            Label title=new Label{Dock=DockStyle.Top,Height=58,Text="UV Console by WRCX 212",ForeColor=TextMain,Font=new Font("Segoe UI Semibold",18f)};card.Controls.Add(title);
            TextBox text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=PanelBg,ForeColor=TextDim,Font=new Font("Segoe UI",10f),Text="Native Windows derivative / interoperability implementation for compatible Quansheng UV-K1 and UV-K5 V3 Fusion firmware.\r\n\r\nFeatures\r\n• Native COM-port viewer at 38,400 baud\r\n• Exact 128×64 framebuffer plus Smooth LCD supersampled renderer\r\n• Virtual UV-K1 / UV-K5 keypad with short and long presses\r\n• Real radio red/green LED flags and deep-sleep state\r\n• RF activity log and CSV export\r\n• Firmware DFU flashing\r\n• Calibration backup and restore\r\n• Boot-logo upload and dump\r\n• No browser and no local web server\r\n\r\nImportant\r\nThe Smooth LCD renderer beautifies the radio's existing 1-bit framebuffer. It cannot invent character metadata that the radio does not transmit; Raw LCD remains available for exact diagnostics. Remote PTT is not implemented because the current viewer protocol does not expose a PTT command.\r\n\r\nAttribution\r\nProtocol behavior was independently translated from the Apache-2.0 licensed UV Studio source by Armel FAUVEAU. Original NOTICE and LICENSE are included with this package.\r\n\r\nThis software is provided for engineering and interoperability work. Firmware flashing and calibration writes can render a radio unusable if interrupted or used with incompatible firmware."};card.Controls.Add(text);text.BringToFront();title.BringToFront();return p;
        }

        private void StartViewerWatchdog()
        {
            viewerWatchdog = new System.Windows.Forms.Timer();
            viewerWatchdog.Interval = 500;
            viewerWatchdog.Tick += delegate
            {
                if (screenStatusLabel == null || screenStatsLabel == null) return;
                long bytes = session.TotalBytesReceived;
                long frames = session.TotalViewerFrames;
                screenStatsLabel.Text = bytes.ToString("N0") + "\r\n" + frames.ToString("N0") + "\r\nON";
                if (!session.IsOpen)
                {
                    screenStatusLabel.Text = "Disconnected";
                    screenStatusLabel.ForeColor = TextDim;
                    return;
                }
                DateTime last = session.LastViewerFrameUtc;
                double age = last == DateTime.MinValue ? Double.MaxValue : (DateTime.UtcNow - last).TotalSeconds;
                if (frames > 0 && age < 2.0)
                {
                    screenStatusLabel.Text = "Live Display Active";
                    screenStatusLabel.ForeColor = Color.FromArgb(80,255,154);
                }
                else if (bytes > 0 && frames == 0)
                {
                    screenStatusLabel.Text = "Serial Data / No Screen";
                    screenStatusLabel.ForeColor = Color.FromArgb(255,188,78);
                }
                else if (frames > 0)
                {
                    screenStatusLabel.Text = "Stream Paused / Resyncing";
                    screenStatusLabel.ForeColor = Color.FromArgb(255,188,78);
                    if ((DateTime.UtcNow - lastAutoSyncUtc).TotalSeconds >= 2.0)
                    {
                        lastAutoSyncUtc = DateTime.UtcNow;
                        try { session.ForceViewerSync(); } catch { }
                    }
                }
                else
                {
                    screenStatusLabel.Text = "Waiting for Radio Data";
                    screenStatusLabel.ForeColor = TextDim;
                }
            };
            viewerWatchdog.Start();
        }

        private void ForceScreenSync()
        {
            if (!session.IsOpen) return;
            try
            {
                session.ForceViewerSync();
                screenStatusLabel.Text = "Sync Request Sent";
                screenStatusLabel.ForeColor = Accent;
                AppendMaintenanceLog("Viewer screen sync requested.");
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void WireSession()
        {
            session.ViewerFrameReceived += HandleViewerFrame;
            session.BytesReceived += delegate { if (scope != null) scope.Activity = 0.65f; };
            session.Faulted += delegate(string s){ BeginUi(delegate{session.Close(); SetConnection(false,"SERIAL ERROR"); AppendMaintenanceLog("Serial error: "+s);}); };
        }

        private void HandleViewerFrame(ViewerFrame frame)
        {
            if(frame==null)return;
            scope.Activity=1f;
            if(frame.Type==ViewerProtocol.TypeScreenshot || frame.Type==ViewerProtocol.TypeDiff)
            {
                lock(framebuffer) ViewerProtocol.ApplyDisplayFrame(framebuffer,frame);
                byte[] copy=new byte[framebuffer.Length];lock(framebuffer)Buffer.BlockCopy(framebuffer,0,copy,0,copy.Length);
                if(!displayFrozen) display.SetFrame(copy);
                frameCounter++;
                bool red=(frame.Flags&ViewerProtocol.FlagLedRed)!=0;bool green=(frame.Flags&ViewerProtocol.FlagLedGreen)!=0;bool sleep=(frame.Flags&ViewerProtocol.FlagDeepSleep)!=0;
                BeginUi(delegate{redLed.IsOn=red;greenLed.IsOn=green;deepSleepLabel.Text=sleep?"DEEP SLEEP":"AWAKE";deepSleepLabel.ForeColor=sleep?Color.FromArgb(245,183,70):Accent;radioStateLabel.Text=red?"RADIO TX/RED":green?"RADIO RX/GREEN":"RADIO ACTIVE";UpdateFps();});
            }
            else if(frame.Type==ViewerProtocol.TypeRfLog)
            {
                byte status;RfLogRow live;List<RfLogRow> rows;
                if(RfLogProtocol.TryParseMain(frame.Payload,out status,out live,out rows))
                {
                    lock(rfRows){rfStatus=status;rfLive=live;foreach(RfLogRow r in rows)rfRows[r.Sequence]=r;TrimRfRows();}
                    BeginUi(RefreshRfGrid);
                }
            }
            else if(frame.Type==ViewerProtocol.TypeRfLogHistory)
            {
                List<RfLogRow> rows=RfLogProtocol.ParseHistory(frame.Payload);lock(rfRows){foreach(RfLogRow r in rows)rfRows[r.Sequence]=r;TrimRfRows();}BeginUi(RefreshRfGrid);
            }
        }

        private void UpdateFps()
        {
            double sec=(DateTime.UtcNow-fpsStart).TotalSeconds;if(sec<1)return;double fps=frameCounter/sec;fpsLabel.Text=fps.ToString("0.0")+" FPS";frameCounter=0;fpsStart=DateTime.UtcNow;
        }
        private void TrimRfRows(){if(rfRows.Count<=540)return;foreach(uint k in rfRows.Keys.OrderByDescending(x=>x).Skip(540).ToList())rfRows.Remove(k);}

        private void RefreshRfGrid()
        {
            if(rfGrid==null)return;List<RfLogRow> list;RfLogRow live;byte status;lock(rfRows){list=rfRows.Values.OrderByDescending(x=>x.Sequence).Take(512).ToList();live=rfLive;status=rfStatus;}
            rfGrid.SuspendLayout();rfGrid.Rows.Clear();if(live!=null&&!live.IsSession)AddRfRow(live,true);foreach(RfLogRow r in list)AddRfRow(r,false);rfGrid.ResumeLayout();
            bool active=(status&1)!=0;bool clearing=(status&4)!=0;bool disabled=(status&8)!=0;
            int rx=list.Count(x=>!x.IsTx&&!x.IsSession),tx=list.Count(x=>x.IsTx&&!x.IsSession);
            rfSummary.Text=disabled?"RF LOG DISABLED BY FIRMWARE":clearing?"RF LOG CLEARING…":(active?"RF LOG LIVE  •  ":"RF LOG IDLE  •  ")+rx+" RX  /  "+tx+" TX  •  "+list.Count+" stored events";
        }

        private void AddRfRow(RfLogRow r,bool live)
        {
            if(r.IsSession){int idx=rfGrid.Rows.Add("POWER ON","—","—","—","—","—",r.Sequence);rfGrid.Rows[idx].DefaultCellStyle.ForeColor=Color.FromArgb(187,155,96);return;}
            string ch=r.Channel==RfLogProtocol.ChannelNone?"—":(!String.IsNullOrWhiteSpace(r.ChannelName)?r.ChannelName:"M"+(r.Channel+1).ToString("000"));
            int row=rfGrid.Rows.Add(r.IsTx?"TX":"RX",RfLogProtocol.FormatFrequency(r.Frequency10Hz),ch,FormatDuration(r.DurationSeconds),RfLogProtocol.FormatMeter(r),RfLogProtocol.FormatBattery(r.Battery),r.Sequence);
            if(live)rfGrid.Rows[row].DefaultCellStyle.BackColor=Color.FromArgb(29,70,79);else if(r.IsTx)rfGrid.Rows[row].DefaultCellStyle.ForeColor=Color.FromArgb(255,165,140);
        }

        private void MainKeyDown(object sender, KeyEventArgs e)
        {
            if (mainTabs == null || mainTabs.SelectedIndex != 0) return;
            if (e.Control || e.Alt) return;
            Button focusedRadioButton = ActiveControl as Button;
            if (focusedRadioButton != null && focusedRadioButton.Tag is byte && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)) return;
            byte? code = null;
            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9) code = (byte)(e.KeyCode - Keys.D0);
            else if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9) code = (byte)(e.KeyCode - Keys.NumPad0);
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.M) code = 0x0A;
            else if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Back) code = 0x0D;
            else if (e.KeyCode == Keys.F1) code = 0x12;
            else if (e.KeyCode == Keys.F2) code = 0x11;
            else if (e.KeyCode == Keys.F) code = 0x0F;
            else if (e.KeyCode == Keys.Multiply) code = 0x0E;
            else if (k1Model && e.KeyCode == Keys.Left) code = 0x0B;
            else if (k1Model && e.KeyCode == Keys.Right) code = 0x0C;
            else if (!k1Model && e.KeyCode == Keys.Up) code = 0x0B;
            else if (!k1Model && e.KeyCode == Keys.Down) code = 0x0C;
            else if (e.KeyCode == Keys.Space) { SaveScreenshot(); e.SuppressKeyPress=true; return; }
            if (code.HasValue) { SendKey(code.Value, e.Shift); e.SuppressKeyPress=true; }
        }

        private void ToggleViewerConnection()
        {
            if (session.IsOpen) { session.Close(); SetConnection(false, "OFFLINE"); return; }
            string port = SelectedPort(); if (port == null) return;
            try
            {
                byte[] blank = new byte[ViewerProtocol.FrameSize];
                lock (framebuffer) Buffer.BlockCopy(blank, 0, framebuffer, 0, blank.Length);
                display.SetFrame(blank);
                frameCounter = 0; fpsStart = DateTime.UtcNow;
                session.Open(port, true);
                lastAutoSyncUtc = DateTime.MinValue;
                SetConnection(true, "VIEWER ONLINE");
                session.ForceViewerSync();
                AppendMaintenanceLog("Viewer connected to " + port + " @ 38400 baud. Screen synchronization started.");
            }
            catch (Exception ex) { ShowError(ex); SetConnection(false, "OFFLINE"); }
        }
        private void SetConnection(bool on, string text)
        {
            connectionLed.IsOn = on; connectionLabel.Text = text; connectionLabel.ForeColor = on ? Color.FromArgb(90,255,161) : TextDim; connectButton.Text = on ? "DISCONNECT" : "CONNECT";
            if (syncScreenButton != null) syncScreenButton.Enabled = on;
            foreach (Button key in radioKeyButtons) key.Enabled = on;
            if (!on)
            {
                if (redLed != null) redLed.IsOn = false; if (greenLed != null) greenLed.IsOn = false;
                if (radioStateLabel != null) radioStateLabel.Text = "WAITING FOR RADIO";
                if (fpsLabel != null) fpsLabel.Text = "0 FPS";
                if (screenStatusLabel != null) { screenStatusLabel.Text = "Disconnected"; screenStatusLabel.ForeColor = TextDim; }
            }
        }
        private void SendKey(byte code,bool longPress){if(!session.IsOpen){System.Media.SystemSounds.Beep.Play();return;}try{session.SendKey(code,longPress);scope.Activity=1f;}catch(Exception ex){ShowError(ex);}}

        private async Task FlashFirmwareAsync()
        {
            if(firmwareData==null){MessageBox.Show(this,"Choose a firmware .BIN first.","Firmware",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            if(MessageBox.Show(this,"Firmware flashing is write-critical. Confirm that this image is intended for your exact UV-K1 / UV-K5 V3 hardware and that the radio is in DFU mode.\n\nContinue?","Confirm firmware flash",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            await RunMaintenance("Firmware flash",delegate(MaintenanceService m){m.FlashFirmware(firmwareData);});
        }
        private async Task DumpCalibrationAsync(){await RunMaintenance("Calibration backup",delegate(MaintenanceService m){byte[] data=m.DumpCalibration();BeginUi(delegate{using(SaveFileDialog d=new SaveFileDialog{Filter="Calibration data (*.dat)|*.dat|All files (*.*)|*.*",FileName="calibration.dat"})if(d.ShowDialog(this)==DialogResult.OK){File.WriteAllBytes(d.FileName,data);AppendMaintenanceLog("Saved: "+d.FileName);}});});}
        private async Task RestoreCalibrationAsync()
        {
            if(calibrationData==null){MessageBox.Show(this,"Load a 512-byte calibration.dat first.","Calibration",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            if(MessageBox.Show(this,"This writes radio calibration data. A wrong calibration file can cause poor RF performance. Keep the cable and power stable.\n\nRestore now?","Confirm calibration restore",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            await RunMaintenance("Calibration restore",delegate(MaintenanceService m){m.RestoreCalibration(calibrationData);});
        }
        private async Task UploadLogoAsync(){if(logoBitmap==null){MessageBox.Show(this,"Choose an image first.","Boot logo",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}await RunMaintenance("Boot logo upload",delegate(MaintenanceService m){m.UploadLogo(logoBitmap);});}
        private async Task DumpLogoAsync(){await RunMaintenance("Boot logo dump",delegate(MaintenanceService m){byte[] data=m.DumpLogo();byte[] bmp=new byte[MaintenanceProtocol.LogoBitmapSize];Buffer.BlockCopy(data,MaintenanceProtocol.LogoHeaderSize,bmp,0,bmp.Length);BeginUi(delegate{using(Bitmap image=LogoBitmapToImage(bmp,8))using(SaveFileDialog d=new SaveFileDialog{Filter="PNG image (*.png)|*.png",FileName="logo.png"})if(d.ShowDialog(this)==DialogResult.OK){image.Save(d.FileName,System.Drawing.Imaging.ImageFormat.Png);AppendMaintenanceLog("Saved: "+d.FileName);}});});}

        private async Task RunMaintenance(string name,Action<MaintenanceService> action)
        {
            if(operationBusy)return;string port=SelectedPort();if(port==null)return;
            operationBusy=true;maintenanceProgress.Value=0;AppendMaintenanceLog("\r\n== "+name+" ==");
            session.Close();SetConnection(false,"MAINTENANCE");
            try
            {
                session.Open(port,false);AppendMaintenanceLog("Maintenance link: "+port+" @ 38400 baud.");
                MaintenanceService svc=new MaintenanceService(session,AppendMaintenanceLog,SetProgress);
                await Task.Run(delegate{action(svc);});
                AppendMaintenanceLog(name+" completed.");
            }
            catch(Exception ex){AppendMaintenanceLog("ERROR: "+ex.Message);BeginUi(delegate{MessageBox.Show(this,ex.Message,name+" failed",MessageBoxButtons.OK,MessageBoxIcon.Error);});}
            finally{session.Close();operationBusy=false;BeginUi(delegate{SetConnection(false,"OFFLINE");});}
        }

        private void ChooseFirmware(){using(OpenFileDialog d=new OpenFileDialog{Filter="Firmware binary (*.bin)|*.bin|All files (*.*)|*.*"})if(d.ShowDialog(this)==DialogResult.OK){firmwareData=File.ReadAllBytes(d.FileName);firmwarePath=d.FileName;firmwareLabel.Text=Path.GetFileName(d.FileName)+"  •  "+firmwareData.Length.ToString("N0")+" bytes";AppendMaintenanceLog("Firmware loaded: "+d.FileName);}}
        private void ChooseCalibration(){using(OpenFileDialog d=new OpenFileDialog{Filter="Calibration data (*.dat)|*.dat|All files (*.*)|*.*"})if(d.ShowDialog(this)==DialogResult.OK){byte[] b=File.ReadAllBytes(d.FileName);if(b.Length!=512){MessageBox.Show(this,"Calibration file must be exactly 512 bytes.","Invalid calibration file",MessageBoxButtons.OK,MessageBoxIcon.Error);return;}calibrationData=b;calibrationLabel.Text=Path.GetFileName(d.FileName)+"  •  512 bytes";AppendMaintenanceLog("Calibration loaded: "+d.FileName);}}
        private void ChooseLogoImage(){using(OpenFileDialog d=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*"})if(d.ShowDialog(this)==DialogResult.OK){if(logoSource!=null)logoSource.Dispose();using(Image i=Image.FromFile(d.FileName))logoSource=new Bitmap(i);logoLabel.Text=Path.GetFileName(d.FileName);UpdateLogoPreview();}}
        private void UpdateLogoPreview(){if(logoSource==null||logoPreview==null)return;logoBitmap=ImageToLogoBitmap(logoSource,logoThreshold.Value,logoInvert.Checked);Bitmap p=LogoBitmapToImage(logoBitmap,4);Image old=logoPreview.Image;logoPreview.Image=p;if(old!=null)old.Dispose();}

        private static byte[] ImageToLogoBitmap(Bitmap source,int threshold,bool invert)
        {
            using(Bitmap fit=new Bitmap(128,64))using(Graphics g=Graphics.FromImage(fit))
            {
                g.Clear(Color.White);g.InterpolationMode=InterpolationMode.HighQualityBicubic;float sr=source.Width/(float)source.Height;float dr=2f;int w,h,x,y;if(sr>dr){w=128;h=(int)Math.Round(128/sr);x=0;y=(64-h)/2;}else{h=64;w=(int)Math.Round(64*sr);y=0;x=(128-w)/2;}g.DrawImage(source,new Rectangle(x,y,w,h));
                byte[] bits=new byte[1024];for(int page=0;page<8;page++)for(int px=0;px<128;px++){byte v=0;for(int bit=0;bit<8;bit++){int py=page*8+bit;Color c=fit.GetPixel(px,py);double lum=.299*c.R+.587*c.G+.114*c.B;bool on=lum<threshold;if(invert)on=!on;if(on)v|=(byte)(1<<bit);}bits[page*128+px]=v;}return bits;
            }
        }
        private static Bitmap LogoBitmapToImage(byte[] bits,int scale){Bitmap b=new Bitmap(128*scale,64*scale);using(Graphics g=Graphics.FromImage(b)){g.Clear(Color.White);using(Brush br=new SolidBrush(Color.Black))for(int page=0;page<8;page++)for(int x=0;x<128;x++){byte v=bits[page*128+x];for(int bit=0;bit<8;bit++)if(((v>>bit)&1)!=0)g.FillRectangle(br,x*scale,(page*8+bit)*scale,scale,scale);}}return b;}

        private void ExportRfCsv(){List<RfLogRow> rows;lock(rfRows)rows=rfRows.Values.ToList();if(rows.Count==0){MessageBox.Show(this,"There is no stored RF-log data yet.","RF Log",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}using(SaveFileDialog d=new SaveFileDialog{Filter="CSV file (*.csv)|*.csv",FileName="uvstudio-rf-log.csv"})if(d.ShowDialog(this)==DialogResult.OK)File.WriteAllText(d.FileName,RfLogProtocol.Csv(rows),new System.Text.UTF8Encoding(true));}
        private void SaveScreenshot(){using(SaveFileDialog d=new SaveFileDialog{Filter="PNG image (*.png)|*.png",FileName="uvstudio-screen-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png"})if(d.ShowDialog(this)==DialogResult.OK)using(Bitmap b=display.RenderToBitmap(1024,512,true))b.Save(d.FileName,System.Drawing.Imaging.ImageFormat.Png);}

        private void RefreshPorts(){string selected=portCombo==null?null:portCombo.SelectedItem as string;string[] ports=SerialPort.GetPortNames().OrderBy(x=>x).ToArray();portCombo.Items.Clear();portCombo.Items.AddRange(ports);if(selected!=null&&ports.Contains(selected))portCombo.SelectedItem=selected;else if(portCombo.Items.Count>0)portCombo.SelectedIndex=0;connectionLabel.Text=ports.Length==0?"NO COM PORT":"OFFLINE";}
        private string SelectedPort(){if(portCombo.SelectedItem==null){MessageBox.Show(this,"No COM port is selected. Connect the radio cable, click REFRESH, and select its COM port.","COM port",MessageBoxButtons.OK,MessageBoxIcon.Information);return null;}return portCombo.SelectedItem.ToString();}
        private void ApplyModel()
        {
            if (modelK1Button != null) modelK1Button.BackColor = k1Model ? Color.FromArgb(28,92,105) : PanelBg2;
            if (modelK5Button != null) modelK5Button.BackColor = !k1Model ? Color.FromArgb(28,92,105) : PanelBg2;
            if (upButton != null) upButton.Text = k1Model ? "◀" : "▲";
            if (downButton != null) downButton.Text = k1Model ? "▶" : "▼";
        }
        private void AppendMaintenanceLog(string s){BeginUi(delegate{string line="["+DateTime.Now.ToString("HH:mm:ss")+"] "+s+"\r\n";if(maintenanceLog!=null){maintenanceLog.AppendText(line);maintenanceLog.SelectionStart=maintenanceLog.TextLength;maintenanceLog.ScrollToCaret();}if(dashboardLog!=null){dashboardLog.AppendText(line);dashboardLog.SelectionStart=dashboardLog.TextLength;dashboardLog.ScrollToCaret();}});}
        private void SetProgress(int p){BeginUi(delegate{if(maintenanceProgress!=null)maintenanceProgress.Value=Math.Max(0,Math.Min(100,p));});}
        private void BeginUi(MethodInvoker action){try{if(IsDisposed)return;if(InvokeRequired)BeginInvoke(action);else action();}catch{}}
        private void ShowError(Exception ex){MessageBox.Show(this,ex.Message,"UV Console by WRCX 212",MessageBoxButtons.OK,MessageBoxIcon.Error);}

        private TabPage MakeTab(string name){return new TabPage(name){BackColor=Bg,ForeColor=TextMain};}
        private Panel Card(){Panel p=new Panel{BackColor=PanelBg,Margin=new Padding(7)};p.Paint+=delegate(object s,PaintEventArgs e){using(Pen pen=new Pen(Edge))e.Graphics.DrawRectangle(pen,0,0,p.Width-1,p.Height-1);};return p;}
        private Label SectionLabel(string t){return new Label{Text=t,AutoSize=false,Width=220,Height=32,ForeColor=Accent,Font=new Font("Segoe UI Semibold",9.4f),TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0,0,12,0)};}
        private Label SmallLabel(string t){return new Label{Text=t,AutoSize=true,ForeColor=TextDim,Font=new Font("Segoe UI",7.8f,FontStyle.Bold)};}
        private Label MakeLampLabel(string t,int x,int y){return new Label{Text=t,AutoSize=true,ForeColor=TextDim,Font=new Font("Segoe UI",7.5f,FontStyle.Bold),Location=new Point(x,y)};}
        private Label InfoLabel(string t){return new Label{Text=t,ForeColor=TextDim,BackColor=Color.FromArgb(14,19,22),Padding=new Padding(10),Font=new Font("Segoe UI",8.7f)};}
        private Label FileLabel(string t){return new Label{Text=t,ForeColor=TextMain,BackColor=Color.FromArgb(9,14,17),Height=40,Padding=new Padding(10,11,10,0),Font=new Font("Consolas",9f)};}
        private Button TechButton(string t,int width){Button b=new Button{Text=t,Width=width,Height=32,FlatStyle=FlatStyle.Flat,BackColor=PanelBg2,ForeColor=TextMain,Font=new Font("Segoe UI Semibold",8f),Cursor=Cursors.Hand,Margin=new Padding(4)};b.FlatAppearance.BorderColor=Color.FromArgb(53,82,91);b.FlatAppearance.MouseOverBackColor=Color.FromArgb(31,72,82);b.FlatAppearance.MouseDownBackColor=Color.FromArgb(43,102,114);return b;}
        private Button ActionButton(string t){Button b=TechButton(t,180);b.BackColor=Color.FromArgb(22,85,98);b.FlatAppearance.BorderColor=Accent;b.Font=new Font("Segoe UI Semibold",9f);return b;}
        private void DrawTab(object sender,DrawItemEventArgs e){TabControl t=(TabControl)sender;Rectangle r=e.Bounds;bool sel=e.Index==t.SelectedIndex;using(Brush b=new SolidBrush(sel?Color.FromArgb(24,70,80):Color.FromArgb(17,22,25)))e.Graphics.FillRectangle(b,r);using(Pen p=new Pen(sel?Accent:Color.FromArgb(44,56,62)))e.Graphics.DrawRectangle(p,r.X,r.Y,r.Width-1,r.Height-1);TextRenderer.DrawText(e.Graphics,t.TabPages[e.Index].Text,new Font("Segoe UI Semibold",8.2f),r,sel?Color.White:TextDim,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
        private static string FormatDuration(int s){return s>=3600?(s/3600)+":"+((s%3600)/60).ToString("00")+":"+(s%60).ToString("00"):(s/60).ToString("00")+":"+(s%60).ToString("00");}
    }
}
