using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
static class TexTest
{
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    [STAThread]
    static int Main()
    {
        try
        {
            Glean.Gfx.InitDpi();
            Directory.CreateDirectory("qr");
            string longZh = ""; for (int i = 0; i < 40; i++) longZh += "拾光测试第" + i + "行;";
            string[] payloads = {
                "Hello", "YT7652500323998", "拾光 Glean 剪切板", "https://github.com/berlin955/Glean/releases/tag/v0.15",
                "http://192.168.1.23:52731/0f3a9c2e7b1d4a5c6e8f9a0b", longZh.Substring(0, 120), longZh.Substring(0, 299),
                new string('a', 600) + "末尾", "最后一段\n多行\n文字 with emoji 😀 and #E28A2E"
            };
            for (int i = 0; i < payloads.Length; i++)
            {
                byte[] b = Encoding.UTF8.GetBytes(payloads[i]);
                bool[,] m = Glean.QrCode.Encode(b);
                if (m == null) { Out("QR " + i + " too long"); continue; }
                using (Bitmap bmp = Glean.QrCode.Render(m, 6, Color.Black, Color.White)) bmp.Save("qr/q" + i + ".png", ImageFormat.Png);
                File.WriteAllText("qr/q" + i + ".txt", payloads[i], new UTF8Encoding(false));
                StringBuilder rows = new StringBuilder();
                for (int y = 0; y < m.GetLength(0); y++) { for (int x = 0; x < m.GetLength(0); x++) rows.Append(m[y, x] ? '1' : '0'); rows.Append('|'); }
                Out("MAT " + i + " mask=" + Glean.QrCode.LastMask + " " + Convert.ToBase64String(b) + " " + rows.ToString());
                Out("QR " + i + " bytes=" + b.Length + " size=" + m.GetLength(0) + " (version " + (m.GetLength(0) - 17) / 4 + ")");
            }
            // 每个版本都测一遍:刚好装满的长度
            for (int v = 1; v <= 40; v += 3)
            {
                // 找到这个版本能装的最大字节数
                int lo = 1, best = 0;
                for (int n = 1; n < 2400; n += (n < 100 ? 1 : 5))
                {
                    bool[,] mm = Glean.QrCode.Encode(new byte[n]);
                    if (mm == null) break;
                    if ((mm.GetLength(0) - 17) / 4 == v) best = n;
                    if ((mm.GetLength(0) - 17) / 4 > v) break;
                }
                if (best == 0) continue;
                StringBuilder sb = new StringBuilder();
                for (int k = 0; k < best; k++) sb.Append((char)('A' + (k * 7 + v) % 26));
                bool[,] m2 = Glean.QrCode.Encode(Encoding.ASCII.GetBytes(sb.ToString()));
                using (Bitmap bmp = Glean.QrCode.Render(m2, 5, Color.Black, Color.White)) bmp.Save("qr/v" + v + ".png", ImageFormat.Png);
                File.WriteAllText("qr/v" + v + ".txt", sb.ToString());
                Out("VER " + v + " max=" + best + " size=" + m2.GetLength(0));
            }
            Out("maxBytes " + Glean.QrCode.MaxBytes);

            // 快递单号
            foreach (string t in new string[] { "YT7652500323998", "SF1234567890123", "JT0001234567890", "JDVA12345678901", "EA123456789CN", "3316980649089088956", "hello", "yt7652500323998 " })
            {
                string[] c = Glean.Courier.Detect(t);
                Out("COURIER " + t + " => " + (c == null ? "-" : c[0] + " / " + Glean.Courier.TrackUrl(t)));
            }

            // 局域网分享
            string img = Path.GetFullPath("share-test.png");
            using (Bitmap bb = new Bitmap(320, 200)) { using (Graphics g = Graphics.FromImage(bb)) g.Clear(Color.Orange); bb.Save(img, ImageFormat.Png); }
            Out("LAN ip " + Glean.ShareServer.LanAddress());
            string err;
            string url = Glean.ShareServer.Start("测试图片", null, new List<string> { img }, out err);
            Out("SHARE url " + url + " err " + err);
            if (url != null)
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    string page = wc.DownloadString(url);
                    Out("SHARE page " + page.Length + " chars, has img: " + page.Contains("<img") + ", title ok: " + page.Contains("测试图片"));
                    byte[] data = wc.DownloadData(url + "/f0");
                    Out("SHARE file " + data.Length + " bytes, same as original: " + (data.Length == new FileInfo(img).Length) + ", type " + wc.ResponseHeaders["Content-Type"]);
                    try { wc.DownloadString(url.Substring(0, url.LastIndexOf('/')) + "/wrongtoken"); Out("SHARE wrong token: SERVED (bad)"); }
                    catch (WebException we) { Out("SHARE wrong token: " + ((HttpWebResponse)we.Response).StatusCode); }
                }
                Glean.ShareServer.Stop();
                try { new WebClient().DownloadString(url); Out("SHARE after stop: still served (bad)"); }
                catch (WebException) { Out("SHARE after stop: refused (good)"); }
            }
            string url2 = Glean.ShareServer.Start("长文字", longZh, null, out err);
            using (WebClient wc = new WebClient()) { wc.Encoding = Encoding.UTF8; string p2 = wc.DownloadString(url2); Out("SHARE text page contains text: " + p2.Contains("拾光测试第39行")); }
            Glean.ShareServer.Stop();
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
