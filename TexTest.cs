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
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(5); } }
    static void Shot(Control c, string tag, int w, int h)
    {
        using (Bitmap b = new Bitmap(w, h))
        {
            c.DrawToBitmap(b, new Rectangle(0, 0, w, h));
            using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); Out("PNG" + tag + " " + Convert.ToBase64String(ms.ToArray())); }
        }
    }
    [STAThread]
    static int Main()
    {
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Out("UI-EXCEPTION " + e.Exception); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        try
        {
            string[] samples = {
                "773123456789012",
                "78123456789012",
                "4312345678901",
                "9812345678901",
                "YT7652500323998",
                "【申通快递】您的包裹 773123456789012 已到达XX驿站,请凭取件码 3-2-1056 取件",
                "顺丰 SF1234567890123 明天送到",
                "您的快递已发出,运单号:4612345678901,韵达快递",
                "中通快递 75312345678901 派送中",
                "邮政包裹 1123456789012 已签收",
                "订单号 773123456789012 已付款",
                "1730600000000",
                "13812345678",
                "6222021234567890123",
                "今天下午三点开会",
                "https://github.com/berlin955/Glean"
            };
            foreach (string t in samples)
            {
                Glean.Courier.Hit h = Glean.Courier.Find(t, true);
                Out("FIND " + t.Replace("\n", " ") + "  =>  " + (h == null ? "-" : h.Company.Name + " " + h.Number + (h.Whole ? " (whole)" : " (in text)")));
            }

            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Glean.Config.ShelfTransparency = 0;
            Glean.Config.ThemeMode = 2;
            Glean.Theme.Refresh();
            Application.EnableVisualStyles();
            string dir = Path.Combine(Path.GetTempPath(), "glean-courier2");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Glean.History hist = new Glean.History(dir);
            hist.AddText("今天下午三点开会");
            Glean.Item q = hist.AddText("来自QQ的文字");
            hist.SetSource(q, @"C:\Windows\explorer.exe", "资源管理器");
            hist.AddText("【申通快递】您的包裹 773123456789012 已到达XX驿站,请凭取件码 3-2-1056 取件");
            hist.AddText("78123456789012");
            hist.AddText("773123456789012");
            hist.AddText("SF1234567890123");
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            Glean.Config.Layout = 1;
            shelf.ShowAt();
            Pump(900);
            Control cards = (Control)typeof(Glean.ShelfForm).GetField("cardView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shelf);
            Shot(cards, "CARD", Math.Min(cards.Width, Glean.Gfx.S(1020)), cards.Height);
            shelf.HideNow();
            Glean.Config.Layout = 0;
            shelf.ShowAt();
            Pump(900);
            Control list = (Control)typeof(Glean.ShelfForm).GetField("listView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shelf);
            Shot(list, "LIST", list.Width, Math.Min(list.Height, Glean.Gfx.S(420)));
            shelf.HideNow();
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
