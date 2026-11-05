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
    static void Case(string text, string expect)
    {
        DateTime now = new DateTime(2026, 11, 5, 10, 0, 0);
        Glean.DueDate.Hit h = Glean.DueDate.Find(text, now);
        string got = h == null ? "null" : h.Date.ToString(h.HasTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd") + (h.Strong ? " S " : " W ") + h.Label;
        Out((got.StartsWith(expect) ? "PASS " : "FAIL ") + text.Replace("\n", "\\n") + "  =>  " + got + "   (want " + expect + ")");
    }
    static Glean.Reminder R(string text, int days, int atDays, string label, bool done, bool hasTime)
    {
        Glean.Reminder r = new Glean.Reminder();
        r.Text = text; r.Due = DateTime.Today.AddDays(days).AddHours(hasTime ? 15 : 0); r.HasTime = hasTime;
        r.At = DateTime.Today.AddDays(atDays).AddHours(9); r.Label = label; r.App = "微信"; r.Done = done;
        r.Created = DateTime.Now.AddDays(-5); r.Notified = true;
        if (done) r.DoneAt = DateTime.Now.AddHours(-3);
        return r;
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
            Case("您的域名 example.com 将于2026年11月20日到期，请及时续费", "2026-11-20 S 到期");
            Case("信用卡还款日：11月25日", "2026-11-25 S 还款");
            Case("会议时间：明天下午3点", "2026-11-06 15:00 S 会议");
            Case("报名截止 12/1", "2026-12-01 S 截止");
            Case("今天天气不错", "null");
            Case("版本 1.2.3 发布", "null");
            Case("11月20日我们去吃饭", "2026-11-20 W");
            Case("合同有效期至2025-03-01", "null");
            Case("下周三之前提交报告", "2026-11-11 S 截止");
            Case("发票请在3天内提交", "2026-11-08 S 提交");
            Case("订单号 20261120123456", "null");
            Case("2026-11-20 10:01:02 INFO a\n2026-11-21 10:01:02 INFO b\n2026-11-22 10:01:02 INFO c\n2026-11-23 x\n2026-11-24 y\n2026-11-25 z", "null");
            Case("Your subscription expires on 2026-12-01", "2026-12-01 S 到期");
            Case("due to the weather, 12/25", "null");
            Case("本月底前缴纳物业费", "2026-11-30 S 缴费");
            Case("10月30日已经到期了", "null");
            Case("1月3日还款", "2027-01-03 S 还款");
            Case("面试安排在11月12日 14:30，请准时", "2026-11-12 14:30 S 面试");
            Case("手机号 13800138000", "null");
            Case("周五前完成", "2026-11-06 S 截止");
            Case("一共 3/4 的人同意", "null");

            Glean.Config.ThemeMode = 1; Glean.Config.ShelfTransparency = 0; Glean.Config.Layout = 1;
            Out("default on " + Glean.Config.TimelineOn);
            Glean.Theme.Refresh();
            foreach (string n in Glean.Chimes.Builtin)
            {
                if (n == "系统提示") continue;
                byte[] w = (byte[])typeof(Glean.Chimes).GetMethod("Synth", NP).Invoke(null, new object[] { n });
                Out("chime " + n + " bytes " + w.Length);
                File.WriteAllBytes("chime-" + n + ".wav", w);
                string b64 = Convert.ToBase64String(w);
                for (int k = 0, part = 0; k < b64.Length; k += 6000, part++)
                    Out("WAVPART " + n + " " + part.ToString("000") + " " + b64.Substring(k, Math.Min(6000, b64.Length - k)));
            }
            Glean.Chimes.Play("水晶", null); Pump(300);
            string tf = Path.Combine(Glean.Config.DefaultDir, "timeline.txt");
            if (File.Exists(tf)) File.Delete(tf);
            typeof(Glean.Timeline).GetField("loaded", NP).SetValue(null, true);
            Glean.Timeline.Items.Clear();
            Glean.Reminder a1 = R("物业费缴纳截止,逾期将产生滞纳金", 0, 0, "缴费", false, false); a1.Note = "交到物业前台"; Glean.Timeline.Items.Add(a1);
            Glean.Timeline.Items.Add(R("信用卡账单 ¥3,280.00 最后还款日", 2, 1, "还款", false, false));
            Glean.Timeline.Save();
            // 保存再读回,备注还在
            Glean.Timeline.Items.Clear(); typeof(Glean.Timeline).GetField("loaded", NP).SetValue(null, false); Glean.Timeline.Load();
            Out("reload count " + Glean.Timeline.Items.Count + " note=" + Glean.Timeline.Items[0].Note);

            string dir = Path.Combine(Path.GetTempPath(), "glean-tl");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Glean.History hist = new Glean.History(dir);
            hist.AddText("普通的一段文字");
            Glean.ShelfForm shelf = new Glean.ShelfForm(null, hist);
            shelf.ShowAt(); Pump(1200);
            Rectangle sr = shelf.Bounds;
            Shot("shelf-top", new Rectangle(sr.Right - Glean.Gfx.S(700), sr.Y, Glean.Gfx.S(700), Glean.Gfx.S(70)));
            shelf.Hide(); Pump(300);
            Glean.Config.Layout = 0; Glean.Config.PopupAtCursor = false;
            shelf.ShowAt(); Pump(1200);
            sr = shelf.Bounds;
            Shot("list-top", new Rectangle(sr.X, sr.Y, sr.Width, Glean.Gfx.S(110)));
            shelf.Hide(); Pump(300);

            Glean.Timeline.Open(); Pump(800);
            Form tw = null;
            foreach (Form f in Application.OpenForms) if (f is Glean.TimelineForm) tw = f;
            tw.Location = new Point(20, 20); Pump(400);
            Shot("timeline", tw.Bounds);

            foreach (int mode in new int[] { 1, 2 })
            {
                Glean.Config.ThemeMode = mode; Glean.Theme.Refresh();
                Form ed = (Form)Activator.CreateInstance(typeof(Glean.ReminderEditor), NP, null, new object[] { mode == 1 ? null : Glean.Timeline.Items[0], null }, null);
                ed.StartPosition = FormStartPosition.Manual; ed.Location = new Point(560, 20);
                ed.Show(); Pump(700);
                Shot("editor-" + mode, ed.Bounds);
                if (mode == 1)
                {
                    TextBox tb = (TextBox)typeof(Glean.ReminderEditor).GetField("content", NP).GetValue(ed);
                    tb.Text = "给妈妈买生日礼物";
                    typeof(Glean.ReminderEditor).GetMethod("Save", NP).Invoke(ed, null);
                    Pump(300);
                    Out("after new count " + Glean.Timeline.Items.Count);
                    tw.Invalidate(true); Pump(300);
                    Shot("timeline2", tw.Bounds);
                }
                else ed.Close();
            }
            tw.Close();
            Glean.Config.ThemeMode = 1; Glean.Theme.Refresh();

            Glean.SettingsForm sf = new Glean.SettingsForm();
            object side = typeof(Glean.SettingsForm).GetField("side", NP).GetValue(sf);
            side.GetType().GetField("Selected").SetValue(side, 2);
            typeof(Glean.SettingsForm).GetMethod("ShowPage", NP).Invoke(sf, null);
            sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(0, 0);
            sf.Show(); Pump(900);
            Shot("settings", sf.Bounds);
            typeof(Glean.SettingsForm).GetMethod("ChimeMenu", NP).Invoke(sf, null); Pump(500);
            Shot("settings-menu", sf.Bounds);
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        Out("DONE");
        return 0;
    }
}
