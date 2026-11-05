using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
static class TexTest
{
    static void Out(string s) { File.AppendAllText(@"C:\gl-out.txt", s + "\r\n"); }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(5); } }
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    static void Setup(string dir)
    {
        if (Directory.Exists(dir)) return;
        Directory.CreateDirectory(dir);
        Glean.History h = new Glean.History(dir);
        string[] apps = { @"C:\Windows\notepad.exe", @"C:\Windows\explorer.exe", @"C:\Windows\System32\cmd.exe", @"C:\Windows\System32\mspaint.exe", @"C:\Windows\regedit.exe" };
        Random rnd = new Random(1);
        for (int i = 0; i < 160; i++)
        {
            Glean.Item it;
            if (i % 8 == 3)
            {
                using (Bitmap b = new Bitmap(1920, 1080))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)));
                        for (int k = 0; k < 200; k++) using (SolidBrush br = new SolidBrush(Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)))) g.FillEllipse(br, rnd.Next(1900), rnd.Next(1060), rnd.Next(300), rnd.Next(300));
                    }
                    using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); byte[] png = ms.ToArray(); it = h.AddImagePng(png, Glean.Util.Md5Short(png), DateTime.Now); }
                }
            }
            else it = h.AddText("第 " + i + " 条记录:这是一段比较长的文字,用来模拟平时复制的内容。" + new string('字', rnd.Next(5, 200)));
            if (it != null) h.SetSource(it, apps[i % apps.Length], "App" + (i % apps.Length));
        }
        h.FinishPendingDelete();
    }

    [STAThread]
    static int Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "parent";
        string dir = @"C:\gl-hist";
        if (mode == "parent")
        {
            Stopwatch s0 = Stopwatch.StartNew();
            Setup(dir);
            Out("setup " + s0.ElapsedMilliseconds);
            foreach (string m in new string[] { "cold", "prejit", "both", "cold", "prejit", "both" })
            {
                Process p = Process.Start(Application.ExecutablePath, m);
                p.WaitForExit();
            }
            File.Copy(@"C:\gl-out.txt", "out.txt", true);
            return 0;
        }
        try
        {
            Stopwatch total = Stopwatch.StartNew();
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Glean.Config.Load();
            Glean.Config.Layout = 1;
            Glean.Theme.Refresh();
            Application.EnableVisualStyles();
            Glean.History hist = new Glean.History(dir);
            Out("[" + mode + "] history load " + total.ElapsedMilliseconds + " items " + hist.Items.Count);
            Stopwatch sw = Stopwatch.StartNew();
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            Out("[" + mode + "] ctor " + sw.ElapsedMilliseconds);
            if (mode == "prejit" || mode == "both")
            {
                sw.Restart();
                typeof(Glean.MainForm).GetMethod("PreJit", NP).Invoke(null, null);
                Pump(1500);
            }
            if (mode == "both")
            {
                Pump(500);
                sw.Restart();
                shelf.Prewarm();
                Out("[" + mode + "] prewarm " + sw.ElapsedMilliseconds);
                Pump(1500);   // 让后台缩略图跑完
            }
            Glean.Perf.On = true;
            sw.Restart();
            shelf.ShowAt();
            long showAt = sw.ElapsedMilliseconds;
            shelf.Update();
            long paint = sw.ElapsedMilliseconds;
            Out("[" + mode + "] ShowAt " + showAt + " ms, first paint done at " + paint + " ms; marks: " + string.Join(", ", Glean.Perf.Log.ToArray()));
            Glean.Perf.Log.Clear();
            // 统计滑出过程中界面卡住的最长一次
            long worst = 0, last = sw.ElapsedMilliseconds;
            while (sw.ElapsedMilliseconds < paint + 900)
            {
                Application.DoEvents();
                long now = sw.ElapsedMilliseconds;
                worst = Math.Max(worst, now - last);
                last = now;
                Thread.Sleep(1);
            }
            Out("[" + mode + "] worst UI stall during slide " + worst + " ms");
            shelf.HideNow();
            Pump(300);
            sw.Restart();
            shelf.ShowAt(); shelf.Update();
            Out("[" + mode + "] second open " + sw.ElapsedMilliseconds + " ms; marks: " + string.Join(", ", Glean.Perf.Log.ToArray()));
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
