using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
static class TexTest
{
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    [STAThread]
    static int Main()
    {
        try
        {
            bool[,] m = Glean.QrCode.Encode(Encoding.UTF8.GetBytes("https://example.com/测试?a=1"));
            int n = m.GetLength(0), sc = 6, q = 4;
            using (Bitmap b = new Bitmap((n + 2 * q) * sc, (n + 2 * q) * sc))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.Clear(Color.White);
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) if (m[y, x]) g.FillRectangle(Brushes.Black, (x + q) * sc, (y + q) * sc, sc, sc);
                }
                ZXing.BarcodeReader r = new ZXing.BarcodeReader();
                r.Options.TryHarder = true;
                ZXing.Result[] rs = r.DecodeMultiple(b);
                Out("decoded " + (rs == null ? 0 : rs.Length));
                if (rs != null) foreach (ZXing.Result x in rs) Out(x.BarcodeFormat + " " + x.Text + " pts " + x.ResultPoints.Length);
            }
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
