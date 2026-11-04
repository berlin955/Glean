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
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string f) { return o.GetType().GetField(f, NP).GetValue(o); }
    static object Call(object o, string m, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(NP)) if (mi.Name == m && mi.GetParameters().Length == a.Length) return mi.Invoke(o, a);
        throw new Exception("no method " + m);
    }
    static int n;
    static void Shot(string name, Rectangle r)
    {
        Pump(350);
        r.Intersect(Screen.PrimaryScreen.Bounds);
        if (r.Width < 4 || r.Height < 4) { Out("SKIP " + name); return; }
        using (Bitmap b = new Bitmap(r.Width, r.Height))
        {
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
            using (MemoryStream ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                string all = Convert.ToBase64String(ms.ToArray());
                string id = (n++).ToString("00") + "-" + name;
                for (int k = 0, part = 0; k < all.Length; k += 6000, part++)
                    Out("PNGPART " + id + " " + part.ToString("000") + " " + all.Substring(k, Math.Min(6000, all.Length - k)));
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
            Out("START");
            Glean.Native.SetProcessDPIAware();
            Glean.Gfx.InitDpi();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // 桌面背景:一个花一点的窗口,方便看毛玻璃
            Form bg = new Form(); bg.FormBorderStyle = FormBorderStyle.None; bg.Bounds = Screen.PrimaryScreen.Bounds; bg.ShowInTaskbar = false;
            bg.Paint += delegate(object s, PaintEventArgs e)
            {
                using (System.Drawing.Drawing2D.LinearGradientBrush lb = new System.Drawing.Drawing2D.LinearGradientBrush(bg.ClientRectangle, Color.FromArgb(70, 110, 170), Color.FromArgb(230, 170, 120), 30f))
                    e.Graphics.FillRectangle(lb, bg.ClientRectangle);
                e.Graphics.FillRectangle(Brushes.White, 120, 380, 380, 300);
                e.Graphics.DrawString("背景里的窗口 Background window", new Font("Microsoft YaHei UI", 16f), Brushes.Black, 140, 400);
            };
            bg.Show();
            Pump(300);

            string dir = Path.Combine(Path.GetTempPath(), "glean-ui");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            string folder = Path.Combine(Path.GetTempPath(), "glean-ui-folder");
            Directory.CreateDirectory(folder);
            Random rnd = new Random(2);
            for (int i = 0; i < 6; i++)
                using (Bitmap b = new Bitmap(800, 520))
                {
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.FromArgb(rnd.Next(80, 220), rnd.Next(80, 220), rnd.Next(80, 220)));
                        for (int k = 0; k < 12; k++) using (SolidBrush br = new SolidBrush(Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)))) g.FillEllipse(br, rnd.Next(700), rnd.Next(420), 160, 120);
                        g.DrawString("照片 " + i, new Font("Microsoft YaHei UI", 40f), Brushes.White, 30, 30);
                    }
                    b.Save(Path.Combine(folder, "照片" + i + ".png"), ImageFormat.Png);
                }
            File.WriteAllText(Path.Combine(folder, "会议纪要.txt"), "第一项\n第二项");
            Directory.CreateDirectory(Path.Combine(folder, "子文件夹"));

            Glean.History hist = new Glean.History(dir);
            hist.AddFiles(new string[] { Path.Combine(folder, "会议纪要.txt") });
            hist.AddText("#E28A2E");
            hist.AddText("https://github.com/berlin955/Glean/releases");
            byte[] png;
            using (MemoryStream ms = new MemoryStream()) { Image.FromFile(Path.Combine(folder, "照片1.png")).Save(ms, ImageFormat.Png); png = ms.ToArray(); }
            hist.AddImagePng(png, Glean.Util.Md5Short(png), DateTime.Now.AddMinutes(-3));
            hist.AddText("【申通快递】您的包裹 773123456789012 已到达XX驿站,请凭取件码 3-2-1056 取件");
            Glean.Item pin = hist.AddText("下午三点在 3 楼会议室开周会,记得带上本周的数据报表。\n第二行内容\n第三行内容");
            hist.TogglePin(pin);
            string longT = ""; for (int k = 0; k < 60; k++) longT += "第 " + k + " 行:一段比较长的内容,用来看预览窗口的滚动条。\n";
            hist.AddText(longT);
            hist.AddText("Hello Glean,这是一段比较短的文字");
            Glean.Board bd = hist.AddBoard("工作", Glean.Theme.Accents[2].ToArgb());
            hist.AddBoard("生活", Glean.Theme.Accents[4].ToArgb());
            hist.AddBoard("学习资料", Glean.Theme.Accents[3].ToArgb());
            hist.SetBoard(hist.Items[2], bd.Id);

            Glean.FolderStore.Load();
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            shelf.Prewarm();
            foreach (int theme in new int[] { 2, 1 })
            {
                string T = theme == 2 ? "dark" : "light";
                Glean.Config.ThemeMode = theme;
                Glean.Theme.Refresh();
                Glean.Config.ShelfTransparency = 35;
                // 卡片
                Glean.Config.Layout = 1; Glean.Config.LastFilesMode = false;
                shelf.ShowAt(); Pump(900);
                Shot(T + "-card", shelf.Bounds);
                Call(shelf, "ShowQuick"); Pump(400);
                Form q = (Form)Get(shelf, "quick");
                Shot(T + "-card-quicklook", q.Bounds);
                object st0 = Get(shelf, "strip"); st0.GetType().GetProperty("Selected").SetValue(st0, 1, null);
                Call(shelf, "ShowQuick"); Pump(400);
                Shot(T + "-card-quicklook-long", q.Bounds);
                st0.GetType().GetProperty("Selected").SetValue(st0, 0, null);
                // 动画帧间隔:滚动卡片时记录每一帧的时间
                if (theme == 2)
                {
                    Control cv = (Control)Get(shelf, "cardView");
                    List<double> stamps = new List<double>();
                    PaintEventHandler ph = delegate { stamps.Add(Glean.Gfx.Ms); };
                    cv.Paint += ph;
                    MethodInfo sc = cv.GetType().GetMethod("ScrollBy", new Type[] { typeof(int) });
                    sc.Invoke(cv, new object[] { 600 });
                    Pump(700);
                    cv.Paint -= ph;
                    List<double> gaps = new List<double>();
                    for (int k = 1; k < stamps.Count; k++) gaps.Add(stamps[k] - stamps[k - 1]);
                    gaps.Sort();
                    if (gaps.Count > 4) Out(string.Format("FRAMES n={0} median={1:0.0}ms p90={2:0.0}ms max={3:0.0}ms", gaps.Count, gaps[gaps.Count / 2], gaps[gaps.Count * 9 / 10], gaps[gaps.Count - 1]));
                    sc.Invoke(cv, new object[] { -600 }); Pump(500);
                }
                Call(shelf, "DeleteSelected"); Pump(200);
                Shot(T + "-card-toast", shelf.Bounds);
                Call(shelf, "UndoLast");
                Call(shelf, "SetMode", true);
                if (Glean.FolderStore.Items.Count == 0) Call(shelf, "AddFolders", new object[] { new string[] { folder } });
                Pump(1500);
                Shot(T + "-card-folder", shelf.Bounds);
                Call(shelf, "SetMode", false);
                shelf.HideNow(); Pump(200);
                // 列表
                Glean.Config.Layout = 0;
                shelf.ShowAt(); Pump(900);
                Shot(T + "-list", shelf.Bounds);
                Call(shelf, "SetMode", true); Pump(1200);
                Shot(T + "-list-folder", shelf.Bounds);
                Call(shelf, "SetMode", false);
                shelf.HideNow(); Pump(200);
                // 设置
                Glean.SettingsForm sf = new Glean.SettingsForm();
                sf.Show(); Pump(300);
                Glean.SettingsSidebar side = (Glean.SettingsSidebar)Get(sf, "side");
                for (int p = 0; p < 6; p++)
                {
                    if (theme == 1 && p != 0 && p != 3) continue;
                    if (theme == 2 && p != 3) continue;
                    side.Selected = p; Call(sf, "ShowPage"); sf.Refresh();
                    Shot(T + "-settings-" + p, sf.Bounds);
                }
                sf.Close();
                if (theme == 2)
                {
                    Glean.UpdateInfo u = new Glean.UpdateInfo(); u.Tag = "v0.21"; u.Notes = "Glean 拾光 0.21\n\n- **修复**:一些问题\n- 优化了流畅度\n- 美化了界面细节"; u.Url = "x";
                    Glean.UpdateForm uf = new Glean.UpdateForm(u); uf.Show(); Shot(T + "-update", uf.Bounds); uf.Close();
                    bool[,] m = Glean.QrCode.Encode(System.Text.Encoding.UTF8.GetBytes("YT7652500323998"));
                    Glean.ShareForm shf = new Glean.ShareForm("圆通速递 · 刚刚", m, "文字已经在二维码里,扫一下就能看到、复制。不需要联网,用手机相机或微信扫一扫都可以", null, false);
                    shf.Show(); Shot(T + "-share", shf.Bounds); shf.Close();
                    Glean.InputDialog idl = new Glean.InputDialog("新建分类", "分类名称", "", true, 0);
                    idl.Show(); Shot(T + "-input", idl.Bounds); idl.Close();
                }
            }
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        Out("DONE");
        return 0;
    }
}
