using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
static class TexTest
{
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    [STAThread]
    static int Main()
    {
        try
        {
            Glean.Gfx.InitDpi();
            int n = Glean.Courier.Companies.Length;
            int[] sizes = { 30, 90 };
            using (Bitmap sheet = new Bitmap(20 + n * 100, 20 + 2 * 110))
            {
                using (Graphics g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.FromArgb(78, 150, 138));
                    for (int si = 0; si < sizes.Length; si++)
                        for (int i = 0; i < n; i++)
                        {
                            int s = sizes[si];
                            Rectangle r = new Rectangle(10 + i * 100 + (90 - s) / 2, 10 + si * 110 + (90 - s) / 2, s, s);
                            Glean.Courier.DrawBadge(g, Glean.Courier.Companies[i], r);
                        }
                }
                // 量一下每个字的墨迹中心离角标中心多远(只看大号的)
                for (int i = 0; i < n; i++)
                {
                    int s = 90;
                    Rectangle r = new Rectangle(10 + i * 100, 120, s, s);
                    int minX = 9999, minY = 9999, maxX = -1, maxY = -1;
                    for (int y = r.Y + 8; y < r.Bottom - 8; y++)
                        for (int x = r.X + 8; x < r.Right - 8; x++)
                        {
                            Color c = sheet.GetPixel(x, y);
                            if (c.R + c.G + c.B < 600) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                        }
                    double dx = (minX + maxX) / 2.0 - (r.X + (s - 1) / 2.0), dy = (minY + maxY) / 2.0 - (r.Y + (s - 1) / 2.0);
                    Out(string.Format("CENTER {0} {1}: offset x={2:0.0}px y={3:0.0}px (of {4}px)", Glean.Courier.Companies[i].Mark, Glean.Courier.Companies[i].Name, dx, dy, s));
                }
                using (MemoryStream ms = new MemoryStream()) { sheet.Save(ms, ImageFormat.Png); Out("PNG " + Convert.ToBase64String(ms.ToArray())); }
            }
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
