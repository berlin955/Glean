using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
static class TexTest
{
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static double Time(Action a) { Stopwatch w = Stopwatch.StartNew(); a(); return w.Elapsed.TotalMilliseconds; }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }

    [STAThread]
    static int Main()
    {
        try
        {
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Glean.Theme.Refresh();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Out("screen " + Screen.PrimaryScreen.Bounds + " scale " + Glean.Gfx.Scale);
            string dir = Path.Combine(Path.GetTempPath(), "glean-bench");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Glean.History hist = new Glean.History(dir);
            Random rnd = new Random(1);
            string[] words = { "剪切板", "测试", "Glean", "hello world", "https://github.com/berlin955/Glean", "会议纪要:下午三点", "订单号 YT7652500323998", "#E28A2E", "D:\\Work\\report.docx", "3316980649089088956" };
            for (int i = 0; i < 175; i++)
            {
                string t = words[i % words.Length] + " " + i;
                if (i % 7 == 0) for (int k = 0; k < 30; k++) t += "\n第 " + k + " 行 一些比较长的文字内容,用来测试多行卡片的绘制速度。";
                hist.AddText(t);
            }
            for (int i = 0; i < 25; i++)
            {
                using (Bitmap b = new Bitmap(1600, 900))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)));
                        for (int k = 0; k < 40; k++)
                            using (SolidBrush br = new SolidBrush(Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256))))
                                g.FillEllipse(br, rnd.Next(1600), rnd.Next(900), 200, 160);
                    }
                    MemoryStream ms = new MemoryStream();
                    b.Save(ms, ImageFormat.Png);
                    byte[] png = ms.ToArray();
                    hist.AddImagePng(png, Glean.Util.Md5Short(png), DateTime.Now.AddMinutes(-i));
                }
            }
            Out("items " + hist.Items.Count);

            Glean.ShelfForm shelf = null;
            double ctor = Time(delegate { shelf = new Glean.ShelfForm(null, hist); });
            double warm = Time(delegate { shelf.Prewarm(); });
            Pump(300);
            Out(string.Format("create {0:0} ms, prewarm {1:0} ms", ctor, warm));
            FieldInfo fStrip = typeof(Glean.ShelfForm).GetField("strip", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fSb = typeof(Glean.ShelfForm).GetField("sb", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (int layout in new int[] { 1, 0 })
            {
                Glean.Config.Layout = layout;
                foreach (int trans in new int[] { 35, 0 })
                {
                    Glean.Config.ShelfTransparency = trans;
                    for (int trial = 0; trial < 3; trial++)
                    {
                        Glean.Perf.On = true;
                        Glean.Perf.Log.Clear();
                        double show = Time(delegate { shelf.ShowAt(); });
                        string steps = string.Join(", ", Glean.Perf.Log.ToArray());
                        Glean.Perf.On = false;
                        double paint1 = Time(delegate { shelf.Refresh(); });
                        Pump(trial == 0 ? 1500 : 600);   // 等滑入动画和缩略图
                        double paint2 = Time(delegate { shelf.Refresh(); });
                        object strip = fStrip.GetValue(shelf);
                        MethodInfo scroll = strip.GetType().GetMethod("ScrollBy", new Type[] { typeof(int) });
                        double scrollTotal = 0;
                        for (int k = 0; k < 10; k++)
                        {
                            scroll.Invoke(strip, new object[] { 260 });
                            scrollTotal += Time(delegate { ((Control)strip).Refresh(); });
                        }
                        Glean.SearchBox sb = (Glean.SearchBox)fSb.GetValue(shelf);
                        double type1 = Time(delegate { sb.Box.Text = "测"; shelf.Refresh(); });
                        double type2 = Time(delegate { sb.Box.Text = "测试"; shelf.Refresh(); });
                        double clear = Time(delegate { sb.Box.Text = ""; shelf.Refresh(); });
                        Out(string.Format("BENCH {0} trans={1} try{2}: ShowAt {3:0.0} ms [{4}] | first paint {5:0.0} | paint {6:0.0} | scroll frame {7:0.0} | type {8:0.0}/{9:0.0} clear {10:0.0}",
                            layout == 1 ? "card" : "list", trans, trial + 1, show, steps, paint1, paint2, scrollTotal / 10, type1, type2, clear));
                        shelf.HideNow();
                        Pump(200);
                    }
                }
            }
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
