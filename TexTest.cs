using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
static class TexTest
{
    static GraphicsPath Round(Rectangle r, int d)
    {
        GraphicsPath p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    static int Main()
    {
        int w = 300, h = 200;
        Bitmap body = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(body))
        {
            g.Clear(Color.White);
            TextRenderer.DrawText(g, "3316980649089\n08895 6 测试文字", new Font("Segoe UI", 20f), new Rectangle(20, 20, 260, 160), Color.FromArgb(38, 33, 28), TextFormatFlags.WordBreak);
        }
        string[] names = { "default", "nearest", "nearest+half", "bilinear+half", "drawimage-clip" };
        for (int c = 0; c < names.Length; c++)
        {
            Bitmap dst = new Bitmap(w + 40, h + 40, PixelFormat.Format32bppArgb);
            Rectangle r = new Rectangle(20, 20, w, h);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.Clear(Color.Gray);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (c == 1 || c == 2) g.InterpolationMode = InterpolationMode.NearestNeighbor;
                if (c == 2 || c == 3) g.PixelOffsetMode = PixelOffsetMode.Half;
                if (c == 3) g.InterpolationMode = InterpolationMode.Bilinear;
                if (c == 4) { using (GraphicsPath cp = Round(r, 28)) { g.SetClip(cp); g.DrawImageUnscaled(body, r.X, r.Y); g.ResetClip(); } }
                else
                using (TextureBrush tb = new TextureBrush(body, WrapMode.Clamp))
                using (GraphicsPath cp = Round(r, 28))
                {
                    tb.TranslateTransform(r.X, r.Y);
                    g.FillPath(tb, cp);
                }
            }
            int diff = 0, shiftDiff = 0;
            for (int y = 30; y < h - 10; y++)
                for (int x = 30; x < w - 10; x++)
                {
                    Color a = body.GetPixel(x, y), b = dst.GetPixel(x + 20, y + 20);
                    if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 6) diff++;
                    Color b2 = dst.GetPixel(x + 21, y + 21);
                    if (Math.Abs(a.R - b2.R) + Math.Abs(a.G - b2.G) + Math.Abs(a.B - b2.B) > 6) shiftDiff++;
                }
            Console.WriteLine("TEX " + names[c] + " diffPixels=" + diff + " (if shifted by 1px: " + shiftDiff + ")");
        }
        return 0;
    }
}
