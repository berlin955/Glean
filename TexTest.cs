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
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(5); } }
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static object Call(object o, string m, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(NP))
            if (mi.Name == m && mi.GetParameters().Length == a.Length) return mi.Invoke(o, a);
        throw new Exception("no method " + m);
    }
    static object Get(object o, string f) { return o.GetType().GetField(f, NP).GetValue(o); }
    static int fails;
    static void Step(string name, Action a)
    {
        try { a(); Pump(120); Out("OK   " + name); }
        catch (Exception ex)
        {
            fails++;
            Exception e = ex; while (e.InnerException != null) e = e.InnerException;
            Out("FAIL " + name + ": " + e.GetType().Name + ": " + e.Message + "\r\n" + e.StackTrace);
        }
    }

    [STAThread]
    static int Main()
    {
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { fails++; Out("UI-EXCEPTION " + e.Exception); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Out("UNHANDLED " + e.ExceptionObject); };
        try
        {
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Glean.Theme.Refresh();
            Application.EnableVisualStyles();
            string dir = Path.Combine(Path.GetTempPath(), "glean-smoke");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Glean.History hist = new Glean.History(dir);
            for (int i = 0; i < 30; i++) hist.AddText("测试文字 " + i + (i % 5 == 0 ? "\nhttps://example.com/?utm_source=x" : ""));
            hist.AddText("#E28A2E");
            hist.AddText("https://github.com/berlin955/Glean");
            string img = Path.Combine(dir, "..", "smoke.png");
            using (Bitmap b = new Bitmap(600, 120))
            {
                using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.White); g.DrawString("Hello Glean OCR 12345", new Font("Segoe UI", 26f), Brushes.Black, 10, 30); }
                MemoryStream ms = new MemoryStream(); b.Save(ms, ImageFormat.Png); byte[] png = ms.ToArray();
                hist.AddImagePng(png, Glean.Util.Md5Short(png), DateTime.Now);
                b.Save(img, ImageFormat.Png);
            }
            string folder = Path.Combine(Path.GetTempPath(), "glean-folder");
            Directory.CreateDirectory(folder);
            File.Copy(img, Path.Combine(folder, "a.png"), true);
            File.WriteAllText(Path.Combine(folder, "note.txt"), "hello");
            hist.AddFiles(new string[] { Path.Combine(folder, "a.png") });
            Glean.FolderStore.Load();

            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            Step("prewarm", delegate { shelf.Prewarm(); });
            foreach (int layout in new int[] { 1, 0 })
            {
                string L = layout == 1 ? "[card] " : "[list] ";
                Glean.Config.Layout = layout;
                Glean.Config.LastFilesMode = false;
                Step(L + "open", delegate { shelf.ShowAt(); });
                Step(L + "quick preview", delegate { Call(shelf, "ShowQuick"); });
                Step(L + "preview next item", delegate { object st = Get(shelf, "strip"); st.GetType().GetProperty("Selected").SetValue(st, 2, null); Call(shelf, "ShowQuick"); });
                Step(L + "delete", delegate { Call(shelf, "DeleteSelected"); });
                Step(L + "undo", delegate { if (!(bool)Call(shelf, "UndoLast")) throw new Exception("undo returned false"); });
                Step(L + "ignore app + undo", delegate { Call(shelf, "IgnoreApp", "testapp", "测试程序"); if (!Glean.Config.IgnoreApps.Contains("testapp")) throw new Exception("not added"); Call(shelf, "UndoLast"); if (Glean.Config.IgnoreApps.Contains("testapp")) throw new Exception("not removed"); });
                Step(L + "search", delegate { ((Glean.SearchBox)Get(shelf, "sb")).Box.Text = "测试"; shelf.Refresh(); ((Glean.SearchBox)Get(shelf, "sb")).Box.Text = ""; });
                Step(L + "folder mode", delegate { Call(shelf, "SetMode", true); shelf.Refresh(); });
                Step(L + "add folder", delegate { Call(shelf, "AddFolders", new object[] { new string[] { folder } }); shelf.Refresh(); });
                Step(L + "folder sizes", delegate { Glean.FileView fv = (Glean.FileView)Get(shelf, "fileView"); foreach (int d in new int[] { 1, 1, 1, -1, -1, -1 }) { fv.StepView(d); fv.Refresh(); } });
                Step(L + "unpin / pin", delegate { Glean.Config.FolderPinned = false; Call(shelf, "ApplyWindowStyle"); Pump(200); Glean.Config.FolderPinned = true; Call(shelf, "ApplyWindowStyle"); });
                Step(L + "back to clipboard", delegate { Call(shelf, "SetMode", false); shelf.Refresh(); });
                Step(L + "hide", delegate { shelf.HideNow(); });
            }
            Glean.Item trk = hist.AddText("【申通快递】您的包裹 773123456789012 已到达驿站");
            Glean.Item num = hist.AddText("78123456789012");
            Step("courier detect", delegate
            {
                if (Glean.Courier.For(trk) == null || Glean.Courier.For(trk).Name != "申通快递") throw new Exception("not detected");
                if (Glean.Courier.For(num) == null) throw new Exception("zto not detected");
            });
            Glean.Config.Layout = 1;
            shelf.ShowAt();
            Step("courier mark + unmark", delegate
            {
                Call(shelf, "MarkCourier", num, Glean.Courier.Companies[3]);
                if (Glean.CardLook.Title(num) != Glean.Courier.Companies[3].Name) throw new Exception("mark failed: " + Glean.CardLook.Title(num));
                Call(shelf, "MarkCourier", num, null);
            });
            Step("item context menus (all kinds)", delegate
            {
                object st = Get(shelf, "strip");
                System.Collections.IList shown = (System.Collections.IList)Get(shelf, "shown");
                for (int i = 0; i < shown.Count; i++)
                {
                    Call(shelf, "ShowItemMenu", i, new Point(200, 200));
                    Pump(60);
                    SendKeys.SendWait("{ESC}");
                    Pump(60);
                }
            });
            Step("tab overflow menu", delegate
            {
                for (int k = 0; k < 6; k++) hist.AddBoard("分类" + k, Glean.Theme.Accents[k % 6].ToArgb());
                Call(shelf, "BuildTabs", 0);
                Glean.Chips tabs = (Glean.Chips)Get(shelf, "tabs");
                tabs.Refresh();
                Call(tabs, "ShowMore"); Pump(80); SendKeys.SendWait("{ESC}");
            });
            Step("share short text", delegate { Call(shelf, "ShareItem", num); Pump(200); Form w = (Form)typeof(Glean.ShelfForm).GetField("shareWin", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); w.Close(); });
            Step("share image (LAN)", delegate
            {
                Glean.Item im = null; foreach (Glean.Item x in hist.Items) if (x.IsImage) im = x;
                Call(shelf, "ShareItem", im); Pump(200);
                Form w = (Form)typeof(Glean.ShelfForm).GetField("shareWin", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                if (!Glean.ShareServer.Running) throw new Exception("server not running");
                w.Close(); Pump(100);
                if (Glean.ShareServer.Running) throw new Exception("server still running after close");
            });
            Step("ocr menu path", delegate
            {
                Glean.Item im = null; foreach (Glean.Item x in hist.Items) if (x.IsImage) im = x;
                Call(shelf, "OcrImage", im.FilePath, false); Pump(3000);
            });
            shelf.HideNow();
            Step("ocr", delegate
            {
                bool nz; string r = Glean.Ocr.Recognize(img, out nz);
                Out("     ocr result: " + r);
                if (!r.Contains("Glean")) throw new Exception("unexpected OCR result");
            });
            Step("settings + live theme", delegate
            {
                Glean.SettingsForm f = new Glean.SettingsForm();
                f.Show(); Pump(200);
                Glean.Segmented seg = (Glean.Segmented)Get(f, "themeSeg");
                foreach (int m in new int[] { 1, 2, 0 }) { seg.Selected = m; Call(f, "PreviewTheme"); f.Refresh(); }
                foreach (Control p in (List<Panel>)Get(f, "pages")) { p.Visible = true; p.Refresh(); p.Visible = false; }
                f.Close();
            });
            Step("update window", delegate
            {
                Glean.UpdateInfo u = new Glean.UpdateInfo();
                u.Tag = "v9.9"; u.Notes = "**新功能**\n- 一\n- 二\n" + new string('长', 900); u.Url = "https://example.com/Glean-Setup.exe";
                Glean.UpdateForm f = new Glean.UpdateForm(u);
                f.Show(); Pump(200); f.Refresh(); f.Close();
            });
            Step("toast while hidden then exit", delegate { hist.FinishPendingDelete(); });
        }
        catch (Exception ex) { fails++; Out("ERROR " + ex); }
        Out("SMOKE DONE, failures = " + fails);
        return 0;
    }
}
