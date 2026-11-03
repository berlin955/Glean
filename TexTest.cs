using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
static class TexTest
{
    static void Run(string name, PixelFormat pf, bool withBack)
    {
        using (Bitmap b = new Bitmap(500, 120, pf))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                Font f = new Font("Microsoft YaHei UI", 20f);
                Rectangle r = new Rectangle(10, 10, 480, 100);
                if (withBack) TextRenderer.DrawText(g, "0 Glean 设置 3316980649", f, r, Color.FromArgb(38, 33, 28), Color.White, TextFormatFlags.WordBreak);
                else TextRenderer.DrawText(g, "0 Glean 设置 3316980649", f, r, Color.FromArgb(38, 33, 28), TextFormatFlags.WordBreak);
            }
            int darker = 0, mid = 0;
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    Color c = b.GetPixel(x, y);
                    int s = c.R + c.G + c.B;
                    if (s < 90) darker++;
                    else if (s > 120 && s < 700) mid++;
                }
            System.IO.File.AppendAllText("out.txt", "CACHE " + name + " darkerThanInk=" + darker + " smoothEdge=" + mid + "\r\n");
        }
    }
    [STAThread]
    static int Main()
    {
        Run("24bpp no-back", PixelFormat.Format24bppRgb, false);
        Run("24bpp with-back", PixelFormat.Format24bppRgb, true);
        Run("32bpp no-back", PixelFormat.Format32bppArgb, false);
        return 0;
    }
}
