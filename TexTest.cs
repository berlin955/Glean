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
    static void Key(Form f, Keys k)
    {
        Message m = Message.Create(f.Handle, 0x100, (IntPtr)(int)k, IntPtr.Zero);
        MethodInfo pk = typeof(Glean.ShelfForm).GetMethod("ProcessCmdKey", NP);
        object[] a = new object[] { m, k };
        Out("KEY " + k + " handled=" + pk.Invoke(f, a));
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
            string dir = Path.Combine(Path.GetTempPath(), "glean-keys");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Glean.History hist = new Glean.History(dir);
            for (int i = 0; i < 6; i++) hist.AddText("卡片内容 " + i);
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            shelf.ShowAt(); Pump(900);
            Control cv = (Control)typeof(Glean.ShelfForm).GetField("cardView", NP).GetValue(shelf);
            PropertyInfo selP = cv.GetType().GetProperty("Selected");
            FieldInfo hover = cv.GetType().GetField("hover", NP);
            Control quick = (Control)typeof(Glean.ShelfForm).GetField("quick", NP).GetValue(shelf);
            cv.Focus();
            Out("start sel=" + selP.GetValue(cv, null));
            hover.SetValue(cv, 2);
            Key(shelf, Keys.Space); Pump(500);
            Out("after space sel=" + selP.GetValue(cv, null) + " quickVisible=" + quick.Visible);
            Key(shelf, Keys.Escape); Pump(300);
            // 键盘移动后,悬停不再生效
            Key(shelf, Keys.Right); Pump(100);
            Out("after right sel=" + selP.GetValue(cv, null));
            Thread.Sleep(300);
            Key(shelf, Keys.Space); Pump(500);
            Out("kbd space sel=" + selP.GetValue(cv, null) + " quickVisible=" + quick.Visible);
            Key(shelf, Keys.Escape); Pump(300);
            // 鼠标重新移动到第 4 张,按 Delete
            FieldInfo kb = cv.GetType().GetField("kbdMoved", NP); kb.SetValue(cv, false);
            hover.SetValue(cv, 4);
            int before = hist.Items.Count;
            Key(shelf, Keys.Delete); Pump(400);
            Out("delete count " + before + " -> " + hist.Items.Count);
            Key(shelf, Keys.Z | Keys.Control); Pump(300);
            Out("undo count " + hist.Items.Count);
            shelf.Hide();
            Glean.SettingsForm sf = new Glean.SettingsForm();
            object side = typeof(Glean.SettingsForm).GetField("side", NP).GetValue(sf);
            side.GetType().GetField("Selected").SetValue(side, 3);
            typeof(Glean.SettingsForm).GetMethod("ShowPage", NP).Invoke(sf, null);
            sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(0, 0);
            sf.Show(); Pump(900);
            Shot("keys", sf.Bounds);
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        Out("DONE");
        return 0;
    }
}
