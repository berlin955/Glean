using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
static class TexTest
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetGuiResources(IntPtr h, int flags);
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static string step = "";
    static double lastBeat; static double worstStall; static string worstStep = "";
    static readonly Dictionary<string, int> firstChance = new Dictionary<string, int>();
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static readonly Stopwatch clock = Stopwatch.StartNew();
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(2); } }
    static int ok, fail;
    static void Step(string name, Action a)
    {
        step = name;
        try { a(); Pump(120); ok++; }
        catch (Exception ex) { fail++; Exception e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex; Out("STEP-FAIL " + name + ": " + e.GetType().Name + ": " + e.Message + " @ " + Top(e)); }
    }
    static string Top(Exception e)
    {
        string st = e.StackTrace ?? "";
        foreach (string l in st.Split('\n')) if (l.Contains("Glean.")) return l.Trim();
        return st.Split('\n')[0].Trim();
    }
    static void Key(Form f, Keys k)
    {
        Message m = Message.Create(f.Handle, 0x100, (IntPtr)(int)(k & Keys.KeyCode), IntPtr.Zero);
        object[] args = new object[] { m, k };
        f.GetType().GetMethod("ProcessCmdKey", NP).Invoke(f, args);
    }
    static object F(object o, string name) { return o.GetType().GetField(name, NP).GetValue(o); }
    static object M(object o, string name, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(NP)) if (mi.Name == name && mi.GetParameters().Length == a.Length) return mi.Invoke(o, a);
        throw new Exception("no method " + name);
    }
    static void CloseMenus()
    {
        foreach (Form f in Application.OpenForms.Cast<Form>().ToList()) { }
        foreach (ToolStripDropDown d in OpenDropDowns()) d.Close();
    }
    static List<ToolStripDropDown> OpenDropDowns()
    {
        List<ToolStripDropDown> l = new List<ToolStripDropDown>();
        try
        {
            Type t = typeof(ToolStripManager);
            // 兜底:按 Esc 关掉弹出菜单
            SendKeys.SendWait("{ESC}");
        }
        catch { }
        return l;
    }
    static Bitmap QrImage(string text)
    {
        bool[,] m = Glean.QrCode.Encode(Encoding.UTF8.GetBytes(text));
        int n = m.GetLength(0), sc = 5, q = 4;
        Bitmap b = new Bitmap((n + 2 * q) * sc + 200, (n + 2 * q) * sc + 100);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.Clear(Color.White);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) if (m[y, x]) g.FillRectangle(Brushes.Black, 100 + (x + q) * sc, 50 + (y + q) * sc, sc, sc);
        }
        return b;
    }
    static Glean.Item AddImg(Glean.History h, Bitmap b)
    {
        using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); byte[] png = ms.ToArray(); return h.AddImagePng(png, Glean.Util.Md5Short(png), DateTime.Now); }
    }
    static int Gdi() { return GetGuiResources(Process.GetCurrentProcess().Handle, 0); }
    static int User() { return GetGuiResources(Process.GetCurrentProcess().Handle, 1); }

    [STAThread]
    static int Main()
    {
        Glean.Embedded.Hook();
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { fail++; Out("UI-EXCEPTION during [" + step + "] " + e.Exception.GetType().Name + ": " + e.Exception.Message + " @ " + Top(e.Exception)); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.FirstChanceException += delegate(object s, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            try
            {
                string st = new StackTrace(1, false).ToString();
                string where = "";
                foreach (string l in st.Split('\n')) if (l.Contains("Glean.")) { where = l.Trim(); break; }
                if (where.Length == 0) return;
                string k = "[" + step + "] " + e.Exception.GetType().Name + ": " + e.Exception.Message.Split('\n')[0] + " @ " + where;
                lock (firstChance) { int c; firstChance.TryGetValue(k, out c); firstChance[k] = c + 1; }
            }
            catch { }
        };
        System.Windows.Forms.Timer beat = new System.Windows.Forms.Timer();
        beat.Interval = 10;
        beat.Tick += delegate
        {
            double now = clock.Elapsed.TotalMilliseconds;
            if (lastBeat > 0 && now - lastBeat > worstStall) { worstStall = now - lastBeat; worstStep = step; }
            if (lastBeat > 0 && now - lastBeat > 250) Out("STALL " + (int)(now - lastBeat) + "ms during [" + step + "]");
            lastBeat = now;
        };
        beat.Start();
        try { Run(); } catch (Exception ex) { Out("ERROR " + ex); }
        Out("RESULT ok " + ok + " fail " + fail + "  worst UI stall " + (int)worstStall + "ms during [" + worstStep + "]");
        foreach (KeyValuePair<string, int> kv in firstChance.OrderByDescending(x => x.Value)) Out("FIRST-CHANCE x" + kv.Value + " " + kv.Key);
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
        string dir = Path.Combine(Path.GetTempPath(), "glean-smoke");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Glean.History hist = new Glean.History(dir);
        string tmpf = Path.Combine(Path.GetTempPath(), "smoke-file.txt"); File.WriteAllText(tmpf, "hello 文件内容");
        string tmpimg = Path.Combine(Path.GetTempPath(), "smoke-img.png"); using (Bitmap b = QrImage("https://example.com/file")) b.Save(tmpimg, ImageFormat.Png);
        Glean.Item it;
        it = hist.AddText("普通文字 " + new string('字', 300));
        it = hist.AddText("https://www.example.com/path?q=1");
        it = hist.AddText("顺丰快递 SF1234567890123 已发货");
        it = hist.AddText("您的域名将于 " + DateTime.Today.AddDays(10).ToString("yyyy年M月d日") + "到期，请及时续费");
        it = hist.AddText("public static void Main() { Console.WriteLine(\"hi\"); }");
        it = hist.AddText("13800138000");
        it = hist.AddText("someone@example.com");
        it = hist.AddText("#FF8800");
        it = hist.AddFiles(new string[] { tmpf, tmpimg });
        it = AddImg(hist, QrImage("WIFI:S:Test;T:WPA;P:pass123;;"));
        it = AddImg(hist, new Bitmap(1600, 900));
        Glean.ShelfForm shelf = null;
        int gdi0 = 0, user0 = 0;

        Step("create shelf", delegate { shelf = new Glean.ShelfForm(null, hist); shelf.Prewarm(); });
        Step("show card mode", delegate { shelf.ShowAt(); Pump(900); });
        object cv = F(shelf, "cardView");
        Step("arrow keys", delegate { for (int i = 0; i < 12; i++) { Key(shelf, Keys.Right); Pump(30); } for (int i = 0; i < 5; i++) { Key(shelf, Keys.Left); Pump(30); } });
        Step("preview each item", delegate
        {
            cv.GetType().GetProperty("Selected").SetValue(cv, 0, null);
            M(shelf, "TogglePreview"); Pump(400);
            for (int i = 0; i < 11; i++) { Key(shelf, Keys.Right); Pump(250); }
            M(shelf, "TogglePreview"); Pump(300);
        });
        Step("item menus", delegate
        {
            for (int i = 0; i < 11; i++) { M(shelf, "ShowItemMenu", i, new Point(300, 300)); Pump(120); SendKeys.SendWait("{ESC}"); Pump(80); }
        });
        Step("pin / unpin", delegate { cv.GetType().GetProperty("Selected").SetValue(cv, 1, null); Key(shelf, Keys.Control | Keys.P); Pump(200); Key(shelf, Keys.Control | Keys.P); });
        Step("delete + undo", delegate { cv.GetType().GetProperty("Selected").SetValue(cv, 2, null); Key(shelf, Keys.Delete); Pump(300); Key(shelf, Keys.Control | Keys.Z); Pump(300); });
        Step("cycle tabs", delegate { for (int i = 0; i < 8; i++) { Key(shelf, Keys.Control | Keys.Tab); Pump(150); } });
        Step("search", delegate
        {
            Glean.SearchBox sb = (Glean.SearchBox)F(shelf, "sb");
            sb.Box.Text = "顺丰"; Pump(300); sb.Box.Text = "zzzz不存在"; Pump(300); sb.Box.Text = ""; Pump(300);
        });
        Step("zoom", delegate { M(shelf, "SetZoom", 1.4f, true); Pump(300); M(shelf, "SetZoom", 0.8f, true); Pump(300); M(shelf, "SetZoom", 1f, true); Pump(300); });
        Step("hide/show x20 (leak check)", delegate
        {
            shelf.HideNow(); Pump(200); shelf.ShowAt(); Pump(700); shelf.HideNow(); Pump(300);
            GC.Collect(); GC.WaitForPendingFinalizers(); Pump(200);
            gdi0 = Gdi(); user0 = User();
            for (int i = 0; i < 20; i++)
            {
                shelf.ShowAt(); Pump(500);
                cv.GetType().GetProperty("Selected").SetValue(cv, i % 9, null);
                M(shelf, "TogglePreview"); Pump(200); M(shelf, "TogglePreview"); Pump(100);
                shelf.HideShelf(); Pump(350);
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); Pump(300);
            Out("LEAK gdi " + gdi0 + " -> " + Gdi() + ", user " + user0 + " -> " + User() + ", mem " + (Process.GetCurrentProcess().PrivateMemorySize64 >> 20) + "MB");
        });
        Step("files mode", delegate
        {
            shelf.ShowAt(); Pump(600);
            M(shelf, "SetMode", true); Pump(500);
            M(shelf, "AddFolders", new List<string> { Path.GetTempPath() }); Pump(800);
            Key(shelf, Keys.Right); Key(shelf, Keys.Right); Pump(200);
            M(shelf, "ShowFileQuick"); Pump(400);
            M(shelf, "SetMode", false); Pump(500);
        });
        Step("list mode", delegate
        {
            shelf.HideNow(); Glean.Config.Layout = 0; Pump(100);
            shelf.ShowAt(); Pump(600);
            for (int i = 0; i < 6; i++) { Key(shelf, Keys.Down); Pump(40); }
            M(shelf, "TogglePreview"); Pump(400); M(shelf, "TogglePreview"); Pump(200);
            shelf.HideNow(); Glean.Config.Layout = 1;
        });
        Step("light theme", delegate { Glean.Config.ThemeMode = 1; Glean.Theme.Refresh(); shelf.ShowAt(); Pump(700); shelf.HideNow(); });
        Step("qr overlay", delegate
        {
            Glean.ScanOverlay.Start(); Pump(2500);
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList()) if (f is Glean.ScanOverlay) f.Close();
            Pump(200);
        });
        Step("timeline", delegate
        {
            Glean.Timeline.Open(); Pump(600);
            Form tw = Application.OpenForms.Cast<Form>().First(f => f is Glean.TimelineForm);
            Glean.Reminder r = new Glean.Reminder(); r.Text = "测试提醒"; r.Due = DateTime.Today.AddDays(3); r.At = DateTime.Now.AddMinutes(-1); r.Label = "到期";
            Glean.Timeline.Load(); Glean.Timeline.Items.Add(r); Glean.Timeline.Save(); Pump(300);
            Glean.Timeline.Tick(); Pump(800);
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList()) if (f is Glean.AlertCard) f.Close();
            Glean.Timeline.Complete(r, true); Pump(200); Glean.Timeline.Remove(r); Pump(200);
            Glean.DueDate.Hit h = Glean.DueDate.Find("您的域名将于 " + DateTime.Today.AddDays(10).ToString("yyyy年M月d日") + "到期");
            Glean.Capsule.Ask("测试", "App", h); Pump(800);
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList()) if (f is Glean.Capsule) f.Close();
            tw.Close();
        });
        Step("settings all pages + OK", delegate
        {
            Glean.SettingsForm sf = new Glean.SettingsForm();
            sf.Show(); Pump(500);
            object side = F(sf, "side");
            for (int pg = 0; pg < 7; pg++) { side.GetType().GetField("Selected").SetValue(side, pg); M(sf, "ShowPage"); Pump(250); }
            M(sf, "OnOk"); Pump(300);
            if (!sf.IsDisposed) sf.Close();
        });
        Step("update toast", delegate
        {
            Glean.UpdateInfo u = new Glean.UpdateInfo(); u.Tag = "v9.9"; u.Notes = "## 9.9\n- 测试";
            Glean.UpdateToast.Pop(u, null); Pump(600);
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList()) if (f is Glean.UpdateToast) f.Close();
        });
        Step("clipboard image", delegate { Clipboard.SetDataObject(Glean.ClipImage.Build(tmpimg), true); });
        Step("final show", delegate { shelf.ShowAt(); Pump(600); shelf.HideNow(); });
    }
}
