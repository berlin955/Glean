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
    [STAThread]
    static int Main()
    {
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Out("UI-EXCEPTION " + e.Exception); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        try
        {
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Glean.Config.ShelfTransparency = 0;
            Glean.Config.ThemeMode = 2;
            Glean.Theme.Refresh();
            Application.EnableVisualStyles();
            string dir = Path.Combine(Path.GetTempPath(), "glean-courier");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Glean.History hist = new Glean.History(dir);
            Glean.Item z = hist.AddText("78123456789012");
            hist.SetTitle(z, "中通快递");
            hist.AddText("SF1234567890123");
            hist.AddText("YT7652500323998");
            foreach (Glean.Item it in hist.Items)
            {
                Glean.Courier.Company c = Glean.Courier.For(it);
                Out("ITEM " + it.Text + " kind=" + it.Kind + " title=" + Glean.CardLook.Title(it) + " courier=" + (c == null ? "-" : c.Name + "/" + c.Mark) + " looksLike=" + Glean.Courier.LooksLikeNumber(it.Text));
            }
            Out("LOOKS bank-like 6222021234567890123: " + Glean.Courier.LooksLikeNumber("6222021234567890123") + " / hello: " + Glean.Courier.LooksLikeNumber("hello world"));
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            shelf.ShowAt();
            Pump(900);
            Control strip = (Control)typeof(Glean.ShelfForm).GetField("cardView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shelf);
            int w = Math.Min(strip.Width, Glean.Gfx.S(760)), h = strip.Height;
            using (Bitmap b = new Bitmap(w, h))
            {
                strip.DrawToBitmap(b, new Rectangle(0, 0, w, h));
                using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); Out("PNG " + Convert.ToBase64String(ms.ToArray())); }
            }
            shelf.HideNow();
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
