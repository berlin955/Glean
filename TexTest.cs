using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
static class TexTest
{
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool StretchBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, int sw, int sh, int rop);
    [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr dc, int mode);
    [DllImport("gdi32.dll")] static extern bool SetBrushOrgEx(IntPtr dc, int x, int y, IntPtr p);
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }

    static Bitmap Upscale(Bitmap small, int w, int h, InterpolationMode mode)
    {
        Bitmap blurred = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(blurred))
        using (ImageAttributes ia = new ImageAttributes())
        {
            ia.SetWrapMode(WrapMode.TileFlipXY);
            g.InterpolationMode = mode;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(small, new Rectangle(0, 0, w, h), 0, 0, small.Width, small.Height, GraphicsUnit.Pixel, ia);
        }
        return blurred;
    }

    // 新方案:直接从屏幕缩小拷贝到 1/4,再缩到 1/18,放大回来
    static Bitmap Fast(Rectangle area, InterpolationMode up, out double tCap, out double tDown, out double tUp)
    {
        Stopwatch w = Stopwatch.StartNew();
        int qw = Math.Max(1, area.Width / 4), qh = Math.Max(1, area.Height / 4);
        Bitmap quarter = new Bitmap(qw, qh, PixelFormat.Format32bppRgb);
        using (Graphics g = Graphics.FromImage(quarter))
        {
            IntPtr dst = g.GetHdc();
            IntPtr scr = GetDC(IntPtr.Zero);
            SetStretchBltMode(dst, 4);
            SetBrushOrgEx(dst, 0, 0, IntPtr.Zero);
            StretchBlt(dst, 0, 0, qw, qh, scr, area.X, area.Y, area.Width, area.Height, 0x00CC0020);
            ReleaseDC(IntPtr.Zero, scr);
            g.ReleaseHdc(dst);
        }
        tCap = w.Elapsed.TotalMilliseconds; w.Restart();
        int sw = Math.Max(1, area.Width / 18), sh = Math.Max(1, area.Height / 18);
        Bitmap small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(small))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(quarter, 0, 0, sw, sh);
        }
        quarter.Dispose();
        tDown = w.Elapsed.TotalMilliseconds; w.Restart();
        Bitmap r = Upscale(small, area.Width, area.Height, up);
        small.Dispose();
        tUp = w.Elapsed.TotalMilliseconds;
        return r;
    }

    static double Diff(Bitmap a, Bitmap b)
    {
        long d = 0; int n = 0;
        for (int y = 0; y < a.Height; y += 7)
            for (int x = 0; x < a.Width; x += 7)
            {
                Color p = a.GetPixel(x, y), q = b.GetPixel(x, y);
                d += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B); n++;
            }
        return d / (3.0 * n);
    }

    [STAThread]
    static int Main()
    {
        try
        {
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Glean.Theme.Refresh();
            // 屏幕上放一个花哨的窗口,让截图有内容
            Form f = new Form();
            f.FormBorderStyle = FormBorderStyle.None;
            f.Bounds = Screen.PrimaryScreen.Bounds;
            f.Paint += delegate(object s, PaintEventArgs e)
            {
                Random rnd = new Random(3);
                for (int k = 0; k < 120; k++)
                    using (SolidBrush br = new SolidBrush(Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256))))
                        e.Graphics.FillRectangle(br, rnd.Next(1024), rnd.Next(768), rnd.Next(30, 300), rnd.Next(20, 200));
                e.Graphics.DrawString("一些文字 Some text 1234567890", new Font("Microsoft YaHei UI", 22f), Brushes.Black, 40, 500);
            };
            f.Show();
            for (int k = 0; k < 30; k++) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
            Rectangle area = new Rectangle(12, 768 - 12 - 520, 1000, 520);
            for (int trial = 0; trial < 4; trial++)
            {
                Stopwatch w = Stopwatch.StartNew();
                Bitmap cur = Glean.Frost.CaptureBlur(area);
                double tCur = w.Elapsed.TotalMilliseconds;
                double c1, d1, u1, c2, d2, u2;
                Bitmap fb = Fast(area, InterpolationMode.Bilinear, out c1, out d1, out u1);
                Bitmap fq = Fast(area, InterpolationMode.HighQualityBicubic, out c2, out d2, out u2);
                w.Restart();
                int lt = Glean.Frost.Legible(cur, 83);
                double tLeg = w.Elapsed.TotalMilliseconds;
                Out(string.Format("GLASS try{0}: current {1:0.0} ms | fast+bilinear {2:0.0} (cap {3:0.0} down {4:0.0} up {5:0.0}) diff {6:0.0} | fast+bicubic {7:0.0} (up {8:0.0}) diff {9:0.0} | legible {10:0.0} ms -> {11}",
                    trial + 1, tCur, c1 + d1 + u1, c1, d1, u1, Diff(cur, fb), c2 + d2 + u2, u2, Diff(cur, fq), tLeg, lt));
                if (trial == 3) { cur.Save("cur.png"); fb.Save("fast-bilinear.png"); fq.Save("fast-bicubic.png"); }
                cur.Dispose(); fb.Dispose(); fq.Dispose();
            }
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
