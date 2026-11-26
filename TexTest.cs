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
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr d, int x, int y, int w, int h, IntPtr s, int sx, int sy, int op);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    static void Pump(int ms) { Stopwatch w = Stopwatch.StartNew(); while (w.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(5); } }
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Shot(string name, Rectangle r)
    {
        using (Bitmap b = new Bitmap(r.Width, r.Height))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                IntPtr dst = g.GetHdc(); IntPtr src = GetDC(IntPtr.Zero);
                BitBlt(dst, 0, 0, r.Width, r.Height, src, r.X, r.Y, 0x00CC0020 | 0x40000000);
                ReleaseDC(IntPtr.Zero, src); g.ReleaseHdc(dst);
            }
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
        Glean.Embedded.Hook();
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Out("UI-EXCEPTION " + e.Exception); };
        try
        {
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Application.EnableVisualStyles();
            Out("default popup " + Glean.Config.UpdatePopup);
            Glean.UpdateInfo u = new Glean.UpdateInfo();
            u.Tag = "v0.36";
            u.Notes = "## 0.36\n\n- **流光动画引擎**：面板、卡片、预览的动画全部换成弹簧物理\n- 修复若干问题";
            foreach (int mode in new int[] { 1, 2 })
            {
                Glean.Config.ThemeMode = mode; Glean.Theme.Refresh();
                bool clicked = false;
                Glean.UpdateToast.Pop(u, delegate { clicked = true; });
                Pump(700);
                Form f = null;
                foreach (Form x in Application.OpenForms) if (x is Glean.UpdateToast) f = x;
                Rectangle r = f.Bounds; r.Inflate(6, 6);
                Shot("toast-" + mode, r);
                f.Close(); Pump(200);
            }
            Glean.Config.ThemeMode = 1; Glean.Theme.Refresh();
            Glean.SettingsForm sf = new Glean.SettingsForm();
            object side = typeof(Glean.SettingsForm).GetField("side", NP).GetValue(sf);
            side.GetType().GetField("Selected").SetValue(side, 3);
            typeof(Glean.SettingsForm).GetMethod("ShowPage", NP).Invoke(sf, null);
            sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(0, 0);
            sf.Show(); Pump(800);
            Shot("settings", sf.Bounds);
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        Out("DONE");
        return 0;
    }
}
