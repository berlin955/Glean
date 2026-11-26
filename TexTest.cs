using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
static class TexTest
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, int op);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(1); } }
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Bitmap Grab(Rectangle r)
    {
        Bitmap b = new Bitmap(r.Width, r.Height);
        using (Graphics g = Graphics.FromImage(b))
        {
            IntPtr dst = g.GetHdc(); IntPtr src = GetDC(IntPtr.Zero);
            BitBlt(dst, 0, 0, r.Width, r.Height, src, r.X, r.Y, 0x00CC0020 | 0x40000000);
            ReleaseDC(IntPtr.Zero, src); g.ReleaseHdc(dst);
        }
        return b;
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
    // 连续截几帧,拼成一条胶片
    static void Film(string name, Rectangle area, int frames, int everyMs, float scale)
    {
        List<Bitmap> shots = new List<Bitmap>();
        Stopwatch w = Stopwatch.StartNew();
        for (int i = 0; i < frames; i++)
        {
            while (w.ElapsedMilliseconds < i * everyMs) { Application.DoEvents(); Thread.Sleep(1); }
            shots.Add(Grab(area));
        }
        int tw = (int)(area.Width * scale), th = (int)(area.Height * scale);
        using (Bitmap film = new Bitmap(tw * Math.Min(4, frames), th * ((frames + 3) / 4)))
        {
            using (Graphics g = Graphics.FromImage(film))
            {
                g.Clear(Color.Black);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                for (int i = 0; i < shots.Count; i++)
                {
                    g.DrawImage(shots[i], (i % 4) * tw, (i / 4) * th, tw, th);
                    g.DrawString((i * everyMs) + "ms", SystemFonts.DefaultFont, Brushes.Red, (i % 4) * tw + 4, (i / 4) * th + 4);
                }
            }
            Save(name, film);
        }
        foreach (Bitmap b in shots) b.Dispose();
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
        Glean.Native.SetProcessDPIAware();
        Glean.Gfx.InitDpi();
        Application.EnableVisualStyles();
        Glean.Config.ThemeMode = 1; Glean.Config.ShelfTransparency = 0; Glean.Config.Layout = 1;
        Glean.Theme.Refresh();
        Out("animations enabled (system): " + Glean.Frames.Enabled);
        Glean.Frames.ForceOn = true;
        // 弹簧:从 0 到 100,记录到位时间和最大过冲
        foreach (double[] cfg in new double[][] { new double[] { 0.44, 0.94 }, new double[] { 0.34, 0.86 }, new double[] { 0.26, 0.74 }, new double[] { 0.36, 0.82 } })
        {
            Glean.Spring sp = new Glean.Spring(cfg[0], cfg[1]); sp.Target = 100; sp.Rest = 0.3;
            double t = 0, max = 0, t95 = -1;
            while (sp.Step(16.67) && t < 3000) { t += 16.67; max = Math.Max(max, sp.Value); if (t95 < 0 && sp.Value >= 95) t95 = t; }
            Out("spring " + cfg[0] + "/" + cfg[1] + ": 95% at " + Math.Round(t95) + "ms, settled " + Math.Round(t) + "ms, overshoot " + Math.Round(max - 100, 2) + "%");
        }
        string dir = Path.Combine(Path.GetTempPath(), "glean-anim");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Glean.History hist = new Glean.History(dir);
        for (int i = 0; i < 8; i++) hist.AddText("第 " + i + " 条:用来看动画效果的一段文字,内容稍微长一点,看看卡片和预览里的排版。");
        Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
        shelf.Prewarm(); Pump(300);
        // 面板弹出:统计帧数
        Rectangle scr = Screen.PrimaryScreen.Bounds;
        int frames = 0, lastTop = -1;
        System.Windows.Forms.Timer probe = null;
        Stopwatch sw = Stopwatch.StartNew();
        shelf.ShowAt();
        List<string> tops = new List<string>();
        while (sw.ElapsedMilliseconds < 900)
        {
            Application.DoEvents();
            if (shelf.Top != lastTop) { frames++; lastTop = shelf.Top; tops.Add(sw.ElapsedMilliseconds + ":" + (scr.Bottom - shelf.Top)); }
            Thread.Sleep(0);
        }
        Out("frame source: " + Glean.Frames.Mode + ", slide distinct positions " + frames);
        Out("slide: " + string.Join(" ", tops.ToArray()));
        Pump(300);
        // 预览从卡片放大出来
        Rectangle area = new Rectangle(0, Math.Max(0, scr.Bottom - Glean.Gfx.S(700)), Math.Min(scr.Width, Glean.Gfx.S(1000)), Math.Min(scr.Height, Glean.Gfx.S(700)));
        object cv = typeof(Glean.ShelfForm).GetField("cardView", NP).GetValue(shelf);
        cv.GetType().GetProperty("Selected").SetValue(cv, 1, null);
        Pump(200);
        Out("card rect " + cv.GetType().GetMethod("ScreenRectOf").Invoke(cv, new object[] { 1 }));
        Glean.QuickLook ql0 = (Glean.QuickLook)typeof(Glean.ShelfForm).GetField("quick", NP).GetValue(shelf);
        typeof(Glean.ShelfForm).GetMethod("ShowQuick", NP).Invoke(shelf, null);
        Out("after ShowQuick: morph running " + Glean.MorphWindow.Running + " quick visible " + ql0.Visible + " snapErr " + Glean.QuickLook.SnapError);
        Film("open", area, 12, 40, 0.5f);
        Pump(500);
        Glean.QuickLook ql = (Glean.QuickLook)typeof(Glean.ShelfForm).GetField("quick", NP).GetValue(shelf);
        Out("quick visible after open: " + ql.Visible + ", morph running " + Glean.MorphWindow.Running);
        typeof(Glean.ShelfForm).GetMethod("CloseQuick", NP).Invoke(shelf, null);
        Film("close", area, 8, 40, 0.5f);
        Pump(500);
        Out("quick visible after close: " + ql.Visible + ", morph running " + Glean.MorphWindow.Running);
        // 悬停浮起
        cv.GetType().GetField("hover", NP).SetValue(cv, 3);
        cv.GetType().GetMethod("Kick", NP).Invoke(cv, null);
        Rectangle sr = shelf.Bounds;
        Film("hover", new Rectangle(sr.X, sr.Y + Glean.Gfx.S(40), Math.Min(sr.Width, Glean.Gfx.S(1000)), sr.Height - Glean.Gfx.S(40)), 8, 30, 0.5f);
        // 收起
        sw.Restart(); frames = 0; lastTop = shelf.Top;
        shelf.HideShelf();
        while (sw.ElapsedMilliseconds < 500) { Application.DoEvents(); if (shelf.Top != lastTop) { frames++; lastTop = shelf.Top; } Thread.Sleep(0); }
        Out("hide frames " + frames + " visible after " + shelf.Visible);
    }
}
