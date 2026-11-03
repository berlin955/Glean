// Glean 拾光 安装程序  ·  作者:Berlin955
// 和 Glean.cs 一起编译(见 make-setup.bat),复用同一套界面控件和主题。
// 同一个 exe 既是安装程序、卸载程序(/uninstall),也是自动更新时的静默安装程序(/update)。

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Glean
{
    static class SetupProgram
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Gfx.InitDpi();
            Config.Load();
            Theme.Refresh();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool uninstall = false, update = false;
            foreach (string a in args)
            {
                if (string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
                if (string.Equals(a, "/update", StringComparison.OrdinalIgnoreCase)) update = true;
            }

            if (uninstall) Application.Run(new UninstallForm());
            else if (update) SilentUpdate();
            else Application.Run(new SetupForm());
        }

        // 自动更新:软件下载好新安装包后用 /update 启动它。
        // 不弹安装界面,按原来的位置和选项覆盖安装,装好后重新打开软件;历史记录和设置都不动。
        static void SilentUpdate()
        {
            try
            {
                // 等旧程序自己退出(最多 6 秒),之后安装时还会再确认一次
                for (int i = 0; i < 30 && Process.GetProcessesByName("Glean").Length > 0; i++) Thread.Sleep(200);
                string dir = Installer.ExistingLocation() ?? Installer.DefaultDir;
                bool desktop = File.Exists(Installer.DesktopLink);
                bool autostart = Startup.Get();
                string exe = Installer.Install(dir, desktop, autostart, delegate(string msg) { });
                Process.Start(exe);
            }
            catch (Exception ex)
            {
                MessageBox.Show("自动更新没有完成:" + ex.Message + "\n\n可以到 GitHub 下载安装包手动安装。", Brand.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    // ================================================================ 安装 / 卸载的实际工作
    static class Installer
    {
        public const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Glean";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // 默认装在当前用户自己的程序目录,不需要管理员权限
        public static string DefaultDir
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Glean");
            }
        }

        public static string StartMenuLink
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Glean 拾光.lnk");
            }
        }

        public static string DesktopLink
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Glean 拾光.lnk");
            }
        }

        // 已安装过的话,返回上次的安装位置
        public static string ExistingLocation()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstKey))
                {
                    if (k == null) return null;
                    string d = k.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
                }
            }
            catch { }
            return null;
        }

        // 安装位置的最后一级总是 Glean 文件夹,避免把文件直接撒在桌面等地方
        public static string NormalizeDir(string d)
        {
            d = d.Trim().TrimEnd('\\', '/');
            if (d.Length == 0) return DefaultDir;
            string last = System.IO.Path.GetFileName(d);
            if (!string.Equals(last, "Glean", StringComparison.OrdinalIgnoreCase))
                d = System.IO.Path.Combine(d, "Glean");
            return d;
        }

        public static void StopRunning()
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (string n in new string[] { "Glean" })
            {
                foreach (Process p in Process.GetProcessesByName(n))
                {
                    try
                    {
                        if (p.Id != me) { p.Kill(); p.WaitForExit(3000); }
                    }
                    catch { }
                }
            }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // 用系统自带的 WScript.Shell 创建快捷方式(后期绑定,不需要额外引用)
        static void CreateShortcut(string lnk, string target, string workDir)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(lnk));
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            try
            {
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc,
                    new object[] { Brand.Name + " " + Brand.NameCn + " —— 剪切板历史" });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                Marshal.FinalReleaseComObject(sc);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        // 返回安装好的 Glean.exe 路径
        public static string Install(string dir, bool desktop, bool autostart, Action<string> report)
        {
            report("正在关闭正在运行的 Glean…");
            StopRunning();

            report("正在复制程序文件…");
            Directory.CreateDirectory(dir);
            string exe = System.IO.Path.Combine(dir, "Glean.exe");
            using (Stream src = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.exe"))
            {
                if (src == null) throw new InvalidOperationException("安装包不完整:缺少程序文件。");
                using (FileStream fs = File.Create(exe)) src.CopyTo(fs);
            }
            string uninst = System.IO.Path.Combine(dir, "Uninstall.exe");
            if (!Util.SamePath(Application.ExecutablePath, uninst))
                File.Copy(Application.ExecutablePath, uninst, true);

            report("正在创建快捷方式…");
            try
            {
                CreateShortcut(StartMenuLink, exe, dir);
                if (desktop) CreateShortcut(DesktopLink, exe, dir);
                else TryDelete(DesktopLink);
            }
            catch { }   // 快捷方式失败不影响程序本身

            report("正在登记到“应用和功能”…");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstKey))
            {
                k.SetValue("DisplayName", Brand.Name + " " + Brand.NameCn);
                k.SetValue("DisplayVersion", Config.Version);
                k.SetValue("Publisher", Brand.Author);
                k.SetValue("DisplayIcon", exe);
                k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", "\"" + uninst + "\" /uninstall");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                long kb = 0;
                try { kb = (new FileInfo(exe).Length + new FileInfo(uninst).Length) / 1024; } catch { }
                k.SetValue("EstimatedSize", (int)kb, RegistryValueKind.DWord);
            }

            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (run != null)
                {
                    if (autostart) run.SetValue("Glean", "\"" + exe + "\"");
                    else run.DeleteValue("Glean", false);
                }
            }
            report("安装完成。");
            return exe;
        }

        // 返回安装目录(之后由 ScheduleSelfDelete 删掉卸载程序自己和空文件夹)
        public static string Uninstall(bool removeData)
        {
            StopRunning();
            string dir = ExistingLocation();
            if (string.IsNullOrEmpty(dir)) dir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);

            TryDelete(StartMenuLink);
            TryDelete(DesktopLink);
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (run != null)
                {
                    run.DeleteValue("Glean", false);
                }
            }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstKey, false); } catch { }
            TryDelete(System.IO.Path.Combine(dir, "Glean.exe"));

            if (removeData)
            {
                // 只删默认的数据文件夹(历史 + 设置);用户自定义的数据文件夹不动
                try { Directory.Delete(Config.DefaultDir, true); } catch { }
                try
                {
                    string thumbs = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glean");
                    if (Directory.Exists(thumbs)) Directory.Delete(thumbs, true);
                }
                catch { }
            }
            return dir;
        }

        // 卸载程序正在运行,删不掉自己:退出后由一个隐藏的命令行稍等片刻再删
        // 只删 Uninstall.exe 本身,文件夹用不带 /s 的 rmdir,不是空的就不会删,绝不会误删别的文件
        public static void ScheduleSelfDelete(string dir)
        {
            try
            {
                string self = Application.ExecutablePath;
                if (!Util.SamePath(System.IO.Path.GetDirectoryName(self), dir)) return;
                string cmd = "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + self + "\" & rmdir \"" + dir + "\"";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmd);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
        }
    }

    // ================================================================ 界面小工具
    static class Ui
    {
        public static void Place(Control c, int x, int y, int w, int h)
        {
            c.SetBounds(Gfx.S(x), Gfx.S(y), Gfx.S(w), Gfx.S(h));
        }

        public static Label AddLabel(Control parent, string text, int x, int y, Font f, Color c, int maxWidth)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = f;
            l.ForeColor = c;
            l.BackColor = SetPal.Back;
            l.AutoSize = true;
            if (maxWidth > 0) l.MaximumSize = new Size(Gfx.S(maxWidth), 0);
            l.Location = new Point(Gfx.S(x), Gfx.S(y));
            parent.Controls.Add(l);
            return l;
        }

        public static void AddLogo(Control parent, int x, int y, int size)
        {
            PictureBox logo = new PictureBox();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = SetPal.Back;
            logo.Image = Brand.Logo(Gfx.S(size));
            Place(logo, x, y, size, size);
            parent.Controls.Add(logo);
        }

        public static void InitForm(Form f, string title, int w, int h)
        {
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.ClientSize = new Size(Gfx.S(w), Gfx.S(h));
            f.BackColor = SetPal.Back;
            f.Font = Gfx.UI;
            try { if (Brand.LargeIcon != null) f.Icon = Brand.LargeIcon; } catch { }
        }
    }

    // ================================================================ 安装窗口
    class SetupForm : Form
    {
        readonly SearchBox pathBox = new SearchBox();
        readonly Toggle tDesktop = new Toggle();
        readonly Toggle tAuto = new Toggle();
        readonly Toggle tRun = new Toggle();
        readonly Btn btnInstall = new Btn();
        readonly Btn btnCancel = new Btn();
        readonly Btn btnBrowse = new Btn();
        readonly Label status;
        bool done, busy;

        public SetupForm()
        {
            Theme t = Theme.Cur;
            string existing = Installer.ExistingLocation();
            Ui.InitForm(this, Brand.Name + " " + Brand.NameCn + " 安装", 520, 470);

            Ui.AddLogo(this, 32, 28, 64);
            Ui.AddLabel(this, Brand.Name + "  " + Brand.NameCn, 112, 30, Gfx.Title, t.Text, 0);
            Ui.AddLabel(this, "版本 " + Config.Version + "  ·  作者 " + Brand.Author, 114, 68, Gfx.Small, t.Sub, 0);
            Ui.AddLabel(this, Brand.Tagline + "安装后常驻在任务栏右下角,按 " + Config.HotkeyText + " 随时呼出剪切板历史。",
                32, 112, Gfx.UI, t.Text, 456);

            Ui.AddLabel(this, "安装位置", 32, 176, Gfx.UI, t.Text, 0);
            pathBox.ShowIcon = false;
            pathBox.Box.Font = Gfx.UI;
            pathBox.Box.Text = Util.FriendlyPath(existing ?? Installer.DefaultDir);
            Ui.Place(pathBox, 32, 202, 372, 38);
            Controls.Add(pathBox);

            btnBrowse.Text = "更改…";
            Ui.Place(btnBrowse, 412, 204, 76, 34);
            btnBrowse.Click += delegate { Browse(); };
            Controls.Add(btnBrowse);

            AddToggle(tDesktop, "创建桌面快捷方式", 258, true);
            AddToggle(tAuto, "开机自动启动", 294, true);
            AddToggle(tRun, "安装完成后立即运行", 330, true);

            status = Ui.AddLabel(this, existing != null
                    ? "检测到已安装的版本,将直接更新,历史记录和设置都会保留。"
                    : "无需管理员权限,只为当前用户安装。",
                32, 378, Gfx.Small, t.Sub, 456);

            btnInstall.Primary = true;
            btnInstall.Text = existing != null ? "更新" : "安装";
            btnInstall.SetBounds(ClientSize.Width - Gfx.S(32 + 120), ClientSize.Height - Gfx.S(58), Gfx.S(120), Gfx.S(38));
            btnInstall.Click += delegate { DoInstall(); };
            Controls.Add(btnInstall);

            btnCancel.Text = "取消";
            btnCancel.SetBounds(ClientSize.Width - Gfx.S(32 + 120 + 10 + 96), ClientSize.Height - Gfx.S(58), Gfx.S(96), Gfx.S(38));
            btnCancel.Click += delegate { if (!busy) Close(); };
            Controls.Add(btnCancel);
        }

        void AddToggle(Toggle tg, string label, int y, bool value)
        {
            tg.Label = label;
            tg.Checked = value;
            Ui.Place(tg, 32, y, 456, 34);
            Controls.Add(tg);
        }

        void Browse()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择安装位置(会在里面建一个 Glean 文件夹)";
                d.ShowNewFolderButton = true;
                if (d.ShowDialog(this) == DialogResult.OK)
                    pathBox.Box.Text = Util.FriendlyPath(Installer.NormalizeDir(d.SelectedPath));
            }
        }

        void SetStatus(string s)
        {
            status.Text = s;
            status.Refresh();
        }

        void DoInstall()
        {
            if (done) { Close(); return; }
            if (busy) return;

            string dir = Installer.NormalizeDir(Util.RealPath(pathBox.Box.Text));
            pathBox.Box.Text = Util.FriendlyPath(dir);
            busy = true;
            btnInstall.Text = "正在安装…";
            btnInstall.Refresh();
            Cursor = Cursors.WaitCursor;
            try
            {
                string exe = Installer.Install(dir, tDesktop.Checked, tAuto.Checked, SetStatus);
                done = true;
                if (tRun.Checked)
                {
                    try { Process.Start(exe); } catch { }
                }
                SetStatus("安装完成。可以在开始菜单" + (tDesktop.Checked ? "或桌面" : "") +
                          "找到 Glean 拾光;以后想卸载,在 Windows 设置的“应用”里就能找到它。");
                btnInstall.Text = "完成";
                btnCancel.Visible = false;
                btnBrowse.Enabled = false;
                pathBox.Enabled = false;
            }
            catch (UnauthorizedAccessException)
            {
                SetStatus("没有权限写入这个位置。");
                MessageBox.Show(this, "没有权限写入这个文件夹,请换一个位置(建议用默认位置)。", Brand.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnInstall.Text = "安装";
            }
            catch (Exception ex)
            {
                SetStatus("安装失败。");
                MessageBox.Show(this, "安装失败:" + ex.Message, Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnInstall.Text = "安装";
            }
            finally
            {
                busy = false;
                Cursor = Cursors.Default;
                btnInstall.Invalidate();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && !busy) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // ================================================================ 卸载窗口
    class UninstallForm : Form
    {
        readonly Toggle tData = new Toggle();
        readonly Btn btnGo = new Btn();
        readonly Btn btnCancel = new Btn();
        readonly Label status;
        bool done;
        string removedDir;

        public UninstallForm()
        {
            Theme t = Theme.Cur;
            Ui.InitForm(this, "卸载 " + Brand.Name + " " + Brand.NameCn, 480, 330);

            Ui.AddLogo(this, 32, 28, 56);
            Ui.AddLabel(this, "卸载 " + Brand.Name + " " + Brand.NameCn, 104, 32, Gfx.Title, t.Text, 0);
            Ui.AddLabel(this, "将删除程序文件、开始菜单和桌面快捷方式,以及开机启动项。", 32, 108, Gfx.UI, t.Text, 416);

            tData.Label = "同时删除剪切板历史和设置";
            tData.Checked = false;
            Ui.Place(tData, 32, 158, 416, 34);
            Controls.Add(tData);
            Ui.AddLabel(this, "不勾选的话,以后重新安装还能找回历史记录。自定义的数据文件夹不会被删除。",
                32, 196, Gfx.Small, t.Sub, 416);

            status = Ui.AddLabel(this, "", 32, 236, Gfx.Small, t.Sub, 416);

            btnGo.Primary = true;
            btnGo.Text = "卸载";
            btnGo.SetBounds(ClientSize.Width - Gfx.S(32 + 110), ClientSize.Height - Gfx.S(58), Gfx.S(110), Gfx.S(38));
            btnGo.Click += delegate { DoUninstall(); };
            Controls.Add(btnGo);

            btnCancel.Text = "取消";
            btnCancel.SetBounds(ClientSize.Width - Gfx.S(32 + 110 + 10 + 96), ClientSize.Height - Gfx.S(58), Gfx.S(96), Gfx.S(38));
            btnCancel.Click += delegate { Close(); };
            Controls.Add(btnCancel);
        }

        void DoUninstall()
        {
            if (done) { Close(); return; }
            Cursor = Cursors.WaitCursor;
            try
            {
                removedDir = Installer.Uninstall(tData.Checked);
                done = true;
                status.Text = "已卸载。感谢使用 Glean 拾光。";
                btnGo.Text = "完成";
                btnCancel.Visible = false;
                tData.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "卸载时出错:" + ex.Message, Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                btnGo.Invalidate();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (done && removedDir != null) Installer.ScheduleSelfDelete(removedDir);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
