using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
static class TexTest
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, int op);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(5); } }
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Shot(string name, Rectangle r)
    {
        using (Bitmap b = new Bitmap(r.Width, r.Height))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                IntPtr dst = g.GetHdc(); IntPtr src = GetDC(IntPtr.Zero);
                BitBlt(dst, 0, 0, r.Width, r.Height, src, r.X, r.Y, 0x00CC0020 | 0x40000000);
                ReleaseDC(IntPtr.Zero, src); g.ReleaseHdc(dst);
            }
            Save(name, b);
        }
    }
    static void Save(string name, Bitmap b)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            b.Save(ms, ImageFormat.Png);
            string all = Convert.ToBase64String(ms.ToArray());
            for (int k = 0, part = 0; k < all.Length; k += 6000, part++)
                Out("PNGPART " + name + " " + part.ToString("000") + " " + all.Substring(k, Math.Min(6000, all.Length - k)));
        }
    }
    static Bitmap Qr(string text, int scale)
    {
        bool[,] m = Glean.QrCode.Encode(Encoding.UTF8.GetBytes(text));
        int n = m.GetLength(0), q = 4;
        Bitmap b = new Bitmap((n + 2 * q) * scale, (n + 2 * q) * scale);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.White);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) if (m[y, x]) g.FillRectangle(Brushes.Black, (x + q) * scale, (y + q) * scale, scale, scale);
        }
        return b;
    }
    static Bitmap Bar(string fmt, string text, int w, int h)
    {
        ZXing.BarcodeWriter wr = new ZXing.BarcodeWriter();
        wr.Format = (ZXing.BarcodeFormat)Enum.Parse(typeof(ZXing.BarcodeFormat), fmt);
        wr.Options = new ZXing.Common.EncodingOptions { Width = w, Height = h, Margin = 10 };
        return wr.Write(text);
    }
    // 模拟一张网页截图:浅色背景、几段文字,中间放几个二维码和条形码
    static Bitmap Screenshot()
    {
        Bitmap b = new Bitmap(1600, 900);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.FromArgb(244, 242, 238));
            using (Font f = new Font("Microsoft YaHei UI", 16f)) using (Font f2 = new Font("Microsoft YaHei UI", 11f))
            {
                g.DrawString("活动报名", f, Brushes.Black, 60, 40);
                for (int i = 0; i < 12; i++) g.DrawString("这是一段模拟网页正文内容,用来测试二维码识别在真实截图中的效果。", f2, Brushes.DimGray, 60, 100 + i * 28);
            }
            using (Bitmap q1 = Qr("https://www.example.com/event/2026?from=glean", 5)) g.DrawImage(q1, 980, 80);
            using (Bitmap q2 = Qr("WIFI:S:Glean-Office;T:WPA;P:hello123456;;", 4)) g.DrawImage(q2, 1300, 90);
            using (Bitmap q3 = Qr("wxp://f2f0abcdefghijk", 3)) g.DrawImage(q3, 1000, 560);
            using (Bitmap q4 = Qr("会议纪要:周五下午三点在 3 号会议室", 3)) g.DrawImage(q4, 1250, 560);
            using (Bitmap b1 = Bar("EAN_13", "6901234567892", 300, 110)) g.DrawImage(b1, 80, 520);
            using (Bitmap b2 = Bar("CODE_128", "SF1234567890123", 420, 110)) g.DrawImage(b2, 80, 700);
        }
        return b;
    }
    static Bitmap Label()
    {
        Bitmap b = new Bitmap(700, 420);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.White);
            using (Font f = new Font("Microsoft YaHei UI", 14f)) g.DrawString("顺丰速运  收件人:张先生  138****0000", f, Brushes.Black, 30, 30);
            using (Bitmap b2 = Bar("CODE_128", "SF1234567890123", 520, 140)) g.DrawImage(b2, 90, 120);
        }
        return b;
    }

    [STAThread]
    static int Main()
    {
        Glean.Embedded.Hook();
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Out("UI-EXCEPTION " + e.Exception); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        try { Run(); } catch (Exception ex) { Out("ERROR " + ex); }
        Out("DONE");
        return 0;
    }

    static void Run()
    {
        Out("zxing loaded from: " + (File.Exists("zxing.dll") ? "disk" : "embedded resource"));
        Glean.Native.SetProcessDPIAware();
        Glean.Gfx.InitDpi();
        Application.EnableVisualStyles();
        Glean.Config.ThemeMode = 1; Glean.Config.ShelfTransparency = 0; Glean.Config.Layout = 1;
        Glean.Theme.Refresh();
        string cf = Path.Combine(Glean.Config.DefaultDir, "codes.txt");
        if (File.Exists(cf)) File.Delete(cf);

        Bitmap shotImg = Screenshot();
        Stopwatch sw = Stopwatch.StartNew();
        List<Glean.Codes.Code> r = Glean.Codes.Scan(shotImg, false);
        Out("scan 1600x900: " + sw.ElapsedMilliseconds + " ms, found " + r.Count);
        foreach (Glean.Codes.Code c in r)
        {
            string t, sub; Glean.Codes.Describe(c, out t, out sub);
            Out("  " + c.Format + " [" + Glean.Codes.KindOf(c) + "] " + c.Text + "  box " + c.Box + "  => " + t + " | " + sub);
        }
        using (Bitmap big = new Bitmap(3840, 2160))
        {
            using (Graphics g = Graphics.FromImage(big)) { g.Clear(Color.FromArgb(30, 30, 34)); g.DrawImage(shotImg, 1200, 700, 1600, 900); }
            sw.Restart(); List<Glean.Codes.Code> r2 = Glean.Codes.Scan(big, false);
            Out("scan 3840x2160 fast: " + sw.ElapsedMilliseconds + " ms, found " + r2.Count);
            sw.Restart(); r2 = Glean.Codes.Scan(big, false, true);
            Out("scan 3840x2160 thorough: " + sw.ElapsedMilliseconds + " ms, found " + r2.Count);
        }
        using (Bitmap empty = new Bitmap(1920, 1080))
        {
            using (Graphics g = Graphics.FromImage(empty)) g.Clear(Color.White);
            sw.Restart(); List<Glean.Codes.Code> r3 = Glean.Codes.Scan(empty, false);
            Out("scan empty 1920x1080: " + sw.ElapsedMilliseconds + " ms, found " + r3.Count);
        }

        // 历史里放几张图:网页截图、快递面单、普通图片
        string dir = Path.Combine(Path.GetTempPath(), "glean-qr");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Glean.History hist = new Glean.History(dir);
        Glean.Item plain;
        using (Bitmap p = new Bitmap(800, 500)) { using (Graphics g = Graphics.FromImage(p)) { g.Clear(Color.SteelBlue); g.FillEllipse(Brushes.Gold, 200, 100, 300, 300); } plain = AddImg(hist, p); }
        Glean.Item label = AddImg(hist, Label());
        Glean.Item web = AddImg(hist, shotImg);
        hist.SetSource(web, @"C:\Windows\explorer.exe", "Chrome");
        Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
        shelf.ShowAt(); Pump(3500);   // 让后台识别跑完
        Out("cached web " + Count(Glean.Codes.Cached(web)) + " label " + Count(Glean.Codes.Cached(label)) + " plain " + Count(Glean.Codes.Cached(plain)));
        Rectangle sr = shelf.Bounds;
        Shot("cards", new Rectangle(sr.X, sr.Y, Math.Min(sr.Width, Glean.Gfx.S(980)), sr.Height));

        // 预览
        Glean.QuickLook ql = (Glean.QuickLook)typeof(Glean.ShelfForm).GetField("quick", NP).GetValue(shelf);
        ql.ShowFor(web, shelf.Bounds, false, false); Pump(900);
        Shot("quick", ql.Bounds);
        // 选第二个(WiFi)
        object pic = typeof(Glean.QuickLook).GetField("pic", NP).GetValue(ql);
        List<Glean.Codes.Code> found = (List<Glean.Codes.Code>)pic.GetType().GetField("Found").GetValue(pic);
        int wifi = found.FindIndex(c => c.Text.StartsWith("WIFI"));
        pic.GetType().GetField("Sel").SetValue(pic, wifi); ((Control)pic).Invalidate(); Pump(300);
        Shot("quick-wifi", ql.Bounds);
        ql.ShowFor(plain, shelf.Bounds, false, true); Pump(900);
        Shot("quick-none", ql.Bounds);
        ql.Hide();
        shelf.HideNow(); Pump(400);

        // 屏幕扫码:把截图全屏显示当作"屏幕内容"
        Form fake = new Form();
        fake.FormBorderStyle = FormBorderStyle.None; fake.StartPosition = FormStartPosition.Manual;
        fake.Bounds = Screen.PrimaryScreen.Bounds; fake.BackgroundImage = shotImg; fake.BackgroundImageLayout = ImageLayout.Zoom; fake.BackColor = Color.FromArgb(244, 242, 238);
        fake.Show(); Pump(600);
        sw.Restart();
        Glean.ScanOverlay.Start(); Pump(150);
        Shot("scan-0", Screen.PrimaryScreen.Bounds);
        Form ov = null;
        while (sw.ElapsedMilliseconds < 8000)
        {
            Pump(100);
            foreach (Form f in Application.OpenForms) if (f is Glean.ScanOverlay) ov = f;
            if (ov != null && !(bool)typeof(Glean.ScanOverlay).GetField("scanning", NP).GetValue(ov)) break;
        }
        Out("screen scan done in " + sw.ElapsedMilliseconds + " ms, found " + ((System.Collections.IList)typeof(Glean.ScanOverlay).GetField("found", NP).GetValue(ov)).Count);
        Pump(400);
        Shot("scan-1", Screen.PrimaryScreen.Bounds);
        typeof(Glean.ScanOverlay).GetField("sel", NP).SetValue(ov, 0); ov.Invalidate(); Pump(300);
        Shot("scan-2", Screen.PrimaryScreen.Bounds);
        ov.Close(); fake.Close(); Pump(300);

        // 设置页
        Glean.SettingsForm sf = new Glean.SettingsForm();
        object side = typeof(Glean.SettingsForm).GetField("side", NP).GetValue(sf);
        sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(0, 0);
        sf.Show(); Pump(500);
        foreach (int pg in new int[] { 3, 4 })
        {
            side.GetType().GetField("Selected").SetValue(side, pg);
            typeof(Glean.SettingsForm).GetMethod("ShowPage", NP).Invoke(sf, null); Pump(500);
            Shot("settings-" + pg, sf.Bounds);
        }
    }
    static int Count(List<Glean.Codes.Code> l) { return l == null ? -1 : l.Count; }
    static Glean.Item AddImg(Glean.History h, Bitmap b)
    {
        using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); byte[] png = ms.ToArray(); return h.AddImagePng(png, Glean.Util.Md5Short(png), DateTime.Now); }
    }
}
