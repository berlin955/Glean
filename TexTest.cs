using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
static class TexTest
{
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(2); } }
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object F(object o, string name) { return o.GetType().GetField(name, NP).GetValue(o); }
    static object M(object o, string name, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(NP)) if (mi.Name == name && mi.GetParameters().Length == a.Length) return mi.Invoke(o, a);
        throw new Exception("no method " + name);
    }
    static void Key(Form f, Keys k)
    {
        Message m = Message.Create(f.Handle, 0x100, (IntPtr)(int)(k & Keys.KeyCode), IntPtr.Zero);
        f.GetType().GetMethod("ProcessCmdKey", NP).Invoke(f, new object[] { m, k });
    }
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
        Glean.Config.ThemeMode = 2; Glean.Config.ShelfTransparency = 35; Glean.Config.Layout = 1;
        Glean.Theme.Refresh();
        string dir = Path.Combine(Path.GetTempPath(), "glean-big");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Stopwatch sw = Stopwatch.StartNew();
        Glean.History h = new Glean.History(dir);
        Random rnd = new Random(3);
        for (int i = 0; i < 3000; i++) h.AddText("记录 " + i + " " + (i % 7 == 0 ? "https://example.com/" + i : "一段普通的文字内容,关键词" + (i % 50)) + new string('字', rnd.Next(10, 400)));
        for (int i = 0; i < 200; i++)
            using (Bitmap b = new Bitmap(1280, 720))
            {
                using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.FromArgb(rnd.Next(255), rnd.Next(255), rnd.Next(255))); g.FillEllipse(Brushes.White, 100, 100, 400, 300); }
                using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); byte[] png = ms.ToArray(); h.AddImagePng(png, Glean.Util.Md5Short(png), DateTime.Now); }
            }
        h.FinishPendingDelete();
        Out("setup " + sw.ElapsedMilliseconds + "ms");
        sw.Restart();
        Glean.History hist = new Glean.History(dir);
        Out("load 3200 items: " + sw.ElapsedMilliseconds + "ms (" + hist.Items.Count + ")");
        Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
        sw.Restart(); shelf.Prewarm(); Out("prewarm " + sw.ElapsedMilliseconds + "ms");
        Pump(1500);
        sw.Restart(); shelf.ShowAt(); shelf.Update(); Out("ShowAt " + sw.ElapsedMilliseconds + "ms");
        Pump(800);
        Glean.SearchBox sb = (Glean.SearchBox)F(shelf, "sb");
        foreach (string q in new string[] { "关", "关键", "关键词1", "关键词12", "example", "zzz", "" })
        {
            sw.Restart(); sb.Box.Text = q; Application.DoEvents(); shelf.Update();
            Out("search '" + q + "': " + sw.ElapsedMilliseconds + "ms");
            Pump(200);
        }
        for (int i = 0; i < 5; i++) { sw.Restart(); Key(shelf, Keys.Control | Keys.Tab); Application.DoEvents(); shelf.Update(); Out("tab switch: " + sw.ElapsedMilliseconds + "ms"); Pump(200); }
        sw.Restart(); for (int i = 0; i < 40; i++) { Key(shelf, Keys.Right); Application.DoEvents(); } Out("40 x right: " + sw.ElapsedMilliseconds + "ms");
        sw.Restart(); Key(shelf, Keys.End); Application.DoEvents(); shelf.Update(); Out("End: " + sw.ElapsedMilliseconds + "ms");
        sw.Restart(); shelf.HideNow(); shelf.ShowAt(); shelf.Update(); Out("reopen: " + sw.ElapsedMilliseconds + "ms");
        Pump(500);
        // 新复制一条:要多久能出现在面板里
        sw.Restart(); Glean.Item it = hist.AddText("新复制的一条"); M(shelf, "Reload", 0); shelf.Update(); Out("add+reload: " + sw.ElapsedMilliseconds + "ms");
        Out("mem " + (Process.GetCurrentProcess().PrivateMemorySize64 >> 20) + "MB");
    }
}
