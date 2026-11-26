using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
static class TexTest
{
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(1); } }
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static string Stats(List<double> l) { if (l.Count == 0) return "n=0"; l.Sort(); return "n=" + l.Count + " avg " + l.Average().ToString("0.0") + " p50 " + l[l.Count / 2].ToString("0.0") + " max " + l[l.Count - 1].ToString("0.0"); }
    [STAThread]
    static int Main()
    {
        Glean.Embedded.Hook();
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Out("UI-EXCEPTION " + e.Exception); };
        try { Run(); } catch (Exception ex) { Out("ERROR " + ex); }
        Out("DONE");
        return 0;
    }
    static void Run()
    {
        Glean.Native.SetProcessDPIAware();
        Glean.Gfx.InitDpi();
        Application.EnableVisualStyles();
        Glean.Frames.ForceOn = true;
        string dir = Path.Combine(Path.GetTempPath(), "glean-perf2");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Glean.History hist = new Glean.History(dir);
        for (int i = 0; i < 30; i++) hist.AddText("第 " + i + " 条:用来测动画性能的一段文字,内容稍微长一点,看看每一帧要画多久。");
        foreach (int glass in new int[] { 0, 35 })
        {
            Glean.Config.ThemeMode = 2; Glean.Config.ShelfTransparency = glass; Glean.Config.Layout = 1;
            Glean.Theme.Refresh();
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            shelf.Prewarm(); Pump(300);
            // 面板滑出:每帧间隔
            List<double> gaps = new List<double>();
            int lastTop = -1; double last = 0;
            Stopwatch sw = Stopwatch.StartNew();
            shelf.ShowAt();
            while (sw.ElapsedMilliseconds < 800) { Application.DoEvents(); if (shelf.Top != lastTop) { double n = sw.Elapsed.TotalMilliseconds; if (last > 0) gaps.Add(n - last); last = n; lastTop = shelf.Top; } }
            Out("[glass " + glass + "] slide frame gaps: " + Stats(gaps));
            Pump(500);
            Control cv = (Control)typeof(Glean.ShelfForm).GetField("cardView", NP).GetValue(shelf);
            // 卡片区整块重画一次要多久
            List<double> paint = new List<double>();
            for (int i = 0; i < 30; i++) { Stopwatch p = Stopwatch.StartNew(); cv.Invalidate(); cv.Update(); paint.Add(p.Elapsed.TotalMilliseconds); }
            Out("[glass " + glass + "] card strip full paint: " + Stats(paint));
            // 悬停时每一帧的间隔(真实运行)
            List<double> hov = new List<double>();
            double lt = 0;
            cv.Paint += delegate { double n = sw.Elapsed.TotalMilliseconds; if (lt > 0) hov.Add(n - lt); lt = n; };
            FieldInfo hover = cv.GetType().GetField("hover", NP);
            MethodInfo kick = cv.GetType().GetMethod("Kick", NP);
            for (int k = 0; k < 6; k++)
            {
                hover.SetValue(cv, k % 2 == 0 ? 2 : 4); kick.Invoke(cv, null);
                lt = 0; Pump(250);
            }
            Out("[glass " + glass + "] hover paint gaps: " + Stats(hov));
            // 预览形变:每帧渲染要多久
            cv.GetType().GetProperty("Selected").SetValue(cv, 1, null);
            Pump(100);
            Stopwatch sq = Stopwatch.StartNew();
            typeof(Glean.ShelfForm).GetMethod("ShowQuick", NP).Invoke(shelf, null);
            Out("[glass " + glass + "] ShowQuick (snapshot + bake + first frame): " + sq.ElapsedMilliseconds + " ms");
            Pump(10);
            object mw = typeof(Glean.MorphWindow).GetField("current", NP).GetValue(null);
            Glean.Frames.Remove((Func<double, bool>)mw.GetType().GetField("stepFn", NP).GetValue(mw));
            object spr = mw.GetType().GetField("sp", NP).GetValue(mw);
            MethodInfo render = mw.GetType().GetMethod("Render", NP);
            List<double> mr = new List<double>();
            for (int i = 0; i < 40; i++) { ((Glean.Spring)spr).Value = (i % 20) / 20.0; Stopwatch p = Stopwatch.StartNew(); render.Invoke(mw, null); mr.Add(p.Elapsed.TotalMilliseconds); }
            Out("[glass " + glass + "] morph frame render: " + Stats(mr));
            Glean.MorphWindow.Cancel(); Pump(200);
            Glean.QuickLook ql = (Glean.QuickLook)typeof(Glean.ShelfForm).GetField("quick", NP).GetValue(shelf);
            ql.HideLook();
            Pump(200);
            // 真实打开预览:形变每帧间隔
            Glean.MorphWindow.Trace = new List<string>();
            typeof(Glean.ShelfForm).GetMethod("ShowQuick", NP).Invoke(shelf, null);
            Pump(700);
            Out("[glass " + glass + "] morph open trace: " + string.Join(" ", Glean.MorphWindow.Trace.ToArray()));
            Glean.MorphWindow.Trace = null;
            Out("frame source " + Glean.Frames.Mode);
            shelf.HideNow(); shelf.Dispose(); Pump(300);
        }
    }
}
