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
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static void Shot(string name, Rectangle r)
    {
        using (Bitmap b = new Bitmap(r.Width, r.Height))
        {
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
            using (MemoryStream ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                string all = Convert.ToBase64String(ms.ToArray());
                for (int k = 0, part = 0; k < all.Length; k += 6000, part++)
                    Out("PNGPART " + name + " " + part.ToString("000") + " " + all.Substring(k, Math.Min(6000, all.Length - k)));
            }
        }
    }
    [STAThread]
    static int Main()
    {
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Out("UI-EXCEPTION " + e.Exception); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        try
        {
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Application.EnableVisualStyles();
            Glean.Config.ThemeMode = 1; Glean.Config.ShelfTransparency = 0; Glean.Config.Layout = 1;
            Glean.Theme.Refresh();
            string dir = Path.Combine(Path.GetTempPath(), "glean-hover");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Glean.History hist = new Glean.History(dir);
            for (int i = 0; i < 6; i++) hist.AddText("卡片内容 " + i + "\n第二行");
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            shelf.ShowAt(); Pump(900);
            Control cv = (Control)typeof(Glean.ShelfForm).GetField("cardView", NP).GetValue(shelf);
            FieldInfo hover = cv.GetType().GetField("hover", NP);
            MethodInfo kick = cv.GetType().GetMethod("Kick", NP);
            FieldInfo selF = cv.GetType().GetField("selected", NP);
            selF.SetValue(cv, 1); cv.Invalidate(); Pump(400);
            foreach (int pct in new int[] { 0, 100, 200 })
            {
                Glean.Config.HoverFloat = pct;
                shelf.Hide(); shelf.ShowAt(); Pump(900);
                selF.SetValue(cv, 1);
                hover.SetValue(cv, 1);
                kick.Invoke(cv, null);
                Pump(600);
                Rectangle sb = Screen.PrimaryScreen.Bounds;
                Rectangle r = new Rectangle(shelf.Left - 4, shelf.Top - 4, Math.Min(shelf.Width, Glean.Gfx.S(720)), sb.Bottom - shelf.Top + 4);
                Out("SHELF " + shelf.Bounds + " WA " + Screen.PrimaryScreen.WorkingArea);
                Shot("hover-" + pct, r);
                hover.SetValue(cv, -1); kick.Invoke(cv, null); Pump(400);
            }
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        Out("DONE");
        return 0;
    }
}
