using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UVConsole
{
    internal sealed class LedLamp : Control
    {
        private bool isOn;
        private Color lampColor = Color.LimeGreen;
        public bool IsOn { get { return isOn; } set { if (isOn != value) { isOn = value; Invalidate(); } } }
        public Color LampColor { get { return lampColor; } set { lampColor = value; Invalidate(); } }
        public LedLamp() { DoubleBuffered = true; Size = new Size(18, 18); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(2,2,Width-5,Height-5);
            using (LinearGradientBrush rim = new LinearGradientBrush(r, Color.FromArgb(90,90,90), Color.FromArgb(15,15,15), 45f)) e.Graphics.FillEllipse(rim, r);
            Rectangle inner = Rectangle.Inflate(r, -3, -3);
            Color c = IsOn ? LampColor : Color.FromArgb(45, 48, 48);
            if (IsOn)
            {
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(Rectangle.Inflate(inner, 4, 4));
                    using (PathGradientBrush glow = new PathGradientBrush(gp))
                    {
                        glow.CenterColor = Color.FromArgb(130, c);
                        glow.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                        e.Graphics.FillEllipse(glow, Rectangle.Inflate(inner, 5, 5));
                    }
                }
            }
            using (SolidBrush b = new SolidBrush(c)) e.Graphics.FillEllipse(b, inner);
            using (SolidBrush hi = new SolidBrush(Color.FromArgb(IsOn ? 190 : 45, Color.White))) e.Graphics.FillEllipse(hi, new Rectangle(inner.X+2,inner.Y+2,Math.Max(2,inner.Width/3),Math.Max(2,inner.Height/3)));
        }
    }

    internal sealed class RadioDisplayControl : Control
    {
        private readonly byte[] framebuffer = new byte[ViewerProtocol.FrameSize];
        private readonly object fbSync = new object();
        private bool smooth = true;
        private bool glow = true;
        private bool invert;
        private Color foreground = Color.FromArgb(142, 255, 188);
        private Color background = Color.FromArgb(9, 26, 20);
        private int repaintPending;

        public bool SmoothMode { get { return smooth; } set { smooth = value; Invalidate(); } }
        public bool Glow { get { return glow; } set { glow = value; Invalidate(); } }
        public bool Invert { get { return invert; } set { invert = value; Invalidate(); } }
        public Color ScreenForeground { get { return foreground; } set { foreground = value; Invalidate(); } }
        public Color ScreenBackground { get { return background; } set { background = value; Invalidate(); } }
        private bool showBezel = true;
        private bool forceFill = false;
        public bool ShowBezel { get { return showBezel; } set { showBezel = value; Invalidate(); } }
        public bool ForceFill { get { return forceFill; } set { forceFill = value; Invalidate(); } }

        public RadioDisplayControl()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            MinimumSize = new Size(400, 210);
        }

        public void SetFrame(byte[] source)
        {
            if (source == null || source.Length != framebuffer.Length) return;
            lock (fbSync) Buffer.BlockCopy(source, 0, framebuffer, 0, framebuffer.Length);

            // Coalesce repaint requests. The radio can produce dozens of frames
            // per second; queueing one BeginInvoke for every frame can starve the
            // WinForms message loop and make the LCD appear frozen or blank.
            if (!IsHandleCreated || IsDisposed) return;
            if (System.Threading.Interlocked.Exchange(ref repaintPending, 1) != 0) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    System.Threading.Interlocked.Exchange(ref repaintPending, 0);
                    if (!IsDisposed) Invalidate();
                });
            }
            catch
            {
                System.Threading.Interlocked.Exchange(ref repaintPending, 0);
            }
        }

        public byte[] Snapshot()
        {
            byte[] copy = new byte[framebuffer.Length];
            lock (fbSync) Buffer.BlockCopy(framebuffer,0,copy,0,copy.Length);
            return copy;
        }

        public Bitmap RenderToBitmap(int width, int height, bool includeBezel)
        {
            Bitmap b = new Bitmap(Math.Max(1,width), Math.Max(1,height));
            using (Graphics g = Graphics.FromImage(b)) DrawEverything(g, new Rectangle(0,0,b.Width,b.Height), includeBezel);
            return b;
        }

        protected override void OnPaint(PaintEventArgs e) { DrawEverything(e.Graphics, ClientRectangle, ShowBezel); }

        private void DrawEverything(Graphics g, Rectangle bounds, bool bezel)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.Clear(Parent == null ? Color.FromArgb(12,15,17) : Parent.BackColor);
            Rectangle outer = Rectangle.Inflate(bounds, -5, -5);
            if (outer.Width < 20 || outer.Height < 20) return;

            if (bezel)
            {
                using (GraphicsPath path = RoundRect(outer, 18))
                using (LinearGradientBrush br = new LinearGradientBrush(outer, Color.FromArgb(48,54,57), Color.FromArgb(15,18,20), 90f))
                    g.FillPath(br,path);
            }

            Rectangle screen = bezel ? Rectangle.Inflate(outer, -20, -20) : outer;
            if (!ForceFill)
            {
                float targetRatio = 2.0f;
                float ratio = screen.Width / (float)Math.Max(1, screen.Height);
                if (ratio > targetRatio)
                {
                    int w = (int)(screen.Height * targetRatio);
                    screen.X += (screen.Width - w)/2; screen.Width = w;
                }
                else
                {
                    int h = (int)(screen.Width / targetRatio);
                    screen.Y += (screen.Height - h)/2; screen.Height = h;
                }
            }

            using (GraphicsPath sp = RoundRect(screen, 10))
            using (SolidBrush bg = new SolidBrush(invert ? foreground : background)) g.FillPath(bg, sp);

            if (glow)
            {
                using (GraphicsPath gp = RoundRect(Rectangle.Inflate(screen,-2,-2), 9))
                using (Pen p = new Pen(Color.FromArgb(38, invert ? background : foreground), 3)) g.DrawPath(p, gp);
            }

            byte[] fb = Snapshot();
            if (smooth) DrawSmooth(g, screen, fb);
            else DrawRaw(g, screen, fb);

            using (LinearGradientBrush glass = new LinearGradientBrush(screen, Color.FromArgb(18,Color.White), Color.FromArgb(0,Color.White), 90f))
            {
                Rectangle top = new Rectangle(screen.X, screen.Y, screen.Width, Math.Max(1, screen.Height/3));
                g.FillRectangle(glass, top);
            }
        }

        private void DrawRaw(Graphics g, Rectangle screen, byte[] fb)
        {
            float sx = screen.Width / 128f, sy = screen.Height / 64f;
            Color on = invert ? background : foreground;
            using (SolidBrush b = new SolidBrush(on))
            {
                for (int y=0;y<64;y++) for(int x=0;x<128;x++) if (GetPixel(fb,x,y))
                {
                    int x0 = screen.X + (int)Math.Floor(x*sx);
                    int y0 = screen.Y + (int)Math.Floor(y*sy);
                    int x1 = screen.X + (int)Math.Ceiling((x+1)*sx);
                    int y1 = screen.Y + (int)Math.Ceiling((y+1)*sy);
                    g.FillRectangle(b,x0,y0,Math.Max(1,x1-x0),Math.Max(1,y1-y0));
                }
            }
        }

        private void DrawSmooth(Graphics g, Rectangle screen, byte[] fb)
        {
            // Fast LCD renderer: convert the exact 1-bit framebuffer to one
            // 128x64 ARGB bitmap in bulk, then scale once. Avoid SetPixel and
            // per-pixel GraphicsPath allocations; both are far too expensive
            // for a radio stream that can update dozens of times per second.
            Color off = invert ? foreground : background;
            Color on = invert ? background : foreground;
            int offArgb = off.ToArgb();
            int onArgb = on.ToArgb();
            int[] pixels = new int[128 * 64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = ((fb[i >> 3] >> (i & 7)) & 1) != 0 ? onArgb : offArgb;
            }

            using (Bitmap src = new Bitmap(128,64,System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                System.Drawing.Imaging.BitmapData data = src.LockBits(
                    new Rectangle(0,0,128,64),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                }
                finally
                {
                    src.UnlockBits(data);
                }

                InterpolationMode oldInterpolation = g.InterpolationMode;
                PixelOffsetMode oldPixelOffset = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, screen);
                g.InterpolationMode = oldInterpolation;
                g.PixelOffsetMode = oldPixelOffset;
            }
        }

        private static bool GetPixel(byte[] fb, int x, int y)
        {
            int bitIndex=y*128+x;
            return ((fb[bitIndex>>3]>>(bitIndex&7))&1)!=0;
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p=new GraphicsPath(); int d=Math.Max(1,radius*2);
            p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
        }
    }

    internal sealed class ActivityScope : Control
    {
        private readonly float[] samples = new float[120];
        private readonly Random rnd = new Random();
        private float target;
        private readonly Timer timer;
        public float Activity { get { return target; } set { target = Math.Max(0f, Math.Min(1f,value)); } }
        public ActivityScope()
        {
            DoubleBuffered=true; Height=74;
            timer=new Timer(); timer.Interval=45; timer.Tick += delegate { Step(); }; timer.Start();
        }
        private void Step()
        {
            for(int i=0;i<samples.Length-1;i++) samples[i]=samples[i+1];
            float noise=(float)(rnd.NextDouble()*0.18-0.09);
            float last=samples[samples.Length-2];
            samples[samples.Length-1]=Math.Max(0,Math.Min(1,last*.55f+target*.45f+noise));
            target*=.93f; Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(7,12,15)); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(Pen grid=new Pen(Color.FromArgb(24,80,104)))
            {
                for(int i=1;i<5;i++) e.Graphics.DrawLine(grid,0,Height*i/5,Width,Height*i/5);
                for(int i=1;i<10;i++) e.Graphics.DrawLine(grid,Width*i/10,0,Width*i/10,Height);
            }
            PointF[] pts=new PointF[samples.Length];
            for(int i=0;i<samples.Length;i++) pts[i]=new PointF(i*(Width-1f)/(samples.Length-1), Height-5-samples[i]*(Height-12));
            using(Pen p=new Pen(Color.FromArgb(75,220,255),1.7f)) if(pts.Length>1) e.Graphics.DrawLines(p,pts);
            using(Font f=new Font("Segoe UI",7.5f,FontStyle.Regular))
            using(Brush b=new SolidBrush(Color.FromArgb(110,155,170))) e.Graphics.DrawString("SERIAL / RADIO ACTIVITY",f,b,7,5);
        }
    }

    internal sealed class FaceplateHotspot : Control
    {
        private bool hover;
        private bool pressed;
        public bool ShowHover { get; set; }
        public FaceplateHotspot()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            ShowHover = false;
            TabStop = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (!ShowHover || (!hover && !pressed)) return;
            Color c = pressed ? Color.FromArgb(70, 80, 180, 255) : Color.FromArgb(40, 255, 255, 255);
            using (Brush b = new SolidBrush(c)) e.Graphics.FillRectangle(b, ClientRectangle);
            using (Pen p = new Pen(Color.FromArgb(140, 170, 220, 255))) e.Graphics.DrawRectangle(p, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        }
    }

}
