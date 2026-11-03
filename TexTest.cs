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
    [STAThread]
    static int Main()
    {
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Out("UI-EXCEPTION " + e.Exception); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        try
        {
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Glean.Theme.Refresh();
            Application.EnableVisualStyles();
            string dir = Path.Combine(Path.GetTempPath(), "glean-share");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Glean.History hist = new Glean.History(dir);
            Glean.Item tItem = hist.AddText("YT7652500323998");
            Glean.Item tLong = hist.AddText(new string('长', 400));
            byte[] png;
            using (Bitmap b = new Bitmap(300, 200)) { using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.SkyBlue); MemoryStream ms = new MemoryStream(); b.Save(ms, ImageFormat.Png); png = ms.ToArray(); }
            Glean.Item tImg = hist.AddImagePng(png, Glean.Util.Md5Short(png), DateTime.Now);
            Out("kind of YT item: " + tItem.Kind + " title: " + Glean.CardLook.Title(tItem));
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            MethodInfo share = typeof(Glean.ShelfForm).GetMethod("ShareItem", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo win = typeof(Glean.ShelfForm).GetField("shareWin", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (Glean.Item it in new Glean.Item[] { tItem, tLong, tImg })
            {
                share.Invoke(shelf, new object[] { it });
                Pump(300);
                Form f = (Form)win.GetValue(null);
                Out("SHAREWIN " + (it == tItem ? "short text" : it == tLong ? "long text" : "image") + ": visible=" + f.Visible + " size=" + f.ClientSize + " server=" + Glean.ShareServer.Running);
                using (Bitmap shot = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(shot, new Rectangle(0, 0, f.Width, f.Height)); shot.Save("qr/win-" + Array.IndexOf(new Glean.Item[] { tItem, tLong, tImg }, it) + ".png"); }
                f.Close();
                Pump(200);
                Out("   after close: server=" + Glean.ShareServer.Running);
            }
            // 截图里的二维码能不能扫出来
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
