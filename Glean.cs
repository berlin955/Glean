// Glean 拾光 —— 剪切板历史与常用文件夹(Windows)  ·  作者:Berlin955  ·  github.com/berlin955/Glean
// 常驻托盘,热键呼出,选中后自动粘贴。支持文字/图片、深浅色主题、置顶、分类、自定义数据文件夹等。
// 用 Windows 自带编译器即可编译(C# 5),见 build.bat。

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Glean 拾光")]
[assembly: AssemblyDescription("剪切板历史与常用文件夹")]
[assembly: AssemblyCompany("Berlin955")]
[assembly: AssemblyProduct("Glean 拾光")]
[assembly: AssemblyCopyright("© 2026 Berlin955")]
[assembly: AssemblyVersion("0.21.0.0")]
[assembly: AssemblyFileVersion("0.21.0.0")]

namespace Glean
{
    // ================================================================ 配置
    static class Config
    {
        public static bool Ctrl = true, Alt = true, Shift = false, Win = false;
        public static Keys Key = Keys.V;
        public static int MaxItems = 200;          // 最多保留多少条(不含置顶)
        public static bool AutoUpdate = true;          // 自动检查更新
        public static long LastUpdateCheck = 0;        // 上次检查的时间(Ticks)
        public static string SkipVersion = "";         // 用户选了"跳过此版本"的版本号
        public static bool LastFilesMode = false;   // 上次关闭时停在"文件夹"
        public static string FolderWin = "";       // 列表模式下文件夹窗口的位置和大小 "x,y,w,h"
        public static bool FolderPinned = true;    // 文件夹窗口钉在最前面
        public static bool WinV = true;            // 用 Win+V 呼出(接管系统自带的剪切板历史)
        public static bool AutoPaste = true;       // 选中后自动粘贴
        public static bool RecordImages = true;    // 是否记录图片
        public static bool PopupAtCursor = true;   // 弹出位置:鼠标旁 / 屏幕中央
        public static float CardZoom = 1f;         // 卡片缩放 0.7 ~ 1.6
        public static int HeaderMode = 0;          // 卡片标题颜色:0 跟随分类圆点  1 来源程序  2 统一颜色
        public static int HeaderColor = Color.FromArgb(0, 120, 212).ToArgb();
        static readonly int[] DefaultTabDots = new int[]
        {
            Color.FromArgb(150, 136, 120).ToArgb(),  // 全部:暖灰
            Color.FromArgb(232, 165, 52).ToArgb(),   // 置顶:金
            Color.FromArgb(78, 150, 138).ToArgb(),   // 文本:鼠尾草绿
            Color.FromArgb(200, 104, 136).ToArgb(),  // 图片:玫瑰
            Color.FromArgb(92, 126, 200).ToArgb(),   // 链接:暮蓝
            Color.FromArgb(214, 118, 66).ToArgb()    // 文件:陶土
        };
        public static int[] TabDotColors = (int[])DefaultTabDots.Clone();
        public static int ShelfTransparency = 35;  // 卡片面板背景透明度 0~90(%),0 = 不透明
        public static int Layout = 1;              // 1 卡片(Paste 风格)  0 经典列表
        public static int ThemeMode = 0;           // 0 跟随系统  1 浅色  2 深色
        public static int AccentIndex = 0;         // 主题色序号
        public static string DataDir = "";         // 自定义数据文件夹,空 = 默认
        public static string IgnoreApps = "keepass,keepassxc,1password,bitwarden,lastpass,dashlane,enpass";
        public static int KeepDays = 0;            // 自动清理 N 天前的记录,0 = 永久保留
        public const string Version = "0.21";
        public const int MaxTextLength = 100000;   // 超长文本不记录

        public static string DefaultDir
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Glean");
            }
        }

        public static string DataPath
        {
            get { return string.IsNullOrEmpty(DataDir) ? DefaultDir : DataDir; }
        }

        static string SettingsFile
        {
            get { return System.IO.Path.Combine(DefaultDir, "settings.ini"); }
        }

        public static uint Modifiers
        {
            get
            {
                uint m = 0;
                if (Alt) m |= 1;
                if (Ctrl) m |= 2;
                if (Shift) m |= 4;
                if (Win) m |= 8;
                return m;
            }
        }

        public static string Describe(bool c, bool a, bool s, bool w, Keys k)
        {
            string r = "";
            if (c) r += "Ctrl + ";
            if (a) r += "Alt + ";
            if (s) r += "Shift + ";
            if (w) r += "Win + ";
            return r + KeyName(k);
        }

        public static string HotkeyText
        {
            get { return Describe(Ctrl, Alt, Shift, Win, Key); }
        }

        public static string KeyName(Keys k)
        {
            string s = k.ToString();
            if (s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1])) return s.Substring(1);
            return s;
        }

        public static Keys ParseKey(string s)
        {
            if (s.Length == 1 && char.IsDigit(s[0])) s = "D" + s;
            return (Keys)Enum.Parse(typeof(Keys), s);
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;
                foreach (string line in File.ReadAllLines(SettingsFile, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    try
                    {
                        switch (k)
                        {
                            case "ctrl": Ctrl = v == "1"; break;
                            case "alt": Alt = v == "1"; break;
                            case "shift": Shift = v == "1"; break;
                            case "win": Win = v == "1"; break;
                            case "key": Key = ParseKey(v); break;
                            case "max": MaxItems = Math.Max(20, Math.Min(5000, int.Parse(v))); break;
                            case "autoupdate": AutoUpdate = v == "1"; break;
                            case "lastcheck": LastUpdateCheck = long.Parse(v, CultureInfo.InvariantCulture); break;
                            case "skipversion": SkipVersion = v; break;
                            case "lastfiles": LastFilesMode = v == "1"; break;
                            case "folderwin": FolderWin = v; break;
                            case "folderpin": FolderPinned = v == "1"; break;
                            case "winv": WinV = v == "1"; break;
                            case "autopaste": AutoPaste = v == "1"; break;
                            case "images": RecordImages = v == "1"; break;
                            case "popupcursor": PopupAtCursor = v == "1"; break;
                            case "cardzoom": CardZoom = Math.Max(0.7f, Math.Min(1.6f, float.Parse(v, CultureInfo.InvariantCulture))); break;
                            case "headerstyle": HeaderMode = Math.Max(0, Math.Min(2, int.Parse(v))); break;
                            case "headercolor": HeaderColor = int.Parse(v, CultureInfo.InvariantCulture); break;
                            case "tabdots":
                                {
                                    string[] parts = v.Split(',');
                                    for (int idx = 0; idx < parts.Length && idx < TabDotColors.Length; idx++)
                                        TabDotColors[idx] = int.Parse(parts[idx], CultureInfo.InvariantCulture);
                                    break;
                                }
                            case "shelftransparency": ShelfTransparency = Math.Max(0, Math.Min(90, int.Parse(v))); break;
                            case "layout": Layout = v == "0" ? 0 : 1; break;
                            case "theme": ThemeMode = Math.Max(0, Math.Min(2, int.Parse(v))); break;
                            case "accent": AccentIndex = Math.Max(0, int.Parse(v)); break;
                            case "datadir": DataDir = v; break;
                            case "ignore": IgnoreApps = v; break;
                            case "keepdays": KeepDays = Math.Max(0, int.Parse(v)); break;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(DefaultDir);
                string[] lines = new string[]
                {
                    "ctrl=" + (Ctrl ? "1" : "0"),
                    "alt=" + (Alt ? "1" : "0"),
                    "shift=" + (Shift ? "1" : "0"),
                    "win=" + (Win ? "1" : "0"),
                    "key=" + KeyName(Key),
                    "max=" + MaxItems,
                    "autoupdate=" + (AutoUpdate ? "1" : "0"),
                    "lastcheck=" + LastUpdateCheck.ToString(CultureInfo.InvariantCulture),
                    "skipversion=" + SkipVersion,
                    "lastfiles=" + (LastFilesMode ? "1" : "0"),
                    "folderwin=" + FolderWin,
                    "folderpin=" + (FolderPinned ? "1" : "0"),
                    "winv=" + (WinV ? "1" : "0"),
                    "autopaste=" + (AutoPaste ? "1" : "0"),
                    "images=" + (RecordImages ? "1" : "0"),
                    "popupcursor=" + (PopupAtCursor ? "1" : "0"),
                    "layout=" + Layout,
                    "shelftransparency=" + ShelfTransparency,
                    "tabdots=" + string.Join(",", TabDotColors.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToArray()),
                    "cardzoom=" + CardZoom.ToString("0.###", CultureInfo.InvariantCulture),
                    "headerstyle=" + HeaderMode,
                    "headercolor=" + HeaderColor.ToString(CultureInfo.InvariantCulture),
                    "theme=" + ThemeMode,
                    "accent=" + AccentIndex,
                    "datadir=" + DataDir,
                    "ignore=" + IgnoreApps,
                    "keepdays=" + KeepDays
                };
                File.WriteAllLines(SettingsFile, lines, new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // 卡片视图和列表视图的共同接口:同一个面板可以换着用,功能完全一致
    interface IClipView
    {
        event Action<int> ItemActivated;
        event Action<int, Point> ItemContext;
        event Action<int> SelectionChanged;
        event Action<int> DragStart;
        event Action<int, int> Reordered;
        event Action<int, Point> DropAbove;
        event Action<int> ZoomWheel;
        event Action<int> PreviewRequested;
        Func<int, Point, bool> DragAbove { get; set; }
        string EmptyText { get; set; }
        int Selected { get; set; }
        bool Visible { get; set; }
        bool Focused { get; }
        void SetItems(List<Item> list, int mode);
        void Move(int d);
        void ScrollBy(float px);
        void Relayout();
        bool Focus();
        void Invalidate();
        void SetBounds(int x, int y, int width, int height);
        DragDropEffects DoDragDrop(object data, DragDropEffects allowedEffects);
    }

    // 能提供"毛玻璃"背景的窗口(卡片面板)
    interface IBackdrop
    {
        bool HasBackdrop { get; }
        void PaintBackdrop(Graphics g, Control child);
    }

    // 自定义分类
    class Board
    {
        public string Id, Name;
        public int Color;
    }

    // 品牌信息
    static class Brand
    {
        public const string Name = "Glean";
        public const string NameCn = "拾光";
        public const string Author = "Berlin955";
        public const string Tagline = "拾起你复制过的每一束光。";
        public const string RepoOwner = "berlin955";   // GitHub 上的发布地址,自动更新从这里检查新版本
        public const string RepoName = "Glean";

        static Icon small, large;

        public static Icon SmallIcon
        {
            get { if (small == null) small = Load(SystemInformation.SmallIconSize); return small; }
        }

        public static Icon LargeIcon
        {
            get { if (large == null) large = Load(SystemInformation.IconSize); return large; }
        }

        public static Bitmap Logo(int px)
        {
            try
            {
                Icon ic = Load(new Size(px, px));
                return ic != null ? ic.ToBitmap() : null;
            }
            catch { return null; }
        }

        static Icon Load(Size sz)
        {
            try
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("Glean.ico"))
                {
                    if (st != null)
                    {
                        Icon ic = new Icon(st, sz);
                        // Icon 是"用到时才解析"的;在这里先转一次位图,图标文件有问题就当场发现并放弃,不会让程序崩溃
                        using (Bitmap test = ic.ToBitmap()) { }
                        return ic;
                    }
                }
            }
            catch { }
            return null;
        }
    }

    // 接管 Win+V:Windows 把这个组合留给了自带的剪切板历史,普通的 RegisterHotKey 抢不到,
    // 所以用低级键盘钩子在系统之前截住它
    static class WinVHook
    {
        static IntPtr hook = IntPtr.Zero;
        static Native.LowLevelKeyboardProc proc;   // 必须一直引用着,否则会被回收导致崩溃
        static Action onPressed;
        static bool swallowing;

        public static bool Active { get { return hook != IntPtr.Zero; } }

        public static bool Install(Action pressed)
        {
            onPressed = pressed;
            if (hook != IntPtr.Zero) return true;
            proc = Callback;
            using (Process cur = Process.GetCurrentProcess())
            using (ProcessModule mod = cur.MainModule)
                hook = Native.SetWindowsHookEx(13, proc, Native.GetModuleHandle(mod.ModuleName), 0);   // WH_KEYBOARD_LL
            return hook != IntPtr.Zero;
        }

        public static void Uninstall()
        {
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
            swallowing = false;
        }

        static bool WinDown()
        {
            return (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0;
        }

        static IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                Native.KBDLLHOOKSTRUCT k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                bool injected = (k.flags & 0x10) != 0;
                int msg = wParam.ToInt32();
                bool down = msg == 0x100 || msg == 0x104;   // WM_KEYDOWN / WM_SYSKEYDOWN
                bool up = msg == 0x101 || msg == 0x105;
                if (!injected && k.vkCode == 0x56)            // V
                {
                    if (down && WinDown())
                    {
                        bool repeat = swallowing;
                        swallowing = true;
                        if (!repeat)
                        {
                            // 补一个无意义的按键,松开 Win 时开始菜单就不会弹出来
                            Native.keybd_event(0xE8, 0, 0, UIntPtr.Zero);
                            Native.keybd_event(0xE8, 0, 2, UIntPtr.Zero);
                            Action a = onPressed;
                            if (a != null) a();
                        }
                        return new IntPtr(1);                 // 吞掉,系统的剪切板历史就不会出现
                    }
                    if (up && swallowing)
                    {
                        swallowing = false;
                        return new IntPtr(1);
                    }
                }
            }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }
    }

    // 开机自启:写入当前用户的注册表 Run 项,不需要管理员权限
    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool Get()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("Glean") != null;
        }

        public static void Set(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue("Glean", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("Glean", false);
            }
        }
    }

    // ================================================================ 主题
    class Theme
    {
        public bool Dark;
        public Color Bg, Hover, Input, Border, Text, Sub, Accent, Card, Shelf;

        public static readonly Color[] Accents = new Color[]
        {
            Color.FromArgb(226, 138, 46),    // 拾光金(默认)
            Color.FromArgb(214, 96, 84),     // 暮色珊瑚
            Color.FromArgb(0, 120, 212),     // 蓝
            Color.FromArgb(124, 92, 255),    // 紫
            Color.FromArgb(16, 163, 127),    // 绿
            Color.FromArgb(112, 100, 90)     // 暖石墨
        };

        public static Theme Cur = Make(false, 0);

        public static Theme Make(bool dark, int accent)
        {
            Theme t = new Theme();
            t.Dark = dark;
            t.Accent = Accents[Math.Abs(accent) % Accents.Length];
            if (dark)
            {
                t.Bg = Color.FromArgb(33, 31, 29);
                t.Hover = Color.FromArgb(50, 47, 44);
                t.Input = Color.FromArgb(25, 23, 22);
                t.Border = Color.FromArgb(66, 61, 56);
                t.Text = Color.FromArgb(241, 236, 229);
                t.Sub = Color.FromArgb(162, 153, 142);
                t.Card = Color.FromArgb(45, 42, 39);
                t.Shelf = Color.FromArgb(24, 22, 21);
            }
            else
            {
                t.Bg = Color.FromArgb(253, 251, 248);
                t.Hover = Color.FromArgb(245, 240, 233);
                t.Input = Color.FromArgb(243, 238, 231);
                t.Border = Color.FromArgb(229, 222, 212);
                t.Text = Color.FromArgb(38, 33, 28);
                t.Sub = Color.FromArgb(136, 125, 112);
                t.Card = Color.White;
                t.Shelf = Color.FromArgb(241, 236, 228);
            }
            return t;
        }

        public static bool SystemDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    object v = k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        public static void Refresh()
        {
            bool dark = Config.ThemeMode == 2 || (Config.ThemeMode == 0 && SystemDark());
            Cur = Make(dark, Config.AccentIndex);
        }

        public static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // 让右键菜单也跟随主题
        public static void StyleMenu(ContextMenuStrip m)
        {
            m.Renderer = new AppleMenuRenderer();
            m.BackColor = AppleMenuRenderer.MenuBack;
            m.ForeColor = Cur.Text;
            m.Font = Gfx.UI;
            m.ShowImageMargin = true;
            m.Padding = new Padding(0, Gfx.S(5), 0, Gfx.S(5));
            m.Opened += delegate { Native.RoundCorners(m.Handle); };
            foreach (ToolStripItem i in m.Items) StyleItem(i);
        }

        static void StyleItem(ToolStripItem i)
        {
            i.ForeColor = Cur.Text;
            i.BackColor = AppleMenuRenderer.MenuBack;
            ToolStripMenuItem mi = i as ToolStripMenuItem;
            if (mi == null) return;
            mi.Padding = new Padding(0, Gfx.S(3), 0, Gfx.S(3));   // 行高更舒展
            if (mi.HasDropDownItems)
            {
                mi.DropDown.Renderer = new AppleMenuRenderer();
                mi.DropDown.BackColor = AppleMenuRenderer.MenuBack;
                mi.DropDown.Padding = new Padding(0, Gfx.S(5), 0, Gfx.S(5));
                ToolStripDropDown dd = mi.DropDown;
                dd.Opened += delegate { Native.RoundCorners(dd.Handle); };
                foreach (ToolStripItem c in mi.DropDownItems) StyleItem(c);
            }
        }
    }

    // 仿 macOS 的菜单绘制
    class AppleMenuRenderer : ToolStripProfessionalRenderer
    {
        public static Color MenuBack
        {
            get { return Theme.Cur.Dark ? Color.FromArgb(46, 43, 40) : Color.FromArgb(252, 250, 247); }
        }

        public AppleMenuRenderer() : base(new MenuColors()) { RoundedEdges = false; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(MenuBack);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen p = new Pen(Theme.Cur.Dark ? Color.FromArgb(70, 70, 75) : Color.FromArgb(214, 214, 220)))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Gfx.FillRound(g, Theme.Cur.Accent, new Rectangle(Gfx.S(5), 0, e.Item.Width - Gfx.S(10), e.Item.Height), 5);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool hot = e.Item.Selected && e.Item.Enabled;
            e.TextColor = !e.Item.Enabled ? Theme.Cur.Sub : (hot ? Color.White : Theme.Cur.Text);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item != null && e.Item.Selected ? Color.White : Theme.Cur.Sub;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (Pen p = new Pen(Theme.Cur.Dark ? Color.FromArgb(64, 64, 68) : Color.FromArgb(222, 222, 228)))
                e.Graphics.DrawLine(p, Gfx.S(12), y, e.Item.Width - Gfx.S(12), y);
        }

        // 勾选项:一个干净的对勾,不要方框
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle;
            Color c = e.Item.Selected ? Color.White : Theme.Cur.Accent;
            using (Pen pen = new Pen(c, Gfx.F(1.8f)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float x = r.X + r.Width * 0.22f, y = r.Y + r.Height * 0.52f;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(x, y),
                    new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.72f),
                    new PointF(r.X + r.Width * 0.80f, r.Y + r.Height * 0.28f)
                });
            }
        }
    }

    class MenuColors : ProfessionalColorTable
    {
        public MenuColors() { UseSystemColors = false; }
        public override Color MenuItemSelected { get { return Theme.Cur.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Cur.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Cur.Hover; } }
        public override Color MenuItemBorder { get { return Theme.Cur.Hover; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Cur.Hover; } }
        public override Color MenuItemPressedGradientMiddle { get { return Theme.Cur.Hover; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Cur.Hover; } }
        public override Color MenuBorder { get { return Theme.Cur.Border; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Cur.Bg; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Cur.Bg; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Cur.Bg; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Cur.Bg; } }
        public override Color SeparatorDark { get { return Theme.Cur.Border; } }
        public override Color SeparatorLight { get { return Theme.Cur.Border; } }
        public override Color CheckBackground { get { return Theme.Cur.Accent; } }
        public override Color CheckSelectedBackground { get { return Theme.Cur.Accent; } }
        public override Color CheckPressedBackground { get { return Theme.Cur.Accent; } }
    }

    // ================================================================ Win32 / 工具
    static class Native
    {
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] public static extern IntPtr GetClipboardOwner();
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool IsProcessDPIAware();
        [DllImport("imm32.dll")] public static extern uint ImmGetVirtualKey(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] public static extern int SetWindowLong(IntPtr h, int index, int value);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);

        // 截屏时把这个窗口"隐身"(Win10 2004 及以上支持),用来给自己拍身后的背景
        public static bool ExcludeFromCapture(IntPtr h, bool on)
        {
            try { return SetWindowDisplayAffinity(h, on ? 0x11u : 0u); } catch { return false; }
        }

        // 低级键盘钩子(用来接管 Win+V)
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode, scanCode, flags, time;
            public IntPtr dwExtraInfo;
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("imm32.dll")] public static extern IntPtr ImmGetContext(IntPtr hWnd);
        [DllImport("imm32.dll")] public static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        public static extern int ImmGetCompositionStringW(IntPtr hIMC, int dwIndex, byte[] lpBuf, int dwBufLen);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // Windows 11 上让无边框窗口有系统级圆角;旧系统无效果但不会出错
        // Windows 10 1809+ / 11:让窗口标题栏跟随深色模式
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string idList);

        // 让控件自带的滚动条跟随深浅色(Windows 10 1809 及以上支持深色滚动条)
        public static void ThemeScrollBars(Control c, bool dark)
        {
            try { if (c.IsHandleCreated || c.Handle != IntPtr.Zero) SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null); }
            catch { }
        }

        public static void DarkTitle(IntPtr h, bool dark)
        {
            try
            {
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(h, 20, ref v, 4) != 0) DwmSetWindowAttribute(h, 19, ref v, 4);
            }
            catch { }
        }

        public static void NoTransitions(IntPtr h)
        {
            try { int v = 1; DwmSetWindowAttribute(h, 3, ref v, 4); } catch { }   // DWMWA_TRANSITIONS_FORCEDISABLED
        }

        public static void RoundCorners(IntPtr h)
        {
            try { int v = 2; DwmSetWindowAttribute(h, 33, ref v, 4); }
            catch { }
        }
    }

    static class Util
    {
        // 剪切板偶尔会被其他程序占用,重试几次
        public static void Retry(Action a)
        {
            for (int i = 0; i < 6; i++)
            {
                try { a(); return; }
                catch (ExternalException) { Thread.Sleep(40); }
            }
            a();
        }

        public static string Md5Short(byte[] data)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] h = md5.ComputeHash(data);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 8; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static Bitmap LoadBitmap(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            using (MemoryStream ms = new MemoryStream(b))
            using (Image im = Image.FromStream(ms))
            {
                return new Bitmap(im);
            }
        }

        public static bool SamePath(string a, string b)
        {
            return string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        // 当前剪切板内容来自哪个程序:返回 exe 路径和显示名称
        // 进程 → (程序路径, 显示名, 进程名);读进程信息比较慢,同一个程序只查一次
        static readonly Dictionary<uint, string[]> ownerCache = new Dictionary<uint, string[]>();

        // 会把剪切板内容"转手"再写一遍的程序(远程控制、虚拟机的剪切板同步)。
        // 它们会变成剪切板的"主人",但内容其实来自你正在用的那个窗口
        static readonly HashSet<string> RelayApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "todesk", "todesk_service", "todesk_lite", "sunloginclient", "sunloginremote", "slrc", "awesun", "oray",
            "teamviewer", "teamviewer_service", "tv_w32", "tv_x64", "anydesk", "rustdesk", "parsecd", "parsec",
            "rdpclip", "remoting_host", "remoting_desktop", "splashtop", "srstreamer", "uuremote", "gameviewer",
            "vmtoolsd", "vboxtray", "vboxclient", "prl_cc", "ditto"
        };

        public static bool IsRelay(string exeOrPath)
        {
            if (string.IsNullOrEmpty(exeOrPath)) return false;
            string n;
            try { n = System.IO.Path.GetFileNameWithoutExtension(exeOrPath); } catch { n = exeOrPath; }
            return RelayApps.Contains(n);
        }

        static uint ownPid;
        static uint OwnPid
        {
            get
            {
                if (ownPid == 0) using (Process me = Process.GetCurrentProcess()) ownPid = (uint)me.Id;
                return ownPid;
            }
        }

        static uint PidOf(IntPtr w)
        {
            if (w == IntPtr.Zero) return 0;
            uint pid;
            Native.GetWindowThreadProcessId(w, out pid);
            return pid;
        }

        static string[] Lookup(uint pid)
        {
            string[] hit;
            lock (ownerCache)
            {
                if (ownerCache.TryGetValue(pid, out hit)) return hit;
            }
            string path, name, exe;
            LookupOwner(pid, out path, out name, out exe);
            hit = new string[] { path, name, exe };
            lock (ownerCache)
            {
                if (ownerCache.Count > 200) ownerCache.Clear();
                ownerCache[pid] = hit;
            }
            return hit;
        }

        // 真正的来源程序:一般是剪切板的"主人";主人是转手程序或者查不到时,用你按 Ctrl+C 时所在的窗口
        public static void ClipboardSource(out string path, out string name, out string exe)
        {
            path = name = exe = null;
            uint pid = PidOf(Native.GetClipboardOwner());
            if (pid != 0)
            {
                string[] o = Lookup(pid);
                path = o[0]; name = o[1]; exe = o[2];
            }
            if (pid == 0 || IsRelay(exe) || IsRelay(path))
            {
                uint fp = PidOf(Native.GetForegroundWindow());
                if (fp != 0 && fp != OwnPid)
                {
                    string[] f = Lookup(fp);
                    if (f[0] != null || f[2] != null) { path = f[0]; name = f[1]; exe = f[2]; }
                }
            }
        }

        public static void ClipboardOwnerApp(out string path, out string name)
        {
            string exe;
            ClipboardSource(out path, out name, out exe);
        }

        static void LookupOwner(uint pid, out string path, out string name, out string exe)
        {
            path = null;
            name = null;
            exe = null;
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                {
                    name = p.ProcessName;
                    exe = p.ProcessName;
                    try { path = p.MainModule.FileName; } catch { path = null; }
                }
                if (path != null)
                {
                    try
                    {
                        FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
                        string d = vi.FileDescription;
                        if (string.IsNullOrWhiteSpace(d)) d = vi.ProductName;
                        if (!string.IsNullOrWhiteSpace(d)) name = d.Trim();
                    }
                    catch { }
                }
            }
            catch { }
        }

        // 显示路径时把用户目录换成环境变量,例如 C:\Users\张三\AppData\Roaming\Glean -> %APPDATA%\Glean
        public static string FriendlyPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string[][] map = new string[][]
            {
                new string[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%" },
                new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%" },
                new string[] { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%" }
            };
            foreach (string[] m in map)
            {
                string root = (m[0] ?? "").TrimEnd('\\');
                if (root.Length == 0) continue;
                if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) return m[1];
                if (path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) return m[1] + path.Substring(root.Length);
            }
            return path;
        }

        // 用户输入的路径里可以带 %APPDATA% 这类变量,用之前展开
        public static string RealPath(string path)
        {
            return Environment.ExpandEnvironmentVariables((path ?? "").Trim());
        }

        public static string CleanUrl(string s)
        {
            s = s.Trim();
            string frag = "";
            int h = s.IndexOf('#');
            if (h >= 0) { frag = s.Substring(h); s = s.Substring(0, h); }
            int q = s.IndexOf('?');
            if (q < 0) return s + frag;
            string baseUrl = s.Substring(0, q);
            List<string> keep = new List<string>();
            foreach (string p in s.Substring(q + 1).Split('&'))
            {
                if (p.Length == 0) continue;
                string key = p.Split('=')[0].ToLowerInvariant();
                if (key.StartsWith("utm_") || key == "fbclid" || key == "gclid" || key == "spm" ||
                    key == "share_source" || key == "mc_cid" || key == "mc_eid" || key == "igshid")
                    continue;
                keep.Add(p);
            }
            return baseUrl + (keep.Count > 0 ? "?" + string.Join("&", keep.ToArray()) : "") + frag;
        }
    }

    // ================================================================ 绘图辅助
    static class Gfx
    {
        // 屏幕缩放比例(125% => 1.25)。程序声明了高分屏支持,所以所有像素尺寸都要乘以它
        public static float Scale = 1f;

        public static void InitDpi()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                    Scale = Math.Max(1f, g.DpiX / 96f);
            }
            catch { Scale = 1f; }
        }

        public static int S(int px) { return (int)Math.Round(px * Scale); }
        public static float F(float px) { return px * Scale; }

        public static readonly Font UI = new Font("Microsoft YaHei UI", 10f);
        public static readonly Font Small = new Font("Microsoft YaHei UI", 8.5f);
        public static readonly Font SmallBold = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
        public static readonly Font Bold = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        public static readonly Font Digit = new Font("Segoe UI Semibold", 7.5f);
        public static readonly Font Big = new Font("Microsoft YaHei UI", 14f);
        public static readonly Font HeaderFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        public static readonly Font Title = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
        public static readonly Font Input = new Font("Microsoft YaHei UI", 11f);
        public static readonly Font Symbol = new Font("Segoe UI Symbol", 11f);

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = Math.Max(2, S(radius) * 2);
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            if (d < 2) d = 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // 带一点回弹的缓动(冲过头一点再回来),c1 越大回弹越明显
        // 动画用的高精度时钟(系统时间每 15 毫秒左右才跳一次,拿来算动画会一卡一卡)
        static readonly Stopwatch clock = Stopwatch.StartNew();
        public static double Ms { get { return clock.Elapsed.TotalMilliseconds; } }

        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint p);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint p);
        static int hiRes;

        // 动画播放时把系统计时器调到 1 毫秒精度,帧间隔才均匀;动画结束就恢复,不额外耗电
        public static void HiRes(bool on)
        {
            try
            {
                if (on) { if (hiRes++ == 0) timeBeginPeriod(1); }
                else if (hiRes > 0) { if (--hiRes == 0) timeEndPeriod(1); }
            }
            catch { }
        }

        // 按真实经过的时间换算"每帧靠拢多少",帧间隔忽长忽短时速度也均匀
        public static float Ease(float perFrame, double dtMs)
        {
            return (float)(1 - Math.Pow(1 - perFrame, dtMs / 16.67));
        }

        public static double BackOut(double p, double c1)
        {
            double c3 = c1 + 1;
            return 1 + c3 * Math.Pow(p - 1, 3) + c1 * Math.Pow(p - 1, 2);
        }

        // 画一个"键帽 + 说明"的快捷键提示,从 right 往左排;返回下一个可用的右边界
        public static int KeyHint(Graphics g, string key, string label, int right, int cy)
        {
            Theme t = Theme.Cur;
            TextFormatFlags nf = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            Size ls = TextRenderer.MeasureText(label, Small, Size.Empty, nf);
            Size ks = TextRenderer.MeasureText(key, Small, Size.Empty, nf);
            int lx = right - ls.Width;
            TextRenderer.DrawText(g, label, Small, new Point(lx, cy - ls.Height / 2), Theme.Blend(t.Sub, t.Text, 0.45), nf);
            int kh = S(20);
            int kw = Math.Max(kh, ks.Width + S(14));
            Rectangle kr = new Rectangle(lx - S(7) - kw, cy - kh / 2, kw, kh);
            Rectangle depth = kr;
            depth.Offset(0, S(1));
            FillRound(g, t.Border, depth, 5);                                        // 键帽底部的"厚度"
            FillRound(g, Theme.Blend(t.Shelf, t.Text, t.Dark ? 0.10 : 0.035), kr, 5);
            DrawRound(g, t.Border, kr, 5, 1f);
            TextRenderer.DrawText(g, key, Small, kr, t.Text, nf | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return kr.X;
        }

        // 子控件画背景:父窗口有毛玻璃背景就画对应的那一块,否则用父窗口底色
        public static void PaintParentBack(Control c, Graphics g)
        {
            IBackdrop bd = c.Parent as IBackdrop;
            if (bd != null && bd.HasBackdrop) { bd.PaintBackdrop(g, c); return; }
            g.Clear(c.Parent != null ? c.Parent.BackColor : Theme.Cur.Bg);
        }

        // 柔和的光晕:在 r 周围铺一圈渐隐的颜色(用于图标背后、选中卡片)
        public static void DrawGlow(Graphics g, Rectangle r, Color c, int spread)
        {
            Rectangle o = r;
            o.Inflate(spread, spread);
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(o);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Color.FromArgb(Theme.Cur.Dark ? 110 : 90, c);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    pb.FocusScales = new PointF(0.35f, 0.35f);
                    g.FillPath(pb, gp);
                }
            }
        }

        // 只有上面两个角是圆角(卡片顶部色带用)
        public static GraphicsPath RoundTop(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = Math.Max(2, S(radius) * 2);
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, Rectangle r, int radius)
        {
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Color c, Rectangle r, int radius, float width)
        {
            using (GraphicsPath p = Round(r, radius))
            using (Pen pen = new Pen(c, width))
                g.DrawPath(pen, p);
        }

        public static void Text(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        }

        // 往缓存图片里写字时用:告诉系统底色,ClearType 抗锯齿才会和底色混合。
        // 不给底色的话,画进图片里的字会按黑底算边缘,浅色背景上看起来发黑、毛糙
        public static void TextOn(Graphics g, string s, Font f, Rectangle r, Color c, Color back, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, back, flags | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        }

        // 彩色底上的浅色字:用灰度抗锯齿直接和底色混合。
        // ClearType 画在彩色色带上会在字的边缘留下一圈发黑的彩边,看起来像"黑边"。
        static StringFormat softFormat;

        static StringFormat SoftFormat(bool center)
        {
            if (softFormat == null)
            {
                softFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
                softFormat.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
                softFormat.Trimming = StringTrimming.EllipsisCharacter;
                softFormat.LineAlignment = StringAlignment.Center;
                softFormat.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;
            }
            StringFormat f = (StringFormat)softFormat.Clone();
            f.Alignment = center ? StringAlignment.Center : StringAlignment.Near;
            return f;
        }

        public static void SoftText(Graphics g, string s, Font f, Rectangle r, Color c, bool center)
        {
            System.Drawing.Text.TextRenderingHint old = g.TextRenderingHint;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (StringFormat sf = SoftFormat(center))
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(s ?? "", f, b, new RectangleF(r.X, r.Y, r.Width, r.Height), sf);
            g.TextRenderingHint = old;
        }

        public static int SoftWidth(Graphics g, string s, Font f)
        {
            using (StringFormat sf = SoftFormat(false))
                return (int)Math.Ceiling(g.MeasureString(s ?? "", f, PointF.Empty, sf).Width);
        }
    }

    // 测量各步骤耗时(只在测试时打开,平时不做任何事)
    static class Perf
    {
        public static bool On;
        public static readonly List<string> Log = new List<string>();
        static readonly Stopwatch sw = new Stopwatch();
        public static void Start() { if (On) sw.Restart(); }
        public static void Mark(string name)
        {
            if (!On) return;
            Log.Add(name + " " + sw.Elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture));
            sw.Restart();
        }
    }

    // ================================================================ 数据
    class Item
    {
        public bool IsImage, IsFiles, Pinned;
        public Courier.Hit CourierHit;     // 文字里识别出的快递单号(内容不变,查一次就记住)
        public bool CourierChecked;
        public string FilePath, Hash, Text;
        public string AppPath, AppName;       // 来源程序
        public int CustomColor;               // 自定义的卡片标题颜色(0 = 默认)
        public string Board;                  // 所属分类 Id(null = 未分类)
        public string CustomTitle;            // 自定义卡片标题(null = 默认)
        public DateTime Time;
        public Bitmap Thumb;
        public Bitmap FileIcon;
        public Bitmap Preview;                // 卡片里的大预览
        bool iconTried, thumbTried;
        volatile bool thumbDone;
        string title;
        int imgFile = -1;
        DateTime infoStamp;
        public int W, H;

        public const int KText = 0, KUrl = 1, KEmail = 2, KColor = 3, KPath = 4, KNumber = 5, KFiles = 6, KTrack = 7;
        public static readonly string[] KindLabels = new string[] { "文字", "链接", "邮箱", "颜色", "路径", "数字", "文件", "快递单号" };

        static readonly Regex ReUrl = new Regex(@"^(https?://|www\.)\S+$", RegexOptions.IgnoreCase);
        static readonly Regex ReEmail = new Regex(@"^[\w.+\-]+@[\w\-]+(\.[\w\-]+)+$");
        static readonly Regex ReColor = new Regex(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");
        static readonly Regex RePath = new Regex(@"^([a-zA-Z]:\\|\\\\)[^\r\n]*$");
        static readonly Regex ReNumber = new Regex(@"^-?[\d,]+(\.\d+)?$");

        int kind = -1;
        string info;

        public int Kind
        {
            get
            {
                if (kind < 0) kind = Classify();
                return kind;
            }
        }

        int Classify()
        {
            if (IsFiles) return KFiles;
            if (IsImage || Text == null) return KText;
            string t = Text.Trim();
            if (t.Length == 0 || t.Length > 2048 || t.IndexOf('\n') >= 0) return KText;
            if (ReUrl.IsMatch(t)) return KUrl;
            if (Courier.Detect(t) != null) return KTrack;
            if (ReEmail.IsMatch(t)) return KEmail;
            if (ReColor.IsMatch(t)) return KColor;
            if (RePath.IsMatch(t)) return KPath;
            if (t.Length <= 24 && ReNumber.IsMatch(t)) return KNumber;
            return KText;
        }

        public Color SwatchColor()
        {
            string h = Text.Trim().TrimStart('#');
            if (h.Length == 3) h = "" + h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
            int v = Convert.ToInt32(h, 16);
            return Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255);
        }

        // 例如 "文字  ·  128 字  ·  3 行"
        public string Info
        {
            get
            {
                if (IsFiles) return FilesInfo();
                if (info == null)
                {
                    int lines = 1;
                    int n = Math.Min(Text.Length, 5000);
                    for (int i = 0; i < n; i++) if (Text[i] == '\n') lines++;
                    info = Kind == KText ? Text.Length + " 字" : KindLabels[Kind];
                    if (lines > 1) info += "  ·  " + lines + " 行";
                }
                return info;
            }
        }

        // 复制的是图片文件(jpg/png 等)时,可以直接显示缩略图
        public bool IsImageFile
        {
            get
            {
                if (!IsFiles) return false;
                if (imgFile < 0)
                {
                    imgFile = 0;
                    string[] ps = Paths;
                    if (ps.Length > 0)
                    {
                        string ex = System.IO.Path.GetExtension(ps[0]).ToLowerInvariant();
                        if (ex == ".jpg" || ex == ".jpeg" || ex == ".png" || ex == ".bmp" ||
                            ex == ".gif" || ex == ".tif" || ex == ".tiff" || ex == ".ico") imgFile = 1;
                    }
                }
                return imgFile == 1;
            }
        }

        // 列表里显示的标题:第一行非空内容
        public string Title
        {
            get
            {
                if (title == null)
                {
                    string r = "";
                    if (Text != null)
                    {
                        string head = Text.Length > 2000 ? Text.Substring(0, 2000) : Text;
                        foreach (string ln in head.Split('\n'))
                        {
                            string x = ln.Trim().Replace('\t', ' ');
                            if (x.Length > 0) { r = x; break; }
                        }
                    }
                    if (r.Length > 200) r = r.Substring(0, 200);
                    title = r;
                }
                return title;
            }
        }

        // 图片条目的说明行:尺寸 · 大小 · 时间
        public string ImageMeta()
        {
            List<string> parts = new List<string>();
            if (W > 0) parts.Add(W + " × " + H);
            if (IsFiles)
            {
                const string pre = "文件  ·  ";
                string fi = FilesInfo();
                if (fi.StartsWith(pre)) fi = fi.Substring(pre.Length);
                else if (fi == "文件") fi = "";
                if (fi.Length > 0) parts.Add(fi);
            }
            parts.Add(TimeAgo());
            return string.Join("  ·  ", parts.ToArray());
        }

        // 大小:截图/图片 = 保存下来的图片文件大小;文件 = 原文件总大小(文件夹或已删除时为空)
        string sizeText;
        DateTime sizeStamp;

        public string SizeText()
        {
            if (!IsImage && !IsFiles) return null;
            if (sizeText != null && (IsImage || (DateTime.Now - sizeStamp).TotalSeconds < 20))
                return sizeText.Length > 0 ? sizeText : null;
            string r = "";
            try
            {
                if (IsImage) r = FormatSize(new FileInfo(FilePath).Length);
                else
                {
                    long total = 0;
                    bool ok = true;
                    foreach (string f in Paths)
                    {
                        if (File.Exists(f)) total += new FileInfo(f).Length;
                        else { ok = false; break; }
                    }
                    if (ok && Paths.Length > 0) r = FormatSize(total);
                }
            }
            catch { r = ""; }
            sizeText = r;
            sizeStamp = DateTime.Now;
            return r.Length > 0 ? r : null;
        }

        public string Ext
        {
            get { return IsImage ? ".png" : (IsFiles ? ".files" : ".txt"); }
        }

        // 文件类记录:每行一个路径(内容不会变,拆一次就记住,画卡片时不用反复拆字符串)
        string[] paths;
        public string[] Paths
        {
            get
            {
                if (Text == null) return new string[0];
                if (paths == null)
                    paths = Text.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => x.Trim('\r')).ToArray();
                return paths;
            }
        }

        public string FileTitle()
        {
            string[] ps = Paths;
            if (ps.Length == 0) return "(空)";
            string first = System.IO.Path.GetFileName(ps[0].TrimEnd('\\', '/'));
            if (first.Length == 0) first = ps[0];
            return ps.Length > 1 ? first + "  等 " + ps.Length + " 项" : first;
        }

        public void EnsureFileIcon()
        {
            if (FileIcon != null || iconTried) return;
            iconTried = true;
            try
            {
                string[] ps = Paths;
                if (ps.Length == 0) return;
                using (Icon ic = Icon.ExtractAssociatedIcon(ps[0]))
                {
                    if (ic != null) FileIcon = ic.ToBitmap();
                }
            }
            catch { }
        }

        public void DisposeCaches()
        {
            thumbTried = false;
            thumbDone = false;
            if (Thumb != null) { Thumb.Dispose(); Thumb = null; }
            if (FileIcon != null) { FileIcon.Dispose(); FileIcon = null; }
            if (Preview != null) { Preview.Dispose(); Preview = null; }
        }

        static string FormatSize(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            if (b < 1024L * 1024 * 1024) return (b / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
            return (b / 1073741824.0).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
        }

        string FilesInfo()
        {
            DateTime now = DateTime.Now;
            if (info != null && (now - infoStamp).TotalSeconds < 20) return info;
            string[] ps = Paths;
            bool missing = false, anyDir = false;
            long size = 0;
            foreach (string f in ps)
            {
                try
                {
                    if (File.Exists(f)) size += new FileInfo(f).Length;
                    else if (Directory.Exists(f)) anyDir = true;
                    else missing = true;
                }
                catch { missing = true; }
            }
            string r;
            if (ps.Length == 1) r = anyDir ? "文件夹" : "文件" + (missing ? "" : "  ·  " + FormatSize(size));
            else r = "文件  ·  " + ps.Length + " 项" + ((anyDir || missing) ? "" : "  ·  " + FormatSize(size));
            if (missing) r += "  ·  已移动或删除";
            info = r;
            infoStamp = now;
            return r;
        }

        public string TimeAgo()
        {
            TimeSpan d = DateTime.Now - Time;
            if (d.TotalSeconds < 60) return "刚刚";
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + " 分钟前";
            if (d.TotalHours < 24) return (int)d.TotalHours + " 小时前";
            if (d.TotalDays < 7) return (int)d.TotalDays + " 天前";
            return Time.ToString("M月d日", CultureInfo.InvariantCulture);
        }

        // ---- 缩略图在后台线程生成,打开窗口时不会卡住;生成好后通知列表重绘
        static readonly System.Collections.Concurrent.BlockingCollection<Item> thumbQueue =
            new System.Collections.Concurrent.BlockingCollection<Item>();
        static Thread thumbWorker;
        public static event Action ThumbReady;

        // 正在生成中(这时显示占位底色,而不是文件图标)
        public bool ThumbPending
        {
            get { return thumbTried && !thumbDone; }
        }

        // 卡片尺寸变了:大预览按新尺寸重新生成
        public void InvalidatePreview()
        {
            if (!(IsImage || IsImageFile)) return;
            Bitmap old = Preview;
            Preview = null;
            if (old != null) old.Dispose();   // 卡片里画的是缓存的副本,旧图可以马上释放
            thumbTried = false;
            thumbDone = false;
        }

        public void EnsureThumb()
        {
            if (!(IsImage || IsImageFile) || (Thumb != null && Preview != null) || thumbTried) return;
            thumbTried = true;
            if (thumbWorker == null)
            {
                thumbWorker = new Thread(ThumbLoop);
                thumbWorker.IsBackground = true;
                thumbWorker.Priority = ThreadPriority.BelowNormal;
                thumbWorker.Start();
            }
            thumbQueue.Add(this);
        }

        static void ThumbLoop()
        {
            foreach (Item it in thumbQueue.GetConsumingEnumerable())
            {
                it.BuildThumb();
                Action h = ThumbReady;
                if (h != null)
                {
                    try { h(); } catch { }
                }
            }
        }

        // 等比缩放后居中裁切铺满;比目标小的图不放大
        static Bitmap MakeCover(Image img, int bw, int bh)
        {
            int w0 = img.Width, h0 = img.Height;
            if (w0 <= bw && h0 <= bh)
            {
                Bitmap c = new Bitmap(w0, h0);
                using (Graphics g = Graphics.FromImage(c)) g.DrawImage(img, 0, 0, w0, h0);
                return c;
            }
            double sc = Math.Max(bw / (double)w0, bh / (double)h0);
            double cw = bw / sc, ch = bh / sc;
            RectangleF crop = new RectangleF((float)((w0 - cw) / 2), (float)((h0 - ch) / 2), (float)cw, (float)ch);
            Bitmap t = new Bitmap(bw, bh);
            using (Graphics g = Graphics.FromImage(t))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(img, new RectangleF(0, 0, bw, bh), crop, GraphicsUnit.Pixel);
            }
            return t;
        }

        void BuildThumb()
        {
            try
            {
                string src = IsImage ? FilePath : Paths[0];
                if (new FileInfo(src).Length > 40L * 1024 * 1024) return;   // 太大的图不生成预览
                byte[] b = File.ReadAllBytes(src);
                using (MemoryStream ms = new MemoryStream(b))
                using (Image img = Image.FromStream(ms))
                {
                    W = img.Width; H = img.Height;
                    int bw = Gfx.S(84), bh = Gfx.S(60);
                    Bitmap t;
                    if (W <= bw && H <= bh)
                    {
                        // 小图不放大,保持清晰
                        t = new Bitmap(W, H);
                        using (Graphics g = Graphics.FromImage(t)) g.DrawImage(img, 0, 0, W, H);
                    }
                    else
                    {
                        // 等比缩放后居中裁切,铺满缩略图框,所有缩略图大小一致更整齐
                        double sc = Math.Max(bw / (double)W, bh / (double)H);
                        double cw = bw / sc, ch = bh / sc;
                        RectangleF crop = new RectangleF((float)((W - cw) / 2), (float)((H - ch) / 2), (float)cw, (float)ch);
                        t = new Bitmap(bw, bh);
                        using (Graphics g = Graphics.FromImage(t))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(img, new RectangleF(0, 0, bw, bh), crop, GraphicsUnit.Pixel);
                        }
                    }
                    Thumb = t;
                    Preview = MakeCover(img, ShelfMetrics.CardW, ShelfMetrics.BodyH);
                }
            }
            catch { }
            finally { thumbDone = true; }
        }
    }

    class History
    {
        public List<Item> Items = new List<Item>();   // 最新的在最前
        public string Dir;

        public History(string dir)
        {
            Dir = dir;
            Directory.CreateDirectory(Dir);
            Instance = this;
            Load();
        }

        public int PinnedCount
        {
            get
            {
                int n = 0;
                foreach (Item it in Items) if (it.Pinned) n++;
                return n;
            }
        }

        void Load()
        {
            foreach (Item old in Items) old.DisposeCaches();
            Items.Clear();
            LoadMeta();
            LoadBoards();
            string[] files = Directory.GetFiles(Dir);
            Array.Sort(files, StringComparer.Ordinal);
            Array.Reverse(files);
            foreach (string f in files)
            {
                string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".txt" && ext != ".png" && ext != ".files") continue;
                string[] parts = System.IO.Path.GetFileNameWithoutExtension(f).Split('_');
                if (parts.Length != 4 && parts.Length != 5) continue;
                DateTime t;
                if (!DateTime.TryParseExact(parts[0] + parts[1] + parts[2], "yyyyMMddHHmmssfff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out t)) continue;
                Item it = new Item();
                it.FilePath = f;
                it.Hash = parts[3];
                it.Time = t;
                it.Pinned = parts.Length == 5 && parts[4] == "p";
                it.IsImage = (ext == ".png");
                it.IsFiles = (ext == ".files");
                if (!it.IsImage)
                {
                    try { it.Text = File.ReadAllText(f, Encoding.UTF8); }
                    catch { continue; }
                }
                ApplyMeta(it);
                Items.Add(it);
            }
            ApplyOrder();
            Trim();
            SaveMeta();
            SaveOrder();
        }

        // ---- 来源程序 + 自定义标题颜色:index.tsv,每行"hash \t 程序路径 \t 程序名 \t 颜色"
        readonly Dictionary<string, string[]> meta = new Dictionary<string, string[]>();

        string MetaFile
        {
            get { return System.IO.Path.Combine(Dir, "index.tsv"); }
        }

        string OrderFile
        {
            get { return System.IO.Path.Combine(Dir, "order.txt"); }
        }

        void LoadMeta()
        {
            meta.Clear();
            try
            {
                if (!File.Exists(MetaFile)) return;
                foreach (string line in File.ReadAllLines(MetaFile, Encoding.UTF8))
                {
                    string[] cols = line.Split('\t');
                    if (cols.Length >= 3 && cols[0].Length > 0)
                        meta[cols[0]] = new string[] { Col(cols, 1), Col(cols, 2), Col(cols, 3), Col(cols, 4), Col(cols, 5) };
                }
            }
            catch { }
        }

        static string Col(string[] cols, int k)
        {
            return k < cols.Length ? cols[k] : "";
        }

        // 列:hash, 程序路径, 程序名, 标题颜色, 分类, 自定义标题
        static string MetaLine(Item it)
        {
            return it.Hash + "\t" + Clean(it.AppPath) + "\t" + Clean(it.AppName) + "\t" +
                   (it.CustomColor != 0 ? it.CustomColor.ToString(CultureInfo.InvariantCulture) : "") + "\t" +
                   Clean(it.Board) + "\t" + Clean(it.CustomTitle);
        }

        void SaveMeta()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (Item it in Items)
                {
                    if (!string.IsNullOrEmpty(it.AppPath) || !string.IsNullOrEmpty(it.AppName) || it.CustomColor != 0 ||
                        it.Board != null || it.CustomTitle != null)
                        lines.Add(MetaLine(it));
                }
                File.WriteAllLines(MetaFile, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        void ApplyMeta(Item it)
        {
            string[] m;
            if (meta.TryGetValue(it.Hash, out m))
            {
                it.AppPath = m[0].Length > 0 ? m[0] : null;
                it.AppName = m[1].Length > 0 ? m[1] : null;
                if (Util.IsRelay(it.AppPath)) { it.AppPath = null; it.AppName = null; }   // 以前被记成远程控制软件的,不显示错的图标
                int cc;
                it.CustomColor = (m.Length > 2 && int.TryParse(m[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out cc)) ? cc : 0;
                it.Board = (m.Length > 3 && m[3].Length > 0) ? m[3] : null;
                it.CustomTitle = (m.Length > 4 && m[4].Length > 0) ? m[4] : null;
            }
        }

        static string Clean(string v)
        {
            return v == null ? "" : v.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        }

        int metaAppends;

        void WriteMeta(Item it)
        {
            meta[it.Hash] = new string[] { Clean(it.AppPath), Clean(it.AppName),
                it.CustomColor != 0 ? it.CustomColor.ToString(CultureInfo.InvariantCulture) : "",
                Clean(it.Board), Clean(it.CustomTitle) };
            // index.tsv 是一直往后追加的;追加多了就整理一次,文件不会越变越大
            if (++metaAppends >= 300) { metaAppends = 0; SaveMeta(); return; }
            try { File.AppendAllText(MetaFile, MetaLine(it) + "\r\n", new UTF8Encoding(false)); }
            catch { }
        }

        // 用户删掉的记录:连同它的标题、颜色、分类一起忘掉
        // (否则之后再复制同样的内容,会"复活"以前起的标题和分类)
        void Forget(Item it)
        {
            if (!Items.Any(x => x.Hash == it.Hash)) meta.Remove(it.Hash);   // 同样的内容又复制回来了:标题等设置留给新的那条
            DeleteFile(it);
        }

        // ---- 可以撤销的删除:先从列表里拿掉,文件过几秒才真正删除
        Item pendingDel;
        int pendingIdx;

        public void RemoveUndoable(Item it)
        {
            FinishPendingDelete();
            int i = Items.IndexOf(it);
            if (i < 0) return;
            Items.RemoveAt(i);
            pendingDel = it;
            pendingIdx = i;
            SaveOrder();
        }

        public Item UndoDelete()
        {
            Item it = pendingDel;
            if (it == null) return null;
            pendingDel = null;
            if (Items.Any(x => x.Hash == it.Hash)) { Forget(it); return null; }   // 期间又复制了同样的内容
            Items.Insert(Math.Min(pendingIdx, Items.Count), it);
            SaveOrder();
            return it;
        }

        public void FinishPendingDelete()
        {
            Item it = pendingDel;
            if (it == null) return;
            pendingDel = null;
            Forget(it);
        }

        public void SetBoard(Item it, string boardId)
        {
            if (it == null) return;
            it.Board = boardId;
            WriteMeta(it);
        }

        public void SetTitle(Item it, string title)
        {
            if (it == null) return;
            it.CustomTitle = string.IsNullOrEmpty(title) ? null : Clean(title);
            WriteMeta(it);
        }

        // ---- 自定义分类:boards.txt,每行"Id \t 名称 \t 颜色"
        public static History Instance;
        public List<Board> Boards = new List<Board>();

        string BoardsFile
        {
            get { return System.IO.Path.Combine(Dir, "boards.txt"); }
        }

        void LoadBoards()
        {
            Boards.Clear();
            try
            {
                if (!File.Exists(BoardsFile)) return;
                foreach (string line in File.ReadAllLines(BoardsFile, Encoding.UTF8))
                {
                    string[] cols = line.Split('\t');
                    if (cols.Length < 3 || cols[0].Length == 0) continue;
                    Board b = new Board();
                    b.Id = cols[0];
                    b.Name = cols[1];
                    int c;
                    b.Color = int.TryParse(cols[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out c) ? c : Theme.Accents[0].ToArgb();
                    Boards.Add(b);
                }
            }
            catch { }
        }

        void SaveBoards()
        {
            try
            {
                File.WriteAllLines(BoardsFile,
                    Boards.Select(b => b.Id + "\t" + Clean(b.Name) + "\t" + b.Color.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    new UTF8Encoding(false));
            }
            catch { }
        }

        public Board FindBoard(string id)
        {
            if (id == null) return null;
            foreach (Board b in Boards) if (b.Id == id) return b;
            return null;
        }

        public Board AddBoard(string name, int color)
        {
            Board b = new Board();
            b.Id = "b" + DateTime.Now.Ticks.ToString("x", CultureInfo.InvariantCulture);
            b.Name = Clean(name);
            b.Color = color;
            Boards.Add(b);
            SaveBoards();
            return b;
        }

        public void UpdateBoard(Board b, string name, int color)
        {
            b.Name = Clean(name);
            b.Color = color;
            SaveBoards();
        }

        // 删除分类:里面的内容保留,只是不再归类
        public void DeleteBoard(Board b)
        {
            Boards.Remove(b);
            foreach (Item it in Items) if (it.Board == b.Id) it.Board = null;
            SaveBoards();
            SaveMeta();
        }

        public void SetSource(Item it, string path, string name)
        {
            if (it == null || (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(name))) return;
            if (it.AppPath == (string.IsNullOrEmpty(path) ? null : path) &&
                it.AppName == (string.IsNullOrEmpty(name) ? null : name)) return;   // 没变就不写文件
            it.AppPath = string.IsNullOrEmpty(path) ? null : path;
            it.AppName = string.IsNullOrEmpty(name) ? null : name;
            WriteMeta(it);
        }

        public void SetColor(Item it, int argb)
        {
            if (it == null) return;
            it.CustomColor = argb;
            WriteMeta(it);
        }

        // ---- 自定义排序:order.txt 按顺序记录每条的 hash
        void ApplyOrder()
        {
            try
            {
                if (!File.Exists(OrderFile)) return;
                string[] lines = File.ReadAllLines(OrderFile);
                Dictionary<string, int> rank = new Dictionary<string, int>();
                for (int k = 0; k < lines.Length; k++)
                {
                    string h = lines[k].Trim();
                    if (h.Length > 0 && !rank.ContainsKey(h)) rank[h] = k;
                }
                List<Item> fresh = Items.Where(x => !rank.ContainsKey(x.Hash)).ToList();
                List<Item> ranked = Items.Where(x => rank.ContainsKey(x.Hash)).OrderBy(x => rank[x.Hash]).ToList();
                Items.Clear();
                Items.AddRange(fresh);
                Items.AddRange(ranked);
            }
            catch { }
        }

        void SaveOrder()
        {
            try { File.WriteAllLines(OrderFile, Items.Select(x => x.Hash).ToArray()); }
            catch { }
        }

        // 拖拽排序:把 it 放到 prev 之后 / next 之前
        public void MoveNear(Item it, Item prev, Item next)
        {
            if (!Items.Contains(it)) return;
            Items.Remove(it);
            int idx;
            if (next != null && (idx = Items.IndexOf(next)) >= 0) Items.Insert(idx, it);
            else if (prev != null && (idx = Items.IndexOf(prev)) >= 0) Items.Insert(idx + 1, it);
            else Items.Insert(0, it);
            SaveOrder();
        }

        string BuildPath(string hash, string ext, DateTime time, bool pinned)
        {
            string name = time.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)
                          + "_" + hash + (pinned ? "_p" : "") + ext;
            return System.IO.Path.Combine(Dir, name);
        }

        void DeleteFile(Item it)
        {
            try { if (File.Exists(it.FilePath)) File.Delete(it.FilePath); } catch { }
            it.DisposeCaches();
        }

        // 同内容的旧记录会被删除;如果旧记录是置顶的,新记录继承置顶
        bool RemoveByHash(string hash)
        {
            Item old = Items.FirstOrDefault(x => x.Hash == hash);
            if (old == null) return false;
            bool pinned = old.Pinned;
            Items.Remove(old);
            DeleteFile(old);
            return pinned;
        }

        void Insert(Item it)
        {
            ApplyMeta(it);
            Items.Insert(0, it);
            Trim();
            SaveOrder();
        }

        public void Trim()
        {
            int count = 0;
            foreach (Item it in Items.ToList())
            {
                if (it.Pinned || it.Board != null) continue;   // 置顶和分类里的不会被自动删除
                count++;
                if (count > Config.MaxItems)
                {
                    Items.Remove(it);
                    Forget(it);
                }
            }
        }

        public void Purge(int days)
        {
            if (days <= 0) return;
            DateTime cutoff = DateTime.Now.AddDays(-days);
            bool changed = false;
            foreach (Item it in Items.ToList())
            {
                if (!it.Pinned && it.Board == null && it.Time < cutoff)
                {
                    Items.Remove(it);
                    Forget(it);
                    changed = true;
                }
            }
            if (changed) SaveOrder();   // 每次打开面板都会检查一遍,没删东西就不写文件
        }

        public Item AddText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > Config.MaxTextLength) return null;
            string hash = Util.Md5Short(Encoding.UTF8.GetBytes(text));
            if (Items.Count > 0 && Items[0].Hash == hash) return Items[0];
            bool pinned = RemoveByHash(hash);
            DateTime now = DateTime.Now;
            string path = BuildPath(hash, ".txt", now, pinned);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Item it = new Item();
            it.IsImage = false; it.FilePath = path; it.Hash = hash; it.Text = text;
            it.Time = now; it.Pinned = pinned;
            Insert(it);
            return it;
        }

        // 复制了文件/文档(在资源管理器里 Ctrl+C):记录路径,之后可以作为文件粘贴回去
        public Item AddFiles(string[] files)
        {
            if (files == null || files.Length == 0) return null;
            string text = string.Join("\n", files);
            if (text.Length > Config.MaxTextLength) return null;
            string hash = Util.Md5Short(Encoding.UTF8.GetBytes("files:" + text));
            if (Items.Count > 0 && Items[0].Hash == hash) return Items[0];
            bool pinned = RemoveByHash(hash);
            DateTime now = DateTime.Now;
            string path = BuildPath(hash, ".files", now, pinned);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Item it = new Item();
            it.IsImage = false; it.IsFiles = true; it.FilePath = path; it.Hash = hash; it.Text = text;
            it.Time = now; it.Pinned = pinned;
            Insert(it);
            return it;
        }

        public Item AddImage(Image img)
        {
            byte[] png;
            using (MemoryStream ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png);
                png = ms.ToArray();
            }
            return AddImagePng(png, Util.Md5Short(png), DateTime.Now);
        }

        // 图片已经在后台编码成 PNG、算好校验值,这里只做去重和保存(很快)
        public Item AddImagePng(byte[] png, string hash, DateTime now)
        {
            if (Items.Count > 0 && Items[0].Hash == hash) return Items[0];
            bool pinned = RemoveByHash(hash);
            string path = BuildPath(hash, ".png", now, pinned);
            File.WriteAllBytes(path, png);
            Item it = new Item();
            it.IsImage = true; it.FilePath = path; it.Hash = hash;
            it.Time = now; it.Pinned = pinned;
            Insert(it);
            return it;
        }

        // 把某条记录顶到最前面(用新的时间重命名)
        public void Touch(Item it)
        {
            Items.Remove(it);
            it.Time = DateTime.Now;
            string np = BuildPath(it.Hash, it.Ext, it.Time, it.Pinned);
            try { File.Move(it.FilePath, np); it.FilePath = np; } catch { }
            Items.Insert(0, it);
            SaveOrder();
        }

        public void TogglePin(Item it)
        {
            it.Pinned = !it.Pinned;
            string np = BuildPath(it.Hash, it.Ext, it.Time, it.Pinned);
            try { File.Move(it.FilePath, np); it.FilePath = np; }
            catch { it.Pinned = !it.Pinned; }
        }

        public void Remove(Item it)
        {
            Items.Remove(it);
            Forget(it);
            SaveOrder();
        }

        public void Clear(bool keepPinned)
        {
            foreach (Item it in Items.ToList())
            {
                if (keepPinned && (it.Pinned || it.Board != null)) continue;
                Items.Remove(it);
                Forget(it);
            }
            SaveOrder();
        }

        // 切换数据文件夹;move=true 时把现有记录一起搬过去
        public bool SwitchTo(string newDir, bool move, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(newDir);
                if (move)
                {
                    List<string> copied = new List<string>();
                    foreach (Item it in Items)
                    {
                        string dest = System.IO.Path.Combine(newDir, System.IO.Path.GetFileName(it.FilePath));
                        File.Copy(it.FilePath, dest, true);
                        copied.Add(it.FilePath);
                    }
                    if (File.Exists(MetaFile))
                    {
                        File.Copy(MetaFile, System.IO.Path.Combine(newDir, "index.tsv"), true);
                        copied.Add(MetaFile);
                    }
                    if (File.Exists(OrderFile))
                    {
                        File.Copy(OrderFile, System.IO.Path.Combine(newDir, "order.txt"), true);
                        copied.Add(OrderFile);
                    }
                    if (File.Exists(BoardsFile))
                    {
                        File.Copy(BoardsFile, System.IO.Path.Combine(newDir, "boards.txt"), true);
                        copied.Add(BoardsFile);
                    }
                    foreach (string f in copied)
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                Dir = newDir;
                Load();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    // ================================================================ 自绘控件
    // 分段按钮 / 标签页 / 分类筛选
    class Chips : Control
    {
        public string[] Labels = new string[0];
        public Color[] Dots = new Color[0];        // 标签前的小圆点颜色(Color.Empty = 无)
        public event Action<int, Point> RightClicked;
        public int DropHighlight = -1;            // 拖卡片经过时高亮的标签
        public bool Multi, Stretch, TabStyle;
        public int Selected;
        public bool[] Checked = new bool[0];
        public event EventHandler Changed;
        int hover = -1;

        public Chips()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Font = Gfx.UI;
            Height = Gfx.S(30);
        }

        public void SetLabels(string[] labels)
        {
            Labels = labels;
            Checked = new bool[labels.Length];
            Dots = new Color[labels.Length];
            Invalidate();
        }

        // 标签放不下时:放得下的照常显示,其余收进末尾的"+N"按钮,点开是个菜单
        Rectangle moreRect = Rectangle.Empty;
        int firstHidden = -1;
        public event Action<bool> MenuState;   // 菜单打开/关闭(面板据此决定失去焦点时要不要收起)

        int FullWidth(out Rectangle[] rs)
        {
            int n = Labels.Length;
            rs = new Rectangle[n];
            int gap = Gfx.S(TabStyle ? 20 : 6);
            int x = 0;
            for (int i = 0; i < n; i++)
            {
                int w;
                if (Stretch) w = (Width - gap * (n - 1)) / Math.Max(1, n);
                else if (TabStyle) w = TextRenderer.MeasureText(Labels[i], Gfx.Bold).Width + (HasDot(i) ? Gfx.S(14) : 0);
                else w = TextRenderer.MeasureText(Labels[i], Font).Width + Gfx.S(14);
                rs[i] = new Rectangle(x, 0, w, Height);
                x += w + gap;
            }
            return n == 0 ? 0 : x - gap;
        }

        // 全部标签需要多宽(面板用来决定要不要给快捷键提示留位置)
        public int ContentWidth { get { Rectangle[] r; return FullWidth(out r); } }

        Rectangle[] Layout()
        {
            Rectangle[] rs;
            int total = FullWidth(out rs);
            moreRect = Rectangle.Empty;
            firstHidden = -1;
            if (TabStyle && !Stretch && total > Width && rs.Length > 1)
            {
                int gap = Gfx.S(20), moreW = Gfx.S(40);
                int k = rs.Length;
                while (k > 1 && rs[k - 1].Right + gap + moreW > Width) k--;
                firstHidden = k;
                for (int i = k; i < rs.Length; i++) rs[i] = Rectangle.Empty;
                int mx = rs[k - 1].Right + gap - Gfx.S(6);
                moreRect = new Rectangle(mx, 0, Math.Min(moreW, Width - mx), Height);
            }
            return rs;
        }

        static string MenuLabel(string s) { return s == "＋" ? "＋ 新建" : s; }

        void ShowMore()
        {
            if (firstHidden < 0) return;
            ContextMenuStrip m = new ContextMenuStrip();
            for (int i = firstHidden; i < Labels.Length; i++)
            {
                int idx = i;
                ToolStripMenuItem mi = new ToolStripMenuItem(MenuLabel(Labels[i]), HasDot(i) ? DotImage(Dots[i]) : null, delegate
                {
                    Selected = idx;
                    Invalidate();
                    if (Changed != null) Changed(this, EventArgs.Empty);
                });
                mi.Checked = idx == Selected && !Multi;
                m.Items.Add(mi);
            }
            Theme.StyleMenu(m);
            if (MenuState != null) MenuState(true);
            m.Closed += delegate { if (MenuState != null) MenuState(false); };
            m.Show(this, new Point(moreRect.X, Height));
        }

        static Bitmap DotImage(Color c)
        {
            int sz = Gfx.S(12), d = Gfx.S(8);
            Bitmap b = new Bitmap(sz, sz);
            using (Graphics g = Graphics.FromImage(b))
            using (SolidBrush br = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(br, (sz - d) / 2, (sz - d) / 2, d, d);
            }
            return b;
        }

        bool HasDot(int i)
        {
            return i < Dots.Length && Dots[i] != Color.Empty;
        }

        public int TabAt(Point p) { int h = HitTest(p); return h < 0 ? -1 : h; }

        int HitTest(Point p)
        {
            Rectangle[] rs = Layout();
            if (!moreRect.IsEmpty && moreRect.Contains(p)) return -2;   // "+N"
            for (int i = 0; i < rs.Length; i++)
            {
                Rectangle r = rs[i];
                if (r.IsEmpty) continue;
                if (TabStyle) r.Inflate(Gfx.S(8), 0);
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Gfx.PaintParentBack(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle[] rs = Layout();

            if (TabStyle)
            {
                // 文字标签 + 底部强调线,比一排按钮更干净
                using (Pen hp = new Pen(t.Border))
                    g.DrawLine(hp, 0, Height - 1, Width, Height - 1);
                if (!moreRect.IsEmpty)
                {
                    int hiddenN = Labels.Length - firstHidden;
                    bool selHidden = Selected >= firstHidden;
                    bool hot = hover == -2;
                    Rectangle pr = new Rectangle(moreRect.X, (Height - Gfx.S(4) - Gfx.S(22)) / 2, moreRect.Width, Gfx.S(22));
                    Gfx.FillRound(g, Color.FromArgb(hot ? (t.Dark ? 60 : 40) : (t.Dark ? 34 : 22), t.Text), pr, 99);
                    Gfx.Text(g, "+" + hiddenN, Gfx.Small, pr, selHidden ? t.Accent : (hot ? t.Text : t.Sub), TextFormatFlags.HorizontalCenter);
                    if (selHidden)
                        Gfx.FillRound(g, t.Accent, new Rectangle(moreRect.X, Height - Gfx.S(3), moreRect.Width, Gfx.S(3)), 2);
                }
                for (int i = 0; i < rs.Length; i++)
                {
                    if (rs[i].IsEmpty) continue;   // 收进"+N"里了
                    bool on = i == Selected;
                    Color fg = (on || i == hover) ? t.Text : t.Sub;
                    Rectangle tr = new Rectangle(rs[i].X, 0, rs[i].Width, Height - Gfx.S(4));
                    if (i == DropHighlight)
                    {
                        Rectangle hl = new Rectangle(rs[i].X - Gfx.S(8), Gfx.S(1), rs[i].Width + Gfx.S(16), Height - Gfx.S(4));
                        Gfx.FillRound(g, Color.FromArgb(50, t.Accent), hl, 99);
                        Gfx.DrawRound(g, t.Accent, hl, 99, Gfx.F(1.5f));
                    }
                    if (HasDot(i))
                    {
                        int ds = Gfx.S(8);
                        using (SolidBrush db = new SolidBrush(Dots[i]))
                            g.FillEllipse(db, tr.X, tr.Y + (tr.Height - ds) / 2, ds, ds);
                        tr = new Rectangle(tr.X + Gfx.S(14), tr.Y, tr.Width - Gfx.S(14), tr.Height);
                    }
                    Gfx.Text(g, Labels[i], on ? Gfx.Bold : Font, tr, fg, TextFormatFlags.HorizontalCenter);
                    if (on)
                        Gfx.FillRound(g, t.Accent, new Rectangle(rs[i].X, Height - Gfx.S(3), rs[i].Width, Gfx.S(3)), 2);
                }
                return;
            }

            for (int i = 0; i < rs.Length; i++)
            {
                bool on = Multi ? (i < Checked.Length && Checked[i]) : i == Selected;
                Color fill = on ? t.Accent : (i == hover ? t.Border : t.Input);
                Color fg = on ? Color.White : (i == hover ? t.Text : t.Sub);
                Gfx.FillRound(g, fill, rs[i], 99);
                Gfx.Text(g, Labels[i], Font, rs[i], fg, TextFormatFlags.HorizontalCenter);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = HitTest(e.Location);
            if (i == -2) { ShowMore(); return; }
            if (i < 0) return;
            if (e.Button == MouseButtons.Right)
            {
                if (RightClicked != null) RightClicked(i, PointToScreen(e.Location));
                return;
            }
            if (Multi) Checked[i] = !Checked[i];
            else Selected = i;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    // 开关
    class Toggle : Control
    {
        public bool Checked;
        public string Label = "";
        public event EventHandler Changed;

        public Toggle()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Font = Gfx.UI;
            Height = Gfx.S(34);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Gfx.Text(g, Label, Font, new Rectangle(0, 0, Width - Gfx.S(56), Height), t.Text, TextFormatFlags.Left);
            Rectangle sw = new Rectangle(Width - Gfx.S(44), (Height - Gfx.S(22)) / 2, Gfx.S(40), Gfx.S(22));
            Gfx.FillRound(g, Checked ? t.Accent : t.Border, sw, 11);
            int kx = Checked ? sw.Right - Gfx.S(19) : sw.X + Gfx.S(3);
            using (SolidBrush b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx, sw.Y + Gfx.S(3), Gfx.S(16), Gfx.S(16));
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Checked = !Checked;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    // 主题色选择
    class Swatches : Control
    {
        public int Selected;
        public event EventHandler Changed;
        int hover = -1;

        public Swatches()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Height = Gfx.S(36);
        }

        // 圆点按控件高度垂直居中,外圈也留足了空间,不会被裁掉;选中时只加一圈同色外环
        Rectangle RectOf(int i)
        {
            int d = Gfx.S(22);
            return new Rectangle(Gfx.S(8) + i * Gfx.S(36), (Height - d) / 2, d, d);
        }

        public int PreferredWidthFor
        {
            get { return Gfx.S(8) * 2 + (Theme.Accents.Length - 1) * Gfx.S(36) + Gfx.S(22); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            for (int i = 0; i < Theme.Accents.Length; i++)
            {
                Rectangle r = RectOf(i);
                Color c = Theme.Accents[i];
                if (i == Selected || i == hover)
                {
                    RectangleF ring = r;
                    ring.Inflate(Gfx.F(4.5f), Gfx.F(4.5f));
                    using (Pen p = new Pen(i == Selected ? c : Color.FromArgb(90, c), Gfx.F(2f))) g.DrawEllipse(p, ring);
                }
                using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, r);
                // 轻微的内描边,让圆点更有质感
                using (Pen ip = new Pen(Color.FromArgb(40, 0, 0, 0), 1f)) g.DrawEllipse(ip, r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
            }
        }

        int Hit(Point p)
        {
            for (int i = 0; i < Theme.Accents.Length; i++)
            {
                Rectangle r = RectOf(i);
                r.Inflate(Gfx.S(6), Gfx.S(6));
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Hit(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = Hit(e.Location);
            if (i >= 0 && i != Selected) { Selected = i; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        }
    }

    // 快捷键录入框:点击后直接按下想要的组合键
    class HotkeyBox : Control
    {
        public bool Ctrl, Alt, Shift, Win;
        public Keys Key = Keys.V;
        bool recording;

        public HotkeyBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Gfx.UI;
            Height = Gfx.S(36);
        }

        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Gfx.FillRound(g, t.Input, r, 9);
            Gfx.DrawRound(g, recording ? t.Accent : t.Border, r, 9, recording ? 2f : 1f);
            string s = recording ? "请按下新的快捷键…(Esc 取消)" : Config.Describe(Ctrl, Alt, Shift, Win, Key);
            Gfx.Text(g, s, Font, new Rectangle(Gfx.S(14), 0, Width - Gfx.S(28), Height),
                recording ? t.Sub : t.Text, TextFormatFlags.Left);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();
            recording = true;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            recording = false;
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!recording) return base.ProcessCmdKey(ref msg, keyData);
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Escape) { recording = false; Invalidate(); return true; }
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu ||
                k == Keys.LWin || k == Keys.RWin) return true;
            bool c = (keyData & Keys.Control) != 0;
            bool a = (keyData & Keys.Alt) != 0;
            bool s = (keyData & Keys.Shift) != 0;
            bool ok = (k >= Keys.A && k <= Keys.Z) || (k >= Keys.D0 && k <= Keys.D9) ||
                      (k >= Keys.F1 && k <= Keys.F12);
            if (ok && (c || a))   // 至少要有 Ctrl 或 Alt,避免影响正常打字
            {
                Ctrl = c; Alt = a; Shift = s; Win = false; Key = k;
                recording = false;
                Invalidate();
            }
            return true;
        }
    }

    class IconButton : Control
    {
        public string Glyph = "";
        public bool MoreDots;     // 画成三个点
        bool hover;

        public IconButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Gfx.PaintParentBack(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hover) Gfx.FillRound(g, t.Input, new Rectangle(0, 0, Width, Height), 99);
            if (MoreDots)
            {
                int d = Gfx.S(4), gap = Gfx.S(4);
                int x0 = (Width - d * 3 - gap * 2) / 2, y0 = (Height - d) / 2;
                using (SolidBrush b = new SolidBrush(hover ? t.Text : Theme.Blend(t.Sub, t.Text, 0.45)))
                    for (int k = 0; k < 3; k++) g.FillEllipse(b, x0 + k * (d + gap), y0, d, d);
                return;
            }
            Gfx.Text(g, Glyph, Gfx.Symbol, new Rectangle(0, 0, Width, Height),
                hover ? t.Text : t.Sub, TextFormatFlags.HorizontalCenter);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    }

    // 文件夹窗口标题栏上的小按钮:图钉(钉在最前面)/ 最小化
    class WinButton : Control
    {
        public bool IsPin;      // true 图钉  false 最小化
        public bool Active;     // 图钉:是否钉住
        bool hover;

        public WinButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Gfx.PaintParentBack(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hover) Gfx.FillRound(g, t.Input, new Rectangle(0, 0, Width, Height), 99);
            float cx = Width / 2f, cy = Height / 2f;
            if (!IsPin)
            {
                using (Pen p = new Pen(hover ? t.Text : Theme.Blend(t.Sub, t.Text, 0.45), Gfx.F(1.8f)))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    g.DrawLine(p, cx - Gfx.F(5), cy + Gfx.F(1), cx + Gfx.F(5), cy + Gfx.F(1));
                }
                return;
            }
            // 图钉:钉住时竖直、主题色实心;没钉住时斜着、空心
            Color c = Active ? t.Accent : (hover ? t.Text : Theme.Blend(t.Sub, t.Text, 0.45));
            GraphicsState st = g.Save();
            g.TranslateTransform(cx, cy);
            if (!Active) g.RotateTransform(45f);
            float u = Gfx.F(0.82f);
            using (GraphicsPath head = new GraphicsPath())
            {
                head.AddLine(-4 * u, -8 * u, 4 * u, -8 * u);
                head.AddLine(3 * u, -7 * u, 3 * u, -2 * u);
                head.AddLine(6 * u, 2 * u, -6 * u, 2 * u);
                head.AddLine(-3 * u, -2 * u, -3 * u, -7 * u);
                head.CloseFigure();
                if (Active) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, head);
                using (Pen p = new Pen(c, Gfx.F(1.5f))) { p.LineJoin = LineJoin.Round; g.DrawPath(p, head); }
            }
            using (Pen n = new Pen(c, Gfx.F(1.6f)))
            {
                n.EndCap = LineCap.Round;
                g.DrawLine(n, 0, 2 * u, 0, 8 * u);
            }
            g.Restore(st);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    }

    class Btn : Control
    {
        public bool Primary;
        bool hover;

        public Btn()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Font = Gfx.UI;
            Height = Gfx.S(34);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);
            Color fill;
            if (Primary) fill = hover ? Theme.Blend(t.Accent, Color.Black, 0.12) : t.Accent;
            else fill = hover ? t.Border : t.Input;
            Gfx.FillRound(g, fill, r, 9);
            Gfx.Text(g, Text, Font, r, Primary ? Color.White : t.Text, TextFormatFlags.HorizontalCenter);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    }

    // 圆角输入框(外壳自绘,里面是无边框 TextBox)
    class SearchBox : Control
    {
        public readonly TextBox Box = new TextBox();
        public string Cue = "";
        public bool Pill;          // 胶囊形(和"剪切板 | 文件夹"开关一样的全圆角)
        public bool ShowClear;     // 有文字时右侧显示清除按钮
        bool showIcon = true;
        bool clearHover;

        public bool ShowIcon
        {
            get { return showIcon; }
            set { showIcon = value; LayoutBox(); Invalidate(); }
        }

        public SearchBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Box.BorderStyle = BorderStyle.None;
            Box.Font = Gfx.Input;
            Box.HandleCreated += delegate
            {
                if (Cue.Length > 0) Native.SendMessage(Box.Handle, 0x1501, (IntPtr)1, Cue);
            };
            Controls.Add(Box);
            Height = Gfx.S(40);
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Box.TextChanged += delegate { if (ShowClear) Invalidate(); };
            ApplyTheme();
        }

        Rectangle ClearRect
        {
            get
            {
                int d = Gfx.S(18);
                return new Rectangle(Width - Gfx.S(Pill ? 10 : 12) - d, (Height - d) / 2, d, d);
            }
        }

        bool ClearVisible { get { return ShowClear && Box.Text.Length > 0; } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool h = ClearVisible && ClearRect.Contains(e.Location);
            if (h != clearHover) { clearHover = h; Cursor = h ? Cursors.Hand : Cursors.IBeam; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (clearHover) { clearHover = false; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (ClearVisible && ClearRect.Contains(e.Location)) Box.Text = "";
            Box.Focus();
        }

        public void SetCue(string c)
        {
            Cue = c;
            if (Box.IsHandleCreated) Native.SendMessage(Box.Handle, 0x1501, (IntPtr)1, c);
        }

        public void ApplyTheme()
        {
            Box.BackColor = Theme.Cur.Input;
            Box.ForeColor = Theme.Cur.Text;
            Invalidate();
        }

        void LayoutBox()
        {
            int x = Pill ? Gfx.S(showIcon ? 34 : 14) : Gfx.S(showIcon ? 38 : 14);
            int right = ShowClear ? Gfx.S(36) : Gfx.S(14);
            Box.SetBounds(x, Math.Max(0, (Height - Box.Height) / 2 + (Pill ? 1 : 0)), Math.Max(10, Width - x - right), Box.Height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutBox();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Gfx.PaintParentBack(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle all = new Rectangle(0, 0, Width - 1, Height - 1);
            int rad = Pill ? 99 : 11;
            bool focused = Box.Focused;
            Gfx.FillRound(g, t.Input, all, rad);
            // 一圈很淡的描边;获得焦点时换成主题色的细光圈
            if (focused) Gfx.DrawRound(g, Color.FromArgb(150, t.Accent), all, rad, Gfx.F(1.4f));
            else Gfx.DrawRound(g, Color.FromArgb(t.Dark ? 34 : 22, t.Text), all, rad, Gfx.F(1f));
            if (showIcon)
            {
                int cy = Height / 2;
                int ix = Pill ? Gfx.S(13) : Gfx.S(15);
                using (Pen p = new Pen(focused ? t.Text : t.Sub, Gfx.F(1.7f)))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    int d = Gfx.S(Pill ? 10 : 11);
                    g.DrawEllipse(p, ix, cy - d / 2 - Gfx.S(1), d, d);
                    g.DrawLine(p, ix + d - Gfx.S(2), cy + d / 2 - Gfx.S(2), ix + d + Gfx.S(3), cy + d / 2 + Gfx.S(3));
                }
            }
            if (ClearVisible)
            {
                Rectangle cr = ClearRect;
                using (SolidBrush cb = new SolidBrush(Color.FromArgb(clearHover ? 150 : 90, t.Sub)))
                    g.FillEllipse(cb, cr);
                using (Pen xp = new Pen(t.Input, Gfx.F(1.6f)))
                {
                    xp.StartCap = xp.EndCap = LineCap.Round;
                    int k = Gfx.S(4);
                    int cx = cr.X + cr.Width / 2, cy2 = cr.Y + cr.Height / 2;
                    g.DrawLine(xp, cx - k, cy2 - k, cx + k, cy2 + k);
                    g.DrawLine(xp, cx - k, cy2 + k, cx + k, cy2 - k);
                }
            }
        }
    }

    // 自绘的历史列表:卡片式条目、悬停高亮、细滚动条
    class ItemList : Control, IClipView
    {
        List<Item> items = new List<Item>();
        string[] heads = new string[0];
        int[] tops = new int[0];
        int[] hs = new int[0];
        int total, scrollY, selected = -1, hover = -1;
        public string EmptyText { get; set; }
        public event Action<int> ItemActivated;
        public event Action<int, Point> ItemContext;
        public event Action<int> SelectionChanged;
        public event Action<int> DragStart;
        public event Action<int, int> Reordered;
        public event Action<int, Point> DropAbove;
        public event Action<int> ZoomWheel;
        public event Action<int> PreviewRequested;
        public Func<int, Point, bool> DragAbove { get; set; }

        // 拖拽
        Point downPt;
        int downIdx = -1, insertAt = -1, grabX, grabY;
        bool dragArmed, rowDragging, overTop;
        readonly DragGhost ghost = new DragGhost();
        Bitmap ghostImg;

        public ItemList()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
            ImeMode = ImeMode.Disable;   // 列表区关闭输入法,空格、方向键直接可用
            EmptyText = "没有记录";
            Item.ThumbReady += OnThumbReady;
        }

        // 后台线程通知:缩略图好了,切回界面线程重绘
        void OnThumbReady()
        {
            try
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(new MethodInvoker(Invalidate));
            }
            catch { }
        }

        static bool IsImg(Item it) { return it.IsImage || it.IsImageFile; }
        static int BaseHeight(Item it) { return IsImg(it) ? Gfx.S(76) : Gfx.S(56); }
        static int HeaderHeight { get { return Gfx.S(28); } }

        int MaxScroll { get { return Math.Max(0, total - Height + Gfx.S(4)); } }

        // headers[i] 不为空时,在第 i 条上方显示分组标题(如"置顶""最近")
        public void SetItems(List<Item> list, string[] headers)
        {
            items = list;
            heads = (headers != null && headers.Length == list.Count) ? headers : new string[list.Count];
            tops = new int[items.Count];
            hs = new int[items.Count];
            int y = Gfx.S(2);
            for (int i = 0; i < items.Count; i++)
            {
                tops[i] = y;
                hs[i] = BaseHeight(items[i]) + (heads[i] != null ? HeaderHeight : 0);
                y += hs[i];
            }
            total = y;
            scrollY = 0;
            selected = items.Count > 0 ? 0 : -1;
            hover = -1;
            Invalidate();
        }

        public int Selected
        {
            get { return selected; }
            set
            {
                if (items.Count == 0) { selected = -1; return; }
                int v = Math.Max(0, Math.Min(items.Count - 1, value));
                bool changed = v != selected;
                selected = v;
                EnsureVisible();
                Invalidate();
                if (changed && SelectionChanged != null) SelectionChanged(selected);
            }
        }

        public void Move(int delta) { Selected = (selected < 0 ? 0 : selected) + delta; }

        // mode 为 0 时保持滚动位置(排序、删除后);其他情况回到顶部
        public void SetItems(List<Item> list, int mode)
        {
            int keep = scrollY;
            SetItems(list, (string[])null);
            if (mode == 0)
            {
                scrollY = Math.Max(0, Math.Min(MaxScroll, keep));
                Invalidate();
            }
        }

        public void ScrollBy(float px) { ScrollBy((int)px); }

        public void Relayout() { Invalidate(); }

        public void ScrollBy(int px)
        {
            scrollY = Math.Max(0, Math.Min(MaxScroll, scrollY + px));
            Invalidate();
        }

        void EnsureVisible()
        {
            if (selected < 0 || selected >= items.Count) return;
            int top = tops[selected], bot = top + hs[selected];
            if (top < scrollY) scrollY = top;
            else if (bot > scrollY + Height) scrollY = bot - Height + Gfx.S(2);
            scrollY = Math.Max(0, Math.Min(MaxScroll, scrollY));
        }

        int HitTest(int y)
        {
            int yy = y + scrollY;
            for (int i = 0; i < items.Count; i++)
                if (yy >= tops[i] && yy < tops[i] + hs[i]) return i;
            return -1;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollBy(-e.Delta / 120 * Gfx.S(60));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int i = HitTest(e.Y);
            downIdx = i;
            downPt = e.Location;
            dragArmed = e.Button == MouseButtons.Left && i >= 0;
            if (i >= 0) Selected = i;
            if (e.Button == MouseButtons.Right && i >= 0 && ItemContext != null) ItemContext(i, PointToScreen(e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (rowDragging) { EndRowDrag(e); return; }
            if (dragArmed && e.Button == MouseButtons.Left)
            {
                int i = HitTest(e.Y);
                if (i >= 0 && i == downIdx && ItemActivated != null) ItemActivated(i);
            }
            dragArmed = false;
        }

        void BeginRowDrag(int idx, Point p)
        {
            if (idx < 0 || idx >= items.Count) return;
            rowDragging = true;
            overTop = false;
            insertAt = idx;
            int hdr = heads[idx] != null ? HeaderHeight : 0;
            int cardX = Gfx.S(8), cardY = tops[idx] - scrollY + hdr + Gfx.S(2);
            int cw = Math.Max(1, Width - Gfx.S(22)), ch = Math.Max(1, hs[idx] - hdr - Gfx.S(4));
            grabX = p.X - cardX;
            grabY = p.Y - cardY;
            try
            {
                ghostImg = new Bitmap(cw, ch, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(ghostImg))
                {
                    g.Clear(Theme.Cur.Card);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TranslateTransform(-cardX, -(hdr + Gfx.S(2)));
                    DrawRow(g, idx, 0);
                }
                ghost.ShowCard(ghostImg, PointToScreen(new Point(p.X - grabX, p.Y - grabY)));
            }
            catch { }
            Cursor = Cursors.SizeAll;
            Invalidate();
        }

        void StopRowDrag(bool clearTop)
        {
            if (clearTop && overTop && DragAbove != null) DragAbove(-1, Point.Empty);
            rowDragging = false;
            overTop = false;
            insertAt = -1;
            ghost.HideGhost();
            if (ghostImg != null) { ghostImg.Dispose(); ghostImg = null; }
            Cursor = Cursors.Default;
            Invalidate();
        }

        void DragRowMove(MouseEventArgs e)
        {
            Point screen = PointToScreen(e.Location);
            if (!ClientRectangle.Contains(e.Location))
            {
                // 拖出列表:在面板里(比如分类标签上)就当作"拖到分类",出了面板就拖到其他程序
                bool inside = DragAbove != null && DragAbove(downIdx, screen);
                if (!inside)
                {
                    int idx = downIdx;
                    StopRowDrag(false);
                    if (DragStart != null) DragStart(idx);
                    return;
                }
                overTop = true;
                insertAt = -1;
                ghost.Location = new Point(screen.X + Gfx.S(16), screen.Y + Gfx.S(16));
            }
            else
            {
                if (overTop)
                {
                    overTop = false;
                    if (DragAbove != null) DragAbove(-1, Point.Empty);
                }
                int i = HitTest(e.Y);
                if (i < 0) i = items.Count - 1;
                int yy = e.Y + scrollY;
                insertAt = (i >= 0 && yy > tops[i] + hs[i] / 2) ? i + 1 : i;
                ghost.Location = new Point(screen.X - grabX, screen.Y - grabY);
                if (e.Y < Gfx.S(30)) ScrollBy(-Gfx.S(14));
                else if (e.Y > Height - Gfx.S(30)) ScrollBy(Gfx.S(14));
            }
            Invalidate();
        }

        void EndRowDrag(MouseEventArgs e)
        {
            int from = downIdx, to = insertAt;
            bool wasTop = overTop;
            StopRowDrag(false);
            dragArmed = false;
            if (wasTop)
            {
                if (DropAbove != null) DropAbove(from, PointToScreen(e.Location));
                return;
            }
            if (to < 0 || from < 0) return;
            if (to > from) to--;   // 先拿走再插入,后面的位置要减一
            to = Math.Max(0, Math.Min(items.Count - 1, to));
            if (to != from && Reordered != null) Reordered(from, to);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (rowDragging) { DragRowMove(e); return; }
            if (dragArmed && (e.Button & MouseButtons.Left) != 0)
            {
                Size ds = SystemInformation.DragSize;
                if (Math.Abs(e.X - downPt.X) > ds.Width || Math.Abs(e.Y - downPt.Y) > ds.Height)
                {
                    dragArmed = false;
                    BeginRowDrag(downIdx, downPt);
                    return;
                }
            }
            int h = HitTest(e.Y);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Gfx.PaintParentBack(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (items.Count == 0)
            {
                Gfx.Text(g, EmptyText, Gfx.UI, ClientRectangle, t.Sub, TextFormatFlags.HorizontalCenter);
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                int top = tops[i] - scrollY;
                if (top + hs[i] < 0) continue;
                if (top > Height) break;
                DrawRow(g, i, top);
            }
            // 拖拽排序时的插入位置提示线
            if (rowDragging && !overTop && insertAt >= 0)
            {
                int ly = (insertAt < items.Count ? tops[insertAt] : total) - scrollY;
                Gfx.FillRound(g, t.Accent, new Rectangle(Gfx.S(14), ly - Gfx.S(1), Width - Gfx.S(34), Gfx.S(3)), 2);
                using (SolidBrush ab = new SolidBrush(t.Accent))
                    g.FillEllipse(ab, Gfx.S(10), ly - Gfx.S(4), Gfx.S(8), Gfx.S(8));
            }
            if (total > Height)
            {
                int th = Math.Max(Gfx.S(30), Height * Height / total);
                int ty = (int)((double)scrollY / MaxScroll * (Height - th));
                Gfx.FillRound(g, t.Border, new Rectangle(Width - Gfx.S(6), ty, Gfx.S(3), th), 2);
            }
        }

        void DrawRow(Graphics g, int i, int top)
        {
            Theme t = Theme.Cur;
            Item it = items[i];
            bool sel = i == selected;
            bool imgRow = IsImg(it);
            Color hc = CardLook.Header(it);   // 和卡片色带同一套颜色(跟随分类圆点等)
            int hdr = heads[i] != null ? HeaderHeight : 0;

            if (hdr > 0)
                Gfx.Text(g, heads[i], Gfx.SmallBold,
                    new Rectangle(Gfx.S(22), top + Gfx.S(6), Width - Gfx.S(44), hdr - Gfx.S(6)),
                    t.Sub, TextFormatFlags.Left);

            Rectangle card = new Rectangle(Gfx.S(8), top + hdr + Gfx.S(2), Width - Gfx.S(22), hs[i] - hdr - Gfx.S(4));
            if (sel)
            {
                // 选中:淡淡的主题色底 + 左侧一条强调线,比整块实色更精致
                Gfx.FillRound(g, Theme.Blend(t.Bg, t.Accent, t.Dark ? 0.30 : 0.12), card, 12);
            }
            else if (i == hover)
            {
                Gfx.FillRound(g, t.Hover, card, 12);
            }
            // 左侧一条分类色条
            Gfx.FillRound(g, hc, new Rectangle(card.X + Gfx.S(4), card.Y + Gfx.S(10), Gfx.S(4), Math.Max(4, card.Height - Gfx.S(20))), 2);

            int right = card.Right - Gfx.S(14);
            if (i < 9)
            {
                Gfx.Text(g, (i + 1).ToString(), Gfx.Small,
                    new Rectangle(right - Gfx.S(14), card.Y, Gfx.S(14), card.Height),
                    sel ? t.Accent : t.Sub, TextFormatFlags.HorizontalCenter);
                right -= Gfx.S(22);
            }
            if (it.Pinned)
            {
                Gfx.Text(g, "★", Gfx.Symbol, new Rectangle(right - Gfx.S(18), card.Y, Gfx.S(18), card.Height),
                    Color.FromArgb(255, 190, 0), TextFormatFlags.HorizontalCenter);
                right -= Gfx.S(24);
            }

            // ---------- 图片:左侧圆角缩略图,右侧标题 + 说明
            if (imgRow)
            {
                it.EnsureThumb();
                int bw = Gfx.S(84), bh = Gfx.S(60);
                Rectangle tbox = new Rectangle(card.X + Gfx.S(14), card.Y + (card.Height - bh) / 2, bw, bh);
                Gfx.FillRound(g, t.Input, tbox, 9);
                Bitmap thumb = it.Thumb;
                if (thumb != null)
                {
                    Rectangle tr = new Rectangle(tbox.X + (bw - thumb.Width) / 2,
                        tbox.Y + (bh - thumb.Height) / 2, thumb.Width, thumb.Height);
                    using (TextureBrush tb = new TextureBrush(thumb))
                    using (GraphicsPath ip = Gfx.Round(tr, 9))
                    {
                        tb.TranslateTransform(tr.X, tr.Y);
                        g.FillPath(tb, ip);
                    }
                }
                else if (!it.ThumbPending)
                {
                    if (it.IsFiles) it.EnsureFileIcon();
                    if (it.FileIcon != null)
                    {
                        int fsz = Gfx.S(28);
                        g.DrawImage(it.FileIcon, new Rectangle(tbox.X + (bw - fsz) / 2, tbox.Y + (bh - fsz) / 2, fsz, fsz));
                    }
                }
                // 选中时缩略图描一圈主题色,平时是极淡的描边
                if (sel) Gfx.DrawRound(g, t.Accent, tbox, 9, Gfx.F(2f));
                else Gfx.DrawRound(g, Color.FromArgb(32, t.Text), tbox, 9, 1f);

                int ix = tbox.Right + Gfx.S(14);
                int cy = card.Y + card.Height / 2;
                string name = !string.IsNullOrEmpty(it.CustomTitle) ? it.CustomTitle : (it.IsImage ? "图片" : it.FileTitle());
                Gfx.Text(g, name, Gfx.UI, new Rectangle(ix, cy - Gfx.S(21), Math.Max(10, right - ix), Gfx.S(22)), t.Text,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                Gfx.Text(g, it.ImageMeta(), Gfx.Small, new Rectangle(ix, cy + Gfx.S(1), Math.Max(10, right - ix), Gfx.S(18)), t.Sub,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                return;
            }

            // ---------- 文字 / 链接 / 文件等:左侧图标,右侧两行
            Rectangle gl = new Rectangle(card.X + Gfx.S(16), card.Y, Gfx.S(28), card.Height);
            int kind = it.Kind;
            if (kind == Item.KColor)
            {
                int d = Gfx.S(18);
                Rectangle sw = new Rectangle(gl.X + (gl.Width - d) / 2, gl.Y + (gl.Height - d) / 2, d, d);
                using (SolidBrush b = new SolidBrush(it.SwatchColor())) g.FillEllipse(b, sw);
                using (Pen p = new Pen(Color.FromArgb(90, 128, 128, 128))) g.DrawEllipse(p, sw);
            }
            else if (kind == Item.KFiles)
            {
                it.EnsureFileIcon();
                if (it.FileIcon != null)
                {
                    int sz = Gfx.S(24);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(it.FileIcon, new Rectangle(gl.X + (gl.Width - sz) / 2, gl.Y + (gl.Height - sz) / 2, sz, sz));
                }
                else Gfx.Text(g, "≡", Gfx.Bold, gl, hc, TextFormatFlags.HorizontalCenter);
            }
            else
            {
                string gs = kind == Item.KUrl ? "↗" : kind == Item.KEmail ? "@" :
                            kind == Item.KPath ? "/" : kind == Item.KNumber ? "#" : kind == Item.KTrack ? "单" : "Aa";
                Courier.Company lc = Courier.For(it);
                if (lc != null)
                {
                    int bsz = Gfx.S(26);
                    Courier.DrawBadge(g, lc, new Rectangle(gl.X + (gl.Width - bsz) / 2, gl.Y + (gl.Height - bsz) / 2, bsz, bsz));
                }
                else Gfx.Text(g, gs, Gfx.Bold, gl, hc, TextFormatFlags.HorizontalCenter);
            }

            int tx = gl.Right + Gfx.S(8);
            string line1 = !string.IsNullOrEmpty(it.CustomTitle) ? it.CustomTitle : (it.IsFiles ? it.FileTitle() : it.Title);
            Gfx.Text(g, line1, Gfx.UI, new Rectangle(tx, card.Y + Gfx.S(8), right - tx, Gfx.S(22)), t.Text,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string sizeTxt = it.SizeText();
            Gfx.Text(g, it.TimeAgo() + "  ·  " + it.Info + (sizeTxt != null && !it.Info.Contains(sizeTxt) ? "  ·  " + sizeTxt : ""), Gfx.Small,
                new Rectangle(tx, card.Y + Gfx.S(29), right - tx, Gfx.S(18)), t.Sub,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }

    // ================================================================ 毛玻璃
    // 两步:先把屏幕上那一块截下来做大半径模糊(慢,只在弹出时做一次),
    // 再按透明度盖一层主题色、加顶部高光和左上角暖光(快,透明度或缩放变了随时重做)
    static class Frost
    {
        public static Bitmap CaptureBlur(Rectangle area)
        {
            if (area.Width <= 0 || area.Height <= 0) return null;
            try
            {
                using (Bitmap shot = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(shot))
                        g.CopyFromScreen(area.Location, Point.Empty, area.Size);
                    return Blur(shot);
                }
            }
            catch { return null; }
        }

        // 设置里的预览用这张内置示例图(软件自己画的彩色背景),不读取屏幕上的任何内容
        public static Bitmap SampleBlur(int w, int h)
        {
            w = Math.Max(16, w);
            h = Math.Max(16, h);
            try
            {
                using (Bitmap pic = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(pic))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                            Color.FromArgb(52, 72, 140), Color.FromArgb(236, 132, 92), LinearGradientMode.ForwardDiagonal))
                            g.FillRectangle(bg, 0, 0, w, h);
                        Color[] blobs = new Color[]
                        {
                            Color.FromArgb(255, 204, 92), Color.FromArgb(120, 196, 180), Color.FromArgb(232, 96, 128),
                            Color.FromArgb(250, 244, 230), Color.FromArgb(90, 120, 220), Color.FromArgb(255, 160, 60)
                        };
                        for (int i = 0; i < blobs.Length; i++)
                        {
                            int d = h * (60 + (i * 37) % 50) / 100;
                            int x = (int)(w * (0.04 + 0.16 * i)), y = (int)(h * (((i * 53) % 70) / 100.0)) - d / 4;
                            using (SolidBrush b = new SolidBrush(blobs[i])) g.FillEllipse(b, x, y, d, d);
                        }
                        // 几块"窗口"一样的浅色方块,用来看出透明度的差别
                        using (SolidBrush wb = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                        {
                            g.FillRectangle(wb, w * 0.30f, h * 0.15f, w * 0.12f, h * 0.55f);
                            g.FillRectangle(wb, w * 0.62f, h * 0.35f, w * 0.18f, h * 0.40f);
                        }
                    }
                    return Blur(pic);
                }
            }
            catch { return null; }
        }

        static Bitmap Blur(Bitmap shot)
        {
            Rectangle area = new Rectangle(0, 0, shot.Width, shot.Height);
            try
            {
                {
                    int f = 18;
                    int sw = Math.Max(1, area.Width / f), sh = Math.Max(1, area.Height / f);
                    Bitmap blurred = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppPArgb);
                    using (Bitmap small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb))
                    {
                        // 先用快速算法缩到 1/4,再用高质量算法缩到最终大小
                        int qw = Math.Max(sw, area.Width / 4), qh = Math.Max(sh, area.Height / 4);
                        using (Bitmap quarter = new Bitmap(qw, qh, PixelFormat.Format32bppPArgb))
                        {
                            using (Graphics g = Graphics.FromImage(quarter))
                            {
                                g.InterpolationMode = InterpolationMode.Bilinear;
                                g.PixelOffsetMode = PixelOffsetMode.Half;
                                g.DrawImage(shot, 0, 0, qw, qh);
                            }
                            using (Graphics g = Graphics.FromImage(small))
                            {
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                g.DrawImage(quarter, 0, 0, sw, sh);
                            }
                        }
                        using (Graphics g = Graphics.FromImage(blurred))
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetWrapMode(WrapMode.TileFlipXY);   // 边缘不发黑
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(small, new Rectangle(0, 0, area.Width, area.Height), 0, 0, sw, sh, GraphicsUnit.Pixel, ia);
                        }
                    }
                    return blurred;
                }
            }
            catch { return null; }
        }

        // 保证文字看得清:量一下背后画面的亮度(取偏亮的那部分),
        // 深色主题下背景太亮、浅色主题下背景太暗时,自动把着色加深一点;背景本来就合适时完全按用户设的透明度
        public static int Legible(Bitmap blur, int transparency)
        {
            if (blur == null || transparency <= 0) return transparency;
            Theme t = Theme.Cur;
            try
            {
                List<int> lum = new List<int>();
                int sx = Math.Max(1, blur.Width / 48), sy = Math.Max(1, blur.Height / 16);
                for (int y = sy / 2; y < blur.Height; y += sy)
                    for (int x = sx / 2; x < blur.Width; x += sx)
                    {
                        Color c = blur.GetPixel(x, y);
                        lum.Add((c.R * 299 + c.G * 587 + c.B * 114) / 1000);
                    }
                if (lum.Count == 0) return transparency;
                lum.Sort();
                double shelf = (t.Shelf.R * 299 + t.Shelf.G * 587 + t.Shelf.B * 114) / 1000.0;
                double need;   // 至少要盖多少主题色(0~1)
                if (t.Dark)
                {
                    double L = lum[(int)(lum.Count * 0.80)];   // 偏亮的那部分
                    const double target = 62;
                    need = L <= target ? 0 : (L - target) / Math.Max(1, L - shelf);
                }
                else
                {
                    double L = lum[(int)(lum.Count * 0.20)];   // 偏暗的那部分
                    const double target = 196;
                    need = L >= target ? 0 : (target - L) / Math.Max(1, shelf - L);
                }
                need = Math.Max(0, Math.Min(0.92, need));
                int maxTrans = (int)Math.Floor((1 - need) * 100);
                return Math.Min(transparency, maxTrans);
            }
            catch { return transparency; }
        }

        // 在 r 上盖主题色 + 高光 + 暖光。panelTop:面板顶边在 r 里的位置;k:缩放(设置里的小预览用)
        public static void Tint(Graphics g, Rectangle r, int transparency, int panelTop, float k)
        {
            Theme t = Theme.Cur;
            int alpha = (int)Math.Round(255 * (1 - Math.Max(0, Math.Min(90, transparency)) / 100.0));
            using (SolidBrush tint = new SolidBrush(Color.FromArgb(alpha, t.Shelf)))
                g.FillRectangle(tint, r);

            // 顶部一道很淡的高光:渐变从面板顶边开始(之前从图片顶端开始,画到面板上会错位,出现一道亮线)
            int hh = Math.Max(2, (int)(Gfx.S(120) * k));
            Rectangle hr = new Rectangle(r.X, r.Y + panelTop, r.Width, hh);
            using (LinearGradientBrush hl = new LinearGradientBrush(new Rectangle(hr.X, hr.Y - 1, hr.Width, hr.Height + 2),
                Color.FromArgb(t.Dark ? 22 : 44, Theme.Blend(t.Accent, Color.White, 0.55)), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
                g.FillRectangle(hl, hr);

            // 左上角一束斜射进来的暖光(拾光)
            Rectangle beam = new Rectangle(r.X - r.Width / 6, r.Y + panelTop - (int)(Gfx.S(220) * k), r.Width / 2, Math.Max(4, (int)(Gfx.S(420) * k)));
            using (GraphicsPath bp = new GraphicsPath())
            {
                bp.AddEllipse(beam);
                using (PathGradientBrush pb = new PathGradientBrush(bp))
                {
                    pb.CenterColor = Color.FromArgb(t.Dark ? 34 : 50, Theme.Blend(t.Accent, Color.White, 0.35));
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, t.Accent) };
                    g.FillPath(pb, bp);
                }
            }
        }

    }

    // 设置里的毛玻璃小预览:拖动透明度滑块时实时变化(背景是软件内置的示例图)
    class GlassPreview : Control
    {
        readonly Bitmap blur;   // 内置示例图的模糊版
        public int Transparency;

        public GlassPreview(Bitmap blurBase)
        {
            blur = blurBase;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(Parent != null ? Parent.BackColor : SetPal.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 4 || Height < 4) return;
            using (Bitmap off = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb))
            {
                float k = 0.4f;
                using (Graphics og = Graphics.FromImage(off))
                {
                    og.Clear(t.Shelf);
                    if (blur != null)
                    {
                        // 等比缩到预览的高度,取左边一段(面板很宽,预览只看开头)
                        k = Height / (float)blur.Height;
                        int dw = Math.Max(Width, (int)(blur.Width * k));
                        og.InterpolationMode = InterpolationMode.Bilinear;
                        og.DrawImage(blur, new Rectangle(0, 0, dw, Height));
                    }
                    Frost.Tint(og, new Rectangle(0, 0, Width, Height), Frost.Legible(blur, Transparency), 0, k);

                    // 迷你面板内容:搜索框、分类圆点、几张卡片
                    og.SmoothingMode = SmoothingMode.AntiAlias;
                    int pad = Gfx.S(10);
                    Gfx.FillRound(og, Color.FromArgb(t.Dark ? 60 : 200, t.Input), new Rectangle(pad, pad, Gfx.S(70), Gfx.S(12)), 6);
                    int[] dots = Config.TabDotColors;
                    for (int i = 0; i < dots.Length; i++)
                        using (SolidBrush db = new SolidBrush(Color.FromArgb(dots[i] == 0 ? t.Sub.ToArgb() : dots[i])))
                            og.FillEllipse(db, pad + Gfx.S(86) + i * Gfx.S(14), pad + Gfx.S(3), Gfx.S(6), Gfx.S(6));
                    int cw = Gfx.S(56), cy = pad + Gfx.S(22), ch = Height - cy - pad;
                    Color[] heads = new Color[]
                    {
                        Color.FromArgb(dots[2]), Color.FromArgb(dots[3]), Color.FromArgb(dots[4]), Color.FromArgb(dots[5]), Color.FromArgb(dots[1])
                    };
                    for (int i = 0, x = pad; x + cw <= Width - pad; i++, x += cw + Gfx.S(8))
                    {
                        Rectangle c = new Rectangle(x, cy, cw, ch);
                        Rectangle sh = c;
                        sh.Offset(0, Gfx.S(2));
                        Gfx.FillRound(og, Color.FromArgb(t.Dark ? 60 : 24, 0, 0, 0), sh, 6);
                        Gfx.FillRound(og, t.Card, c, 6);
                        using (GraphicsPath hp = Gfx.RoundTop(new Rectangle(c.X, c.Y, c.Width, Gfx.S(14)), 6))
                        using (SolidBrush hb = new SolidBrush(heads[i % heads.Length]))
                            og.FillPath(hb, hp);
                        using (SolidBrush lb = new SolidBrush(Color.FromArgb(t.Dark ? 70 : 40, t.Text)))
                        {
                            og.FillRectangle(lb, c.X + Gfx.S(6), c.Y + Gfx.S(22), c.Width - Gfx.S(12), Gfx.S(3));
                            og.FillRectangle(lb, c.X + Gfx.S(6), c.Y + Gfx.S(29), c.Width - Gfx.S(20), Gfx.S(3));
                        }
                    }
                }
                using (TextureBrush tb = new TextureBrush(off))
                using (GraphicsPath p = Gfx.Round(new Rectangle(0, 0, Width - 1, Height - 1), 10))
                    g.FillPath(tb, p);
                Gfx.DrawRound(g, Color.FromArgb(t.Dark ? 50 : 30, t.Text), new Rectangle(0, 0, Width - 1, Height - 1), 10, 1f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && blur != null) blur.Dispose();
            base.Dispose(disposing);
        }
    }

    // ================================================================ 设置窗口(仿 macOS 系统设置:左侧边栏 + 右侧分组卡片)
    static class SetPal
    {
        public static Color Back { get { return Theme.Cur.Dark ? Color.FromArgb(30, 28, 26) : Color.FromArgb(245, 242, 237); } }
        public static Color Side { get { return Theme.Cur.Dark ? Color.FromArgb(38, 35, 33) : Color.FromArgb(237, 232, 225); } }
        public static Color Card { get { return Theme.Cur.Dark ? Color.FromArgb(46, 43, 40) : Color.White; } }
        public static Color Sep { get { return Theme.Cur.Dark ? Color.FromArgb(63, 58, 54) : Color.FromArgb(230, 224, 216); } }
        public static readonly Font Seg = new Font("Microsoft YaHei UI", 9f);
        public static readonly Font PageTitle = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold);
        public static readonly Font Hero = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold);
        public static readonly Font Header = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
    }

    // 苹果风格的分段控件:灰色底槽 + 白色(深色模式下浅灰)选中块
    class Segmented : Control
    {
        public string[] Labels = new string[0];
        public int Selected;
        public event EventHandler Changed;
        int hover = -1;

        public Segmented()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Font = SetPal.Seg;
            Height = Gfx.S(28);
        }

        Rectangle SegRect(int i)
        {
            int n = Math.Max(1, Labels.Length);
            int inner = Width - Gfx.S(4);
            int w = inner / n;
            int x = Gfx.S(2) + i * w;
            int ww = i == n - 1 ? inner - i * w : w;
            return new Rectangle(x, Gfx.S(2), ww, Height - Gfx.S(4));
        }

        int Hit(Point p)
        {
            for (int i = 0; i < Labels.Length; i++) if (SegRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Color bg = Parent != null ? Parent.BackColor : SetPal.Card;
            g.Clear(bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color track = t.Dark ? Theme.Blend(bg, Color.White, 0.09) : Theme.Blend(bg, Color.Black, 0.065);
            Color pill = t.Dark ? Theme.Blend(bg, Color.White, 0.24) : Color.White;
            Gfx.FillRound(g, track, new Rectangle(0, 0, Width - 1, Height - 1), 8);
            for (int i = 0; i < Labels.Length; i++)
            {
                Rectangle r = SegRect(i);
                if (i == Selected)
                {
                    Rectangle sh = r;
                    sh.Offset(0, 1);
                    Gfx.FillRound(g, Color.FromArgb(t.Dark ? 60 : 30, 0, 0, 0), sh, 6);
                    Gfx.FillRound(g, pill, r, 6);
                }
                else if (i == hover)
                {
                    Gfx.FillRound(g, Color.FromArgb(t.Dark ? 18 : 14, t.Text), r, 6);
                }
                // 两个未选中块之间的细竖线
                if (i > 0 && i != Selected && i - 1 != Selected)
                    using (Pen p = new Pen(Color.FromArgb(t.Dark ? 50 : 40, t.Text)))
                        g.DrawLine(p, r.X, r.Y + Gfx.S(6), r.X, r.Bottom - Gfx.S(6));
                Gfx.Text(g, Labels[i], Font, r, t.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Hit(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = Hit(e.Location);
            if (i < 0 || i == Selected) return;
            Selected = i;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    // 显示一组按键的小键帽,例如 Ctrl + Tab
    class KeyCaps : Control
    {
        readonly string[] keys;

        public KeyCaps(params string[] k)
        {
            keys = k;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Height = Gfx.S(26);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Color bg = Parent != null ? Parent.BackColor : SetPal.Card;
            g.Clear(bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextFormatFlags nf = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            int plusW = Gfx.S(18), kh = Gfx.S(22);
            int total = 0;
            int[] ws = new int[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ws[i] = Math.Max(kh, TextRenderer.MeasureText(keys[i], Gfx.Small, Size.Empty, nf).Width + Gfx.S(14));
                total += ws[i] + (i > 0 ? plusW : 0);
            }
            int x = Width - total;
            int y = (Height - kh) / 2;
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0)
                {
                    Gfx.Text(g, "+", Gfx.Small, new Rectangle(x, 0, plusW, Height), t.Sub, TextFormatFlags.HorizontalCenter);
                    x += plusW;
                }
                Rectangle kr = new Rectangle(x, y, ws[i], kh);
                Rectangle depth = kr;
                depth.Offset(0, Gfx.S(1));
                Gfx.FillRound(g, Theme.Blend(bg, Color.Black, t.Dark ? 0.35 : 0.12), depth, 5);
                Gfx.FillRound(g, t.Dark ? Theme.Blend(bg, Color.White, 0.12) : Theme.Blend(bg, Color.Black, 0.03), kr, 5);
                TextRenderer.DrawText(g, keys[i], Gfx.Small, kr, t.Text, nf | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += ws[i];
            }
        }
    }

    // 一组设置项:圆角白卡片,每行左边标题(可带一行灰色说明),右边是控件,行之间有细分隔线
    class SettingsGroup : Control
    {
        class Row
        {
            public string Title, Sub, Value;
            public Control Ctl;
            public int CtlW, H;
            public bool Wide;
        }

        readonly List<Row> rows = new List<Row>();

        public SettingsGroup()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = SetPal.Card;   // 子控件用它当底色
        }

        public void AddRow(string title, string sub, Control ctl, int ctlW)
        {
            AddRow(title, sub, ctl, ctlW, sub != null ? Gfx.S(58) : Gfx.S(46));
        }

        public void AddRow(string title, string sub, Control ctl, int ctlW, int h)
        {
            Row r = new Row();
            r.Title = title; r.Sub = sub; r.Ctl = ctl; r.CtlW = ctlW; r.H = h;
            rows.Add(r);
            if (ctl != null) Controls.Add(ctl);
        }

        public void AddValue(string title, string value)
        {
            Row r = new Row();
            r.Title = title; r.Value = value; r.H = Gfx.S(44);
            rows.Add(r);
        }

        public void AddWide(Control ctl, int h)
        {
            Row r = new Row();
            r.Ctl = ctl; r.Wide = true; r.H = h;
            rows.Add(r);
            Controls.Add(ctl);
        }

        public void SetSub(Control ctl, string sub)
        {
            foreach (Row r in rows) if (r.Ctl == ctl) r.Sub = sub;
            Invalidate();
        }

        public int TotalHeight
        {
            get
            {
                int h = 0;
                foreach (Row r in rows) h += r.H;
                return h;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int y = 0;
            foreach (Row r in rows)
            {
                if (r.Ctl != null)
                {
                    if (r.Wide) r.Ctl.SetBounds(Gfx.S(14), y + Gfx.S(8), Width - Gfx.S(28), r.H - Gfx.S(16));
                    else
                    {
                        int ch = Math.Min(r.Ctl.Height, r.H - Gfx.S(8));
                        r.Ctl.SetBounds(Width - Gfx.S(14) - r.CtlW, y + (r.H - ch) / 2, r.CtlW, ch);
                    }
                }
                y += r.H;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(Parent != null ? Parent.BackColor : SetPal.Back);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle card = new Rectangle(0, 0, Width - 1, Height - 1);
            Gfx.FillRound(g, SetPal.Card, card, 10);
            if (!t.Dark) Gfx.DrawRound(g, Color.FromArgb(14, 0, 0, 0), card, 10, 1f);

            int y = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                int textRight = r.Ctl != null && !r.Wide ? Width - Gfx.S(28) - r.CtlW : Width - Gfx.S(16);
                if (r.Value != null)
                {
                    Gfx.Text(g, r.Value, Gfx.UI, new Rectangle(Width / 3, y, Width - Width / 3 - Gfx.S(16), r.H), t.Sub,
                        TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    textRight = Width / 3;
                }
                if (r.Title != null)
                {
                    if (r.Sub != null)
                    {
                        Gfx.Text(g, r.Title, Gfx.UI, new Rectangle(Gfx.S(16), y + Gfx.S(9), textRight - Gfx.S(16), Gfx.S(22)), t.Text,
                            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                        Gfx.Text(g, r.Sub, Gfx.Small, new Rectangle(Gfx.S(16), y + Gfx.S(31), textRight - Gfx.S(16), Gfx.S(18)), t.Sub,
                            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                    }
                    else
                        Gfx.Text(g, r.Title, Gfx.UI, new Rectangle(Gfx.S(16), y, textRight - Gfx.S(16), r.H), t.Text,
                            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
                y += r.H;
                if (i < rows.Count - 1)
                    using (Pen p = new Pen(SetPal.Sep)) g.DrawLine(p, Gfx.S(16), y, Width - Gfx.S(16), y);
            }
        }
    }

    // 左侧边栏:应用名 + 带彩色小图标的导航项
    class SettingsSidebar : Control
    {
        public readonly List<string> Titles = new List<string>();
        public readonly List<string> Glyphs = new List<string>();
        public readonly List<Color> Colors = new List<Color>();
        public int Selected;
        public event EventHandler Changed;
        int hover = -1;
        Bitmap logo;
        static Font glyphFont;

        public SettingsSidebar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            if (glyphFont == null)
            {
                Font f = new Font("Segoe MDL2 Assets", 9.5f);
                glyphFont = f.Name == "Segoe MDL2 Assets" ? f : null;   // 老系统没有这个图标字体,就用文字首字代替
            }
        }

        public void Add(string title, string glyph, Color c)
        {
            Titles.Add(title);
            Glyphs.Add(glyph);
            Colors.Add(c);
        }

        Rectangle ItemRect(int i)
        {
            return new Rectangle(Gfx.S(10), Gfx.S(86) + i * Gfx.S(36), Width - Gfx.S(20), Gfx.S(32));
        }

        int Hit(Point p)
        {
            for (int i = 0; i < Titles.Count; i++) if (ItemRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(SetPal.Side);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Gfx.DrawGlow(g, new Rectangle(Gfx.S(10), Gfx.S(18), Gfx.S(50), Gfx.S(50)), t.Accent, Gfx.S(60));
            using (Pen p = new Pen(SetPal.Sep)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);

            if (logo == null) logo = Brand.Logo(Gfx.S(34));
            if (logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, new Rectangle(Gfx.S(18), Gfx.S(26), Gfx.S(34), Gfx.S(34)));
            }
            Gfx.Text(g, Brand.Name, Gfx.Bold, new Rectangle(Gfx.S(62), Gfx.S(24), Width - Gfx.S(70), Gfx.S(20)), t.Text, TextFormatFlags.Left);
            Gfx.Text(g, Brand.NameCn + "  ·  设置", Gfx.Small, new Rectangle(Gfx.S(62), Gfx.S(44), Width - Gfx.S(70), Gfx.S(18)), t.Sub, TextFormatFlags.Left);

            for (int i = 0; i < Titles.Count; i++)
            {
                Rectangle r = ItemRect(i);
                bool sel = i == Selected;
                if (sel) Gfx.FillRound(g, t.Accent, r, 7);
                else if (i == hover) Gfx.FillRound(g, Color.FromArgb(t.Dark ? 22 : 16, t.Text), r, 7);
                int isz = Gfx.S(22);
                Rectangle ic = new Rectangle(r.X + Gfx.S(8), r.Y + (r.Height - isz) / 2, isz, isz);
                Gfx.FillRound(g, Colors[i], ic, 6);
                if (sel) Gfx.DrawRound(g, Color.FromArgb(90, 255, 255, 255), ic, 6, 1f);
                if (glyphFont != null)
                    TextRenderer.DrawText(g, Glyphs[i], glyphFont, ic, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                else
                    Gfx.Text(g, Titles[i].Substring(0, 1), Gfx.Small, ic, Color.White, TextFormatFlags.HorizontalCenter);
                Gfx.Text(g, Titles[i], Gfx.UI, new Rectangle(ic.Right + Gfx.S(10), r.Y, r.Right - ic.Right - Gfx.S(14), r.Height),
                    sel ? Color.White : t.Text, TextFormatFlags.Left | TextFormatFlags.SingleLine);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = Hit(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = Hit(e.Location);
            if (i < 0 || i == Selected) return;
            Selected = i;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
    }

    class SettingsForm : Form
    {
        static readonly int[] MaxOptions = new int[] { 100, 200, 500, 1000 };
        static readonly int[] DayOptions = new int[] { 0, 7, 30, 90 };
        const int SideW = 210;

        readonly SettingsSidebar side = new SettingsSidebar();
        readonly List<Panel> pages = new List<Panel>();

        readonly Segmented themeSeg = new Segmented();
        readonly Swatches accent = new Swatches();
        readonly Segmented posSeg = new Segmented();
        readonly Segmented layoutSeg = new Segmented();
        readonly Segmented headerSeg = new Segmented();
        readonly ColorChip headerColor = new ColorChip();
        readonly Slider transSlider = new Slider();
        readonly HotkeyBox hotkey = new HotkeyBox();
        readonly Toggle tPaste = new Toggle();
        readonly Toggle tImages = new Toggle();
        readonly Toggle tAuto = new Toggle();
        readonly Toggle tUpdate = new Toggle();
        readonly Toggle tWinV = new Toggle();
        readonly SearchBox pathBox = new SearchBox();
        readonly Toggle tMove = new Toggle();
        readonly Segmented maxSeg = new Segmented();
        readonly Segmented daysSeg = new Segmented();
        readonly SearchBox ignoreBox = new SearchBox();

        public bool MoveExisting;

        public SettingsForm()
        {
            Theme t = Theme.Cur;
            Text = Brand.Name + " 设置";
            try { if (Brand.LargeIcon != null) Icon = Brand.LargeIcon; } catch { }
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ClientSize = new Size(Gfx.S(780), Gfx.S(628));
            BackColor = SetPal.Back;
            Font = Gfx.UI;

            side.Add("外观", "", Color.FromArgb(0, 122, 255));
            side.Add("面板", "", Color.FromArgb(48, 176, 199));
            side.Add("常规", "", Color.FromArgb(142, 142, 147));
            side.Add("快捷键", "", Color.FromArgb(255, 149, 0));
            side.Add("存储与隐私", "", Color.FromArgb(52, 199, 89));
            side.Add("关于", "", Color.FromArgb(88, 86, 214));
            side.SetBounds(0, 0, Gfx.S(SideW), ClientSize.Height);
            side.Changed += delegate { ShowPage(); };
            Controls.Add(side);

            for (int i = 0; i < 6; i++)
            {
                Panel p = new Panel();
                p.BackColor = SetPal.Back;
                p.SetBounds(Gfx.S(SideW), 0, ClientSize.Width - Gfx.S(SideW), ClientSize.Height - Gfx.S(64));
                pages.Add(p);
                Controls.Add(p);
            }

            BuildLook(pages[0]);
            BuildPanel(pages[1]);
            BuildGeneral(pages[2]);
            BuildKeys(pages[3]);
            BuildStorage(pages[4]);
            BuildAbout(pages[5]);

            // 切换浅色 / 深色 / 主题色时,设置窗口立刻换成对应的样子
            themeSeg.Changed += delegate { PreviewTheme(); };
            accent.Changed += delegate { PreviewTheme(); };

            // ---- 底部按钮(放在右侧内容区的右下角)
            Btn ok = new Btn();
            ok.Text = "完成";
            ok.Primary = true;
            ok.SetBounds(ClientSize.Width - Gfx.S(28 + 96), ClientSize.Height - Gfx.S(52), Gfx.S(96), Gfx.S(34));
            ok.Click += delegate { OnOk(); };
            Controls.Add(ok);

            Btn cancel = new Btn();
            cancel.Text = "取消";
            cancel.SetBounds(ClientSize.Width - Gfx.S(28 + 96 + 10 + 86), ClientSize.Height - Gfx.S(52), Gfx.S(86), Gfx.S(34));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            ShowPage();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(Handle, Theme.Cur.Dark);   // 深色模式下标题栏也是深色
        }

        // ---------------- 主题实时预览
        static Color[] PaletteNow()
        {
            Theme t = Theme.Cur;
            return new Color[] { SetPal.Back, SetPal.Side, SetPal.Card, SetPal.Sep,
                t.Text, t.Sub, t.Bg, t.Hover, t.Input, t.Border, t.Card, t.Shelf, t.Accent };
        }

        void PreviewTheme()
        {
            Color[] before = PaletteNow();
            int m = themeSeg.Selected;
            bool dark = m == 2 || (m == 0 && Theme.SystemDark());
            Theme.Cur = Theme.Make(dark, accent.Selected);
            Color[] after = PaletteNow();
            Dictionary<int, Color> map = new Dictionary<int, Color>();
            for (int i = 0; i < before.Length; i++)
            {
                int k = before[i].ToArgb();
                if (!map.ContainsKey(k)) map[k] = after[i];
            }
            SuspendLayout();
            Remap(this, map);
            ResumeLayout();
            Native.DarkTitle(Handle, dark);
            // 让标题栏立刻重画成新颜色
            Width += 1; Width -= 1;
            Invalidate(true);
            Update();
        }

        static void Remap(Control c, Dictionary<int, Color> map)
        {
            Color v;
            if (map.TryGetValue(c.BackColor.ToArgb(), out v)) c.BackColor = v;
            if (map.TryGetValue(c.ForeColor.ToArgb(), out v)) c.ForeColor = v;
            SearchBox sb = c as SearchBox;
            if (sb != null) sb.ApplyTheme();
            foreach (Control ch in c.Controls) Remap(ch, map);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 点了取消:预览过的主题不算数,恢复原来的
            if (DialogResult != DialogResult.OK) Theme.Refresh();
            base.OnFormClosed(e);
        }

        // ---------------- 页面
        static Label AddLabel(Control parent, string text, int x, int y, Font f, Color c, int maxW)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = f;
            l.ForeColor = c;
            l.BackColor = SetPal.Back;
            l.AutoSize = true;
            if (maxW > 0) l.MaximumSize = new Size(maxW, 0);
            l.Location = new Point(x, y);
            parent.Controls.Add(l);
            return l;
        }

        static int PageTop(Panel p, string title)
        {
            AddLabel(p, title, Gfx.S(28), Gfx.S(22), SetPal.PageTitle, Theme.Cur.Text, 0);
            return Gfx.S(72);
        }

        // 放一个分组:小标题 + 卡片 + 灰色脚注;返回下一个分组的起始位置
        static int AddGroup(Panel p, int y, string header, SettingsGroup grp, string footer)
        {
            int x = Gfx.S(28), w = p.Width - Gfx.S(56);
            if (header != null)
            {
                AddLabel(p, header, x + Gfx.S(6), y, SetPal.Header, Theme.Cur.Sub, 0);
                y += Gfx.S(22);
            }
            grp.SetBounds(x, y, w, grp.TotalHeight);
            p.Controls.Add(grp);
            y += grp.TotalHeight;
            if (footer != null)
            {
                Label f = AddLabel(p, footer, x + Gfx.S(6), y + Gfx.S(7), Gfx.Small, Theme.Cur.Sub, w - Gfx.S(12));
                y += Gfx.S(7) + f.PreferredHeight;
            }
            return y + Gfx.S(20);
        }

        static Toggle Switch(Toggle tg, bool value)
        {
            tg.Label = "";
            tg.Checked = value;
            tg.Height = Gfx.S(30);
            return tg;
        }

        static Segmented Seg(Segmented s, string[] labels, int selected)
        {
            s.Labels = labels;
            s.Selected = Math.Max(0, Math.Min(labels.Length - 1, selected));
            return s;
        }

        void BuildLook(Panel p)
        {
            int y = PageTop(p, "外观");

            SettingsGroup g1 = new SettingsGroup();
            g1.AddRow("外观", null, Seg(themeSeg, new string[] { "跟随系统", "浅色", "深色" }, Config.ThemeMode), Gfx.S(240));
            accent.Selected = Math.Abs(Config.AccentIndex) % Theme.Accents.Length;
            accent.Height = Gfx.S(36);
            g1.AddRow("主题色", null, accent, accent.PreferredWidthFor);
            y = AddGroup(p, y, "主题", g1, "选择“跟随系统”时,会随 Windows 的深色 / 浅色模式自动切换。");

            // 毛玻璃:拖动滑块,下面的预览立刻变化(背景是内置示例图,不读取屏幕)
            SettingsGroup g2 = new SettingsGroup();
            transSlider.Minimum = 0;
            transSlider.Maximum = 90;
            transSlider.Value = Math.Max(0, Math.Min(90, Config.ShelfTransparency));
            transSlider.Suffix = "%";
            g2.AddRow("透明度", null, transSlider, Gfx.S(260));
            GlassPreview preview = new GlassPreview(Frost.SampleBlur(Gfx.S(900), Gfx.S(128)));
            preview.Transparency = transSlider.Value;
            g2.AddWide(preview, Gfx.S(128));
            transSlider.Changed += delegate
            {
                preview.Transparency = transSlider.Value;
                preview.Invalidate();
            };
            AddGroup(p, y, "毛玻璃", g2, "0% 是不透明的实色背景,数值越大,越能透出面板后面的内容。");
        }

        void BuildPanel(Panel p)
        {
            int y = PageTop(p, "面板");

            SettingsGroup g1 = new SettingsGroup();
            g1.AddRow("界面风格", null, Seg(layoutSeg, new string[] { "卡片", "列表" }, Config.Layout == 1 ? 0 : 1), Gfx.S(240));
            g1.AddRow("弹出位置", null, Seg(posSeg, new string[] { "鼠标旁边", "屏幕中央", "屏幕底部" },
                Config.Layout == 1 ? 2 : (Config.PopupAtCursor ? 0 : 1)), Gfx.S(240));
            // 卡片风格固定从屏幕底部弹出;两个选项联动
            layoutSeg.Changed += delegate
            {
                if (layoutSeg.Selected == 0) posSeg.Selected = 2;
                else if (posSeg.Selected == 2) posSeg.Selected = Config.PopupAtCursor ? 0 : 1;
                posSeg.Invalidate();
            };
            posSeg.Changed += delegate
            {
                layoutSeg.Selected = posSeg.Selected == 2 ? 0 : 1;
                layoutSeg.Invalidate();
            };
            y = AddGroup(p, y, "布局", g1, null);

            SettingsGroup g2 = new SettingsGroup();
            g2.AddRow("标题颜色", null, Seg(headerSeg, new string[] { "跟随分类", "来源程序", "统一颜色" }, Config.HeaderMode), Gfx.S(240));
            headerColor.Value = Color.FromArgb(Config.HeaderColor);
            headerColor.Height = Gfx.S(26);
            g2.AddRow("统一颜色", "标题颜色选“统一颜色”时使用", headerColor, Gfx.S(48));
            AddGroup(p, y, "卡片", g2, null);
        }

        void BuildGeneral(Panel p)
        {
            int y = PageTop(p, "常规");
            SettingsGroup g = new SettingsGroup();
            g.AddRow("选中后自动粘贴", "直接粘贴到呼出面板之前所在的窗口", Switch(tPaste, Config.AutoPaste), Gfx.S(52));
            g.AddRow("记录图片", "截图等图片内容也保存到历史里", Switch(tImages, Config.RecordImages), Gfx.S(52));
            g.AddRow("开机自动启动", null, Switch(tAuto, Startup.Get()), Gfx.S(52));
            y = AddGroup(p, y, null, g, null);

            // ---- 软件更新
            SettingsGroup gu = new SettingsGroup();
            gu.AddRow("自动检查更新", "每天在后台检查一次 GitHub 上的新版本", Switch(tUpdate, Config.AutoUpdate), Gfx.S(52));
            Btn check = new Btn();
            check.Text = "检查更新";
            check.Height = Gfx.S(30);
            string last = Config.LastUpdateCheck > 0
                ? "上次检查:" + new DateTime(Config.LastUpdateCheck).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : "还没有检查过";
            gu.AddRow("当前版本 " + Config.Version, last, check, Gfx.S(96));
            UpdateInfo found = null;
            check.Click += delegate
            {
                if (found != null)
                {
                    using (UpdateForm uf = new UpdateForm(found)) uf.ShowDialog(this);
                    return;
                }
                check.Text = "检查中…";
                check.Invalidate();
                gu.SetSub(check, "正在连接 GitHub…");
                Updater.CheckAsync(delegate(UpdateInfo u, string err)
                {
                    try
                    {
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            if (err != null) { check.Text = "重试"; gu.SetSub(check, err); }
                            else if (u == null) { check.Text = "检查更新"; gu.SetSub(check, "已经是最新版本"); }
                            else
                            {
                                found = u;
                                check.Text = "更新…";
                                gu.SetSub(check, "发现新版本 " + u.Tag.TrimStart('v', 'V'));
                            }
                            check.Invalidate();
                        }));
                    }
                    catch { }
                });
            };
            AddGroup(p, y, "软件更新", gu, "更新时只会访问 GitHub 下载安装包,不会上传任何内容。");
        }

        void BuildKeys(Panel p)
        {
            int y = PageTop(p, "快捷键");
            SettingsGroup g1 = new SettingsGroup();
            hotkey.Ctrl = Config.Ctrl; hotkey.Alt = Config.Alt; hotkey.Shift = Config.Shift;
            hotkey.Win = Config.Win; hotkey.Key = Config.Key;
            hotkey.Height = Gfx.S(32);
            g1.AddRow("呼出面板", "点一下右边的框,再按下新的组合键", hotkey, Gfx.S(210));
            g1.AddRow("使用 Win + V", "替代系统自带的剪切板历史", Switch(tWinV, Config.WinV), Gfx.S(52));
            y = AddGroup(p, y, "全局快捷键", g1, "管理员身份运行的窗口里 Win + V 可能仍弹出系统面板,可改用上面的快捷键。");

            SettingsGroup g2 = new SettingsGroup();
            int rh = Gfx.S(36);
            g2.AddRow("粘贴 / 打开", null, new KeyCaps("Enter"), Gfx.S(160), rh);
            g2.AddRow("快速预览", null, new KeyCaps("空格"), Gfx.S(160), rh);
            g2.AddRow("切换剪切板 / 文件夹", null, new KeyCaps("Ctrl", "Tab"), Gfx.S(160), rh);
            g2.AddRow("直接粘贴第 1~9 条", null, new KeyCaps("Ctrl", "1~9"), Gfx.S(160), rh);
            g2.AddRow("置顶", null, new KeyCaps("Ctrl", "P"), Gfx.S(160), rh);
            g2.AddRow("删除", null, new KeyCaps("Ctrl", "Delete"), Gfx.S(200), rh);
            g2.AddRow("撤销刚才的删除", null, new KeyCaps("Ctrl", "Z"), Gfx.S(160), rh);
            AddGroup(p, y, "面板里", g2, null);
        }

        void BuildStorage(Panel p)
        {
            int y = PageTop(p, "存储与隐私");
            SettingsGroup g1 = new SettingsGroup();
            g1.AddRow("最多保留", null, Seg(maxSeg, new string[] { "100", "200", "500", "1000" }, ClosestIndex(MaxOptions, Config.MaxItems)), Gfx.S(250));
            g1.AddRow("自动清理", null, Seg(daysSeg, new string[] { "不清理", "7 天", "30 天", "90 天" }, ClosestIndex(DayOptions, Config.KeepDays)), Gfx.S(250));
            y = AddGroup(p, y, "历史记录", g1, "置顶和放进分类的内容不会被自动清理。");

            SettingsGroup g2 = new SettingsGroup();
            Panel row = new Panel();
            row.BackColor = SetPal.Card;
            pathBox.ShowIcon = false;
            pathBox.Box.Font = Gfx.UI;
            pathBox.Box.Text = Util.FriendlyPath(Config.DataPath);
            Btn browse = new Btn();
            browse.Text = "选择…";
            browse.Click += delegate { BrowseFolder(); };
            Btn def = new Btn();
            def.Text = "默认";
            def.Click += delegate { pathBox.Box.Text = Util.FriendlyPath(Config.DefaultDir); };
            row.Controls.Add(pathBox);
            row.Controls.Add(browse);
            row.Controls.Add(def);
            row.Resize += delegate
            {
                int bw = Gfx.S(72), gap = Gfx.S(8), h = Gfx.S(32);
                int by = (row.Height - h) / 2;
                def.SetBounds(row.Width - bw, by, bw, h);
                browse.SetBounds(def.Left - gap - bw, by, bw, h);
                pathBox.SetBounds(0, by, browse.Left - gap, h);
            };
            g2.AddWide(row, Gfx.S(52));
            g2.AddRow("更换时把现有记录一起搬过去", null, Switch(tMove, true), Gfx.S(52));
            Btn clearThumbs = new Btn();
            clearThumbs.Text = "清除";
            clearThumbs.Height = Gfx.S(30);
            clearThumbs.Click += delegate { ThumbCache.ClearDisk(); clearThumbs.Text = "已清除"; clearThumbs.Invalidate(); };
            g2.AddRow("缩略图缓存", "文件夹里图片、视频的预览图缓存", clearThumbs, Gfx.S(80));
            y = AddGroup(p, y, "数据文件夹", g2, null);

            SettingsGroup g3 = new SettingsGroup();
            ignoreBox.ShowIcon = false;
            ignoreBox.Box.Font = Gfx.UI;
            ignoreBox.Box.Text = Config.IgnoreApps;
            ignoreBox.Height = Gfx.S(32);
            g3.AddRow("忽略的应用", null, ignoreBox, Gfx.S(280));
            AddGroup(p, y, "隐私", g3, "这些程序复制的内容不会被记录(比如密码管理器),用英文逗号分隔。");
        }

        void BuildAbout(Panel p)
        {
            Theme t = Theme.Cur;
            int ls = Gfx.S(88);
            Bitmap logoBmp = Brand.Logo(ls);
            Rectangle logoR = new Rectangle((p.Width - ls) / 2, Gfx.S(54), ls, ls);
            typeof(Panel).InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, p, new object[] { true });   // 光晕和文字一起画,不闪
            p.Paint += delegate(object sender, PaintEventArgs pe)
            {
                Graphics pg = pe.Graphics;
                pg.SmoothingMode = SmoothingMode.AntiAlias;
                // 完整的一圈光晕:整个都在页面里,不会被任何东西挡住或切掉
                Gfx.DrawGlow(pg, logoR, Theme.Cur.Accent, Gfx.S(42));
                if (logoBmp != null)
                {
                    pg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    pg.DrawImage(logoBmp, logoR);
                }
                TextFormatFlags cf = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                TextRenderer.DrawText(pg, Brand.Name + " " + Brand.NameCn, SetPal.Hero,
                    new Rectangle(0, logoR.Bottom + Gfx.S(16), p.Width, Gfx.S(40)), Theme.Cur.Text, cf);
                TextRenderer.DrawText(pg, "版本 " + Config.Version, Gfx.Small,
                    new Rectangle(0, logoR.Bottom + Gfx.S(58), p.Width, Gfx.S(20)), Theme.Cur.Sub, cf);
                TextRenderer.DrawText(pg, Brand.Tagline, Gfx.UI,
                    new Rectangle(0, logoR.Bottom + Gfx.S(82), p.Width, Gfx.S(24)), Theme.Cur.Text, cf);
            };

            SettingsGroup g = new SettingsGroup();
            g.AddValue("作者", Brand.Author);
            g.AddValue("数据位置", Util.FriendlyPath(Config.DataPath));
            g.AddValue("高分屏适配", (Native.IsProcessDPIAware() ? "已启用" : "未启用") + "  ·  缩放 " + (int)Math.Round(Gfx.Scale * 100) + "%");
            AddGroup(p, Gfx.S(276), null, g, "© 2026 " + Brand.Author);
        }

        static int ClosestIndex(int[] opts, int v)
        {
            int best = 0, bd = int.MaxValue;
            for (int i = 0; i < opts.Length; i++)
            {
                int d = Math.Abs(opts[i] - v);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void ShowPage()
        {
            for (int i = 0; i < pages.Count; i++) pages[i].Visible = i == side.Selected;
        }

        void BrowseFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择用来保存剪切板历史的文件夹";
                d.ShowNewFolderButton = true;
                try { d.SelectedPath = Util.RealPath(pathBox.Box.Text); } catch { }
                if (d.ShowDialog(this) == DialogResult.OK) pathBox.Box.Text = Util.FriendlyPath(d.SelectedPath);
            }
        }

        void OnOk()
        {
            string dir = Util.RealPath(pathBox.Box.Text);
            if (dir.Length == 0) dir = Config.DefaultDir;
            try { Directory.CreateDirectory(dir); }
            catch
            {
                MessageBox.Show(this, "这个数据文件夹无法创建或访问,请换一个位置。", Brand.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Config.ThemeMode = themeSeg.Selected;
            Config.AccentIndex = accent.Selected;
            if (posSeg.Selected != 2) Config.PopupAtCursor = posSeg.Selected == 0;
            Config.Layout = layoutSeg.Selected == 0 ? 1 : 0;
            Config.HeaderMode = headerSeg.Selected;
            Config.ShelfTransparency = transSlider.Value;
            Config.HeaderColor = headerColor.Value.ToArgb();
            Config.Ctrl = hotkey.Ctrl; Config.Alt = hotkey.Alt; Config.Shift = hotkey.Shift;
            Config.Win = hotkey.Win; Config.Key = hotkey.Key;
            Config.AutoPaste = tPaste.Checked;
            Config.AutoUpdate = tUpdate.Checked;
            Config.WinV = tWinV.Checked;
            Config.RecordImages = tImages.Checked;
            Config.MaxItems = MaxOptions[maxSeg.Selected];
            Config.KeepDays = DayOptions[daysSeg.Selected];
            Config.IgnoreApps = ignoreBox.Box.Text.Trim();
            Config.DataDir = Util.SamePath(dir, Config.DefaultDir) ? "" : dir;
            MoveExisting = tMove.Checked;
            Config.Save();

            try { Startup.Set(tAuto.Checked); }
            catch
            {
                MessageBox.Show(this, "写入开机启动项失败(可能被安全软件拦截)。", Brand.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // ================================================================ 卡片画廊(Paste 风格)
    static class ShelfMetrics
    {
        // 卡片尺寸跟随缩放(在面板顶部上下拖动,或 Ctrl+滚轮)
        public static int Z(int px) { return Gfx.S((int)Math.Round(px * Config.CardZoom)); }
        public static int CardW { get { return Z(212); } }
        public static int CardH { get { return Z(236); } }
        public static int HeaderH { get { return Z(52); } }
        public static int FooterH { get { return Z(30); } }
        public static int BodyH { get { return CardH - HeaderH - FooterH; } }
        public static int Gap { get { return Z(16); } }
        public static int ShelfH { get { return Gfx.S(60) + CardH + Gfx.S(40); } }
    }

    // 来源程序的图标和"主色"(卡片顶部色带的颜色就取自这里)
    static class AppIcons
    {
        static readonly Dictionary<string, Bitmap> icons = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, int> tints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static Bitmap Get(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Bitmap b;
            if (icons.TryGetValue(path, out b)) return b;
            b = null;
            try
            {
                if (File.Exists(path))
                {
                    using (Icon ic = Icon.ExtractAssociatedIcon(path))
                    {
                        if (ic != null) b = ic.ToBitmap();
                    }
                }
            }
            catch { b = null; }
            icons[path] = b;
            return b;
        }

        public static bool TryTint(string path, out Color c)
        {
            c = Color.Empty;
            if (string.IsNullOrEmpty(path)) return false;
            int argb;
            if (!tints.TryGetValue(path, out argb))
            {
                argb = 0;
                Bitmap b = Get(path);
                if (b != null)
                {
                    try { argb = ComputeTint(b); } catch { argb = 0; }
                }
                tints[path] = argb;
            }
            if (argb == 0) return false;
            c = Color.FromArgb(argb);
            return true;
        }

        // 取图标里饱和度高、亮度适中的像素的加权平均色
        static int ComputeTint(Bitmap b)
        {
            double r = 0, g = 0, bl = 0, wsum = 0;
            for (int y = 0; y < b.Height; y++)
            {
                for (int x = 0; x < b.Width; x++)
                {
                    Color p = b.GetPixel(x, y);
                    if (p.A < 128) continue;
                    double sat = p.GetSaturation();
                    double br = p.GetBrightness();
                    double w = sat * (1 - Math.Abs(br - 0.5) * 1.2);
                    if (w <= 0.05) continue;
                    r += p.R * w; g += p.G * w; bl += p.B * w; wsum += w;
                }
            }
            if (wsum < 8) return 0;
            return Color.FromArgb(255, (int)(r / wsum), (int)(g / wsum), (int)(bl / wsum)).ToArgb();
        }
    }

    // 卡片的配色和文字
    static class CardLook
    {
        public static Color ForType(Item it)
        {
            if (it.IsImage || it.IsImageFile) return Color.FromArgb(155, 89, 214);
            if (it.IsFiles) return Color.FromArgb(232, 136, 38);
            switch (it.Kind)
            {
                case Item.KUrl: return Color.FromArgb(0, 122, 255);
                case Item.KEmail: return Color.FromArgb(46, 160, 90);
                case Item.KColor: return it.SwatchColor();
                case Item.KPath: return Color.FromArgb(232, 136, 38);
                case Item.KNumber: return Color.FromArgb(32, 160, 190);
                case Item.KTrack: return Color.FromArgb(214, 112, 52);
            }
            return Theme.Cur.Accent;
        }

        // 优先级:单张卡片自定义 > 颜色值本身 > 设置里的模式(来源程序 / 内容类型 / 统一颜色)
        public static Color Header(Item it)
        {
            if (it.CustomColor != 0) return Color.FromArgb(it.CustomColor);
            if (Config.HeaderMode == 2) return Color.FromArgb(Config.HeaderColor);
            if (Config.HeaderMode == 0)
            {
                int cc = CategoryColor(it);
                return cc != 0 ? Color.FromArgb(cc) : Readable(ForType(it));
            }
            if (it.Kind == Item.KColor) return Readable(it.SwatchColor());
            Color c;
            if (AppIcons.TryTint(it.AppPath, out c)) return Readable(c);
            return Readable(ForType(it));
        }

        // 内容所在分类的圆点颜色:自定义分类 > 置顶 > 内容类型(文本/图片/链接/文件) > 全部
        public static int CategoryColor(Item it)
        {
            Board b = History.Instance != null ? History.Instance.FindBoard(it.Board) : null;
            if (b != null && b.Color != 0) return b.Color;
            int[] d = Config.TabDotColors;
            if (it.Pinned && d[1] != 0) return d[1];
            int k;
            if (it.IsImage || it.IsImageFile) k = 3;
            else if (it.IsFiles) k = 5;
            else if (it.Kind == Item.KUrl) k = 4;
            else k = 2;
            return d[k] != 0 ? d[k] : d[0];
        }

        // 色带上的文字颜色:浅色底(比如黄色)用深色字,深色底用白字
        public static Color InkOn(Color c)
        {
            double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return lum > 0.66 ? Color.FromArgb(34, 34, 38) : Color.White;
        }

        // 保证白字在色带上看得清
        static Color Readable(Color c)
        {
            float l = c.GetBrightness();
            if (l > 0.60f) return Theme.Blend(c, Color.Black, 0.32);
            if (l < 0.16f) return Theme.Blend(c, Color.White, 0.20);
            return c;
        }

        public static string Title(Item it)
        {
            return !string.IsNullOrEmpty(it.CustomTitle) ? it.CustomTitle : TypeName(it);
        }

        public static string TypeName(Item it)
        {
            if (it.IsImage) return "图片";
            if (it.IsImageFile) return "图片文件";
            if (it.IsFiles)
            {
                int n = it.Paths.Length;
                return n > 1 ? n + " 个文件" : "文件";
            }
            Courier.Company tc = Courier.For(it);
            if (tc != null) return tc.Name;
            switch (it.Kind)
            {
                case Item.KUrl: return "链接";
                case Item.KEmail: return "邮箱";
                case Item.KColor: return "颜色";
                case Item.KPath: return "路径";
                case Item.KNumber: return "数字";
                case Item.KTrack:
                    {
                        string[] c = Courier.Detect(it.Text);
                        return c != null ? c[0] : "快递单号";
                    }
            }
            return "文本";
        }

        public static string Footer(Item it)
        {
            if (it.IsImage) return it.W > 0 ? it.W + " × " + it.H : "图片";
            if (it.IsFiles)
            {
                if (it.IsImageFile && it.W > 0) return it.W + " × " + it.H;
                string[] ps = it.Paths;
                if (ps.Length == 0) return "文件";
                if (ps.Length > 1) return ps.Length + " 个项目";
                try
                {
                    if (Directory.Exists(ps[0])) return "文件夹";
                    if (!File.Exists(ps[0])) return "已移动或删除";
                }
                catch { }
                string ext = System.IO.Path.GetExtension(ps[0]).TrimStart('.').ToUpperInvariant();
                return ext.Length > 0 ? ext + " 文件" : "文件";
            }
            if (it.Kind == Item.KColor)
            {
                Color c = it.SwatchColor();
                return "RGB  " + c.R + ", " + c.G + ", " + c.B;
            }
            if (it.Kind == Item.KText) return it.Info.Replace(" 字", " 字符");
            return it.Text.Trim().Length + " 字符";
        }

        public static string Host(string url)
        {
            try
            {
                string u = url.Trim();
                if (u.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) u = "http://" + u;
                Uri uri;
                if (Uri.TryCreate(u, UriKind.Absolute, out uri))
                {
                    string h = uri.Host;
                    return h.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? h.Substring(4) : h;
                }
            }
            catch { }
            return url;
        }

        public static string FolderOf(Item it)
        {
            try
            {
                string[] ps = it.Paths;
                if (ps.Length == 0) return "";
                string d = System.IO.Path.GetDirectoryName(ps[0].TrimEnd('\\', '/'));
                return d ?? "";
            }
            catch { return ""; }
        }

        public static string BodyText(Item it)
        {
            string s = it.Text ?? "";
            if (s.Length > 800) s = s.Substring(0, 800);
            return s.Replace("\t", "    ").Trim('\r', '\n');
        }
    }

    // 横向滚动的卡片条
    // 横向卡片条:入场动画、悬停上浮、拖拽排序、平滑滚动
    class CardStrip : Control, IClipView
    {
        class Anim
        {
            public float X;      // 卡片当前位置(内容坐标)
            public float Lift;   // 悬停上浮程度 0~1
            public float V;      // 位置速度(弹簧动画)
        }

        List<Item> items = new List<Item>();
        readonly Dictionary<Item, Anim> anims = new Dictionary<Item, Anim>();
        int selected = -1, hover = -1, downIdx = -1;
        float scrollX, targetX, scrollV;
        Point downPt;
        bool dragArmed, reordering;
        int dragIdx = -1, insertIdx = -1, dragMouseX;
        float grabDX;
        double appearStart = -1e9;
        double lastTick;
        bool animHiRes;
        int appearStagger = 30, appearDur = 260;
        readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer();
        public string EmptyText { get; set; }
        public event Action<int> ItemActivated;
        public event Action<int, Point> ItemContext;
        public event Action<int> SelectionChanged;
        public event Action<int> DragStart;
        public event Action<int, int> Reordered;
        public event Action<int> ZoomWheel;
        // 拖到卡片区上方(分类标签那里):返回 true 表示鼠标在可放下的区域;idx 为 -1 表示已离开
        public Func<int, Point, bool> DragAbove { get; set; }
        public event Action<int, Point> DropAbove;
        bool overTop;
        readonly DragGhost ghost = new DragGhost();
        Bitmap ghostFull, ghostMini;   // 正常大小 / 拖到分类标签上时缩小的样子
        bool ghostMiniShown;
        int grabDY;
        public event Action<int> PreviewRequested;

        public CardStrip()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
            ImeMode = ImeMode.Disable;   // 卡片区关闭输入法:空格、方向键不会被输入法截走
            EmptyText = "";
            anim.Interval = 15;
            anim.Tick += delegate { Animate(); };
            Item.ThumbReady += OnThumbReady;
        }

        void OnThumbReady()
        {
            try
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(new MethodInvoker(Invalidate));
            }
            catch { }
        }

        int Pad { get { return Gfx.S(24); } }
        int Step { get { return ShelfMetrics.CardW + ShelfMetrics.Gap; } }
        int TotalW { get { return items.Count == 0 ? 0 : Pad * 2 + items.Count * Step - ShelfMetrics.Gap; } }
        float MaxScroll { get { return Math.Max(0, TotalW - Width); } }
        float Clamp(float v) { return Math.Max(0f, Math.Min(MaxScroll, v)); }
        int BaseY { get { return Gfx.S(16); } }   // 上方留足空间,浮起/拖动的卡片和选中描边都不会被裁掉

        Anim AnimOf(int i)
        {
            Item it = items[i];
            Anim a;
            if (!anims.TryGetValue(it, out a))
            {
                a = new Anim();
                a.X = Pad + i * Step;
                anims[it] = a;
            }
            return a;
        }

        // 每张卡片应该在的位置;拖拽排序时,其他卡片会让出空位
        float TargetX(int i)
        {
            int slot = i;
            if (reordering && dragIdx >= 0)
            {
                if (i == dragIdx) return dragMouseX - grabDX + scrollX;
                int j = i > dragIdx ? i - 1 : i;
                slot = j < insertIdx ? j : j + 1;
            }
            return Pad + slot * Step;
        }

        Rectangle RectOf(int i)
        {
            Anim a = AnimOf(i);
            return new Rectangle((int)(a.X - scrollX), BaseY - (int)(a.Lift * Gfx.S(6)),
                ShelfMetrics.CardW, ShelfMetrics.CardH);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (reordering && i == dragIdx) continue;
                Rectangle r = RectOf(i);
                if (r.Right < 0 || r.X > Width) continue;
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        void Kick()
        {
            if (!anim.Enabled)
            {
                lastTick = Gfx.Ms - 16.67;
                if (!animHiRes) { animHiRes = true; Gfx.HiRes(true); }
                anim.Start();
            }
        }

        void StopAnim()
        {
            anim.Stop();
            if (animHiRes) { animHiRes = false; Gfx.HiRes(false); }
        }

        float AppearOf(int i)
        {
            double ms = (Gfx.Ms - appearStart) - Math.Min(i, 14) * appearStagger;
            if (ms <= 0) return 0f;
            double p = ms / appearDur;
            if (p >= 1) return 1f;
            return (float)Gfx.BackOut(p, 0.6);   // 柔和上浮,几乎察觉不到的落定感
        }

        void Animate()
        {
            bool busy = false;
            double now = Gfx.Ms;
            double dt = Math.Max(1, Math.Min(50, now - lastTick));
            lastTick = now;
            float kScroll = Gfx.Ease(0.18f, dt), kX = Gfx.Ease(0.2f, dt), kLift = Gfx.Ease(0.3f, dt);
            if (reordering)
            {
                // 拖到两侧边缘时自动滚动
                int edge = Gfx.S(70);
                if (dragMouseX < edge) targetX = Clamp(targetX - Gfx.S(16));
                else if (dragMouseX > Width - edge) targetX = Clamp(targetX + Gfx.S(16));
                UpdateInsert();
                busy = true;
            }
            // 滚动用弹簧:有惯性,到边缘会轻轻回弹
            float d = targetX - scrollX;
            if (Math.Abs(d) < 0.6f) { scrollX = targetX; scrollV = 0; }
            else { scrollX += d * kScroll; busy = true; }

            for (int i = 0; i < items.Count; i++)
            {
                Anim a = AnimOf(i);
                float tx = TargetX(i);
                if (reordering && i == dragIdx) { a.X = tx; a.V = 0; }
                else
                {
                    // 弹簧:卡片让位、归位时带一点点回弹
                    // 平滑靠拢,不回弹
                    float dx = tx - a.X;
                    if (Math.Abs(dx) < 0.5f) a.X = tx;
                    else { a.X += dx * kX; busy = true; }
                }
                float tl = reordering ? 0f : (i == hover ? 1f : (i == selected ? 0.5f : 0f));
                float dl = tl - a.Lift;
                if (Math.Abs(dl) < 0.02f) a.Lift = tl;
                else { a.Lift += dl * kLift; busy = true; }
            }
            if ((now - appearStart) < appearDur + appearStagger * 14) busy = true;
            Invalidate();
            if (!busy) StopAnim();
        }

        // mode:0 保持位置(排序/删除后平滑挪动)  1 快速入场  2 完整入场
        public void SetItems(List<Item> list, int mode)
        {
            items = list;
            if (mode > 0)
            {
                anims.Clear();
                scrollX = targetX = 0;
                scrollV = 0;
                appearStart = Gfx.Ms;
                appearStagger = mode == 2 ? 45 : 14;
                appearDur = mode == 2 ? 460 : 220;
            }
            else
            {
                HashSet<Item> keep = new HashSet<Item>(items);
                foreach (Item k in anims.Keys.ToList())
                    if (!keep.Contains(k)) anims.Remove(k);
                targetX = Clamp(targetX);
            }
            for (int i = 0; i < items.Count; i++) AnimOf(i);
            PruneCache();
            selected = items.Count > 0 ? 0 : -1;
            hover = -1;
            Kick();
            Invalidate();
        }

        // 卡片尺寸变了:直接按新尺寸排好
        public void Relayout()
        {
            ClearRenderCache();
            for (int i = 0; i < items.Count; i++) AnimOf(i).X = Pad + i * Step;
            targetX = Clamp(targetX);
            scrollX = Clamp(scrollX);
            Invalidate();
        }

        public int Selected
        {
            get { return selected; }
            set
            {
                if (items.Count == 0) { selected = -1; return; }
                int v = Math.Max(0, Math.Min(items.Count - 1, value));
                bool changed = v != selected;
                selected = v;
                EnsureVisible();
                Invalidate();
                if (changed && SelectionChanged != null) SelectionChanged(selected);
            }
        }

        public void Move(int d) { Selected = (selected < 0 ? 0 : selected) + d; }

        public void ScrollBy(float px)
        {
            targetX = Clamp(targetX + px);
            Kick();
        }

        void EnsureVisible()
        {
            if (selected < 0) return;
            int left = selected * Step;
            int right = left + ShelfMetrics.CardW + Pad * 2;
            if (left < targetX) targetX = left;
            else if (right > targetX + Width) targetX = right - Width;
            targetX = Clamp(targetX);
            Kick();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            targetX = Clamp(targetX);
            scrollX = Clamp(scrollX);
            Invalidate();
        }

        // ---------------- 鼠标
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
            {
                if (ZoomWheel != null) ZoomWheel(e.Delta);
                return;
            }
            ScrollBy(-e.Delta / 120f * Step * 0.9f);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = HitTest(e.Location);
            Focus();
            downIdx = i;
            downPt = e.Location;
            dragArmed = e.Button == MouseButtons.Left && i >= 0;
            if (i >= 0) Selected = i;
            if (e.Button == MouseButtons.Right && i >= 0 && ItemContext != null) ItemContext(i, PointToScreen(e.Location));
        }

        void BeginReorder(int idx, Point p)
        {
            if (idx < 0 || idx >= items.Count) return;
            reordering = true;
            dragIdx = idx;
            insertIdx = idx;
            Anim a = AnimOf(idx);
            grabDX = p.X - (a.X - scrollX);
            grabDY = p.Y - RectOf(idx).Y;
            try
            {
                ghostFull = RenderCard(idx);
                int mw = Math.Max(1, (int)(ghostFull.Width * 0.6f)), mh = Math.Max(1, (int)(ghostFull.Height * 0.6f));
                ghostMini = new Bitmap(mw, mh, PixelFormat.Format24bppRgb);
                using (Graphics mg = Graphics.FromImage(ghostMini))
                {
                    mg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    mg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    mg.DrawImage(ghostFull, 0, 0, mw, mh);
                }
                ghostMiniShown = false;
                ghost.ShowCard(ghostFull, GhostPos(p));
            }
            catch { }
            dragMouseX = p.X;
            hover = -1;
            Cursor = Cursors.SizeAll;
            Kick();
        }

        void ClearAbove()
        {
            if (!overTop) return;
            overTop = false;
            if (DragAbove != null) DragAbove(-1, Point.Empty);
        }

        void UpdateInsert()
        {
            if (!reordering || items.Count == 0) return;
            if (overTop) { insertIdx = dragIdx; return; }   // 拖在上方时,其他卡片回原位
            float center = dragMouseX - grabDX + scrollX + ShelfMetrics.CardW / 2f - Pad;
            int slot = (int)Math.Floor(center / Step);
            insertIdx = Math.Max(0, Math.Min(items.Count - 1, slot));
        }

        // 卡片跟着鼠标:正常时保持按住的位置并稍微提起;拖到分类标签上时缩小并让到鼠标右下方,不挡住标签
        Point GhostPos(Point local)
        {
            return PointToScreen(new Point(local.X - (int)grabDX, local.Y - grabDY - Gfx.S(6)));
        }

        void MoveGhost(Point local)
        {
            if (!ghost.Visible || ghostFull == null) return;
            if (overTop)
            {
                Point at = PointToScreen(new Point(local.X + Gfx.S(16), local.Y + Gfx.S(16)));
                if (!ghostMiniShown) { ghost.ShowCard(ghostMini, at); ghostMiniShown = true; }
                else ghost.Location = at;
            }
            else
            {
                if (ghostMiniShown) { ghost.ShowCard(ghostFull, GhostPos(local)); ghostMiniShown = false; }
                else ghost.Location = GhostPos(local);
            }
        }

        // 把一张卡片画成位图(给拖拽用)
        Bitmap RenderCard(int idx)
        {
            int w = ShelfMetrics.CardW, h = ShelfMetrics.CardH;
            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Theme t = Theme.Cur;
                g.Clear(t.Card);
                using (SolidBrush hb = new SolidBrush(CardLook.Header(items[idx])))
                    g.FillRectangle(hb, 0, 0, w, ShelfMetrics.HeaderH / 2);   // 让窗口圆角处和色带同色
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImageUnscaled(BodyFor(idx, new Size(w, h)), 0, 0);
                DrawCardOverlay(g, idx, new Rectangle(0, 0, w, h), 1f, false);
            }
            return bmp;
        }

        void ReleaseGhost()
        {
            ghost.HideGhost();
            if (ghostFull != null) { ghostFull.Dispose(); ghostFull = null; }
            if (ghostMini != null) { ghostMini.Dispose(); ghostMini = null; }
            ghostMiniShown = false;
        }

        void EndReorder(bool commit)
        {
            if (!reordering) return;
            ReleaseGhost();
            int from = dragIdx, to = insertIdx;
            reordering = false;
            dragIdx = -1;
            Cursor = Cursors.Default;
            if (commit && from >= 0 && to >= 0 && from != to && Reordered != null) Reordered(from, to);
            Kick();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (reordering)
            {
                dragMouseX = e.X;
                // 往下拖出面板:拖到其他程序里
                if (e.Y > Height + Gfx.S(24))
                {
                    int idx = dragIdx;
                    ClearAbove();
                    EndReorder(false);
                    if (DragStart != null) DragStart(idx);
                    return;
                }
                // 往上拖:先看是不是拖到了分类标签上;拖出整个面板才变成拖到其他程序
                if (e.Y < -Gfx.S(24))
                {
                    bool inside = DragAbove != null && DragAbove(dragIdx, PointToScreen(e.Location));
                    if (!inside)
                    {
                        int idx = dragIdx;
                        ClearAbove();
                        EndReorder(false);
                        if (DragStart != null) DragStart(idx);
                        return;
                    }
                    overTop = true;
                    UpdateInsert();
                    MoveGhost(e.Location);
                    Kick();
                    return;
                }
                if (overTop) ClearAbove();
                UpdateInsert();
                MoveGhost(e.Location);
                Kick();
                return;
            }
            if (dragArmed && (e.Button & MouseButtons.Left) != 0)
            {
                Size ds = SystemInformation.DragSize;
                if (Math.Abs(e.X - downPt.X) > ds.Width || Math.Abs(e.Y - downPt.Y) > ds.Height)
                {
                    dragArmed = false;
                    BeginReorder(downIdx, downPt);
                    return;
                }
            }
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Kick(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragArmed = false;
            if (reordering && overTop)
            {
                // 在分类标签上松手:放进那个分类
                int idx = dragIdx;
                overTop = false;
                EndReorder(false);
                if (DropAbove != null) DropAbove(idx, PointToScreen(e.Location));
                return;
            }
            if (reordering) EndReorder(true);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!reordering) { hover = -1; Kick(); }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            int i = HitTest(e.Location);
            if (i >= 0 && ItemActivated != null) ItemActivated(i);
        }

        // ---------------- 绘制
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Gfx.PaintParentBack(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (items.Count == 0)
            {
                Gfx.Text(g, EmptyText, Gfx.UI, ClientRectangle, t.Sub, TextFormatFlags.HorizontalCenter);
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (reordering && i == dragIdx) continue;
                Rectangle r = RectOf(i);
                if (r.Right < -Gfx.S(20) || r.X > Width + Gfx.S(20)) continue;
                DrawCard(g, i, r, AppearOf(i), false);
            }
            if (TotalW > Width)
            {
                int tw = Math.Max(Gfx.S(40), (int)((long)Width * Width / TotalW));
                int tx = (int)(scrollX / MaxScroll * (Width - tw));
                Gfx.FillRound(g, t.Border, new Rectangle(tx, Height - Gfx.S(6), tw, Gfx.S(3)), 2);
            }
        }

        // 按比例铺满:与目标同尺寸直接画;比目标小的原图居中;否则居中裁切缩放
        static void DrawCover(Graphics g, Bitmap bmp, Rectangle dest)
        {
            if (bmp.Width == dest.Width && bmp.Height == dest.Height)
            {
                g.DrawImage(bmp, dest);
                return;
            }
            if (bmp.Width <= dest.Width && bmp.Height <= dest.Height)
            {
                g.DrawImage(bmp, new Rectangle(dest.X + (dest.Width - bmp.Width) / 2,
                    dest.Y + (dest.Height - bmp.Height) / 2, bmp.Width, bmp.Height));
                return;
            }
            double sc = Math.Max(dest.Width / (double)bmp.Width, dest.Height / (double)bmp.Height);
            double cw = dest.Width / sc, ch = dest.Height / sc;
            RectangleF src = new RectangleF((float)((bmp.Width - cw) / 2), (float)((bmp.Height - ch) / 2), (float)cw, (float)ch);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(bmp, (RectangleF)dest, src, GraphicsUnit.Pixel);
        }

        Rectangle EyeRect(Rectangle card)
        {
            int sz = Gfx.S(28);
            return new Rectangle(card.Right - sz - Gfx.S(10), card.Y + ShelfMetrics.HeaderH + Gfx.S(10), sz, sz);
        }

        // 半透明圆底 + 一只线条眼睛
        static void DrawEye(Graphics g, Rectangle b)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(160, 24, 24, 28))) g.FillEllipse(bg, b);
            float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
            float w = b.Width * 0.30f, h = b.Height * 0.17f;
            using (GraphicsPath eye = new GraphicsPath())
            {
                eye.AddBezier(cx - w, cy, cx - w * 0.4f, cy - h * 2f, cx + w * 0.4f, cy - h * 2f, cx + w, cy);
                eye.AddBezier(cx + w, cy, cx + w * 0.4f, cy + h * 2f, cx - w * 0.4f, cy + h * 2f, cx - w, cy);
                eye.CloseFigure();
                using (Pen pen = new Pen(Color.White, Gfx.F(1.5f))) g.DrawPath(pen, eye);
            }
            float pr = b.Width * 0.09f;
            using (SolidBrush pb = new SolidBrush(Color.White)) g.FillEllipse(pb, cx - pr, cy - pr, pr * 2, pr * 2);
        }

        static FontFamily digitFamily;   // 只创建一次

        // 用字形的实际轮廓来居中,数字在圆里上下左右都正
        static void DrawCenteredDigit(Graphics g, string text, Rectangle box, Color c)
        {
            try
            {
                if (digitFamily == null) digitFamily = new FontFamily("Segoe UI");
                FontFamily ff = digitFamily;
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddString(text, ff, (int)FontStyle.Bold, box.Height * 0.6f, new PointF(0, 0), StringFormat.GenericTypographic);
                    RectangleF bb = gp.GetBounds();
                    using (Matrix m = new Matrix())
                    {
                        m.Translate(box.X + (box.Width - bb.Width) / 2f - bb.X, box.Y + (box.Height - bb.Height) / 2f - bb.Y);
                        gp.Transform(m);
                    }
                    using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, gp);
                }
            }
            catch
            {
                TextRenderer.DrawText(g, text, Gfx.Digit, box, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        void DrawCard(Graphics g, int i, Rectangle r, float appear, bool floating)
        {
            r.Offset(0, (int)((1 - appear) * Gfx.S(40)));

            // 投影(预先画好的图,按浮起高度分几档)
            float lift = floating ? 1f : AnimOf(i).Lift;
            Bitmap shadow = ShadowFor(r.Width, r.Height, floating, lift);
            g.DrawImageUnscaled(shadow, r.X - ShadowPad, r.Y - ShadowPad);

            // 卡片主体(预先画好的图,内容不变就不重画)
            Bitmap body0 = BodyFor(i, r.Size);
            using (TextureBrush tb = new TextureBrush(body0, WrapMode.Clamp))
            using (GraphicsPath cp = Gfx.Round(r, 14))
            {
                tb.TranslateTransform(r.X, r.Y);
                g.FillPath(tb, cp);
            }
            DrawCardOverlay(g, i, r, appear, floating);
        }

        // ---------------- 渲染缓存
        // 卡片主体:内容(标题、时间、预览图、文字…)没变化时直接复用,动画、滚动、悬停时不再重新排版文字
        readonly Dictionary<Item, KeyValuePair<string, Bitmap>> bodyCache = new Dictionary<Item, KeyValuePair<string, Bitmap>>();
        static readonly Dictionary<string, Bitmap> shadowCache = new Dictionary<string, Bitmap>();
        static int ShadowPad { get { return Gfx.S(16); } }

        Bitmap ShadowFor(int w, int h, bool floating, float lift)
        {
            Theme t = Theme.Cur;
            int bucket = floating ? 9 : (int)Math.Round(Math.Max(0, Math.Min(1, lift)) * 4);
            string key = w + "x" + h + "|" + t.Dark + "|" + bucket;
            Bitmap b;
            if (shadowCache.TryGetValue(key, out b)) return b;
            if (shadowCache.Count > 40) shadowCache.Clear();   // 尺寸变了(缩放)就重新来
            int pad = ShadowPad;
            b = new Bitmap(w + pad * 2, h + pad * 2 + Gfx.S(8), PixelFormat.Format32bppPArgb);
            float lf = floating ? 1f : bucket / 4f;
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(pad, pad, w, h);
                int layers = floating ? 9 : 6;
                for (int k = layers; k >= 1; k--)
                {
                    Rectangle sr = r;
                    int grow = Gfx.S(k) / 2 + 1;
                    sr.Inflate(grow, grow);
                    sr.Offset(0, (int)(Gfx.S(k) * (floating ? 1.1f : 0.55f) + lf * Gfx.S(2)));
                    int alpha = (floating ? (t.Dark ? 15 : 6) : (t.Dark ? 12 : 4)) + (int)(lf * 2);
                    Gfx.FillRound(g, Color.FromArgb(alpha, 0, 0, 0), sr, 14 + k / 2);
                }
            }
            shadowCache[key] = b;
            return b;
        }

        string BodyKey(int i, Size sz)
        {
            Theme t = Theme.Cur;
            Item it = items[i];
            Board bd = History.Instance != null ? History.Instance.FindBoard(it.Board) : null;
            Bitmap pv = it.Preview;
            return sz.Width + "x" + sz.Height + "|" + t.Dark + "|" + t.Accent.ToArgb() + "|" + CardLook.Header(it).ToArgb() + "|" +
                   CardLook.Title(it) + "|" + it.TimeAgo() + "|" + it.SizeText() + "|" + it.Pinned + "|" +
                   (bd != null ? bd.Color.ToString() : "") + "|" + (i < 9) + "|" + (pv != null ? pv.GetHashCode() : 0) + "|" +
                   it.ThumbPending + "|" + (it.FileIcon != null) + "|" + (AppIcons.Get(it.AppPath) != null);
        }

        Bitmap BodyFor(int i, Size sz)
        {
            Item it = items[i];
            if (it.IsImage || it.IsImageFile) it.EnsureThumb();
            if (it.IsFiles) it.EnsureFileIcon();
            string key = BodyKey(i, sz);
            KeyValuePair<string, Bitmap> hit;
            if (bodyCache.TryGetValue(it, out hit) && hit.Key == key) return hit.Value;
            if (bodyCache.ContainsKey(it) && hit.Value != null) hit.Value.Dispose();
            // 24 位位图:不带透明通道,文字(ClearType)才能画得清晰
            Bitmap b = new Bitmap(Math.Max(1, sz.Width), Math.Max(1, sz.Height), PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Theme.Cur.Card);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                DrawCardBody(g, i, new Rectangle(0, 0, sz.Width, sz.Height));
            }
            bodyCache[it] = new KeyValuePair<string, Bitmap>(key, b);
            return b;
        }

        void PruneCache()
        {
            HashSet<Item> live = new HashSet<Item>(items);
            foreach (Item k in bodyCache.Keys.ToList())
            {
                if (!live.Contains(k))
                {
                    bodyCache[k].Value.Dispose();
                    bodyCache.Remove(k);
                }
            }
        }

        public void ClearRenderCache()
        {
            foreach (KeyValuePair<string, Bitmap> kv in bodyCache.Values) kv.Value.Dispose();
            bodyCache.Clear();
        }

        // 卡片里的内容:只在内容变化时画一次(画到缓存图里)
        void DrawCardBody(Graphics g, int i, Rectangle r)
        {
            Theme t = Theme.Cur;
            Item it = items[i];
            int hh = ShelfMetrics.HeaderH, fh = ShelfMetrics.FooterH;

            // 顶部色带
            Color hc = CardLook.Header(it);
            Color ink = CardLook.InkOn(hc);
            Rectangle hr = new Rectangle(r.X, r.Y, r.Width, hh);
            using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(hr.X, hr.Y - 1, hr.Width, hr.Height + 2),
                Theme.Blend(hc, Color.White, 0.10), Theme.Blend(hc, Color.Black, 0.10), LinearGradientMode.Vertical))
                g.FillRectangle(lb, hr);

            Courier.Company courier = Courier.For(it);
            Bitmap appIcon = courier != null ? null : AppIcons.Get(it.AppPath);
            int iconSz = Math.Min(Gfx.S(30), hh - Gfx.S(14));
            int textRight = hr.Right - Gfx.S(16) - (appIcon != null || courier != null ? iconSz + Gfx.S(10) : 0);
            // 色带:标题 + 小字"多久之前 · 大小"
            string sizeTxt = it.SizeText();
            string subt = it.TimeAgo() + (sizeTxt != null ? "  ·  " + sizeTxt : "");
            int tx0 = hr.X + Gfx.S(16);
            int tw0 = Math.Max(10, textRight - tx0);
            Color subInk = Theme.Blend(hc, ink, 0.80);
            if (hh < Gfx.S(40))
            {
                // 卡片缩得很小时:标题和时间放在同一行
                string ttl = CardLook.Title(it);
                int titleW = Math.Min(Gfx.SoftWidth(g, ttl, Gfx.HeaderFont) + Gfx.S(2), tw0);
                Gfx.SoftText(g, ttl, Gfx.HeaderFont, new Rectangle(tx0, hr.Y, titleW, hh), ink, false);
                if (titleW + Gfx.S(30) < tw0)
                    Gfx.SoftText(g, subt, Gfx.Small, new Rectangle(tx0 + titleW + Gfx.S(8), hr.Y, tw0 - titleW - Gfx.S(8), hh), subInk, false);
            }
            else
            {
                int blockY = hr.Y + (hh - Gfx.S(40)) / 2;
                Gfx.SoftText(g, CardLook.Title(it), Gfx.HeaderFont, new Rectangle(tx0, blockY, tw0, Gfx.S(22)), ink, false);
                Gfx.SoftText(g, subt, Gfx.Small, new Rectangle(tx0, blockY + Gfx.S(22), tw0, Gfx.S(18)), subInk, false);
            }
            if (courier != null)
                Courier.DrawBadge(g, courier, new Rectangle(hr.Right - Gfx.S(16) - iconSz, hr.Y + (hh - iconSz) / 2, iconSz, iconSz));
            else if (appIcon != null)
            {
                Rectangle ir = new Rectangle(hr.Right - Gfx.S(16) - iconSz, hr.Y + (hh - iconSz) / 2, iconSz, iconSz);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(appIcon, ir);
            }

            // 内容区
            Rectangle body = new Rectangle(r.X, hr.Bottom, r.Width, r.Height - hh - fh);
            Rectangle inner = new Rectangle(body.X + Gfx.S(16), body.Y + Gfx.S(12), body.Width - Gfx.S(32), body.Height - Gfx.S(14));
            int kind = it.Kind;
            if (it.IsImage || it.IsImageFile)
            {
                using (SolidBrush ib = new SolidBrush(t.Input)) g.FillRectangle(ib, body);
                Bitmap pv = it.Preview;
                if (pv != null) DrawCover(g, pv, body);
                else if (!it.ThumbPending)
                    Gfx.TextOn(g, "预览不可用", Gfx.Small, body, t.Sub, t.Input, TextFormatFlags.HorizontalCenter);
            }
            else if (kind == Item.KColor)
            {
                Color sc = it.SwatchColor();
                using (SolidBrush cb = new SolidBrush(sc)) g.FillRectangle(cb, body);
                Color swInk = CardLook.InkOn(sc);
                Gfx.SoftText(g, it.Text.Trim().ToUpperInvariant(), Gfx.Big, body, swInk, true);
            }
            else if (it.IsFiles)
            {
                it.EnsureFileIcon();
                int isz = ShelfMetrics.Z(46);
                int iy = body.Y + ShelfMetrics.Z(14);
                if (it.FileIcon != null)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(it.FileIcon, new Rectangle(body.X + (body.Width - isz) / 2, iy, isz, isz));
                }
                Rectangle nr = new Rectangle(inner.X, iy + isz + ShelfMetrics.Z(6), inner.Width, ShelfMetrics.Z(40));
                TextRenderer.DrawText(g, it.FileTitle(), Gfx.Bold, nr, t.Text, t.Card,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
                TextRenderer.DrawText(g, CardLook.FolderOf(it), Gfx.Small,
                    new Rectangle(inner.X, nr.Bottom + ShelfMetrics.Z(2), inner.Width, Math.Max(10, body.Bottom - nr.Bottom - ShelfMetrics.Z(4))), t.Sub, t.Card,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            }
            else if (kind == Item.KUrl)
            {
                string url = it.Text.Trim();
                int hostH = ShelfMetrics.Z(52);
                TextRenderer.DrawText(g, CardLook.Host(url), Gfx.Big,
                    new Rectangle(inner.X, inner.Y, inner.Width, hostH), t.Text, t.Card,
                    TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
                TextRenderer.DrawText(g, url, Gfx.Small,
                    new Rectangle(inner.X, inner.Y + hostH + Gfx.S(4), inner.Width, Math.Max(10, inner.Height - hostH - Gfx.S(4))), t.Accent, t.Card,
                    TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            }
            else
            {
                string bt = CardLook.BodyText(it);
                bool shortText = bt.Length <= 48 && bt.IndexOf('\n') < 0;
                TextRenderer.DrawText(g, bt, shortText ? Gfx.Big : Gfx.UI, inner, t.Text, t.Card,
                    TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            }

            // 底部:大小/字数 + 置顶星(序号在每帧单独画,因为它跟着选中状态变色)
            Rectangle fr = new Rectangle(r.X + Gfx.S(16), r.Bottom - fh, r.Width - Gfx.S(32), fh - Gfx.S(2));
            int fright = fr.Right;
            if (i < 9)
            {
                int cd = Math.Min(Gfx.S(18), fr.Height - Gfx.S(6));
                fright -= cd + Gfx.S(8);
            }
            if (it.Pinned)
            {
                Gfx.TextOn(g, "★", Gfx.Symbol, new Rectangle(fright - Gfx.S(18), fr.Y, Gfx.S(18), fr.Height),
                    Color.FromArgb(255, 190, 0), t.Card, TextFormatFlags.HorizontalCenter);
                fright -= Gfx.S(22);
            }
            int fx = fr.X;
            Board bd = History.Instance != null ? History.Instance.FindBoard(it.Board) : null;
            if (bd != null)
            {
                int dsz = Gfx.S(8);
                using (SolidBrush dbr = new SolidBrush(Color.FromArgb(bd.Color)))
                    g.FillEllipse(dbr, fx, fr.Y + (fr.Height - dsz) / 2, dsz, dsz);
                fx += Gfx.S(14);
            }
            Gfx.TextOn(g, CardLook.Footer(it), Gfx.Small, new Rectangle(fx, fr.Y, Math.Max(10, fright - fx), fr.Height), t.Sub, t.Card,
                TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        // 每一帧都要画的部分:序号、选中/悬停描边、入场淡入(都很轻)
        void DrawCardOverlay(Graphics g, int i, Rectangle r, float appear, bool floating)
        {
            Theme t = Theme.Cur;
            bool sel = i == selected;
            bool hov = i == hover;
            int fh = ShelfMetrics.FooterH;
            if (i < 9)
            {
                Rectangle fr = new Rectangle(r.X + Gfx.S(16), r.Bottom - fh, r.Width - Gfx.S(32), fh - Gfx.S(2));
                int cd = Math.Min(Gfx.S(18), fr.Height - Gfx.S(6));
                Rectangle kc = new Rectangle(fr.Right - cd, fr.Y + (fr.Height - cd) / 2, cd, cd);
                using (SolidBrush cbr = new SolidBrush(sel ? t.Accent : Theme.Blend(t.Card, t.Text, t.Dark ? 0.14 : 0.08)))
                    g.FillEllipse(cbr, kc);
                DrawCenteredDigit(g, (i + 1).ToString(), kc, sel ? Color.White : t.Sub);
            }

            // 选中:主题色描边 + 外面一圈柔光;悬停:淡描边
            if (sel)
            {
                for (int k = 3; k >= 1; k--)
                {
                    Rectangle gr = r;
                    gr.Inflate(Gfx.S(3 + k * 2), Gfx.S(3 + k * 2));
                    Gfx.DrawRound(g, Color.FromArgb(t.Dark ? 26 : 22, t.Accent), gr, 17 + k * 2, Gfx.F(2.5f));
                }
                Rectangle rr = r;
                rr.Inflate(Gfx.S(3), Gfx.S(3));
                Gfx.DrawRound(g, t.Accent, rr, 17, Gfx.F(3f));
            }
            else if (hov)
            {
                Rectangle rr = r;
                rr.Inflate(Gfx.S(1), Gfx.S(1));
                Gfx.DrawRound(g, Color.FromArgb(90, t.Accent), rr, 15, Gfx.F(1.5f));
            }

            // 入场时从背景色里淡入(毛玻璃背景下改为只做上浮,避免出现色块)
            IBackdrop pbd = Parent as IBackdrop;
            if (appear < 1f && (pbd == null || !pbd.HasBackdrop))
            {
                Rectangle cover = r;
                cover.Inflate(Gfx.S(8), Gfx.S(8));
                cover.Height += Gfx.S(8);
                using (SolidBrush fb = new SolidBrush(Color.FromArgb((int)((1 - appear) * 255), t.Shelf)))
                    g.FillRectangle(fb, cover);
            }
        }
    }

    // 滑块
    class Slider : Control
    {
        public int Value, Minimum = 0, Maximum = 100;
        public string Suffix;   // 设置了就在右边显示数值,例如 "35%"
        public event EventHandler Changed;
        bool dragging;

        public Slider()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
            Height = Gfx.S(32);
        }

        Rectangle Track
        {
            get
            {
                int pad = Gfx.S(10);
                int textW = Suffix != null ? Gfx.S(46) : 0;
                return new Rectangle(pad, Height / 2 - Gfx.S(2), Math.Max(10, Width - pad * 2 - textW), Gfx.S(4));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            Gfx.PaintParentBack(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle tr = Track;
            Gfx.FillRound(g, t.Border, tr, 2);
            double f = (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum);
            int kx = tr.X + (int)(f * tr.Width);
            Gfx.FillRound(g, t.Accent, new Rectangle(tr.X, tr.Y, Math.Max(4, kx - tr.X), tr.Height), 2);
            int kd = Gfx.S(18);
            Rectangle knob = new Rectangle(kx - kd / 2, Height / 2 - kd / 2, kd, kd);
            Rectangle ks = knob;
            ks.Offset(0, Gfx.S(1));
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(sh, ks);
            using (SolidBrush kb = new SolidBrush(Color.White)) g.FillEllipse(kb, knob);
            using (Pen kp = new Pen(dragging ? t.Accent : t.Border, Gfx.F(1.5f))) g.DrawEllipse(kp, knob);
            if (Suffix != null)
                Gfx.Text(g, Value + Suffix, Gfx.UI, new Rectangle(Width - Gfx.S(46), 0, Gfx.S(46), Height), t.Sub, TextFormatFlags.Right);
        }

        void SetFromX(int x)
        {
            Rectangle tr = Track;
            double f = (x - tr.X) / (double)tr.Width;
            if (f < 0) f = 0;
            if (f > 1) f = 1;
            int v = Minimum + (int)Math.Round(f * (Maximum - Minimum));
            if (v != Value)
            {
                Value = v;
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Invalidate();
        }
    }

    // 拖拽时跟着鼠标走的卡片(独立的小窗口,所以能在整个面板上自由移动,包括拖到上方的分类标签)
    class DragGhost : Form
    {
        Bitmap img;

        public DragGhost()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000 | 0x8 | 0x20;   // 工具窗口、不激活、置顶、鼠标穿透
                cp.ClassStyle |= 0x20000;                      // 阴影
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        // 换一张图(大小跟着变)并显示在 screenTopLeft;图片由调用方负责释放
        public void ShowCard(Bitmap bmp, Point screenTopLeft)
        {
            img = bmp;
            Bounds = new Rectangle(screenTopLeft, bmp.Size);
            Invalidate();
            if (!Visible) Show();
            // 保证在面板(也是置顶窗口)上面,且不抢焦点
            Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        }

        public void HideGhost()
        {
            if (Visible) Hide();
            img = null;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (img != null) e.Graphics.DrawImageUnscaled(img, 0, 0);
        }
    }

    // 颜色块按钮:点一下弹出取色器
    class ColorChip : Control
    {
        public Color Value = Color.FromArgb(0, 120, 212);

        public ColorChip()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            Gfx.FillRound(g, Value, r, 9);
            Gfx.DrawRound(g, t.Border, r, 9, 1f);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            using (ColorDialog d = new ColorDialog())
            {
                d.FullOpen = true;
                d.Color = Value;
                if (d.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    Value = d.Color;
                    Invalidate();
                }
            }
        }
    }

    // 空格键快速预览(不抢焦点,键盘仍由卡片条处理)
    class QuickLook : Form
    {
        readonly TextBox text = new TextBox();
        readonly PictureBox pic = new PictureBox();
        string title = "", meta = "";
        Color band = Color.Gray;
        string hint = "";
        int lookToken;
        readonly System.Windows.Forms.Timer pop = new System.Windows.Forms.Timer();
        double popStart;
        bool popHiRes;
        int popY;

        public QuickLook()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            text.Multiline = true;
            text.ReadOnly = true;
            text.BorderStyle = BorderStyle.None;
            text.ScrollBars = ScrollBars.Vertical;
            text.WordWrap = true;
            text.MaxLength = int.MaxValue;
            text.Font = Gfx.Input;
            text.TabStop = false;
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            // 不用半透明淡入:半透明窗口里 Windows 的 ClearType 文字会算错,边缘发黑、毛糙
            pop.Interval = 15;
            pop.Tick += delegate
            {
                double p = (Gfx.Ms - popStart) / 180.0;
                if (p >= 1) { p = 1; StopPop(); }
                double e = 1 - Math.Pow(1 - p, 3);
                Top = popY + (int)((1 - e) * Gfx.S(12));   // 只保留轻轻上浮
            };
            Controls.Add(text);
            Controls.Add(pic);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000 | 0x8;   // 工具窗口 + 不激活 + 置顶(不用 TopMost 属性,它会抢焦点)
                cp.ClassStyle |= 0x20000;          // 阴影
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
            Native.NoTransitions(Handle);
        }

        public void ShowFor(Item it, Rectangle shelf, bool beside)
        {
            Theme t = Theme.Cur;
            BackColor = t.Card;
            hint = "空格 关闭预览   ·   Enter 粘贴   ·   ← → 切换";
            Rectangle wa = Screen.FromRectangle(shelf).WorkingArea;
            int w, h, x, y;
            if (beside)
            {
                // 列表模式:放在列表窗口的右边(右边放不下就放左边)
                w = Math.Min(Gfx.S(560), wa.Width / 2);
                h = Math.Min(Gfx.S(470), wa.Height - Gfx.S(24));
                x = shelf.Right + Gfx.S(12);
                if (x + w > wa.Right) x = shelf.Left - Gfx.S(12) - w;
                x = Math.Max(wa.Left, x);
                y = Math.Max(wa.Top, Math.Min(shelf.Y, wa.Bottom - h));
            }
            else
            {
                // 卡片模式:放在面板上方
                w = Math.Min(Gfx.S(700), shelf.Width - Gfx.S(40));
                h = Math.Min(Gfx.S(470), shelf.Y - wa.Top - Gfx.S(28));
                if (h < Gfx.S(200)) h = Gfx.S(200);
                x = shelf.X + (shelf.Width - w) / 2;
                y = Math.Max(wa.Top + Gfx.S(12), shelf.Y - h - Gfx.S(16));
            }
            Bounds = new Rectangle(x, y, w, h);

            band = CardLook.Header(it);
            title = CardLook.Title(it);
            meta = it.TimeAgo() + (string.IsNullOrEmpty(it.AppName) ? "" : "  ·  " + it.AppName) + "  ·  " + CardLook.Footer(it);

            Rectangle content = new Rectangle(Gfx.S(22), Gfx.S(74), w - Gfx.S(44), h - Gfx.S(74) - Gfx.S(42));
            Image old = pic.Image;
            pic.Image = null;
            if (old != null) old.Dispose();
            if (it.IsImage || it.IsImageFile)
            {
                text.Visible = false;
                pic.Visible = true;
                pic.BackColor = t.Card;
                pic.Bounds = content;
                try { pic.Image = Util.LoadBitmap(it.IsImage ? it.FilePath : it.Paths[0]); } catch { }
            }
            else
            {
                pic.Visible = false;
                text.Visible = true;
                text.BackColor = t.Card;
                text.ForeColor = t.Text;
                text.Bounds = content;
                text.Text = Regex.Replace(it.Text ?? "", "\r?\n", "\r\n");
                text.Select(0, 0);
                FitScrollBar();
            }
            Invalidate();
            popY = y;
            if (!Visible)
            {
                // 淡入 + 轻轻上浮
                Top = y + Gfx.S(12);
                Show();
                popStart = Gfx.Ms;
                if (!popHiRes) { popHiRes = true; Gfx.HiRes(true); }
                pop.Start();
            }
        }

        static readonly HashSet<string> TextExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".log", ".csv", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts", ".cs", ".py",
            ".java", ".c", ".cpp", ".h", ".go", ".rs", ".sql", ".ini", ".cfg", ".conf", ".yaml", ".yml", ".bat",
            ".cmd", ".ps1", ".sh", ".srt", ".tsv"
        };
        static readonly HashSet<string> ImageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".ico"
        };

        // 文本文件按 UTF-8 读,读不通就按 GBK(中文 Windows 上老文件常见)
        static string ReadText(string path)
        {
            byte[] data;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int n = (int)Math.Min(fs.Length, 256 * 1024);
                data = new byte[n];
                int read = 0;
                while (read < n)
                {
                    int r = fs.Read(data, read, n - read);
                    if (r <= 0) break;
                    read += r;
                }
            }
            string txt;
            try { txt = new UTF8Encoding(false, true).GetString(data); }
            catch
            {
                try { txt = Encoding.GetEncoding(936).GetString(data); }
                catch { txt = Encoding.Default.GetString(data); }
            }
            return txt.TrimStart('\uFEFF');
        }

        public void ShowFile(string path, Rectangle shelf, bool beside)
        {
            Theme t = Theme.Cur;
            BackColor = t.Card;
            hint = "空格 关闭预览   ·   Enter 打开   ·   方向键 切换";
            Rectangle wa = Screen.FromRectangle(shelf).WorkingArea;
            int w, h, x, y;
            if (beside)
            {
                w = Math.Min(Gfx.S(560), wa.Width / 2);
                h = Math.Min(Gfx.S(470), wa.Height - Gfx.S(24));
                x = shelf.Right + Gfx.S(12);
                if (x + w > wa.Right) x = shelf.Left - Gfx.S(12) - w;
                x = Math.Max(wa.Left, x);
                y = Math.Max(wa.Top, Math.Min(shelf.Y, wa.Bottom - h));
            }
            else
            {
                w = Math.Min(Gfx.S(700), shelf.Width - Gfx.S(40));
                h = Math.Min(Gfx.S(470), shelf.Y - wa.Top - Gfx.S(28));
                if (h < Gfx.S(200)) h = Gfx.S(200);
                x = shelf.X + (shelf.Width - w) / 2;
                y = Math.Max(wa.Top + Gfx.S(12), shelf.Y - h - Gfx.S(16));
            }
            Bounds = new Rectangle(x, y, w, h);

            bool isDir = Directory.Exists(path);
            string ext = isDir ? "" : System.IO.Path.GetExtension(path);
            band = t.Accent;
            title = FolderStore.NameOf(path);
            try
            {
                if (isDir)
                {
                    int n = 0;
                    foreach (string unused in Directory.EnumerateFileSystemEntries(path)) n++;
                    meta = "文件夹  ·  " + n + " 项";
                }
                else
                {
                    FileInfo fi = new FileInfo(path);
                    double kb = fi.Length / 1024.0;
                    string size = kb < 1024 ? kb.ToString("0.#", CultureInfo.InvariantCulture) + " KB"
                                            : (kb / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
                    meta = (ext.Length > 0 ? ext.TrimStart('.').ToUpperInvariant() + " 文件  ·  " : "") + size +
                           "  ·  修改于 " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                }
            }
            catch { meta = ""; }

            Rectangle content = new Rectangle(Gfx.S(22), Gfx.S(74), w - Gfx.S(44), h - Gfx.S(74) - Gfx.S(42));
            Image old = pic.Image;
            pic.Image = null;
            if (old != null) old.Dispose();
            bool shownText = false;
            if (!isDir && TextExts.Contains(ext))
            {
                try
                {
                    string txt = ReadText(path);
                    pic.Visible = false;
                    text.Visible = true;
                    text.BackColor = t.Card;
                    text.ForeColor = t.Text;
                    text.Bounds = content;
                    text.Text = Regex.Replace(txt, "\r?\n", "\r\n");
                    text.Select(0, 0);
                    FitScrollBar();
                    shownText = true;
                }
                catch { }
            }
            if (!shownText)
            {
                text.Visible = false;
                pic.Visible = true;
                pic.BackColor = t.Card;
                pic.Bounds = content;
                pic.Image = null;
                // 预览图在后台线程生成(大照片、视频、PDF 可能要一会儿),界面不会卡住
                int token = ++lookToken;
                Size box = content.Size;
                FileEntry req = new FileEntry();
                req.Path = path;
                req.Want = (!isDir && ImageExts.Contains(ext)) ? Math.Max(box.Width, box.Height) : Gfx.S(256);
                req.Callback = delegate(Bitmap b)
                {
                    try
                    {
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            if (token != lookToken || !Visible || b == null)
                            {
                                if (b != null) b.Dispose();
                                return;
                            }
                            Image prev = pic.Image;
                            pic.SizeMode = (b.Width <= box.Width && b.Height <= box.Height)
                                ? PictureBoxSizeMode.CenterImage : PictureBoxSizeMode.Zoom;
                            pic.Image = b;
                            if (prev != null) prev.Dispose();
                        }));
                    }
                    catch { if (b != null) b.Dispose(); }
                };
                ThumbLoader.Request(req);
            }
            Invalidate();
            popY = y;
            if (!Visible)
            {
                Top = y + Gfx.S(12);
                Show();
                popStart = Gfx.Ms;
                if (!popHiRes) { popHiRes = true; Gfx.HiRes(true); }
                pop.Start();
            }
        }

        // 文字放得下就不显示滚动条;要显示时跟随深浅色(不再是一根白色的系统滚动条)
        void FitScrollBar()
        {
            int h = TextRenderer.MeasureText(text.Text.Length > 20000 ? text.Text.Substring(0, 20000) : text.Text, text.Font,
                new Size(Math.Max(10, text.Width - Gfx.S(22)), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
            ScrollBars want = h > text.Height ? ScrollBars.Vertical : ScrollBars.None;
            if (text.ScrollBars != want) text.ScrollBars = want;
            Native.ThemeScrollBars(text, Theme.Cur.Dark);
        }

        void StopPop()
        {
            pop.Stop();
            if (popHiRes) { popHiRes = false; Gfx.HiRes(false); }
        }

        public void HideLook()
        {
            lookToken++;
            StopPop();
            if (Visible) Hide();
            Image old = pic.Image;
            pic.Image = null;
            if (old != null) old.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.Clear(t.Card);
            Rectangle hr = new Rectangle(0, 0, Width, Gfx.S(58));
            using (LinearGradientBrush lb = new LinearGradientBrush(hr, Theme.Blend(band, Color.White, 0.10),
                Theme.Blend(band, Color.Black, 0.10), LinearGradientMode.Vertical))
                g.FillRectangle(lb, hr);
            Color ink = CardLook.InkOn(band);
            Gfx.SoftText(g, title, Gfx.HeaderFont, new Rectangle(Gfx.S(22), Gfx.S(6), Width - Gfx.S(44), Gfx.S(26)), ink, false);
            Gfx.SoftText(g, meta, Gfx.Small, new Rectangle(Gfx.S(22), Gfx.S(32), Width - Gfx.S(44), Gfx.S(18)),
                Theme.Blend(band, ink, 0.80), false);
            Gfx.Text(g, hint, Gfx.Small,
                new Rectangle(0, Height - Gfx.S(36), Width, Gfx.S(30)), t.Sub, TextFormatFlags.HorizontalCenter);
            using (Pen p = new Pen(t.Border)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // 小输入框:新建/重命名分类、重命名卡片
    class InputDialog : Form
    {
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(Handle, Theme.Cur.Dark);   // 标题栏也跟随深色
        }

        readonly SearchBox box = new SearchBox();
        readonly Swatches colors = new Swatches();

        public InputDialog(string title, string prompt, string initial, bool withColor, int colorIndex)
        {
            Theme t = Theme.Cur;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = t.Bg;
            Font = Gfx.UI;
            try { if (Brand.LargeIcon != null) Icon = Brand.LargeIcon; } catch { }
            ClientSize = new Size(Gfx.S(400), Gfx.S(withColor ? 214 : 164));

            Label l = new Label();
            l.Text = prompt;
            l.AutoSize = true;
            l.ForeColor = t.Text;
            l.BackColor = t.Bg;
            l.Location = new Point(Gfx.S(24), Gfx.S(18));
            Controls.Add(l);

            box.ShowIcon = false;
            box.Box.Font = Gfx.UI;
            box.Box.Text = initial ?? "";
            box.SetBounds(Gfx.S(24), Gfx.S(44), Gfx.S(352), Gfx.S(38));
            Controls.Add(box);

            if (withColor)
            {
                Label cl = new Label();
                cl.Text = "颜色";
                cl.AutoSize = true;
                cl.ForeColor = t.Text;
                cl.BackColor = t.Bg;
                cl.Location = new Point(Gfx.S(24), Gfx.S(102));
                Controls.Add(cl);
                colors.Selected = Math.Max(0, Math.Min(Theme.Accents.Length - 1, colorIndex));
                colors.SetBounds(Gfx.S(84), Gfx.S(94), Gfx.S(292), Gfx.S(34));
                Controls.Add(colors);
            }

            Btn ok = new Btn();
            ok.Text = "确定";
            ok.Primary = true;
            ok.SetBounds(ClientSize.Width - Gfx.S(24 + 90), ClientSize.Height - Gfx.S(54), Gfx.S(90), Gfx.S(36));
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);

            Btn cancel = new Btn();
            cancel.Text = "取消";
            cancel.SetBounds(ClientSize.Width - Gfx.S(24 + 90 + 10 + 90), ClientSize.Height - Gfx.S(54), Gfx.S(90), Gfx.S(36));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
        }

        public string Value { get { return box.Box.Text.Trim(); } }
        public int ColorIndex { get { return colors.Selected; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            box.Box.Focus();
            box.Box.SelectAll();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter) { DialogResult = DialogResult.OK; Close(); return true; }
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // ================================================================ 文件夹(和剪切板历史完全分开)
    // 系统缩略图接口:图片、视频、PDF、文件夹等都能拿到和资源管理器一样的预览
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(ShellSize size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ShellSize
    {
        public int cx, cy;
    }

    static class ShellThumbs
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string pszPath, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, ref DIBSECTION ds);

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPSTRUCT
        {
            public int bmType, bmWidth, bmHeight, bmWidthBytes;
            public short bmPlanes, bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth, biHeight;
            public short biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DIBSECTION
        {
            public BITMAPSTRUCT dsBm;
            public BITMAPINFOHEADER dsBmih;
            public uint dsBitfields0, dsBitfields1, dsBitfields2;
            public IntPtr dshSection;
            public uint dsOffset;
        }

        static readonly HashSet<string> GdiExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".ico"
        };

        // 注意:这个方法可能很慢(视频、网盘文件等),只能在后台线程调用
        public static Bitmap Get(string path, int size)
        {
            // 1) 常见图片:自己解码,最可靠(不依赖系统缩略图,也不会出现黑块)
            if (GdiExts.Contains(System.IO.Path.GetExtension(path)))
            {
                Bitmap own = DecodeImage(path, size);
                if (own != null) return own;
            }
            // 2) 其他类型(视频、PDF、Office、文件夹…):用系统缩略图
            Bitmap shell = ShellImage(path, size);
            if (shell != null) return shell;
            // 3) 最后兜底:文件图标
            try
            {
                if (File.Exists(path))
                    using (Icon ic = Icon.ExtractAssociatedIcon(path))
                        if (ic != null) return ic.ToBitmap();
            }
            catch { }
            return null;
        }

        // 读图片并缩到 size 以内;按照片里的 EXIF 方向转正(手机竖拍的照片不会横着)
        public static Bitmap DecodeImage(string path, int size)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image img = Image.FromStream(fs, false, false))
                {
                    RotateFlipType rf = RotateFlipType.RotateNoneFlipNone;
                    try
                    {
                        if (Array.IndexOf(img.PropertyIdList, 0x0112) >= 0)
                        {
                            int o = img.GetPropertyItem(0x0112).Value[0];
                            switch (o)
                            {
                                case 2: rf = RotateFlipType.RotateNoneFlipX; break;
                                case 3: rf = RotateFlipType.Rotate180FlipNone; break;
                                case 4: rf = RotateFlipType.Rotate180FlipX; break;
                                case 5: rf = RotateFlipType.Rotate90FlipX; break;
                                case 6: rf = RotateFlipType.Rotate90FlipNone; break;
                                case 7: rf = RotateFlipType.Rotate270FlipX; break;
                                case 8: rf = RotateFlipType.Rotate270FlipNone; break;
                            }
                        }
                    }
                    catch { }
                    double s = size > 0 ? Math.Min(1.0, Math.Min(size / (double)img.Width, size / (double)img.Height)) : 1.0;
                    int w = Math.Max(1, (int)(img.Width * s)), h = Math.Max(1, (int)(img.Height * s));
                    Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(img, 0, 0, w, h);
                    }
                    if (rf != RotateFlipType.RotateNoneFlipNone) b.RotateFlip(rf);
                    return b;
                }
            }
            catch { return null; }
        }

        static Bitmap ShellImage(string path, int size)
        {
            IShellItemImageFactory f = null;
            IntPtr hbm = IntPtr.Zero;
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), out f);
                ShellSize sz;
                sz.cx = size;
                sz.cy = size;
                if (f != null && f.GetImage(sz, 0, out hbm) == 0 && hbm != IntPtr.Zero)
                {
                    Bitmap b = FromHBitmap(hbm);
                    if (b != null && LooksBlank(b)) { b.Dispose(); b = null; }   // 拿到全黑的图就当失败
                    return b;
                }
            }
            catch { }
            finally
            {
                if (hbm != IntPtr.Zero) DeleteObject(hbm);
                if (f != null) Marshal.ReleaseComObject(f);
            }
            return null;
        }

        // 直接读 DIB 的像素:自己处理上下方向和透明度
        static Bitmap FromHBitmap(IntPtr h)
        {
            DIBSECTION ds = new DIBSECTION();
            int got = 0;
            try { got = GetObject(h, Marshal.SizeOf(typeof(DIBSECTION)), ref ds); } catch { got = 0; }
            if (got == 0 || ds.dsBm.bmBits == IntPtr.Zero || ds.dsBm.bmBitsPixel != 32)
            {
                try { return Image.FromHbitmap(h); } catch { return null; }
            }
            int w = ds.dsBm.bmWidth, hh = Math.Abs(ds.dsBm.bmHeight);
            int stride = ds.dsBm.bmWidthBytes;
            bool bottomUp = RowsBottomUp(h, ds.dsBm.bmBits, w, hh, stride);
            byte[] buf = new byte[w * 4 * hh];
            for (int y = 0; y < hh; y++)
            {
                int srcRow = bottomUp ? hh - 1 - y : y;
                Marshal.Copy(IntPtr.Add(ds.dsBm.bmBits, srcRow * stride), buf, y * w * 4, w * 4);
            }
            bool anyAlpha = false;
            for (int i = 3; i < buf.Length; i += 4) if (buf[i] != 0) { anyAlpha = true; break; }
            if (!anyAlpha) for (int i = 3; i < buf.Length; i += 4) buf[i] = 255;   // 没有透明度信息:当作不透明
            Bitmap dst = new Bitmap(w, hh, PixelFormat.Format32bppPArgb);
            BitmapData dd = dst.LockBits(new Rectangle(0, 0, w, hh), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < hh; y++)
                    Marshal.Copy(buf, y * w * 4, IntPtr.Add(dd.Scan0, y * dd.Stride), w * 4);
            }
            finally { dst.UnlockBits(dd); }
            return dst;
        }

        // 判断像素是从下往上存还是从上往下存。
        // 上一版只比较第一行和最后一行:文件夹图标上下都是透明的空白,两行一样,判断不出来,结果还是反的。
        // 现在在整张图上取 32×32 个点,分别按"正着读"和"倒着读"去和 Image.FromHbitmap(方向一定对)比较,
        // 只看有颜色的点;实在分不出来时按 Windows 位图的默认存法(从下往上)处理。
        static bool RowsBottomUp(IntPtr h, IntPtr bits, int w, int hh, int stride)
        {
            if (hh < 2) return true;
            try
            {
                using (Bitmap refBmp = Image.FromHbitmap(h))
                {
                    if (refBmp.Width != w || refBmp.Height != hh) return true;
                    byte[] mem = new byte[stride * hh];
                    Marshal.Copy(bits, mem, 0, mem.Length);
                    long dTop = 0, dBottom = 0;
                    int used = 0;
                    int sx = Math.Max(1, w / 32), sy = Math.Max(1, hh / 32);
                    for (int y = 0; y < hh; y += sy)
                    {
                        for (int x = 0; x < w; x += sx)
                        {
                            Color c = refBmp.GetPixel(x, y);
                            if (c.R + c.G + c.B < 30) continue;   // 透明或黑色的点没有区分度,跳过
                            used++;
                            int oT = y * stride + x * 4;              // 当作从上往下存
                            int oB = (hh - 1 - y) * stride + x * 4;   // 当作从下往上存
                            dTop += Math.Abs(mem[oT] - c.B) + Math.Abs(mem[oT + 1] - c.G) + Math.Abs(mem[oT + 2] - c.R);
                            dBottom += Math.Abs(mem[oB] - c.B) + Math.Abs(mem[oB + 1] - c.G) + Math.Abs(mem[oB + 2] - c.R);
                        }
                    }
                    if (used == 0 || dTop == dBottom) return true;
                    return dBottom < dTop;
                }
            }
            catch { return true; }
        }

        // 抽样检查:不透明的像素全是黑色 → 视为无效
        static bool LooksBlank(Bitmap b)
        {
            try
            {
                int opaque = 0, dark = 0;
                int stepX = Math.Max(1, b.Width / 16), stepY = Math.Max(1, b.Height / 16);
                for (int y = 0; y < b.Height; y += stepY)
                    for (int x = 0; x < b.Width; x += stepX)
                    {
                        Color c = b.GetPixel(x, y);
                        if (c.A < 40) continue;
                        opaque++;
                        if (c.R < 12 && c.G < 12 && c.B < 12) dark++;
                    }
                return opaque > 0 && dark == opaque;
            }
            catch { return false; }
        }
    }

    // 缩略图缓存:内存里留最近 800 张(切换文件夹再回来是瞬间的),
    // 硬盘上存一份(%LOCALAPPDATA%\Glean\thumbs),重启软件后也不用重新生成。
    // 文件的大小或修改时间变了,缓存自动失效。
    static class ThumbCache
    {
        static readonly object gate = new object();
        static readonly Dictionary<string, Bitmap> mem = new Dictionary<string, Bitmap>();
        static readonly Queue<string> order = new Queue<string>();
        static readonly Dictionary<string, long> pixels = new Dictionary<string, long>();
        static long totalPixels;
        static long Budget { get { long u = Gfx.S(96); return 800L * u * u; } }   // 约等于 800 张普通缩略图
        static string dir;

        public static string Key(string path, long size, DateTime modified, int want)
        {
            return path.ToLowerInvariant() + "|" + size + "|" + modified.Ticks + "|" + want;
        }

        static string Dir
        {
            get
            {
                if (dir == null)
                {
                    dir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glean", "thumbs");
                    try { Directory.CreateDirectory(dir); } catch { }
                }
                return dir;
            }
        }

        static string FileFor(string key)
        {
            return System.IO.Path.Combine(Dir, Util.Md5Short(Encoding.UTF8.GetBytes(key)) + ".png");
        }

        public static Bitmap GetMem(string key)
        {
            lock (gate)
            {
                Bitmap b;
                return mem.TryGetValue(key, out b) ? b : null;
            }
        }

        // 超出数量时只是丢掉引用,不主动释放(界面可能还在画它),交给系统回收
        public static void PutMem(string key, Bitmap b)
        {
            lock (gate)
            {
                if (mem.ContainsKey(key)) return;
                long px = 1;
                try { px = (long)b.Width * b.Height; } catch { }
                mem[key] = b;
                pixels[key] = px;
                totalPixels += px;
                order.Enqueue(key);
                while (totalPixels > Budget && order.Count > 1)
                {
                    string old = order.Dequeue();
                    long op;
                    if (pixels.TryGetValue(old, out op)) { totalPixels -= op; pixels.Remove(old); }
                    mem.Remove(old);
                }
            }
        }

        public static Bitmap LoadDisk(string key)
        {
            try
            {
                string f = FileFor(key);
                if (!File.Exists(f)) return null;
                return Util.LoadBitmap(f);
            }
            catch { return null; }
        }

        public static void SaveDisk(string key, Bitmap b)
        {
            try { b.Save(FileFor(key), ImageFormat.Png); } catch { }
        }

        // 硬盘缓存超过 6000 张时,删掉最旧的,留 4000 张
        public static void Trim()
        {
            try
            {
                FileInfo[] files = new DirectoryInfo(Dir).GetFiles("*.png");
                if (files.Length <= 6000) return;
                foreach (FileInfo f in files.OrderBy(x => x.LastWriteTimeUtc).Take(files.Length - 4000))
                {
                    try { f.Delete(); } catch { }
                }
            }
            catch { }
        }

        public static void ClearDisk()
        {
            try
            {
                foreach (FileInfo f in new DirectoryInfo(Dir).GetFiles("*.png"))
                {
                    try { f.Delete(); } catch { }
                }
            }
            catch { }
            lock (gate) { mem.Clear(); order.Clear(); }
        }
    }

    class FileEntry
    {
        public string Path, Name;
        public bool IsDir;
        public long Size;
        public DateTime Modified;
        public Bitmap Thumb;
        public bool Requested;
        public volatile bool Done;
        public int Gen;
        public Bitmap Fit;        // 按格子大小缩好的图(画的时候直接贴,不用每次缩放)
        public Bitmap FitSrc;
        public int FitBox;
        public int Want;                       // 想要的尺寸(0 = 列表缩略图大小)
        public Action<Bitmap> Callback;        // 设置了就把结果交给它(快速预览用),不受切换文件夹影响
    }

    // 缩略图在后台(STA 线程)生成,不卡界面;切换文件夹后旧的请求自动作废
    static class ThumbLoader
    {
        public static int Gen;
        static readonly System.Collections.Concurrent.BlockingCollection<FileEntry> queue =
            new System.Collections.Concurrent.BlockingCollection<FileEntry>();
        static Thread worker;
        public static event Action Ready;

        public static void NewGeneration() { Interlocked.Increment(ref Gen); }

        public static void Request(FileEntry e)
        {
            e.Requested = true;
            e.Gen = Thread.VolatileRead(ref Gen);
            if (worker == null)
            {
                worker = new Thread(Loop);
                worker.IsBackground = true;
                worker.SetApartmentState(ApartmentState.STA);
                worker.Priority = ThreadPriority.BelowNormal;
                worker.Start();
                ThreadPool.QueueUserWorkItem(delegate { ThumbCache.Trim(); });
            }
            queue.Add(e);
        }

        static void Loop()
        {
            foreach (FileEntry e in queue.GetConsumingEnumerable())
            {
                if (e.Callback == null && e.Gen != Thread.VolatileRead(ref Gen)) continue;
                Bitmap b = null;
                if (e.Callback != null)
                {
                    // 快速预览的大图:不进缓存
                    try { b = ShellThumbs.Get(e.Path, e.Want > 0 ? e.Want : Gfx.S(96)); } catch { b = null; }
                    try { e.Callback(b); } catch { }
                    continue;
                }
                // 先查内存缓存,再查硬盘缓存,都没有才真正去生成
                int want = e.Want > 0 ? e.Want : Gfx.S(96);
                string key = ThumbCache.Key(e.Path, e.Size, e.Modified, want);
                b = ThumbCache.GetMem(key);
                if (b == null)
                {
                    b = ThumbCache.LoadDisk(key);
                    if (b == null)
                    {
                        try { b = ShellThumbs.Get(e.Path, want); } catch { b = null; }
                        if (b != null) ThumbCache.SaveDisk(key, b);   // 先存盘,再交给界面去画(避免两个线程同时用一张图)
                    }
                    if (b != null) ThumbCache.PutMem(key, b);
                }
                if (e.Gen != Thread.VolatileRead(ref Gen)) continue;   // 已经换了文件夹:结果留在缓存里,下次直接用
                e.Thumb = b;
                e.Done = true;
                Action h = Ready;
                if (h != null)
                {
                    try { h(); } catch { }
                }
            }
        }
    }

    // 常用文件夹:folders.txt,每行"显示方式 \t 路径"
    static class FolderStore
    {
        public class Folder
        {
            public string Path;
            public int View;   // 0 中图  1 列表  2 小图  3 大图
        }

        public static List<Folder> Items = new List<Folder>();

        static string StoreFile
        {
            get { return System.IO.Path.Combine(Config.DefaultDir, "folders.txt"); }
        }

        public static void Load()
        {
            Items.Clear();
            try
            {
                if (!File.Exists(StoreFile)) return;
                foreach (string line in File.ReadAllLines(StoreFile, Encoding.UTF8))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    Folder f = new Folder();
                    int vw;
                    f.View = int.TryParse(line.Substring(0, tab), out vw) && vw >= 0 && vw <= 3 ? vw : 0;
                    f.Path = line.Substring(tab + 1).Trim();
                    if (f.Path.Length > 0) Items.Add(f);
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Config.DefaultDir);
                File.WriteAllLines(StoreFile, Items.Select(f => f.View + "\t" + f.Path).ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        public static string NameOf(string p)
        {
            string t = (p ?? "").TrimEnd('\\', '/');
            string n = System.IO.Path.GetFileName(t);
            return n.Length > 0 ? n : t;
        }

        public static int Add(string path)
        {
            string full = path;
            try { full = System.IO.Path.GetFullPath(path); } catch { }
            for (int i = 0; i < Items.Count; i++)
                if (Util.SamePath(Items[i].Path, full)) return i;
            Folder f = new Folder();
            f.Path = full;
            Items.Add(f);
            Save();
            return Items.Count - 1;
        }

        public static void Remove(int i)
        {
            if (i < 0 || i >= Items.Count) return;
            Items.RemoveAt(i);
            Save();
        }
    }

    // 文件夹浏览:顶部是路径栏(可点击返回上级),下面是网格或列表
    class FileView : Control
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int StrCmpLogicalW(string a, string b);

        static readonly HashSet<string> PhotoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".heic", ".tif", ".tiff",
            ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".psd"
        };

        public string Root, Current;
        public int ViewMode;   // 0 中图  1 列表  2 小图  3 大图
        List<FileEntry> all = new List<FileEntry>();
        List<FileEntry> shown = new List<FileEntry>();
        readonly HashSet<FileEntry> sel = new HashSet<FileEntry>();
        int anchor = -1, focusIdx = -1, hover = -1, scrollY;
        string filter = "", error;
        FileSystemWatcher watcher;
        readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
        Point downPt;
        int downIdx = -1;
        bool dragArmed;
        readonly List<KeyValuePair<Rectangle, string>> crumbs = new List<KeyValuePair<Rectangle, string>>();
        Rectangle backRect;
        readonly Rectangle[] viewBtns = new Rectangle[4];   // 小 中 大 列表
        static readonly int[] ViewOrder = new int[] { 1, 2, 0, 3 };   // Ctrl+滚轮:列表 → 小 → 中 → 大

        public event Action<string> FileOpened;
        public event Action SelectionChanged;
        public event Action<List<string>, Point> ContextRequested;
        public event Action DragBegin, DragEnd;
        public event Action<int> ViewModeChanged;

        public FileView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
            ImeMode = ImeMode.Disable;
            refreshTimer.Interval = 400;
            refreshTimer.Tick += delegate { refreshTimer.Stop(); if (Current != null) Load(true); };
            ThumbLoader.Ready += delegate
            {
                try
                {
                    if (IsHandleCreated && !IsDisposed && Visible) BeginInvoke(new MethodInvoker(Invalidate));
                }
                catch { }
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && watcher != null) { watcher.Dispose(); watcher = null; }
            base.Dispose(disposing);
        }

        // ---------------- 尺寸
        static int BarH { get { return Gfx.S(38); } }
        static int Pad { get { return Gfx.S(14); } }
        bool Grid { get { return ViewMode != 1; } }
        int TileW { get { return Gfx.S(ViewMode == 2 ? 84 : ViewMode == 3 ? 168 : 112); } }
        int TileH { get { return Gfx.S(ViewMode == 2 ? 100 : ViewMode == 3 ? 190 : 124); } }
        int ThumbBox { get { return Gfx.S(ViewMode == 2 ? 52 : ViewMode == 3 ? 136 : 76); } }
        int ThumbWant { get { return Gfx.S(ViewMode == 3 ? 176 : 96); } }   // 大图用更清晰的缩略图
        static int Gap { get { return Gfx.S(8); } }
        static int RowH { get { return Gfx.S(38); } }
        int Top0 { get { return BarH + Gfx.S(6); } }

        int Cols
        {
            get { return Grid ? Math.Max(1, (Width - Pad * 2 + Gap) / (TileW + Gap)) : 1; }
        }

        int TotalH
        {
            get
            {
                if (shown.Count == 0) return Height;
                int rows = Grid ? (shown.Count + Cols - 1) / Cols : shown.Count;
                int rh = Grid ? TileH + Gap : RowH;
                return Top0 + rows * rh + Gfx.S(8);
            }
        }

        int MaxScroll { get { return Math.Max(0, TotalH - Height); } }

        // 内容坐标(未减去滚动)
        Rectangle ItemRect(int i)
        {
            if (Grid)
            {
                int c = Cols;
                return new Rectangle(Pad + (i % c) * (TileW + Gap), Top0 + (i / c) * (TileH + Gap), TileW, TileH);
            }
            return new Rectangle(Pad - Gfx.S(4), Top0 + i * RowH, Width - Pad * 2 - Gfx.S(2), RowH - Gfx.S(2));
        }

        int HitTest(Point p)
        {
            if (p.Y < BarH) return -1;
            int y = p.Y + scrollY;
            int i;
            if (Grid)
            {
                int col = (p.X - Pad) / (TileW + Gap);
                int row = (y - Top0) / (TileH + Gap);
                if (p.X < Pad || y < Top0 || col >= Cols) return -1;
                i = row * Cols + col;
            }
            else
            {
                if (y < Top0) return -1;
                i = (y - Top0) / RowH;
            }
            if (i < 0 || i >= shown.Count) return -1;
            return ItemRect(i).Contains(p.X, y) ? i : -1;
        }

        // ---------------- 数据
        public void SetRoot(string root, int view)
        {
            Root = root;
            ViewMode = view;
            Navigate(root);
        }

        public void Navigate(string path)
        {
            if (watcher != null) { watcher.Dispose(); watcher = null; }
            Current = path;
            scrollY = 0;
            if (path == null)
            {
                ThumbLoader.NewGeneration();
                all = new List<FileEntry>();
                shown = new List<FileEntry>();
                sel.Clear();
                error = null;
                Invalidate();
                return;
            }
            Load(false);
            try
            {
                watcher = new FileSystemWatcher(path);
                watcher.IncludeSubdirectories = false;
                watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite;
                watcher.SynchronizingObject = this;
                FileSystemEventHandler h = delegate { refreshTimer.Stop(); refreshTimer.Start(); };
                watcher.Created += h;
                watcher.Deleted += h;
                watcher.Changed += h;
                watcher.Renamed += delegate { refreshTimer.Stop(); refreshTimer.Start(); };
                watcher.EnableRaisingEvents = true;
            }
            catch { watcher = null; }
        }

        public void Reload() { if (Current != null) Load(true); }

        void Load(bool keepState)
        {
            List<string> keepSel = keepState ? sel.Select(x => x.Path).ToList() : null;
            int keepScroll = scrollY;
            ThumbLoader.NewGeneration();
            Dictionary<string, FileEntry> old = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (FileEntry o in all) old[o.Path] = o;
            List<FileEntry> fresh = new List<FileEntry>();
            error = null;
            try
            {
                DirectoryInfo di = new DirectoryInfo(Current);
                if (!di.Exists) error = "文件夹不存在,可能已被移动或删除";
                else
                {
                    foreach (FileSystemInfo fi in di.EnumerateFileSystemInfos())
                    {
                        try
                        {
                            if ((fi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                            bool isDir = (fi.Attributes & FileAttributes.Directory) != 0;
                            long size = isDir ? 0 : ((FileInfo)fi).Length;
                            DateTime mod = fi.LastWriteTime;
                            FileEntry e;
                            if (keepState && old.TryGetValue(fi.FullName, out e) && e.IsDir == isDir && e.Size == size && e.Modified == mod)
                            {
                                old.Remove(fi.FullName);
                                if (!e.Done || e.Want != ThumbWant) e.Requested = false;   // 之前没生成完的(或大小不对),按新一轮重新排队
                            }
                            else
                            {
                                e = new FileEntry();
                                e.Path = fi.FullName;
                                e.Name = fi.Name;
                                e.IsDir = isDir;
                                e.Size = size;
                                e.Modified = mod;
                                // 内存里已经有这张缩略图:直接用,一点都不用等
                                Bitmap hit = ThumbCache.GetMem(ThumbCache.Key(e.Path, size, mod, ThumbWant));
                                if (hit != null)
                                {
                                    e.Want = ThumbWant;
                                    e.Thumb = hit;
                                    e.Done = true;
                                    e.Requested = true;
                                }
                            }
                            fresh.Add(e);
                        }
                        catch { }
                    }
                }
            }
            catch (UnauthorizedAccessException) { error = "没有权限访问这个文件夹"; }
            catch (Exception ex) { error = "无法读取这个文件夹:" + ex.Message; }

            // 旧条目的缩略图不再释放:它们在缓存里,回到这个文件夹时还能直接用

            fresh.Sort(delegate(FileEntry a, FileEntry b)
            {
                if (a.IsDir != b.IsDir) return a.IsDir ? -1 : 1;   // 文件夹在前
                return StrCmpLogicalW(a.Name, b.Name);            // 自然排序:图2 在 图10 前面
            });
            all = fresh;
            sel.Clear();
            if (keepSel != null)
            {
                HashSet<string> ks = new HashSet<string>(keepSel, StringComparer.OrdinalIgnoreCase);
                foreach (FileEntry e in all) if (ks.Contains(e.Path)) sel.Add(e);
            }
            ApplyFilter(!keepState);
            scrollY = keepState ? Math.Min(keepScroll, MaxScroll) : 0;
            Invalidate();
        }

        public void Filter(string q)
        {
            filter = q ?? "";
            sel.Clear();
            ApplyFilter(true);
            scrollY = 0;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged();
        }

        void ApplyFilter(bool selectFirst)
        {
            shown = filter.Length == 0 ? new List<FileEntry>(all)
                : all.Where(e => e.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            HashSet<FileEntry> vis = new HashSet<FileEntry>(shown);
            sel.RemoveWhere(e => !vis.Contains(e));
            if (sel.Count == 0 && selectFirst && shown.Count > 0) sel.Add(shown[0]);
            focusIdx = -1;
            for (int i = 0; i < shown.Count; i++) if (sel.Contains(shown[i])) { focusIdx = i; break; }
            anchor = focusIdx;
            hover = -1;
        }

        public FileEntry FocusedEntry
        {
            get { return (focusIdx >= 0 && focusIdx < shown.Count) ? shown[focusIdx] : null; }
        }

        public List<string> SelectedPaths()
        {
            return shown.Where(e => sel.Contains(e)).Select(e => e.Path).ToList();
        }

        // ---------------- 键盘操作(由面板调用)
        public void MoveSel(int dx, int dy)
        {
            if (shown.Count == 0) return;
            int i = focusIdx < 0 ? 0 : focusIdx;
            if (Grid) i += dx + dy * Cols;
            else i += dy != 0 ? dy : dx;
            SelectOnly(Math.Max(0, Math.Min(shown.Count - 1, i)));
        }

        void SelectOnly(int i)
        {
            sel.Clear();
            sel.Add(shown[i]);
            focusIdx = anchor = i;
            EnsureVisible(i);
            Invalidate();
            if (SelectionChanged != null) SelectionChanged();
        }

        public void SelectAll()
        {
            foreach (FileEntry e in shown) sel.Add(e);
            Invalidate();
        }

        public void OpenSelected()
        {
            if (focusIdx >= 0) Activate(focusIdx);
        }

        void Activate(int i)
        {
            if (i < 0 || i >= shown.Count) return;
            FileEntry e = shown[i];
            if (e.IsDir) Navigate(e.Path);
            else if (FileOpened != null) FileOpened(e.Path);
        }

        public void GoUp()
        {
            if (Current == null || Root == null || Util.SamePath(Current, Root)) return;
            string child = Current;
            DirectoryInfo parent = null;
            try { parent = Directory.GetParent(Current.TrimEnd('\\', '/')); } catch { }
            if (parent == null) return;
            Navigate(parent.FullName);
            for (int i = 0; i < shown.Count; i++)
                if (Util.SamePath(shown[i].Path, child)) { SelectOnly(i); break; }
        }

        void EnsureVisible(int i)
        {
            Rectangle r = ItemRect(i);
            if (r.Y - Gfx.S(6) < scrollY + BarH) scrollY = r.Y - Gfx.S(6) - BarH;
            else if (r.Bottom + Gfx.S(8) > scrollY + Height) scrollY = r.Bottom + Gfx.S(8) - Height;
            scrollY = Math.Max(0, Math.Min(MaxScroll, scrollY));
        }

        void SetView(int m)
        {
            if (m == ViewMode) return;
            int oldWant = ThumbWant;
            ViewMode = m;
            if (ThumbWant != oldWant) ResetThumbs();
            scrollY = 0;
            if (focusIdx >= 0) EnsureVisible(focusIdx);
            Invalidate();
            if (ViewModeChanged != null) ViewModeChanged(m);
        }

        // 换了缩略图清晰度:旧图先留着当占位(不会变黑),新图生成好了再替换
        void ResetThumbs()
        {
            ThumbLoader.NewGeneration();
            foreach (FileEntry fe in all)
            {
                Bitmap hit = ThumbCache.GetMem(ThumbCache.Key(fe.Path, fe.Size, fe.Modified, ThumbWant));
                if (hit != null) { fe.Thumb = hit; fe.Done = true; fe.Requested = true; fe.Want = ThumbWant; }
                else fe.Requested = false;
            }
        }

        // Ctrl+滚轮:列表 / 小图 / 中图 / 大图 之间切换
        public void StepView(int dir)
        {
            int k = Array.IndexOf(ViewOrder, ViewMode);
            if (k < 0) k = 2;
            k = Math.Max(0, Math.Min(ViewOrder.Length - 1, k + dir));
            SetView(ViewOrder[k]);
        }

        // ---------------- 鼠标
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) != 0) { StepView(e.Delta > 0 ? 1 : -1); return; }
            scrollY = Math.Max(0, Math.Min(MaxScroll, scrollY - e.Delta / 120 * Gfx.S(60)));
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (Root == null) return;
            if (e.Y < BarH)
            {
                if (e.Button != MouseButtons.Left) return;
                if (backRect.Contains(e.Location)) { GoUp(); return; }
                for (int vb = 0; vb < 4; vb++)
                    if (viewBtns[vb].Contains(e.Location)) { SetView(new int[] { 2, 0, 3, 1 }[vb]); return; }
                foreach (KeyValuePair<Rectangle, string> c in crumbs)
                    if (c.Key.Contains(e.Location)) { Navigate(c.Value); return; }
                return;
            }
            int i = HitTest(e.Location);
            Keys mod = Control.ModifierKeys;
            if (i < 0)
            {
                if (e.Button == MouseButtons.Left && mod == Keys.None)
                {
                    sel.Clear();
                    focusIdx = -1;
                    Invalidate();
                    if (SelectionChanged != null) SelectionChanged();
                }
                return;
            }
            FileEntry fe = shown[i];
            if ((mod & Keys.Control) != 0 && e.Button == MouseButtons.Left)
            {
                if (!sel.Remove(fe)) sel.Add(fe);
                anchor = i;
            }
            else if ((mod & Keys.Shift) != 0 && e.Button == MouseButtons.Left && anchor >= 0)
            {
                sel.Clear();
                for (int k = Math.Min(anchor, i); k <= Math.Max(anchor, i); k++) sel.Add(shown[k]);
            }
            else if (!sel.Contains(fe))
            {
                sel.Clear();
                sel.Add(fe);
                anchor = i;
            }
            focusIdx = i;
            downIdx = i;
            downPt = e.Location;
            dragArmed = e.Button == MouseButtons.Left;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged();
            if (e.Button == MouseButtons.Right && ContextRequested != null)
                ContextRequested(SelectedPaths(), PointToScreen(e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragArmed && Control.ModifierKeys == Keys.None && sel.Count > 1 && HitTest(e.Location) == downIdx && downIdx >= 0)
            {
                // 在多选里单击某一个(没有拖动):只留下它
                sel.Clear();
                sel.Add(shown[downIdx]);
                Invalidate();
                if (SelectionChanged != null) SelectionChanged();
            }
            dragArmed = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragArmed && (e.Button & MouseButtons.Left) != 0)
            {
                Size ds = SystemInformation.DragSize;
                if (Math.Abs(e.X - downPt.X) > ds.Width || Math.Abs(e.Y - downPt.Y) > ds.Height)
                {
                    dragArmed = false;
                    StartDrag();
                    return;
                }
            }
            bool hand = false;
            if (e.Y < BarH)
            {
                hand = backRect.Contains(e.Location) || viewBtns.Any(vr => vr.Contains(e.Location));
                foreach (KeyValuePair<Rectangle, string> c in crumbs) if (c.Key.Contains(e.Location)) hand = true;
            }
            Cursor = hand ? Cursors.Hand : Cursors.Default;
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            int i = HitTest(e.Location);
            if (i >= 0) Activate(i);
        }

        // 把选中的文件拖到别的程序里(聊天窗口、网页上传框、PS、资源管理器…)
        void StartDrag()
        {
            StringCollection sc = new StringCollection();
            foreach (string p in SelectedPaths())
                if (File.Exists(p) || Directory.Exists(p)) sc.Add(p);
            if (sc.Count == 0) return;
            DataObject d = new DataObject();
            d.SetFileDropList(sc);
            if (DragBegin != null) DragBegin();
            try { DoDragDrop(d, DragDropEffects.Copy | DragDropEffects.Link); }
            catch { }
            if (DragEnd != null) DragEnd();
        }

        // ---------------- 绘制
        static string FormatSize(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            if (b < 1024L * 1024 * 1024) return (b / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
            return (b / 1073741824.0).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
        }

        static void DrawFolderGlyph(Graphics g, Rectangle r)
        {
            int w = r.Width, h = (int)(r.Width * 0.78);
            int x = r.X, y = r.Y + (r.Height - h) / 2;
            Gfx.FillRound(g, Color.FromArgb(232, 172, 52), new Rectangle(x, y, (int)(w * 0.45), (int)(h * 0.3)), 3);
            Gfx.FillRound(g, Color.FromArgb(248, 200, 82), new Rectangle(x, y + (int)(h * 0.14), w, h - (int)(h * 0.14)), 4);
        }

        void DrawThumb(Graphics g, FileEntry e, Rectangle box)
        {
            if (!e.Requested) { e.Want = ThumbWant; ThumbLoader.Request(e); }
            Bitmap b = e.Thumb;
            if (b == null)
            {
                if (e.IsDir) DrawFolderGlyph(g, box);
                else
                {
                    Rectangle ph = new Rectangle(box.X + box.Width / 5, box.Y + box.Height / 10, box.Width * 3 / 5, box.Height * 4 / 5);
                    Gfx.FillRound(g, Color.FromArgb(Theme.Cur.Dark ? 24 : 18, Theme.Cur.Text), ph, 6);
                }
                return;
            }
            if (e.Fit == null || e.FitSrc != b || e.FitBox != box.Width)
            {
                double s = Math.Min(box.Width / (double)b.Width, box.Height / (double)b.Height);
                int fw = Math.Max(1, (int)(b.Width * s)), fhh = Math.Max(1, (int)(b.Height * s));
                Bitmap fit = new Bitmap(fw, fhh, PixelFormat.Format32bppPArgb);
                using (Graphics fg = Graphics.FromImage(fit))
                {
                    fg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    fg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    fg.DrawImage(b, 0, 0, fw, fhh);
                }
                if (e.Fit != null) e.Fit.Dispose();
                e.Fit = fit;
                e.FitSrc = b;
                e.FitBox = box.Width;
            }
            Bitmap f = e.Fit;
            Rectangle r = new Rectangle(box.X + (box.Width - f.Width) / 2, box.Y + (box.Height - f.Height) / 2, f.Width, f.Height);
            g.DrawImageUnscaled(f, r.X, r.Y);
            if (!e.IsDir && PhotoExts.Contains(System.IO.Path.GetExtension(e.Name)) && box.Width > Gfx.S(30))
                using (Pen p = new Pen(Color.FromArgb(40, 0, 0, 0))) g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            Theme t = Theme.Cur;
            Gfx.PaintParentBack(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            crumbs.Clear();
            backRect = Rectangle.Empty;
            for (int vb = 0; vb < 4; vb++) viewBtns[vb] = Rectangle.Empty;

            if (Root == null)
            {
                // 还没有添加任何文件夹:显示拖放区域
                Rectangle zone = new Rectangle(Gfx.S(16), Gfx.S(10), Width - Gfx.S(32), Height - Gfx.S(20));
                using (Pen dp = new Pen(Color.FromArgb(t.Dark ? 90 : 110, t.Sub), Gfx.F(1.5f)))
                {
                    dp.DashStyle = DashStyle.Dash;
                    using (GraphicsPath zp = Gfx.Round(zone, 14)) g.DrawPath(dp, zp);
                }
                int gs = Gfx.S(40);
                DrawFolderGlyph(g, new Rectangle(zone.X + (zone.Width - gs) / 2, zone.Y + zone.Height / 2 - gs - Gfx.S(6), gs, gs));
                TextRenderer.DrawText(g, "把文件夹拖到这里\n或点击上方的 ＋ 添加常用文件夹", Gfx.UI,
                    new Rectangle(zone.X, zone.Y + zone.Height / 2 + Gfx.S(4), zone.Width, Gfx.S(48)), t.Sub,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                return;
            }

            DrawBar(g, t);

            Rectangle content = new Rectangle(0, BarH, Width, Height - BarH);
            if (error != null || shown.Count == 0)
            {
                string msg = error ?? (filter.Length > 0 ? "没有匹配的文件" : "这个文件夹是空的");
                Gfx.Text(g, msg, Gfx.UI, content, t.Sub, TextFormatFlags.HorizontalCenter);
                return;
            }

            g.SetClip(content);
            int first = 0;
            if (Grid) first = Math.Max(0, (scrollY - Top0) / (TileH + Gap)) * Cols;
            else first = Math.Max(0, (scrollY - Top0) / RowH);
            for (int i = first; i < shown.Count; i++)
            {
                Rectangle r = ItemRect(i);
                r.Offset(0, -scrollY);
                if (r.Y > Height) break;
                if (r.Bottom < BarH) continue;
                if (Grid) DrawTile(g, i, r, t);
                else DrawRowItem(g, i, r, t);
            }
            g.ResetClip();

            if (TotalH > Height)
            {
                int track = Height - BarH;
                int th = Math.Max(Gfx.S(30), track * track / Math.Max(1, TotalH - BarH));
                int ty = BarH + (int)((double)scrollY / Math.Max(1, MaxScroll) * (track - th));
                Gfx.FillRound(g, Color.FromArgb(t.Dark ? 90 : 70, t.Sub), new Rectangle(Width - Gfx.S(6), ty, Gfx.S(3), th), 2);
            }
        }

        void DrawBar(Graphics g, Theme t)
        {
            int cy = BarH / 2;
            int x = Pad;
            bool atRoot = Util.SamePath(Current, Root);

            // 右侧:项目数 + 小图 / 中图 / 大图 / 列表 切换(一组小按钮)
            int bs = Gfx.S(26);
            int[] modes = new int[] { 2, 0, 3, 1 };
            Rectangle group = new Rectangle(Width - Pad - bs * 4 - Gfx.S(4), cy - bs / 2 - Gfx.S(2), bs * 4 + Gfx.S(4), bs + Gfx.S(4));
            Gfx.FillRound(g, Color.FromArgb(t.Dark ? 26 : 14, t.Text), group, 9);
            for (int vb = 0; vb < 4; vb++)
            {
                Rectangle br = new Rectangle(group.X + Gfx.S(2) + vb * bs, group.Y + Gfx.S(2), bs, bs);
                viewBtns[vb] = br;
                bool on = ViewMode == modes[vb];
                if (on) Gfx.FillRound(g, Color.FromArgb(t.Dark ? 60 : 255, t.Dark ? t.Text : Color.White), br, 7);
                Color ic = on ? t.Text : t.Sub;
                using (SolidBrush ib = new SolidBrush(ic))
                {
                    int ccx = br.X + bs / 2, ccy = br.Y + bs / 2;
                    if (modes[vb] == 1)
                    {
                        using (Pen lp = new Pen(ic, Gfx.F(1.6f)))
                        {
                            int lw = Gfx.S(12);
                            for (int k = -1; k <= 1; k++) g.DrawLine(lp, ccx - lw / 2, ccy + k * Gfx.S(4), ccx + lw / 2, ccy + k * Gfx.S(4));
                        }
                    }
                    else
                    {
                        // 小图 3×3,中图 2×2,大图 1 块
                        int n = modes[vb] == 2 ? 3 : modes[vb] == 0 ? 2 : 1;
                        int total = Gfx.S(13), gp = n == 1 ? 0 : Gfx.S(2);
                        int q = (total - gp * (n - 1)) / n;
                        int sx = ccx - (q * n + gp * (n - 1)) / 2, sy = ccy - (q * n + gp * (n - 1)) / 2;
                        for (int r0 = 0; r0 < n; r0++)
                            for (int c0 = 0; c0 < n; c0++)
                            {
                                Rectangle cell = new Rectangle(sx + c0 * (q + gp), sy + r0 * (q + gp), q, q);
                                if (n == 1) Gfx.FillRound(g, ic, cell, 2); else g.FillRectangle(ib, cell);
                            }
                    }
                }
            }
            string count = shown.Count + " 项";
            Size cs = TextRenderer.MeasureText(count, Gfx.Small);
            int limit = group.X - cs.Width - Gfx.S(18);
            Gfx.Text(g, count, Gfx.Small, new Rectangle(group.X - cs.Width - Gfx.S(10), 0, cs.Width, BarH), t.Sub, TextFormatFlags.Left);

            // 左侧:返回 + 路径
            if (!atRoot)
            {
                backRect = new Rectangle(x - Gfx.S(4), cy - Gfx.S(13), Gfx.S(26), Gfx.S(26));
                Gfx.FillRound(g, Color.FromArgb(t.Dark ? 40 : 22, t.Text), backRect, 7);
                using (Pen bp = new Pen(t.Text, Gfx.F(1.8f)))
                {
                    bp.StartCap = bp.EndCap = LineCap.Round;
                    int bx = backRect.X + Gfx.S(15);
                    g.DrawLine(bp, bx, cy - Gfx.S(5), bx - Gfx.S(5), cy);
                    g.DrawLine(bp, bx - Gfx.S(5), cy, bx, cy + Gfx.S(5));
                }
                x = backRect.Right + Gfx.S(10);
            }

            List<KeyValuePair<string, string>> segs = new List<KeyValuePair<string, string>>();
            if (atRoot)
            {
                // 在常用文件夹的最上层:名字已经在标签上了,这里不重复,只留一条细线
                using (Pen sp0 = new Pen(Color.FromArgb(t.Dark ? 36 : 26, t.Text)))
                    g.DrawLine(sp0, Pad, BarH - 1, Width - Pad, BarH - 1);
                return;
            }
            segs.Add(new KeyValuePair<string, string>(FolderStore.NameOf(Root), Root));
            if (!atRoot && Current.Length > Root.Length)
            {
                string acc = Root.TrimEnd('\\', '/');
                foreach (string part in Current.Substring(Root.TrimEnd('\\', '/').Length).Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    acc = acc + "\\" + part;
                    segs.Add(new KeyValuePair<string, string>(part, acc));
                }
            }
            TextFormatFlags nf = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            int sepW = Gfx.S(18);
            // 太长时只保留最后几级,前面用"…"
            int startSeg = 0;
            int need = 0;
            for (int i = segs.Count - 1; i >= 0; i--)
            {
                int w = TextRenderer.MeasureText(segs[i].Key, i == segs.Count - 1 ? Gfx.Bold : Gfx.UI, Size.Empty, nf).Width + sepW;
                if (x + need + w > limit && i < segs.Count - 1) { startSeg = i + 1; break; }
                need += w;
            }
            if (startSeg > 0)
            {
                Gfx.Text(g, "…  ›", Gfx.UI, new Rectangle(x, 0, Gfx.S(40), BarH), t.Sub, TextFormatFlags.Left);
                x += Gfx.S(40);
            }
            for (int i = startSeg; i < segs.Count; i++)
            {
                bool last = i == segs.Count - 1;
                Font f = last ? Gfx.Bold : Gfx.UI;
                int w = Math.Min(TextRenderer.MeasureText(segs[i].Key, f, Size.Empty, nf).Width, Math.Max(Gfx.S(30), limit - x));
                Rectangle tr = new Rectangle(x, 0, w, BarH);
                Gfx.Text(g, segs[i].Key, f, tr, last ? t.Text : t.Sub, TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                if (!last) crumbs.Add(new KeyValuePair<Rectangle, string>(tr, segs[i].Value));
                x += w;
                if (!last)
                {
                    Gfx.Text(g, "›", Gfx.UI, new Rectangle(x, 0, sepW, BarH), t.Sub, TextFormatFlags.HorizontalCenter);
                    x += sepW;
                }
            }

            using (Pen sp = new Pen(Color.FromArgb(t.Dark ? 36 : 26, t.Text)))
                g.DrawLine(sp, Pad, BarH - 1, Width - Pad, BarH - 1);
        }

        void DrawSelection(Graphics g, int i, Rectangle r, Theme t, int radius)
        {
            bool s = sel.Contains(shown[i]);
            if (s)
            {
                Gfx.FillRound(g, Color.FromArgb(t.Dark ? 70 : 42, t.Accent), r, radius);
                Gfx.DrawRound(g, Color.FromArgb(170, t.Accent), r, radius, Gfx.F(1.5f));
            }
            else if (i == hover)
            {
                Gfx.FillRound(g, Color.FromArgb(t.Dark ? 30 : 20, t.Text), r, radius);
            }
        }

        void DrawTile(Graphics g, int i, Rectangle r, Theme t)
        {
            FileEntry e = shown[i];
            DrawSelection(g, i, r, t, 10);
            int bs = ThumbBox;
            Rectangle box = new Rectangle(r.X + (r.Width - bs) / 2, r.Y + Gfx.S(8), bs, bs);
            DrawThumb(g, e, box);
            Rectangle nr = new Rectangle(r.X + Gfx.S(6), box.Bottom + Gfx.S(5), r.Width - Gfx.S(12), r.Bottom - box.Bottom - Gfx.S(7));
            TextRenderer.DrawText(g, e.Name, Gfx.Small, nr, t.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
        }

        void DrawRowItem(Graphics g, int i, Rectangle r, Theme t)
        {
            FileEntry e = shown[i];
            DrawSelection(g, i, r, t, 8);
            int isz = Gfx.S(24);
            DrawThumb(g, e, new Rectangle(r.X + Gfx.S(10), r.Y + (r.Height - isz) / 2, isz, isz));
            int dateW = Gfx.S(124), sizeW = Gfx.S(78);
            int nx = r.X + Gfx.S(10) + isz + Gfx.S(10);
            int right = r.Right - Gfx.S(12);
            bool showDate = right - dateW - sizeW - nx > Gfx.S(120);
            int nameRight = right - sizeW - (showDate ? dateW : 0);
            Gfx.Text(g, e.Name, Gfx.UI, new Rectangle(nx, r.Y, Math.Max(10, nameRight - nx - Gfx.S(8)), r.Height), t.Text,
                TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            Gfx.Text(g, e.IsDir ? "文件夹" : FormatSize(e.Size), Gfx.Small,
                new Rectangle(nameRight, r.Y, sizeW, r.Height), t.Sub, TextFormatFlags.Right | TextFormatFlags.SingleLine);
            if (showDate)
                Gfx.Text(g, e.Modified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), Gfx.Small,
                    new Rectangle(right - dateW, r.Y, dateW, r.Height), t.Sub, TextFormatFlags.Right | TextFormatFlags.SingleLine);
        }
    }

    // 屏幕底部滑出的卡片面板
    class ShelfForm : Form, IBackdrop
    {
        readonly MainForm owner;
        readonly History hist;
        readonly SearchBox sb = new SearchBox();
        readonly Chips tabs = new Chips();
        readonly IconButton gear = new IconButton();
        readonly CardStrip cardView = new CardStrip();
        readonly ItemList listView = new ItemList();
        IClipView strip;     // 当前在用的视图:卡片或列表
        readonly Chips modeSwitch = new Chips();     // 左上角:剪切板 | 文件夹
        readonly Chips folderTabs = new Chips();     // 文件夹模式下的常用文件夹标签
        readonly FileView fileView = new FileView();
        // 底部(卡片模式在右上角)的小提示条:"已删除 · 撤销"
        string toastText;
        bool toastIcon;
        bool hintsShown = true;
        Action toastUndo, toastCommit;
        Rectangle toastRect, toastBtn;
        readonly System.Windows.Forms.Timer toastTimer = new System.Windows.Forms.Timer();
        readonly WinButton pinBtn = new WinButton();
        readonly WinButton minBtn = new WinButton();
        readonly System.Windows.Forms.Timer glassTimer = new System.Windows.Forms.Timer();
        bool taskbarShown;
        bool filesMode;
        int folderIdx = -1;
        bool listMode;
        readonly QuickLook quick = new QuickLook();
        readonly System.Windows.Forms.Timer slide = new System.Windows.Forms.Timer();
        List<Item> shown = new List<Item>();
        Bitmap backdrop;   // 毛玻璃背景:面板后面那块屏幕的模糊 + 着色
        bool menuOpen, suppress, hiding, dragging, resizing, dialogOpen, spaceDown;
        readonly System.Windows.Forms.Timer keyPoll = new System.Windows.Forms.Timer();
        DateTime lastPreviewToggle = DateTime.MinValue;
        const int BaseTabs = 6;     // 全部 置顶 文本 图片 链接 文件,之后是自定义分类,最后是"新建分类"
        int lastTab;
        int resizeStartY, resizeStartH;
        int slideFrom, slideTo, slideMs;
        double slideStart;
        bool slideHiRes;

        public ShelfForm(MainForm owner, History hist)
        {
            this.owner = owner;
            this.hist = hist;
            strip = cardView;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;

            sb.Cue = "直接打字即可搜索…";
            sb.Pill = true;
            sb.ShowClear = true;
            sb.Box.Font = Gfx.UI;
            sb.Box.TextChanged += delegate { Reload(); };
            sb.Box.MouseWheel += delegate(object s, MouseEventArgs e) { strip.ScrollBy(-e.Delta / 120f * Gfx.S(220)); };

            tabs.TabStyle = true;
            tabs.SetLabels(new string[] { "全部", "置顶", "文本", "图片", "链接", "文件" });
            tabs.Changed += delegate
            {
                if (tabs.Selected == tabs.Labels.Length - 1 && tabs.Labels.Length > BaseTabs)
                {
                    // 点了"新建分类"
                    tabs.Selected = lastTab;
                    tabs.Invalidate();
                    NewBoard(null);
                    return;
                }
                lastTab = tabs.Selected;
                Reload();
            };
            tabs.RightClicked += OnTabRightClick;
            tabs.MenuState += delegate(bool open) { menuOpen = open; };

            gear.MoreDots = true;   // 设置
            gear.Click += delegate { HideNow(); owner.ShowSettingsFromPopup(); };

            Wire(cardView);
            Wire(listView);

            Controls.Add(sb);
            Controls.Add(tabs);
            Controls.Add(gear);
            Controls.Add(cardView);
            Controls.Add(listView);
            listView.Visible = false;

            // ---- 文件夹:和剪切板完全分开,用左上角的开关(或 Ctrl+Tab)切换
            FolderStore.Load();
            modeSwitch.Stretch = true;
            modeSwitch.Font = Gfx.Small;
            modeSwitch.SetLabels(new string[] { "剪切板", "文件夹" });
            modeSwitch.Changed += delegate { SetMode(modeSwitch.Selected == 1); };
            folderTabs.TabStyle = true;
            folderTabs.Visible = false;
            folderTabs.Changed += delegate { OnFolderTab(); };
            folderTabs.RightClicked += OnFolderTabRightClick;
            folderTabs.MenuState += delegate(bool open) { menuOpen = open; };
            fileView.Visible = false;
            fileView.FileOpened += OpenFile;
            fileView.SelectionChanged += delegate { if (quick.Visible) ShowFileQuick(); };
            fileView.ContextRequested += ShowFileMenu;
            fileView.DragBegin += delegate { dragging = true; };
            fileView.DragEnd += delegate { dragging = false; };
            fileView.ViewModeChanged += delegate(int vm)
            {
                if (folderIdx >= 0 && folderIdx < FolderStore.Items.Count)
                {
                    FolderStore.Items[folderIdx].View = vm;
                    FolderStore.Save();
                }
            };
            Controls.Add(modeSwitch);
            Controls.Add(folderTabs);
            Controls.Add(fileView);

            // ---- 列表模式下的文件夹窗口:图钉 + 最小化
            Text = Brand.Name + " " + Brand.NameCn;
            try { if (Brand.LargeIcon != null) Icon = Brand.LargeIcon; } catch { }
            MaximizeBox = false;
            pinBtn.IsPin = true;
            pinBtn.Visible = false;
            pinBtn.Click += delegate
            {
                Config.FolderPinned = !Config.FolderPinned;
                Config.Save();
                ApplyWindowStyle();
                Activate();
                fileView.Focus();
            };
            minBtn.Visible = false;
            minBtn.Click += delegate
            {
                quick.HideLook();
                WindowState = FormWindowState.Minimized;
            };
            Controls.Add(pinBtn);
            Controls.Add(minBtn);
            toastTimer.Interval = 6000;
            toastTimer.Tick += delegate { CloseToast(true); };
            glassTimer.Interval = 90;
            glassTimer.Tick += delegate { glassTimer.Stop(); CaptureGlassNow(); };
            // 把文件夹从资源管理器拖进面板 = 添加为常用文件夹
            AllowDrop = true;
            foreach (Control c in new Control[] { this, sb, sb.Box, tabs, gear, cardView, listView, modeSwitch, folderTabs, fileView })
            {
                c.AllowDrop = true;
                c.DragEnter += OnFolderDragEnter;
                c.DragOver += OnFolderDragEnter;
                c.DragDrop += OnFolderDrop;
            }

            // 兜底:直接读物理按键状态。不管输入法怎么处理空格,只要面板在前台、焦点不在搜索框,按空格就预览
            keyPoll.Interval = 30;
            keyPoll.Tick += delegate
            {
                bool down = (Native.GetAsyncKeyState(0x20) & 0x8000) != 0;
                bool ready = Visible && !hiding && !menuOpen && !dialogOpen && !sb.Box.Focused &&
                             (Form.ActiveForm == this);
                if (ready && down && !spaceDown) TogglePreview();
                spaceDown = down;
            };
            slide.Interval = 10;
            slide.Tick += OnSlide;
            Size = new Size(Gfx.S(1200), ShelfMetrics.ShelfH);
        }

        // 两个视图的事件都接到同样的处理上
        void Wire(IClipView v)
        {
            v.ItemActivated += delegate(int i) { Choose(i, true); };
            v.ItemContext += ShowItemMenu;
            v.SelectionChanged += delegate(int i) { if (quick.Visible) ShowQuick(); };
            v.DragStart += StartDrag;
            v.Reordered += OnReordered;
            v.DragAbove = delegate(int idx, Point screen)
            {
                if (idx < 0)
                {
                    if (tabs.DropHighlight != -1) { tabs.DropHighlight = -1; tabs.Invalidate(); }
                    return false;
                }
                Point tp = tabs.PointToClient(screen);
                int hit = -1;
                if (new Rectangle(0, -Gfx.S(12), tabs.Width, tabs.Height + Gfx.S(24)).Contains(tp))
                {
                    int ti = tabs.TabAt(new Point(tp.X, tabs.Height / 2));
                    if (CanDropOnTab(ti)) hit = ti;
                }
                if (hit != tabs.DropHighlight) { tabs.DropHighlight = hit; tabs.Invalidate(); }
                // 只要还在面板里,就不算"拖出去"
                return ClientRectangle.Contains(PointToClient(screen));
            };
            v.DropAbove += delegate(int idx, Point screen)
            {
                int ti = tabs.DropHighlight;
                tabs.DropHighlight = -1;
                tabs.Invalidate();
                if (ti >= 0 && idx >= 0 && idx < shown.Count) DropOnTab(shown[idx], ti);
            };
            v.PreviewRequested += delegate(int i)
            {
                bool same = quick.Visible && strip.Selected == i;
                strip.Selected = i;
                if (same) quick.HideLook(); else ShowQuick();
            };
            v.ZoomWheel += delegate(int delta) { SetZoom(Config.CardZoom + (delta > 0 ? 0.1f : -0.1f), true); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (taskbarShown) { cp.ExStyle &= ~0x80; cp.ExStyle |= 0x40000; }   // 在任务栏显示
                else cp.ExStyle |= 0x80;                                             // 工具窗口,不进任务栏
                cp.Style |= 0x20000;    // 可以最小化
                cp.ClassStyle |= 0x20000;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
            Native.NoTransitions(Handle);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (listMode)
            {
                // 列表模式:竖着排,搜索在上,分类在中,列表在下,快捷键提示在底部
                int pad = Gfx.S(16);
                int hh = Gfx.S(32), hy = pad + Gfx.S(4);   // 顶部一排:开关、搜索框、按钮同一高度
                modeSwitch.SetBounds(pad, hy, Gfx.S(124), hh);
                int rightX = Width - pad - hh;
                gear.SetBounds(rightX, hy, hh, hh);
                int sm = Gfx.S(28);
                if (pinBtn.Visible) { rightX -= sm + Gfx.S(2); pinBtn.SetBounds(rightX, hy + (hh - sm) / 2, sm, sm); }
                if (minBtn.Visible) { rightX -= sm + Gfx.S(2); minBtn.SetBounds(rightX, hy + (hh - sm) / 2, sm, sm); }
                sb.SetBounds(pad + Gfx.S(132), hy, Math.Max(Gfx.S(60), rightX - Gfx.S(8) - pad - Gfx.S(132)), hh);
                tabs.SetBounds(pad, Gfx.S(66), Width - pad * 2, Gfx.S(32));
                listView.SetBounds(Gfx.S(4), Gfx.S(106), Width - Gfx.S(8), Math.Max(10, Height - Gfx.S(106) - Gfx.S(46)));
                folderTabs.Bounds = tabs.Bounds;
                // 左右各留出几像素给窗口边框,方便拖动调整大小
                fileView.SetBounds(Gfx.S(7), Gfx.S(106), Width - Gfx.S(14), Math.Max(10, Height - Gfx.S(106) - Gfx.S(46)));
                return;
            }
            int top = Gfx.S(14);
            int hh2 = Gfx.S(32), hy2 = top + Gfx.S(2);
            modeSwitch.SetBounds(Gfx.S(20), hy2, Gfx.S(132), hh2);
            sb.SetBounds(Gfx.S(162), hy2, Gfx.S(230), hh2);
            gear.SetBounds(Width - Gfx.S(20) - hh2, hy2, hh2, hh2);
            int tx = sb.Right + Gfx.S(28);
            // 分类标签放不下时,先把右侧的快捷键提示让出来(显示删除提示条时除外)
            int withHints = gear.Left - Gfx.S(230) - tx, full = gear.Left - Gfx.S(20) - tx;
            Chips active = filesMode ? folderTabs : tabs;
            hintsShown = active.ContentWidth <= withHints;
            int tw = (hintsShown || toastText != null) ? withHints : full;
            tabs.SetBounds(tx, top + Gfx.S(2), Math.Max(Gfx.S(120), tw), Gfx.S(32));
            cardView.SetBounds(0, Gfx.S(60), Width, Math.Max(10, Height - Gfx.S(60) - Gfx.S(6)));
            folderTabs.Bounds = tabs.Bounds;
            fileView.Bounds = cardView.Bounds;
        }

        public bool HasBackdrop
        {
            get { return backdrop != null; }
        }

        // 画毛玻璃背景:背景图底边和面板底边对齐;child 为空时画整个面板
        public void PaintBackdrop(Graphics g, Control child)
        {
            if (backdrop == null) { g.Clear(Theme.Cur.Shelf); return; }
            if (listMode && (backdrop.Width != Width || backdrop.Height != Height)) g.Clear(Theme.Cur.Shelf);   // 窗口刚拖大,新背景还没拍好
            int ox = child == null ? 0 : child.Left;
            int oy = child == null ? 0 : child.Top;
            CompositingMode cm = g.CompositingMode;
            InterpolationMode im = g.InterpolationMode;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(backdrop, new Rectangle(-ox, Height - backdrop.Height - oy, backdrop.Width, backdrop.Height));
            g.CompositingMode = cm;
            g.InterpolationMode = im;
        }

        Bitmap blurBase;   // 面板后面那块屏幕的模糊图(还没着色)

        void BuildBackdrop(Rectangle area)
        {
            if (blurBase != null) { blurBase.Dispose(); blurBase = null; }
            if (Config.ShelfTransparency > 0) blurBase = Frost.CaptureBlur(area);   // 0% 不透明:不用拍屏幕
            ComposeBackdrop();
        }

        // ---------------- 列表模式下的文件夹窗口:能移动、能调大小、能取消置顶
        bool FolderWindow { get { return listMode && filesMode; } }

        // 移动或调整大小后,重新拍一张自己身后的背景(拍的时候让自己在截图里"隐身")
        void RefreshGlass()
        {
            if (!listMode || !Visible || WindowState == FormWindowState.Minimized) return;
            if (Config.ShelfTransparency <= 0 || !Native.ExcludeFromCapture(Handle, true))
            {
                // 不透明,或者系统太旧不支持:用实色背景
                if (blurBase != null) { blurBase.Dispose(); blurBase = null; }
                ComposeBackdrop();
                Invalidate(true);
                return;
            }
            glassTimer.Stop();
            glassTimer.Start();   // 等系统把"隐身"生效再拍
        }

        void CaptureGlassNow()
        {
            Bitmap nb = null;
            try { if (Visible && WindowState == FormWindowState.Normal) nb = Frost.CaptureBlur(Bounds); }
            catch { nb = null; }
            Native.ExcludeFromCapture(Handle, false);
            if (nb == null) return;
            if (blurBase != null) blurBase.Dispose();
            blurBase = nb;
            ComposeBackdrop();
            Invalidate(true);
        }

        void SetTaskbar(bool on)
        {
            if (on == taskbarShown) return;
            taskbarShown = on;
            if (!IsHandleCreated) return;
            int ex = Native.GetWindowLong(Handle, -20);
            ex = on ? ((ex & ~0x80) | 0x40000) : ((ex & ~0x40000) | 0x80);
            bool vis = Visible;
            if (vis) Native.ShowWindow(Handle, 0);    // 先藏一下,任务栏才会认新样式
            Native.SetWindowLong(Handle, -20, ex);
            if (vis) Native.ShowWindow(Handle, 5);
        }

        // 按当前状态决定:置顶与否、任务栏图标、最小尺寸、标题栏按钮
        void ApplyWindowStyle()
        {
            bool fw = FolderWindow;
            bool pinned = !fw || Config.FolderPinned;
            SetTaskbar(!pinned);
            TopMost = pinned;
            MinimumSize = fw ? new Size(Gfx.S(400), Gfx.S(340)) : Size.Empty;
            pinBtn.Visible = fw;
            pinBtn.Active = Config.FolderPinned;
            minBtn.Visible = fw && !Config.FolderPinned;
            pinBtn.Invalidate();
            OnResize(EventArgs.Empty);
            Invalidate();
        }

        static Size ClipSize { get { return new Size(Gfx.S(480), Gfx.S(600)); } }

        // 文件夹窗口上次调整后的大小(位置不记,每次都在鼠标旁出现)
        static Size FolderSize()
        {
            try
            {
                string[] p = (Config.FolderWin ?? "").Split(',');
                if (p.Length >= 2)
                {
                    int w = int.Parse(p[p.Length - 2]), h = int.Parse(p[p.Length - 1]);
                    return new Size(Math.Max(w, Gfx.S(400)), Math.Max(h, Gfx.S(340)));
                }
            }
            catch { }
            return ClipSize;
        }

        void SaveFolderSize()
        {
            if (!FolderWindow || WindowState != FormWindowState.Normal) return;
            Config.FolderWin = Width + "," + Height;
            Config.Save();
        }

        // 在剪切板和文件夹之间切换时:左上角不动,换成各自的大小
        void SwitchBounds()
        {
            if (!listMode || !Visible) return;
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            Size sz = filesMode ? FolderSize() : ClipSize;
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int w = Math.Min(sz.Width, wa.Width), h = Math.Min(sz.Height, wa.Height);
            int x = Math.Max(wa.Left, Math.Min(Left, wa.Right - w));
            int y = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - h));
            Rectangle target = new Rectangle(x, y, w, h);
            if (target != Bounds)
            {
                Bounds = target;
                RefreshGlass();
            }
        }

        // 快捷键:文件夹窗口被挡住或最小化了,就把它叫到最前面
        public bool BringFrontIfHidden()
        {
            if (!Visible || !FolderWindow) return false;
            if (WindowState != FormWindowState.Minimized && Native.GetForegroundWindow() == Handle) return false;
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show();
            Activate();
            Native.SetForegroundWindow(Handle);
            fileView.Focus();
            keyPoll.Start();
            return true;
        }

        // ---------------- 文件夹窗口:自己处理拖动和调整大小(四个方向都能随意拖)
        int dragEdge = -1;            // -1 没在拖;0 移动;其余是边的组合:1 左 2 右 4 上 8 下
        Point dragStartCur;
        Rectangle dragStartBounds;

        int EdgeAt(Point p)
        {
            int b = Gfx.S(8), e = 0;
            if (p.X < b) e |= 1; else if (p.X >= Width - b) e |= 2;
            if (p.Y < b) e |= 4; else if (p.Y >= Height - b) e |= 8;
            return e;
        }

        static Cursor CursorFor(int e)
        {
            if (e == 5 || e == 10) return Cursors.SizeNWSE;
            if (e == 6 || e == 9) return Cursors.SizeNESW;
            if (e == 1 || e == 2) return Cursors.SizeWE;
            if (e == 4 || e == 8) return Cursors.SizeNS;
            return Cursors.SizeAll;
        }

        bool BeginWindowDrag(MouseEventArgs e)
        {
            if (!FolderWindow || WindowState != FormWindowState.Normal || e.Button != MouseButtons.Left) return false;
            dragEdge = EdgeAt(e.Location);
            dragStartCur = Cursor.Position;
            dragStartBounds = Bounds;
            Capture = true;
            return true;
        }

        bool DoWindowDrag()
        {
            if (dragEdge < 0) return false;
            Point c = Cursor.Position;
            int dx = c.X - dragStartCur.X, dy = c.Y - dragStartCur.Y;
            Rectangle r = dragStartBounds;
            Rectangle wa = Screen.FromPoint(c).WorkingArea;
            int snap = Gfx.S(14);
            if (dragEdge == 0)
            {
                int x = r.X + dx, y = r.Y + dy;
                // 靠近屏幕边缘时自动贴齐
                if (Math.Abs(x - wa.Left) < snap) x = wa.Left;
                else if (Math.Abs(x + r.Width - wa.Right) < snap) x = wa.Right - r.Width;
                if (Math.Abs(y - wa.Top) < snap) y = wa.Top;
                else if (Math.Abs(y + r.Height - wa.Bottom) < snap) y = wa.Bottom - r.Height;
                Location = new Point(x, y);
                return true;
            }
            int minW = Gfx.S(400), minH = Gfx.S(340);
            int left = r.Left, top = r.Top, right = r.Right, bottom = r.Bottom;
            if ((dragEdge & 1) != 0) left = Math.Min(r.Right - minW, r.Left + dx);
            if ((dragEdge & 2) != 0) right = Math.Max(r.Left + minW, r.Right + dx);
            if ((dragEdge & 4) != 0) top = Math.Min(r.Bottom - minH, r.Top + dy);
            if ((dragEdge & 8) != 0) bottom = Math.Max(r.Top + minH, r.Bottom + dy);
            SetBounds(left, top, right - left, bottom - top);
            Update();
            return true;
        }

        bool EndWindowDrag()
        {
            if (dragEdge < 0) return false;
            bool sized = dragEdge > 0;
            dragEdge = -1;
            Capture = false;
            if (sized) SaveFolderSize();
            RefreshGlass();
            if (quick.Visible) ShowFileQuick();
            return true;
        }

        // 按当前的透明度、主题、面板高度合成成品;很快,随时可以重做
        void ComposeBackdrop()
        {
            if (backdrop != null) { backdrop.Dispose(); backdrop = null; }
            if (blurBase == null) return;
            Bitmap b = new Bitmap(blurBase.Width, blurBase.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.DrawImageUnscaled(blurBase, 0, 0);
                g.CompositingMode = CompositingMode.SourceOver;
                int panelTop = listMode ? 0 : Math.Max(0, b.Height - ShelfMetrics.ShelfH);
                Frost.Tint(g, new Rectangle(0, 0, b.Width, b.Height), Frost.Legible(blurBase, Config.ShelfTransparency), panelTop, 1f);
            }
            backdrop = b;
        }

        // 设置里改了透明度:面板开着的话立刻看到效果
        public void ApplyGlass()
        {
            if (!Visible) return;
            ComposeBackdrop();
            Invalidate(true);
        }

        void ApplyTheme()
        {
            BackColor = Theme.Cur.Shelf;
            sb.ApplyTheme();
            tabs.Invalidate();
            gear.Invalidate();
            strip.Invalidate();
            modeSwitch.Invalidate();
            folderTabs.Invalidate();
            fileView.Invalidate();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            PaintBackdrop(g, null);
            using (Pen p = new Pen(backdrop != null ? Color.FromArgb(t.Dark ? 50 : 90, 255, 255, 255) : t.Border))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            if (listMode)
            {
                // 列表模式:底部一行快捷键提示
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen lp = new Pen(Color.FromArgb(t.Dark ? 40 : 30, t.Text)))
                    g.DrawLine(lp, Gfx.S(16), Height - Gfx.S(42), Width - Gfx.S(16), Height - Gfx.S(42));
                int ly = Height - Gfx.S(21);
                int lr = Width - Gfx.S(16);
                if (toastText != null) { DrawToast(g, lr, ly); return; }
                lr = Gfx.KeyHint(g, "Enter", filesMode ? "打开" : "粘贴", lr, ly);
                lr = Gfx.KeyHint(g, "空格", "预览", lr - Gfx.S(14), ly);
                Gfx.KeyHint(g, filesMode ? "Backspace" : "Tab", filesMode ? "上一级" : "分类", lr - Gfx.S(14), ly);
                return;
            }
            // 顶部中间的小横条:按住上下拖动可以放大缩小卡片
            Gfx.FillRound(g, resizing ? t.Accent : t.Border, new Rectangle(Width / 2 - Gfx.S(24), Gfx.S(4), Gfx.S(48), Gfx.S(4)), 2);
            // 右上角:快捷键提示(键帽样式)
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = Gfx.S(14) + Gfx.S(18);
            int right = gear.Left - Gfx.S(16);
            if (toastText != null) { DrawToast(g, right, cy); return; }
            if (!hintsShown) return;
            right = Gfx.KeyHint(g, "Enter", filesMode ? "打开" : "粘贴", right, cy);
            Gfx.KeyHint(g, "空格", "预览", right - Gfx.S(16), cy);
        }

        // ---------------- 显示 / 隐藏(从屏幕底部滑入滑出)
        public void ShowAt()
        {
            Perf.Start();
            Theme.Refresh();
            ApplyTheme();
            Perf.Mark("theme");
            // 先按剪切板准备好,下面再按上次停留的模式切过去
            bool reopenFiles = Config.LastFilesMode;
            filesMode = false;
            modeSwitch.Selected = 0;
            modeSwitch.Invalidate();
            folderTabs.Visible = false;
            fileView.Visible = false;
            tabs.Visible = true;
            sb.SetCue("直接打字即可搜索…");
            suppress = true;
            sb.Box.Text = "";
            BuildTabs(0);
            suppress = false;
            Perf.Mark("tabs");

            // 按设置选择视图:卡片(屏幕底部) / 列表(鼠标旁或屏幕中央)
            listMode = Config.Layout == 0;
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
            strip = listMode ? (IClipView)listView : cardView;
            listView.Visible = listMode;
            cardView.Visible = !listMode;
            Reload(2);
            Perf.Mark("reload");
            if (reopenFiles) SetMode(true);   // 上次停在文件夹,就直接回到文件夹
            ApplyWindowStyle();
            Perf.Mark("mode+style");

            if (listMode)
            {
                Point cur = Cursor.Position;
                Rectangle wa2 = Screen.FromPoint(cur).WorkingArea;
                Size want = filesMode ? FolderSize() : ClipSize;   // 文件夹窗口用上次调好的大小
                int lw = Math.Min(want.Width, wa2.Width), lh = Math.Min(want.Height, wa2.Height);
                int lx, ly;
                if (Config.PopupAtCursor) { lx = cur.X; ly = cur.Y; }
                else { lx = wa2.Left + (wa2.Width - lw) / 2; ly = wa2.Top + (wa2.Height - lh) / 3; }
                lx = Math.Max(wa2.Left, Math.Min(lx, wa2.Right - lw));
                ly = Math.Max(wa2.Top, Math.Min(ly, wa2.Bottom - lh));
                hiding = false;
                BuildBackdrop(new Rectangle(lx, ly, lw, lh));
                Perf.Mark("glass");
                SetBounds(lx, ly + Gfx.S(10), lw, lh);
                Show();
                Perf.Mark("show");
                Activate();
                Native.SetForegroundWindow(Handle);
                if (filesMode) fileView.Focus(); else strip.Focus();
                spaceDown = (Native.GetAsyncKeyState(0x20) & 0x8000) != 0;
                keyPoll.Start();
                StartSlide(ly + Gfx.S(10), ly, 220);   // 轻轻落下
                return;
            }

            Screen scr = Screen.FromPoint(Cursor.Position);
            Rectangle wa = scr.WorkingArea;
            int m = Gfx.S(12);
            int h = ShelfMetrics.ShelfH;
            hiding = false;
            // 弹出前先拍下面板将要覆盖的那块屏幕,做成毛玻璃背景(往上多拍一些,放大卡片时也够用)
            int maxH = Gfx.S(100) + Gfx.S((int)Math.Round(236 * 1.6));
            int capTop = Math.Max(scr.Bounds.Top, wa.Bottom - m - maxH);
            BuildBackdrop(new Rectangle(wa.Left + m, capTop, wa.Width - 2 * m, wa.Bottom - m - capTop));
            Perf.Mark("glass");
            SetBounds(wa.Left + m, scr.Bounds.Bottom, wa.Width - 2 * m, h);
            Show();
            Perf.Mark("show");
            Activate();
            Native.SetForegroundWindow(Handle);
            if (filesMode) fileView.Focus(); else strip.Focus();
            spaceDown = (Native.GetAsyncKeyState(0x20) & 0x8000) != 0;
            keyPoll.Start();
            StartSlide(scr.Bounds.Bottom, wa.Bottom - h - m, 480);
        }

        void StartSlide(int from, int to, int ms)
        {
            slideFrom = from;
            slideTo = to;
            slideMs = ms;
            slideStart = Gfx.Ms;
            if (!slideHiRes) { slideHiRes = true; Gfx.HiRes(true); }
            Top = from;
            slide.Start();
        }

        void StopSlide()
        {
            slide.Stop();
            if (slideHiRes) { slideHiRes = false; Gfx.HiRes(false); }
        }

        void OnSlide(object sender, EventArgs e)
        {
            double p = (Gfx.Ms - slideStart) / slideMs;
            if (p >= 1) p = 1;
            double ease = hiding ? p * p * p : 1 - Math.Pow(1 - p, 5);   // 弹出:先快后慢、稳稳停住;收起:加速滑走
            Top = slideFrom + (int)((slideTo - slideFrom) * ease);
            if (p >= 1)
            {
                StopSlide();
                if (hiding) { hiding = false; keyPoll.Stop(); Hide(); }
            }
        }

        public void HideShelf()
        {
            if (!Visible || hiding) return;
            if (listMode) { HideNow(); return; }
            CloseToast(true);
            quick.HideLook();
            hiding = true;
            StartSlide(Top, Screen.FromControl(this).Bounds.Bottom + Gfx.S(10), 220);
        }

        public void HideNow()
        {
            CloseToast(true);
            keyPoll.Stop();
            StopSlide();
            hiding = false;
            quick.HideLook();
            Hide();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (Visible) { spaceDown = (Native.GetAsyncKeyState(0x20) & 0x8000) != 0; keyPoll.Start(); }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            keyPoll.Stop();   // 不在前台时不用一直查按键(文件夹窗口可能长时间开着)
            if (filesMode) return;   // 文件夹模式不自动收起,方便在别的窗口和面板之间拖文件;Esc 或快捷键关闭
            if (menuOpen || dragging || hiding || resizing || dialogOpen || !Visible) return;
            BeginInvoke(new MethodInvoker(delegate
            {
                if (Visible && !menuOpen && !dragging && !dialogOpen && !resizing && Form.ActiveForm != this && Form.ActiveForm != quick) HideShelf();
            }));
        }

        // ---------------- 数据
        bool Match(Item it, string q)
        {
            switch (tabs.Selected)
            {
                case 1: if (!it.Pinned) return false; break;
                case 2: if (it.IsImage || it.IsFiles) return false; break;
                case 3: if (!(it.IsImage || it.IsImageFile)) return false; break;
                case 4: if (it.IsImage || it.IsFiles || it.Kind != Item.KUrl) return false; break;
                case 5: if (!it.IsFiles) return false; break;
            }
            if (tabs.Selected >= BaseTabs)
            {
                Board cur = BoardAt(tabs.Selected);
                if (cur == null || it.Board != cur.Id) return false;
            }
            if (q.Length == 0) return true;
            if (it.AppName != null && it.AppName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (it.CustomTitle != null && it.CustomTitle.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (it.IsImage || it.Text == null) return false;
            return it.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Reload() { Reload(1); }

        void Reload(int mode)
        {
            if (suppress) return;
            if (filesMode) { fileView.Filter(sb.Box.Text.Trim()); return; }
            string q = sb.Box.Text.Trim();
            shown = hist.Items.Where(x => Match(x, q)).Take(300).ToList();
            strip.EmptyText = (q.Length > 0 || tabs.Selected != 0) ? "没有匹配的内容" : "复制点什么,这里就会出现";
            strip.SetItems(shown, mode);
            if (quick.Visible)
            {
                if (shown.Count > 0) ShowQuick(); else quick.HideLook();
            }
            Invalidate();
        }

        Item Current()
        {
            int i = strip.Selected;
            return (i >= 0 && i < shown.Count) ? shown[i] : null;
        }

        // 打开/关闭快速预览(两条检测路径都走这里;很短时间内只响应一次)
        void TogglePreview()
        {
            if ((DateTime.Now - lastPreviewToggle).TotalMilliseconds < 250) return;
            lastPreviewToggle = DateTime.Now;
            if (quick.Visible) quick.HideLook(); else if (filesMode) ShowFileQuick(); else ShowQuick();
        }

        void ShowQuick()
        {
            Item it = Current();
            if (it == null) return;
            quick.ShowFor(it, Bounds, listMode);
        }

        void Choose(int index, bool paste)
        {
            if (index < 0 || index >= shown.Count) return;
            Item it = shown[index];
            HideNow();
            owner.Apply(it, paste);
        }

        void Reselect(Item it, int fallback)
        {
            int i = shown.IndexOf(it);
            if (i < 0) i = Math.Min(fallback, shown.Count - 1);
            if (i >= 0) strip.Selected = i;
        }

        void TogglePin()
        {
            Item it = Current();
            if (it == null) return;
            int i = strip.Selected;
            hist.TogglePin(it);
            Reload(0);
            Reselect(it, i);
        }

        void DeleteSelected()
        {
            Item it = Current();
            if (it == null) return;
            int i = strip.Selected;
            CloseToast(true);   // 上一次的删除先真正完成
            hist.RemoveUndoable(it);
            Reload(0);
            Reselect(null, i);
            ShowToast("已删除", delegate
            {
                Item back = hist.UndoDelete();
                Reload(0);
                if (back != null) Reselect(back, i);
            }, delegate { hist.FinishPendingDelete(); });
        }

        static List<string> IgnoreList()
        {
            return (Config.IgnoreApps ?? "").Split(new char[] { ',', ';', '，', '；' })
                .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }

        static bool IgnoreMatch(string a, string exe)
        {
            string x = a.ToLowerInvariant();
            if (x.EndsWith(".exe")) x = x.Substring(0, x.Length - 4);
            return x == exe.ToLowerInvariant();
        }

        void IgnoreApp(string exe, string shownName)
        {
            List<string> list = IgnoreList();
            if (!list.Any(a => IgnoreMatch(a, exe)))
            {
                list.Add(exe.ToLowerInvariant());
                Config.IgnoreApps = string.Join(",", list.ToArray());
                Config.Save();
            }
            ShowToast("不再记录「" + shownName + "」", delegate
            {
                Config.IgnoreApps = string.Join(",", IgnoreList().Where(a => !IgnoreMatch(a, exe)).ToArray());
                Config.Save();
            }, null);
        }

        void MarkCourier(Item it, Courier.Company c)
        {
            hist.SetTitle(it, c == null ? null : c.Name);
            Reload(0);
            Reselect(it, strip.Selected);
        }

        // ---------------- 扫码带走
        static ShareForm shareWin;

        void OpenUrl(string url)
        {
            try { HideNow(); Process.Start(url); } catch { }
        }

        void ShareItem(Item it)
        {
            if (it.IsImage) ShareToPhone("截图 · " + it.TimeAgo(), null, new List<string> { it.FilePath });
            else if (it.IsFiles) ShareToPhone(it.FileTitle(), null, it.Paths.Where(x => File.Exists(x)).Take(20).ToList());
            else ShareToPhone(CardLook.Title(it) + " · " + it.TimeAgo(), it.Text ?? "", null);
        }

        // 短文字直接编进二维码(不用联网);图片、文件、长文字由本机临时开一个只服务这条内容的小网页
        void ShareToPhone(string title, string text, List<string> files)
        {
            HideNow();
            if (shareWin != null && !shareWin.IsDisposed) shareWin.Close();
            bool[,] m;
            if (text != null && Encoding.UTF8.GetByteCount(text) <= 700)
            {
                m = QrCode.Encode(Encoding.UTF8.GetBytes(text));
                if (m != null)
                {
                    bool isUrl = Regex.IsMatch(text.Trim(), @"^(https?://|www\.)\S+$", RegexOptions.IgnoreCase);
                    shareWin = new ShareForm(title, m, isUrl ? "扫码后手机会直接打开这个链接。不需要联网传输,用手机相机或微信扫一扫都可以"
                                                              : "文字已经在二维码里,扫一下就能看到、复制。不需要联网,用手机相机或微信扫一扫都可以", null, false);
                    shareWin.Show();
                    shareWin.Activate();
                    return;
                }
            }
            string err;
            string url = ShareServer.Start(title, text, files, out err);
            if (url == null) { owner.Balloon("扫码带走失败:" + err, true); return; }
            m = QrCode.Encode(Encoding.UTF8.GetBytes(url));
            if (m == null) { ShareServer.Stop(); return; }
            bool hasImage = files != null && files.Any(f => ShareImageExts.Contains(System.IO.Path.GetExtension(f)));
            shareWin = new ShareForm(title, m,
                "手机和电脑连同一个 Wi-Fi,用手机相机或浏览器扫码打开" + (hasImage ? ",图片长按即可保存" : "") + "。关掉这个窗口或 10 分钟后失效。" +
                "第一次使用时,如果 Windows 询问是否允许 Glean 访问网络,请选择“允许”", url, true);
            shareWin.Show();
            shareWin.Activate();
        }

        static readonly HashSet<string> ShareImageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

        // ---------------- 图片转文字
        static readonly HashSet<string> OcrExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
        };
        bool ocrBusy;

        // paste = true:识别完直接粘贴到原来的窗口(从卡片发起);false:只复制(从文件夹发起)
        void OcrImage(string path, bool paste)
        {
            if (ocrBusy) return;
            ocrBusy = true;
            ShowToast("正在识别文字…", null, null);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string text = null, err = null;
                bool noChinese = false;
                try { text = Ocr.Recognize(path, out noChinese); }
                catch (Exception ex) { err = Ocr.Reason(ex); }
                try { BeginInvoke(new MethodInvoker(delegate { OcrDone(text, err, noChinese, paste); })); }
                catch { ocrBusy = false; }
            });
        }

        void OcrDone(string text, string err, bool noChinese, bool paste)
        {
            ocrBusy = false;
            if (noChinese)
                owner.Balloon("这台电脑没有安装中文识别组件,这次只能识别英文。在“设置 → 时间和语言 → 语言”中添加“中文(简体)”后即可识别中文。", true);
            if (err != null)
            {
                ShowToast("识别失败", null, null);
                owner.Balloon("文字识别失败:" + err, true);
                return;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowToast("没有识别到文字", null, null);
                return;
            }
            CloseToast(true);
            Item ni = owner.Record(text);
            if (ni == null) return;
            if (paste && Visible && !filesMode)
            {
                HideNow();
                owner.Apply(ni, true);   // 和"粘贴为…"一样:粘贴到原来的窗口,同时作为新卡片留在历史里
                return;
            }
            owner.Apply(ni, false);
            if (Visible) { Reload(0); ShowToast("文字已复制", null, null); }
            else owner.Balloon("识别出的文字已复制到剪切板", false);
        }

        // ---------------- 提示条
        void ShowToast(string text, Action undo, Action commit)
        {
            CloseToast(true);   // 上一个提示的操作就此生效
            toastText = text;
            toastIcon = undo != null || commit != null;
            toastUndo = undo;
            toastCommit = commit;
            toastTimer.Stop();
            toastTimer.Start();
            if (!listMode) OnResize(EventArgs.Empty);
            Invalidate();
        }

        // commit=true:时间到了,操作生效;false:点了撤销
        void CloseToast(bool commit)
        {
            toastTimer.Stop();
            if (toastText == null) return;
            Action a = commit ? toastCommit : toastUndo;
            toastText = null;
            toastUndo = toastCommit = null;
            toastRect = toastBtn = Rectangle.Empty;
            if (!listMode) OnResize(EventArgs.Empty);
            Invalidate();
            if (a != null) { try { a(); } catch { } }
        }

        public bool UndoLast()
        {
            if (toastText == null || toastUndo == null) return false;
            CloseToast(false);
            return true;
        }

        // 画提示条:右端对齐 right,竖直居中在 cy
        void DrawToast(Graphics g, int right, int cy)
        {
            Theme t = Theme.Cur;
            const TextFormatFlags nf = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            string undo = "撤销";
            string key = "Ctrl+Z";
            int tw = TextRenderer.MeasureText(toastText, Gfx.UI, Size.Empty, nf).Width;
            int uw = TextRenderer.MeasureText(undo, Gfx.Bold, Size.Empty, nf).Width;
            int kw = TextRenderer.MeasureText(key, Gfx.Small, Size.Empty, nf).Width;
            int h = Gfx.S(30), padX = Gfx.S(14), gap = Gfx.S(12);
            bool hasUndo = toastUndo != null;
            int iconW = toastIcon ? Gfx.S(16) + Gfx.S(8) : 0;
            int w = hasUndo ? padX + iconW + tw + gap + Gfx.S(1) + gap + uw + Gfx.S(6) + kw + padX
                            : padX + iconW + tw + padX;
            Rectangle r = new Rectangle(right - w, cy - h / 2, w, h);
            toastRect = r;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Gfx.FillRound(g, t.Input, r, 99);
            Gfx.DrawRound(g, Color.FromArgb(t.Dark ? 40 : 28, t.Text), r, 99, Gfx.F(1f));
            int x = r.X + padX;
            if (toastIcon)
            {
                // 小勾
                using (Pen p = new Pen(t.Accent, Gfx.F(1.8f)))
                {
                    p.StartCap = p.EndCap = LineCap.Round;
                    p.LineJoin = LineJoin.Round;
                    g.DrawLines(p, new Point[] { new Point(x + Gfx.S(2), cy), new Point(x + Gfx.S(6), cy + Gfx.S(4)), new Point(x + Gfx.S(14), cy - Gfx.S(4)) });
                }
                x += iconW;
            }
            TextRenderer.DrawText(g, toastText, Gfx.UI, new Rectangle(x, r.Y, tw + 2, h), t.Text, nf | TextFormatFlags.VerticalCenter);
            if (!hasUndo) { toastBtn = Rectangle.Empty; return; }
            x += tw + gap;
            using (Pen sp = new Pen(Color.FromArgb(t.Dark ? 50 : 40, t.Text))) g.DrawLine(sp, x, r.Y + Gfx.S(8), x, r.Bottom - Gfx.S(8));
            x += Gfx.S(1) + gap;
            toastBtn = new Rectangle(x - Gfx.S(8), r.Y, r.Right - x + Gfx.S(8), h);
            TextRenderer.DrawText(g, undo, Gfx.Bold, new Rectangle(x, r.Y, uw + 2, h), t.Accent, nf | TextFormatFlags.VerticalCenter);
            x += uw + Gfx.S(6);
            TextRenderer.DrawText(g, key, Gfx.Small, new Rectangle(x, r.Y, kw + 2, h), t.Sub, nf | TextFormatFlags.VerticalCenter);
        }

        void CycleTab(int d)
        {
            int n = Math.Max(1, tabs.Labels.Length - 1);   // 跳过最后的"新建分类"
            tabs.Selected = (tabs.Selected + d + n) % n;
            lastTab = tabs.Selected;
            tabs.Invalidate();
            Reload();
        }

        void OpenLink(Item it)
        {
            try
            {
                string url = it.Text.Trim();
                if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) url = "http://" + url;
                HideNow();
                Process.Start(url);
            }
            catch { }
        }

        void ShowInFolder(Item it)
        {
            try
            {
                string target = it.FilePath;
                if (it.IsFiles)
                {
                    target = null;
                    foreach (string f in it.Paths)
                        if (File.Exists(f) || Directory.Exists(f)) { target = f; break; }
                    if (target == null) return;
                }
                HideNow();
                Process.Start("explorer.exe", "/select,\"" + target + "\"");
            }
            catch { }
        }

        void ApplyTransform(Item it, Func<string, string> f)
        {
            string r;
            try { r = f(it.Text); }
            catch { return; }
            HideNow();
            Item ni = owner.Record(r);
            owner.Apply(ni ?? it, true);
        }

        void AddTransform(ToolStripMenuItem parent, string title, Item it, Func<string, string> f)
        {
            parent.DropDownItems.Add(title, null, delegate { ApplyTransform(it, f); });
        }

        void ShowItemMenu(int index, Point screenPt)
        {
            if (index < 0 || index >= shown.Count) return;
            Item it = shown[index];
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("粘贴", null, delegate { Choose(index, true); });
            m.Items.Add("仅复制到剪切板", null, delegate { Choose(index, false); });
            m.Items.Add("快速预览(空格)", null, delegate { ShowQuick(); });
            m.Items.Add("扫码带走(手机)", null, delegate { ShareItem(it); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(it.Pinned ? "取消置顶" : "置顶", null, delegate { TogglePin(); });
            m.Items.Add("重命名…", null, delegate { RenameCard(it); });

            // 加入分类
            ToolStripMenuItem boardMenu = new ToolStripMenuItem("加入分类");
            foreach (Board bd in hist.Boards)
            {
                Board target = bd;
                ToolStripMenuItem bi = new ToolStripMenuItem(target.Name, Swatch(Color.FromArgb(target.Color)),
                    delegate { hist.SetBoard(it, target.Id); AfterBoardChange(); });
                bi.Checked = it.Board == target.Id;
                boardMenu.DropDownItems.Add(bi);
            }
            if (hist.Boards.Count > 0) boardMenu.DropDownItems.Add(new ToolStripSeparator());
            boardMenu.DropDownItems.Add("新建分类…", null, delegate { NewBoard(it); });
            if (it.Board != null)
                boardMenu.DropDownItems.Add("移出分类", null, delegate { hist.SetBoard(it, null); AfterBoardChange(); });
            m.Items.Add(boardMenu);

            if (!it.IsImage && !it.IsFiles)
            {
                ToolStripMenuItem sub = new ToolStripMenuItem("粘贴为…");
                AddTransform(sub, "全部大写", it, delegate(string s) { return s.ToUpperInvariant(); });
                AddTransform(sub, "全部小写", it, delegate(string s) { return s.ToLowerInvariant(); });
                AddTransform(sub, "每个单词首字母大写", it, delegate(string s)
                {
                    return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
                });
                AddTransform(sub, "去掉首尾空白", it, delegate(string s) { return s.Trim(); });
                AddTransform(sub, "合并为一行", it, delegate(string s) { return Regex.Replace(s, @"\s+", " ").Trim(); });
                AddTransform(sub, "去掉空行", it, delegate(string s) { return Regex.Replace(s, @"(\r?\n)\s*(\r?\n)+", "$1").Trim(); });
                if (it.Kind == Item.KUrl)
                    AddTransform(sub, "清理链接追踪参数", it, delegate(string s) { return Util.CleanUrl(s); });
                m.Items.Add(sub);
                if (it.Kind == Item.KUrl) m.Items.Add("在浏览器中打开", null, delegate { OpenLink(it); });
                Courier.Hit hit0 = Courier.HitOf(it);
                Courier.Company cc0 = hit0 != null ? hit0.Company : null;
                if (cc0 != null || Courier.LooksLikeNumber(it.Text))
                {
                    string num0 = hit0 != null ? hit0.Number : Courier.Number(it.Text);
                    string shown0 = num0.Length > 18 ? num0.Substring(0, 17) + "…" : num0;
                    ToolStripMenuItem trk = new ToolStripMenuItem(cc0 != null ? "查物流(" + cc0.Name + " " + shown0 + ")" : "当作快递单号查询");
                    trk.Click += delegate { OpenUrl(Courier.TrackUrl(num0, cc0)); };
                    m.Items.Add(trk);
                    m.Items.Add("用百度查物流", null, delegate { OpenUrl(Courier.SearchUrl(num0)); });
                    if (hit0 != null && !hit0.Whole)
                        m.Items.Add("只复制单号", null, delegate { owner.Apply(owner.Record(num0) ?? it, false); });
                    if (Courier.LooksLikeNumber(it.Text))
                    {
                        // 纯数字单号认不出是哪家:让用户标记一下,卡片上就会显示公司名和角标
                        ToolStripMenuItem mark = new ToolStripMenuItem("标记为快递单号");
                        foreach (Courier.Company co in Courier.Companies)
                        {
                            Courier.Company pick = co;
                            ToolStripMenuItem mi = new ToolStripMenuItem(co.Name, null, delegate { MarkCourier(it, pick); });
                            mi.Checked = cc0 == co;
                            mark.DropDownItems.Add(mi);
                        }
                        if (cc0 != null)
                        {
                            mark.DropDownItems.Add(new ToolStripSeparator());
                            mark.DropDownItems.Add("取消标记", null, delegate { MarkCourier(it, null); });
                        }
                        m.Items.Add(mark);
                    }
                }
            }
            else if (it.IsFiles)
            {
                ToolStripMenuItem sub2 = new ToolStripMenuItem("粘贴为…");
                AddTransform(sub2, "文件路径(文本)", it, delegate(string s) { return s.Replace("\r", "").Replace("\n", "\r\n"); });
                AddTransform(sub2, "文件名(文本)", it, delegate(string s)
                {
                    return string.Join("\r\n", s.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => System.IO.Path.GetFileName(x.Trim('\r').TrimEnd('\\', '/'))).ToArray());
                });
                m.Items.Add(sub2);
                m.Items.Add("在文件夹中显示", null, delegate { ShowInFolder(it); });
            }
            else
            {
                m.Items.Add("在文件夹中显示", null, delegate { ShowInFolder(it); });
            }
            if ((it.IsImage || it.IsImageFile) && Ocr.Available)
            {
                string imgPath = it.IsImage ? it.FilePath : it.Paths[0];
                m.Items.Add("识别图片里的文字并粘贴", null, delegate { OcrImage(imgPath, true); });
            }

            // 单张卡片的标题颜色
            ToolStripMenuItem colorMenu = new ToolStripMenuItem("标题颜色");
            string[] cnames = new string[] { "蓝", "紫", "绿", "橙", "红", "粉", "青", "石墨", "黑" };
            Color[] ccols = new Color[]
            {
                Color.FromArgb(0, 122, 255), Color.FromArgb(124, 92, 255), Color.FromArgb(40, 167, 69),
                Color.FromArgb(240, 120, 0), Color.FromArgb(220, 53, 69), Color.FromArgb(224, 60, 138),
                Color.FromArgb(23, 162, 184), Color.FromArgb(88, 96, 110), Color.FromArgb(28, 30, 34)
            };
            for (int ci = 0; ci < ccols.Length; ci++)
            {
                Color cc = ccols[ci];
                colorMenu.DropDownItems.Add(cnames[ci], Swatch(cc), delegate { SetCardColor(it, cc.ToArgb()); });
            }
            colorMenu.DropDownItems.Add(new ToolStripSeparator());
            colorMenu.DropDownItems.Add("自定义颜色…", null, delegate { PickCardColor(it); });
            colorMenu.DropDownItems.Add("恢复默认", null, delegate { SetCardColor(it, 0); });
            m.Items.Add(colorMenu);

            // 不再记录这个程序复制的内容(加进设置里的忽略名单)
            string exe = null;
            try { if (!string.IsNullOrEmpty(it.AppPath)) exe = System.IO.Path.GetFileNameWithoutExtension(it.AppPath); } catch { exe = null; }
            if (!string.IsNullOrEmpty(exe) && !Util.IsRelay(exe) &&
                !string.Equals(exe, System.IO.Path.GetFileNameWithoutExtension(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase))
            {
                string shownName = string.IsNullOrEmpty(it.AppName) ? exe : it.AppName;
                if (shownName.Length > 16) shownName = shownName.Substring(0, 15) + "…";
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add("不再记录「" + shownName + "」的内容", null, delegate { IgnoreApp(exe, shownName); });
            }

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("删除", null, delegate { DeleteSelected(); });

            Theme.StyleMenu(m);
            menuOpen = true;
            m.Closed += delegate
            {
                menuOpen = false;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (Visible && !hiding && !dialogOpen && Form.ActiveForm != this) HideShelf();
                }));
            };
            m.Show(screenPt);
        }

        // 把卡片直接拖到别的程序里(文字、图片、文件都可以)
        void StartDrag(int i)
        {
            if (i < 0 || i >= shown.Count) return;
            Item it = shown[i];
            DataObject d = new DataObject();
            try
            {
                if (it.IsImage)
                {
                    d.SetImage(Util.LoadBitmap(it.FilePath));
                    StringCollection sc = new StringCollection();
                    sc.Add(it.FilePath);
                    d.SetFileDropList(sc);
                }
                else if (it.IsFiles)
                {
                    StringCollection sc = new StringCollection();
                    foreach (string f in it.Paths)
                        if (File.Exists(f) || Directory.Exists(f)) sc.Add(f);
                    if (sc.Count == 0) return;
                    d.SetFileDropList(sc);
                }
                else
                {
                    d.SetText(it.Text);
                }
            }
            catch { return; }
            dragging = true;
            try { strip.DoDragDrop(d, DragDropEffects.Copy); }
            catch { }
            dragging = false;
        }

        // 提前创建窗口和所有子控件(只是建好,不显示)
        // 启动后在后台把第一次打开要用到的代码都先跑一遍(毛玻璃、卡片绘制、列表、搜索),
        // 这样第一次按快捷键和之后一样快
        public void Prewarm()
        {
            CreateHandles(this);
            IntPtr q = quick.Handle;
            if (Visible) return;
            try
            {
                Bitmap bl = Frost.CaptureBlur(new Rectangle(0, 0, 96, 48));
                if (bl != null)
                {
                    Frost.Legible(bl, 35);
                    using (Graphics g = Graphics.FromImage(bl)) Frost.Tint(g, new Rectangle(0, 0, 96, 48), 35, 0, 1f);
                    bl.Dispose();
                }
                bool oldSuppress = suppress;
                suppress = false;
                IClipView keep = strip;
                foreach (IClipView v in new IClipView[] { cardView, listView })
                {
                    strip = v;
                    Reload(2);
                    Reload(1);   // 打字搜索走的是这条路
                    Control c = (Control)v;
                    if (c.Width < 10 || c.Height < 10) c.Size = new Size(Gfx.S(480), Gfx.S(480));   // 还没排过版的视图先给个大小
                    if (c.Width > 0 && c.Height > 0)
                        using (Bitmap off = new Bitmap(c.Width, Math.Min(c.Height, Gfx.S(400))))
                            c.DrawToBitmap(off, new Rectangle(0, 0, off.Width, off.Height));
                }
                strip = keep;
                sb.Box.Text = "a";   // 搜索框的输入处理也先跑一遍
                sb.Box.Text = "";
                suppress = oldSuppress;
            }
            catch { }
        }

        static void CreateHandles(Control c)
        {
            IntPtr h = c.Handle;
            foreach (Control child in c.Controls) CreateHandles(child);
        }

        // ================= 文件夹模式 =================
        public void ShowFiles()
        {
            if (!Visible || hiding)
            {
                Config.LastFilesMode = true;   // 直接按文件夹的大小打开,不用先开剪切板再切换
                ShowAt();
                return;
            }
            if (!filesMode) SetMode(true);
            BringFrontIfHidden();
        }

        void SetMode(bool files)
        {
            filesMode = files;
            if (Config.LastFilesMode != files) { Config.LastFilesMode = files; Config.Save(); }
            modeSwitch.Selected = files ? 1 : 0;
            modeSwitch.Invalidate();
            quick.HideLook();
            tabs.Visible = !files;
            folderTabs.Visible = files;
            fileView.Visible = files;
            cardView.Visible = !files && !listMode;
            listView.Visible = !files && listMode;
            suppress = true;
            sb.Box.Text = "";
            suppress = false;
            sb.SetCue(files ? "搜索当前文件夹…" : "直接打字即可搜索…");
            if (files)
            {
                BuildFolderTabs(folderIdx < 0 ? 0 : folderIdx);
                OpenFolderTab();
                fileView.Focus();
            }
            else
            {
                Reload(1);
                strip.Focus();
            }
            ApplyWindowStyle();
            SwitchBounds();
            Invalidate();
        }

        void BuildFolderTabs(int select)
        {
            List<string> labels = FolderStore.Items.Select(f => FolderStore.NameOf(f.Path)).ToList();
            labels.Add("＋");
            folderTabs.SetLabels(labels.ToArray());
            folderIdx = FolderStore.Items.Count == 0 ? -1 : Math.Max(0, Math.Min(FolderStore.Items.Count - 1, select));
            folderTabs.Selected = folderIdx;
            folderTabs.Invalidate();
            if (!listMode) OnResize(EventArgs.Empty);
        }

        void OpenFolderTab()
        {
            if (folderIdx < 0) fileView.SetRoot(null, 0);
            else
            {
                FolderStore.Folder f = FolderStore.Items[folderIdx];
                fileView.SetRoot(f.Path, f.View);
            }
            string q = sb.Box.Text.Trim();
            if (q.Length > 0) fileView.Filter(q);
        }

        void OnFolderTab()
        {
            if (folderTabs.Selected == folderTabs.Labels.Length - 1)
            {
                // 点了"＋"
                folderTabs.Selected = folderIdx;
                folderTabs.Invalidate();
                AddFolderDialog();
                return;
            }
            folderIdx = folderTabs.Selected;
            OpenFolderTab();
            fileView.Focus();
        }

        void SelectFolder(int i)
        {
            if (i < 0 || i >= FolderStore.Items.Count) return;
            folderIdx = i;
            folderTabs.Selected = i;
            folderTabs.Invalidate();
            OpenFolderTab();
            fileView.Focus();
        }

        void AddFolderDialog()
        {
            dialogOpen = true;
            string chosen = null;
            try
            {
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = "选择一个常用文件夹";
                    d.ShowNewFolderButton = false;
                    if (d.ShowDialog(this) == DialogResult.OK) chosen = d.SelectedPath;
                }
            }
            finally { dialogOpen = false; }
            Activate();
            if (chosen != null) AddFolders(new string[] { chosen });
            else fileView.Focus();
        }

        void AddFolders(IEnumerable<string> paths)
        {
            int last = -1;
            foreach (string p in paths)
                if (Directory.Exists(p)) last = FolderStore.Add(p);
            if (last < 0) return;
            folderIdx = last;
            if (!filesMode) SetMode(true);
            else
            {
                BuildFolderTabs(last);
                OpenFolderTab();
                fileView.Focus();
            }
        }

        void OnFolderTabRightClick(int i, Point pt)
        {
            if (i < 0 || i >= FolderStore.Items.Count) return;
            FolderStore.Folder f = FolderStore.Items[i];
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("在资源管理器中打开", null, delegate
            {
                try { Process.Start("explorer.exe", "\"" + f.Path + "\""); } catch { }
            });
            string[] viewNames = new string[] { "小图", "中图", "大图", "列表" };
            int[] viewModes = new int[] { 2, 0, 3, 1 };
            for (int vk = 0; vk < 4; vk++)
            {
                int mode = viewModes[vk];
                ToolStripMenuItem vi = new ToolStripMenuItem(viewNames[vk], null, delegate
                {
                    f.View = mode;
                    FolderStore.Save();
                    if (i == folderIdx) OpenFolderTab();
                });
                vi.Checked = f.View == mode;
                m.Items.Add(vi);
            }
            m.Items.Add(new ToolStripSeparator());
            if (i > 0) m.Items.Add("左移", null, delegate { MoveFolder(i, i - 1); });
            if (i < FolderStore.Items.Count - 1) m.Items.Add("右移", null, delegate { MoveFolder(i, i + 1); });
            m.Items.Add("从这里移除(不会删除文件夹)", null, delegate
            {
                FolderStore.Remove(i);
                BuildFolderTabs(Math.Min(i, FolderStore.Items.Count - 1));
                OpenFolderTab();
            });
            Theme.StyleMenu(m);
            menuOpen = true;
            m.Closed += delegate { menuOpen = false; };
            m.Show(pt);
        }

        void MoveFolder(int from, int to)
        {
            FolderStore.Folder f = FolderStore.Items[from];
            FolderStore.Items.RemoveAt(from);
            FolderStore.Items.Insert(to, f);
            FolderStore.Save();
            BuildFolderTabs(to);
            OpenFolderTab();
        }

        List<string> FoldersIn(IDataObject d)
        {
            try
            {
                if (d == null || !d.GetDataPresent(DataFormats.FileDrop)) return new List<string>();
                string[] arr = d.GetData(DataFormats.FileDrop) as string[];
                return arr == null ? new List<string>() : arr.Where(x => Directory.Exists(x)).ToList();
            }
            catch { return new List<string>(); }
        }

        void OnFolderDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = (!dragging && FoldersIn(e.Data).Count > 0) ? DragDropEffects.Link : DragDropEffects.None;
        }

        void OnFolderDrop(object sender, DragEventArgs e)
        {
            List<string> f = FoldersIn(e.Data);
            if (f.Count == 0 || dragging) return;
            Activate();
            AddFolders(f);
        }

        void OpenFile(string path)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(path);
                psi.UseShellExecute = true;
                Process.Start(psi);
                // 没钉住的文件夹窗口就像普通窗口一样留着;其他情况打开后收起
                if (!(FolderWindow && !Config.FolderPinned)) HideShelf();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开:" + ex.Message, Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ShowFileQuick()
        {
            FileEntry e = fileView.FocusedEntry;
            if (e == null) { quick.HideLook(); return; }
            quick.ShowFile(e.Path, Bounds, listMode);
        }

        void CopyFiles(List<string> paths)
        {
            StringCollection sc = new StringCollection();
            foreach (string p in paths) if (File.Exists(p) || Directory.Exists(p)) sc.Add(p);
            if (sc.Count == 0) return;
            try { Util.Retry(delegate { Clipboard.SetFileDropList(sc); }); } catch { }
        }

        void ShowFileMenu(List<string> paths, Point pt)
        {
            if (paths.Count == 0) return;
            string first = paths[0];
            bool single = paths.Count == 1;
            bool isDir = single && Directory.Exists(first);
            ContextMenuStrip m = new ContextMenuStrip();
            if (single)
            {
                m.Items.Add("打开", null, delegate { if (isDir) fileView.Navigate(first); else OpenFile(first); });
                if (!isDir)
                    m.Items.Add("打开方式…", null, delegate
                    {
                        try { Process.Start("rundll32.exe", "shell32.dll,OpenAs_RunDLL " + first); } catch { }
                    });
                m.Items.Add("快速预览(空格)", null, delegate { ShowFileQuick(); });
                if (!isDir && Ocr.Available && OcrExts.Contains(System.IO.Path.GetExtension(first)))
                    m.Items.Add("识别图片里的文字", null, delegate { OcrImage(first, false); });
                m.Items.Add(new ToolStripSeparator());
            }
            m.Items.Add(single ? "复制" : "复制 " + paths.Count + " 项", null, delegate { CopyFiles(paths); });
            List<string> shareable = paths.Where(x => File.Exists(x)).Take(20).ToList();
            if (shareable.Count > 0)
                m.Items.Add("扫码带走(手机)", null, delegate
                {
                    ShareToPhone(shareable.Count == 1 ? System.IO.Path.GetFileName(shareable[0]) : shareable.Count + " 个文件", null, shareable);
                });
            m.Items.Add("复制路径", null, delegate
            {
                try
                {
                    string txt = string.Join("\r\n", paths.ToArray());
                    Util.Retry(delegate { Clipboard.SetText(txt); });
                }
                catch { }
            });
            m.Items.Add("在资源管理器中显示", null, delegate
            {
                try { Process.Start("explorer.exe", "/select,\"" + first + "\""); } catch { }
            });
            if (isDir)
            {
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add("添加为常用文件夹", null, delegate { AddFolders(new string[] { first }); });
            }
            Theme.StyleMenu(m);
            menuOpen = true;
            m.Closed += delegate { menuOpen = false; };
            m.Show(pt);
        }

        bool HandleFileKey(ref Message msg, Keys keyData)
        {
            bool inSearch = sb.Box.Focused;
            Keys real = keyData;
            if ((keyData & Keys.KeyCode) == Keys.ProcessKey)
            {
                uint scan = (uint)((msg.LParam.ToInt64() >> 16) & 0xFF);
                uint vk = Native.MapVirtualKey(scan, 1);
                if (vk != 0) real = (Keys)vk | (keyData & Keys.Modifiers);
            }
            if (real == Keys.Space && (!inSearch || sb.Box.Text.Length == 0))
            {
                TogglePreview();
                return true;
            }
            // 在文件上直接打字 = 搜索当前文件夹
            if (!inSearch && (real & (Keys.Control | Keys.Alt)) == 0)
            {
                Keys ck = real & Keys.KeyCode;
                if ((ck >= Keys.A && ck <= Keys.Z) || (ck >= Keys.D0 && ck <= Keys.D9) ||
                    (ck >= Keys.NumPad0 && ck <= Keys.NumPad9))
                {
                    sb.Box.Focus();
                    Native.keybd_event((byte)ck, 0, 0, UIntPtr.Zero);
                    Native.keybd_event((byte)ck, 0, 2, UIntPtr.Zero);
                    return true;
                }
            }
            switch (keyData)
            {
                case Keys.F | Keys.Control:
                    sb.Box.Focus();
                    sb.Box.SelectAll();
                    return true;
                case Keys.Escape:
                    if (quick.Visible) quick.HideLook();
                    else if (sb.Box.Text.Length > 0) { sb.Box.Text = ""; fileView.Focus(); }
                    else if (inSearch) fileView.Focus();
                    else HideShelf();
                    return true;
                case Keys.Left:
                    if (inSearch) return false;
                    fileView.MoveSel(-1, 0);
                    return true;
                case Keys.Right:
                    if (inSearch) return false;
                    fileView.MoveSel(1, 0);
                    return true;
                case Keys.Up: fileView.Focus(); fileView.MoveSel(0, -1); return true;
                case Keys.Down: fileView.Focus(); fileView.MoveSel(0, 1); return true;
                case Keys.Enter: fileView.OpenSelected(); return true;
                case Keys.Back:
                    if (inSearch && sb.Box.Text.Length > 0) return false;
                    fileView.GoUp();
                    return true;
                case Keys.Left | Keys.Alt: fileView.GoUp(); return true;
                case Keys.A | Keys.Control:
                    if (inSearch) return false;
                    fileView.SelectAll();
                    return true;
                case Keys.C | Keys.Control:
                    if (inSearch) return false;
                    CopyFiles(fileView.SelectedPaths());
                    return true;
                case Keys.F5: fileView.Reload(); return true;
            }
            if ((keyData & Keys.Modifiers) == Keys.Control)
            {
                Keys k = keyData & Keys.KeyCode;
                if (k >= Keys.D1 && k <= Keys.D9) { SelectFolder((int)k - (int)Keys.D1); return true; }
            }
            return false;
        }

        // ---------------- 自定义分类
        void BuildTabs(int select)
        {
            List<string> labels = new List<string>(new string[] { "全部", "置顶", "文本", "图片", "链接", "文件" });
            List<Color> dots = new List<Color>();
            for (int k = 0; k < BaseTabs; k++)
                dots.Add(Config.TabDotColors[k] != 0 ? Color.FromArgb(Config.TabDotColors[k]) : Color.Empty);
            foreach (Board bd in hist.Boards)
            {
                labels.Add(bd.Name);
                dots.Add(Color.FromArgb(bd.Color));
            }
            labels.Add("＋");
            dots.Add(Color.Empty);
            tabs.SetLabels(labels.ToArray());
            tabs.Dots = dots.ToArray();
            tabs.Selected = Math.Max(0, Math.Min(labels.Count - 2, select));
            lastTab = tabs.Selected;
            tabs.Invalidate();
            if (!listMode) OnResize(EventArgs.Empty);   // 标签多了少了:重新决定快捷键提示放不放得下
        }

        Board BoardAt(int tab)
        {
            int idx = tab - BaseTabs;
            return (idx >= 0 && idx < hist.Boards.Count) ? hist.Boards[idx] : null;
        }

        string AskText(string title, string prompt, string initial, ref int colorIndex, bool withColor)
        {
            dialogOpen = true;
            string result = null;
            try
            {
                using (InputDialog d = new InputDialog(title, prompt, initial, withColor, colorIndex))
                {
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        result = d.Value;
                        colorIndex = d.ColorIndex;
                    }
                }
            }
            finally { dialogOpen = false; }
            Activate();
            strip.Focus();
            return result;
        }

        void NewBoard(Item addItem)
        {
            int ci = hist.Boards.Count % Theme.Accents.Length;
            string name = AskText("新建分类", "分类名称(例如:常用回复、代码片段、地址)", "", ref ci, true);
            if (string.IsNullOrEmpty(name)) return;
            Board b = hist.AddBoard(name, Theme.Accents[ci].ToArgb());
            if (addItem != null) hist.SetBoard(addItem, b.Id);
            BuildTabs(BaseTabs + hist.Boards.Count - 1);
            Reload(1);
        }

        void EditBoard(Board b)
        {
            int ci = 0;
            for (int k = 0; k < Theme.Accents.Length; k++)
                if (Theme.Accents[k].ToArgb() == b.Color) ci = k;
            string name = AskText("编辑分类", "分类名称", b.Name, ref ci, true);
            if (string.IsNullOrEmpty(name)) return;
            hist.UpdateBoard(b, name, Theme.Accents[ci].ToArgb());
            BuildTabs(tabs.Selected);
            strip.Invalidate();
        }

        void DeleteBoard(Board b)
        {
            dialogOpen = true;
            DialogResult r;
            try
            {
                r = MessageBox.Show(this, "删除分类“" + b.Name + "”?\n里面的内容不会被删除,只是不再归类。", Brand.Name,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            }
            finally { dialogOpen = false; }
            Activate();
            if (r != DialogResult.Yes) return;
            hist.DeleteBoard(b);
            BuildTabs(0);
            Reload(1);
        }

        void AfterBoardChange()
        {
            if (tabs.Selected >= BaseTabs) Reload(0);
            else strip.Invalidate();
        }

        static readonly string[] DotNames = new string[] { "蓝", "紫", "绿", "橙", "红", "粉", "青", "黄", "石墨" };
        static readonly Color[] DotColors = new Color[]
        {
            Color.FromArgb(0, 122, 255), Color.FromArgb(124, 92, 255), Color.FromArgb(40, 167, 69),
            Color.FromArgb(240, 120, 0), Color.FromArgb(220, 53, 69), Color.FromArgb(224, 60, 138),
            Color.FromArgb(23, 162, 184), Color.FromArgb(255, 176, 0), Color.FromArgb(88, 96, 110)
        };

        // 右键任意分类标签:设置圆点颜色;自定义分类还能重命名、删除
        void OnTabRightClick(int i, Point pt)
        {
            if (i < 0 || i >= tabs.Labels.Length - 1) return;   // 最后的"+"没有菜单
            Board b = BoardAt(i);
            ContextMenuStrip m = new ContextMenuStrip();

            ToolStripMenuItem dot = new ToolStripMenuItem("圆点颜色");
            for (int ci = 0; ci < DotColors.Length; ci++)
            {
                Color cc = DotColors[ci];
                dot.DropDownItems.Add(DotNames[ci], Swatch(cc), delegate { SetTabDot(i, b, cc.ToArgb()); });
            }
            dot.DropDownItems.Add(new ToolStripSeparator());
            dot.DropDownItems.Add("自定义颜色…", null, delegate { PickTabDot(i, b); });
            if (b == null) dot.DropDownItems.Add("不显示圆点", null, delegate { SetTabDot(i, null, 0); });
            m.Items.Add(dot);

            if (b != null)
            {
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add("重命名…", null, delegate { EditBoard(b); });
                m.Items.Add("删除分类", null, delegate { DeleteBoard(b); });
            }

            Theme.StyleMenu(m);
            menuOpen = true;
            m.Closed += delegate
            {
                menuOpen = false;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (Visible && !hiding && !dialogOpen && Form.ActiveForm != this) HideShelf();
                }));
            };
            m.Show(pt);
        }

        // 能接收拖放的标签:置顶、自定义分类、最后的"+"(新建分类并放进去)
        bool CanDropOnTab(int ti)
        {
            if (ti < 0) return false;
            if (ti == 1) return true;
            if (ti == tabs.Labels.Length - 1) return true;
            return BoardAt(ti) != null;
        }

        void DropOnTab(Item it, int ti)
        {
            if (ti == 1)
            {
                if (!it.Pinned) hist.TogglePin(it);
                Reload(0);
                return;
            }
            if (ti == tabs.Labels.Length - 1) { NewBoard(it); return; }
            Board b = BoardAt(ti);
            if (b == null) return;
            hist.SetBoard(it, b.Id);
            AfterBoardChange();
        }

        void SetTabDot(int i, Board b, int argb)
        {
            if (b != null) hist.UpdateBoard(b, b.Name, argb);
            else if (i >= 0 && i < BaseTabs)
            {
                Config.TabDotColors[i] = argb;
                Config.Save();
            }
            BuildTabs(tabs.Selected);
            strip.Invalidate();   // 卡片底部的分类圆点也跟着变
        }

        void PickTabDot(int i, Board b)
        {
            dialogOpen = true;
            try
            {
                using (ColorDialog d = new ColorDialog())
                {
                    d.FullOpen = true;
                    if (b != null) d.Color = Color.FromArgb(b.Color);
                    else if (i < BaseTabs && Config.TabDotColors[i] != 0) d.Color = Color.FromArgb(Config.TabDotColors[i]);
                    if (d.ShowDialog(this) == DialogResult.OK) SetTabDot(i, b, d.Color.ToArgb());
                }
            }
            finally { dialogOpen = false; }
            Activate();
            strip.Focus();
        }

        void RenameCard(Item it)
        {
            int dummy = 0;
            string v = AskText("重命名卡片", "卡片标题(留空则恢复默认)", it.CustomTitle ?? "", ref dummy, false);
            if (v == null) return;
            hist.SetTitle(it, v);
            strip.Invalidate();
            if (quick.Visible) ShowQuick();
        }

        // ---------------- 拖拽排序
        void OnReordered(int from, int to)
        {
            if (from < 0 || from >= shown.Count || to < 0 || to >= shown.Count) return;
            Item it = shown[from];
            List<Item> tmp = new List<Item>(shown);
            tmp.RemoveAt(from);
            tmp.Insert(to, it);
            Item next = to + 1 < tmp.Count ? tmp[to + 1] : null;
            Item prev = to - 1 >= 0 ? tmp[to - 1] : null;
            hist.MoveNear(it, prev, next);
            shown = tmp;
            strip.SetItems(shown, 0);
            strip.Selected = to;
        }

        // ---------------- 卡片标题颜色
        static Bitmap Swatch(Color c)
        {
            int sz = Gfx.S(14);
            Bitmap b = new Bitmap(sz, sz);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush br = new SolidBrush(c)) g.FillEllipse(br, 0, 0, sz - 1, sz - 1);
            }
            return b;
        }

        void SetCardColor(Item it, int argb)
        {
            hist.SetColor(it, argb);
            strip.Invalidate();
            if (quick.Visible) ShowQuick();
        }

        void PickCardColor(Item it)
        {
            dialogOpen = true;
            try
            {
                using (ColorDialog d = new ColorDialog())
                {
                    d.FullOpen = true;
                    d.Color = CardLook.Header(it);
                    if (d.ShowDialog(this) == DialogResult.OK) SetCardColor(it, d.Color.ToArgb());
                }
            }
            finally { dialogOpen = false; }
            Activate();
            strip.Focus();
        }

        // ---------------- 缩放:按住顶部小横条上下拖动 / Ctrl+滚轮 / Ctrl+加减号
        int GripH { get { return Gfx.S(14); } }

        void SetZoom(float z, bool finish)
        {
            if (listMode) return;
            z = Math.Max(0.7f, Math.Min(1.6f, z));
            if (Math.Abs(z - Config.CardZoom) >= 0.005f)
            {
                Config.CardZoom = z;
                int bottom = Bottom;
                int h = ShelfMetrics.ShelfH;
                SetBounds(Left, bottom - h, Width, h);   // 底边不动,往上长高
                ComposeBackdrop();                       // 顶部高光跟着面板顶边走
                strip.Relayout();
                Invalidate();
                if (quick.Visible) ShowQuick();
            }
            if (finish) FinishZoom();
        }

        void FinishZoom()
        {
            Config.Save();
            foreach (Item it in hist.Items) it.InvalidatePreview();
            strip.Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (toastText != null && e.Button == MouseButtons.Left && toastBtn.Contains(e.Location)) { UndoLast(); return; }
            if (BeginWindowDrag(e)) return;
            if (listMode || e.Button != MouseButtons.Left || e.Y >= GripH) return;
            if (e.Clicks >= 2) { SetZoom(1f, true); return; }   // 双击小横条恢复默认大小
            resizing = true;
            resizeStartY = Cursor.Position.Y;
            resizeStartH = Height;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (DoWindowDrag()) return;
            if (toastText != null && toastBtn.Contains(e.Location)) { Cursor = Cursors.Hand; return; }
            if (FolderWindow) { Cursor = CursorFor(EdgeAt(e.Location)); return; }
            if (resizing)
            {
                int newH = resizeStartH + (resizeStartY - Cursor.Position.Y);
                float z = (newH - Gfx.S(100)) / (float)Gfx.S(236);
                SetZoom(z, false);
                return;
            }
            Cursor = (!listMode && e.Y < GripH) ? Cursors.SizeNS : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (EndWindowDrag()) return;
            if (resizing)
            {
                resizing = false;
                FinishZoom();
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!resizing && dragEdge < 0) Cursor = Cursors.Default;
        }

        // 空格:中文输入法开着时,空格键会变成"输入法处理键",这里把它认回来;
        // 但如果输入法正在拼字(有候选词),空格是用来选字的,不拦截
        bool IsSpaceKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space) return true;
            if ((keyData & Keys.KeyCode) != Keys.ProcessKey || (keyData & Keys.Modifiers) != 0) return false;
            try
            {
                if (Native.ImmGetVirtualKey(msg.HWnd) != 0x20) return false;
                IntPtr ctx = Native.ImmGetContext(msg.HWnd);
                if (ctx == IntPtr.Zero) return true;
                int len = Native.ImmGetCompositionStringW(ctx, 0x8, null, 0);   // GCS_COMPSTR
                Native.ImmReleaseContext(msg.HWnd, ctx);
                return len <= 0;
            }
            catch { return false; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Tab | Keys.Control) || keyData == (Keys.Tab | Keys.Control | Keys.Shift))
            {
                SetMode(!filesMode);
                return true;
            }
            if (filesMode) return HandleFileKey(ref msg, keyData) || base.ProcessCmdKey(ref msg, keyData);
            bool onCards = !sb.Box.Focused;
            // 输入法开着时按键会变成"输入法处理键",用扫描码把真正按下的键认回来
            Keys real = keyData;
            if ((keyData & Keys.KeyCode) == Keys.ProcessKey)
            {
                uint scan = (uint)((msg.LParam.ToInt64() >> 16) & 0xFF);
                uint vk = Native.MapVirtualKey(scan, 1);   // MAPVK_VSC_TO_VK
                if (vk != 0) real = (Keys)vk | (keyData & Keys.Modifiers);
            }
            // Ctrl+Z:撤销刚才的删除 / 屏蔽(搜索框里有字时,留给搜索框自己撤销输入)
            if (real == (Keys.Z | Keys.Control) && (onCards || sb.Box.Text.Length == 0) && UndoLast()) return true;
            // 空格 = 快速预览(焦点在卡片上时)
            if (onCards && real == Keys.Space)
            {
                TogglePreview();
                return true;
            }
            if (!onCards && keyData == Keys.Space && sb.Box.Text.Length == 0)
            {
                TogglePreview();
                return true;
            }
            // 在卡片上直接打字:跳到搜索框,并把这个键重新交给输入法,中文也能正常拼
            if (onCards && (real & (Keys.Control | Keys.Alt)) == 0)
            {
                Keys ck = real & Keys.KeyCode;
                if ((ck >= Keys.A && ck <= Keys.Z) || (ck >= Keys.D0 && ck <= Keys.D9) ||
                    (ck >= Keys.NumPad0 && ck <= Keys.NumPad9))
                {
                    sb.Box.Focus();
                    Native.keybd_event((byte)ck, 0, 0, UIntPtr.Zero);
                    Native.keybd_event((byte)ck, 0, 2, UIntPtr.Zero);
                    return true;
                }
            }
            if (keyData == (Keys.F | Keys.Control))
            {
                sb.Box.Focus();
                sb.Box.SelectAll();
                return true;
            }
            switch (keyData)
            {
                case Keys.Oemplus | Keys.Control:
                case Keys.Add | Keys.Control:
                    SetZoom(Config.CardZoom + 0.1f, true);
                    return true;
                case Keys.OemMinus | Keys.Control:
                case Keys.Subtract | Keys.Control:
                    SetZoom(Config.CardZoom - 0.1f, true);
                    return true;
                case Keys.D0 | Keys.Control:
                    SetZoom(1f, true);
                    return true;
                case Keys.Escape:
                    if (quick.Visible) quick.HideLook();
                    else if (sb.Box.Text.Length > 0) { sb.Box.Text = ""; strip.Focus(); }
                    else if (sb.Box.Focused) strip.Focus();
                    else HideShelf();
                    return true;
                case Keys.Left: strip.Focus(); if (!listMode) strip.Move(-1); return true;
                case Keys.Right: strip.Focus(); if (!listMode) strip.Move(1); return true;
                case Keys.Up: strip.Focus(); if (listMode) strip.Move(-1); return true;
                case Keys.Down: strip.Focus(); if (listMode) strip.Move(1); return true;
                case Keys.PageUp: strip.Focus(); strip.Move(listMode ? -6 : -4); return true;
                case Keys.PageDown: strip.Focus(); strip.Move(listMode ? 6 : 4); return true;
                case Keys.Home | Keys.Control: strip.Selected = 0; return true;
                case Keys.End | Keys.Control: strip.Selected = shown.Count - 1; return true;
                case Keys.Enter: Choose(strip.Selected, true); return true;
                case Keys.Enter | Keys.Shift: Choose(strip.Selected, false); return true;
                case Keys.Tab: CycleTab(1); return true;
                case Keys.Tab | Keys.Shift: CycleTab(-1); return true;
                case Keys.Delete | Keys.Control: DeleteSelected(); return true;
                case Keys.P | Keys.Control: TogglePin(); return true;
                case Keys.O | Keys.Control:
                    {
                        Item it = Current();
                        if (it != null && !it.IsImage && !it.IsFiles && it.Kind == Item.KUrl) OpenLink(it);
                        return true;
                    }
            }
            if ((keyData & Keys.Control) == Keys.Control)
            {
                Keys k = keyData & Keys.KeyCode;
                if (k >= Keys.D1 && k <= Keys.D9)
                {
                    Choose((int)k - (int)Keys.D1, true);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // ================================================================ 自动更新(从 GitHub Releases 检查新版本)
    // 发布新版:在 github.com/berlin955/Glean 建一个 Release,标签写版本号(如 v4.1),附件放 Glean-Setup.exe。
    // 软件读取"最新发布"的版本号,比自己新就提示;下载安装包后核对 GitHub 提供的 SHA-256 校验值,再静默安装。
    class UpdateInfo
    {
        public Version Ver;
        public string Tag, Notes, Url, Sha256, PageUrl;
        public long Size;
    }

    // ================================================================ 二维码(手机扫码带走)
    // 按二维码标准(ISO 18004)自己生成:字节模式、纠错等级 M、版本 1~40 自动选择
    static class QrCode
    {
        static readonly int[] EccPerBlockM = { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 };
        static readonly int[] BlocksM = { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 };

        static int RawModules(int ver)
        {
            int r = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int na = ver / 7 + 2;
                r -= (25 * na - 10) * na - 55;
                if (ver >= 7) r -= 36;
            }
            return r;
        }

        static int DataCodewords(int ver) { return RawModules(ver) / 8 - EccPerBlockM[ver] * BlocksM[ver]; }

        // 最多能装多少字节(UTF-8)
        public static int MaxBytes { get { return (DataCodewords(40) * 8 - 4 - 16) / 8; } }
        public static int LastMask;   // 最后一次选用的掩码

        // 返回 true/false 矩阵(true = 深色),装不下返回 null
        public static bool[,] Encode(byte[] data)
        {
            // 有非英文字符时,先写一个"内容是 UTF-8"的标记(ECI 26),扫码软件就不用猜编码,中文不会乱码
            bool eci = false;
            foreach (byte b0 in data) if (b0 >= 0x80) { eci = true; break; }
            int ver = 0;
            for (int v = 1; v <= 40; v++)
            {
                int cc = v < 10 ? 8 : 16;
                if ((eci ? 12 : 0) + 4 + cc + data.Length * 8 <= DataCodewords(v) * 8) { ver = v; break; }
            }
            if (ver == 0) return null;

            // 1) 数据比特
            List<bool> bits = new List<bool>();
            Action<int, int> put = delegate(int val, int n) { for (int i = n - 1; i >= 0; i--) bits.Add(((val >> i) & 1) != 0); };
            if (eci) { put(7, 4); put(26, 8); }
            put(4, 4);
            put(data.Length, ver < 10 ? 8 : 16);
            foreach (byte b in data) put(b, 8);
            int cap = DataCodewords(ver) * 8;
            put(0, Math.Min(4, cap - bits.Count));
            while (bits.Count % 8 != 0) bits.Add(false);
            byte[] dataCw = new byte[DataCodewords(ver)];
            for (int i = 0; i < bits.Count / 8; i++)
            {
                int v = 0;
                for (int j = 0; j < 8; j++) v = (v << 1) | (bits[i * 8 + j] ? 1 : 0);
                dataCw[i] = (byte)v;
            }
            for (int i = bits.Count / 8, k = 0; i < dataCw.Length; i++, k++) dataCw[i] = (byte)(k % 2 == 0 ? 0xEC : 0x11);

            // 2) 分块 + 纠错码 + 交织
            int numBlocks = BlocksM[ver], eccLen = EccPerBlockM[ver];
            int raw = RawModules(ver) / 8;
            int numShort = numBlocks - raw % numBlocks;
            int shortLen = raw / numBlocks;
            byte[] div = RsDivisor(eccLen);
            List<byte[]> blocks = new List<byte[]>();
            for (int i = 0, k = 0; i < numBlocks; i++)
            {
                int len = shortLen - eccLen + (i < numShort ? 0 : 1);
                byte[] dat = new byte[len];
                Array.Copy(dataCw, k, dat, 0, len);
                k += len;
                byte[] ecc = RsRemainder(dat, div);
                byte[] blk = new byte[shortLen + 1];
                Array.Copy(dat, 0, blk, 0, len);
                if (i < numShort) { Array.Copy(ecc, 0, blk, len + 1, eccLen); }
                else Array.Copy(ecc, 0, blk, len, eccLen);
                blocks.Add(blk);
            }
            List<byte> all = new List<byte>();
            for (int i = 0; i < shortLen + 1; i++)
                for (int j = 0; j < numBlocks; j++)
                    if (i != shortLen - eccLen || j >= numShort) all.Add(blocks[j][i]);

            // 3) 画图形
            int size = ver * 4 + 17;
            bool[,] mod = new bool[size, size];   // [y, x]
            bool[,] fn = new bool[size, size];
            Action<int, int, bool> set = delegate(int x, int y, bool d) { mod[y, x] = d; fn[y, x] = true; };
            for (int i = 0; i < size; i++) { set(6, i, i % 2 == 0); set(i, 6, i % 2 == 0); }
            Action<int, int> finder = delegate(int cx, int cy)
            {
                for (int dy = -4; dy <= 4; dy++)
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        int xx = cx + dx, yy = cy + dy;
                        if (xx >= 0 && xx < size && yy >= 0 && yy < size) set(xx, yy, dist != 2 && dist != 4);
                    }
            };
            finder(3, 3); finder(size - 4, 3); finder(3, size - 4);
            int[] ap = AlignPositions(ver, size);
            for (int i = 0; i < ap.Length; i++)
                for (int j = 0; j < ap.Length; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == ap.Length - 1) || (i == ap.Length - 1 && j == 0)) continue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            set(ap[i] + dx, ap[j] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
            DrawFormat(mod, fn, size, 0);
            if (ver >= 7)
            {
                int rem = ver;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int vb = ver << 12 | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool bit = ((vb >> i) & 1) != 0;
                    int a = size - 11 + i % 3, b = i / 3;
                    set(a, b, bit);
                    set(b, a, bit);
                }
            }

            // 4) 放数据(之字形)
            int bi = 0;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool up = ((right + 1) & 2) == 0;
                        int y = up ? size - 1 - vert : vert;
                        if (!fn[y, x] && bi < all.Count * 8)
                        {
                            mod[y, x] = ((all[bi >> 3] >> (7 - (bi & 7))) & 1) != 0;
                            bi++;
                        }
                    }
            }

            // 5) 选一个最容易扫的掩码
            int best = 0, bestScore = int.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                ApplyMask(mod, fn, size, m);
                DrawFormat(mod, fn, size, m);
                int sc = Penalty(mod, size);
                if (sc < bestScore) { bestScore = sc; best = m; }
                ApplyMask(mod, fn, size, m);   // 再异或一次 = 还原
            }
            ApplyMask(mod, fn, size, best);
            DrawFormat(mod, fn, size, best);
            LastMask = best;
            return mod;
        }

        static int[] AlignPositions(int ver, int size)
        {
            if (ver == 1) return new int[0];
            int na = ver / 7 + 2;
            int step = ver == 32 ? 26 : (ver * 4 + na * 2 + 1) / (na * 2 - 2) * 2;
            int[] r = new int[na];
            r[0] = 6;
            for (int i = na - 1, pos = size - 7; i >= 1; i--, pos -= step) r[i] = pos;
            return r;
        }

        static void DrawFormat(bool[,] mod, bool[,] fn, int size, int mask)
        {
            int data = 0 << 3 | mask;   // 纠错等级 M 的格式位是 00
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;
            Action<int, int, bool> set = delegate(int x, int y, bool d) { mod[y, x] = d; fn[y, x] = true; };
            Func<int, bool> bit = delegate(int i) { return ((bits >> i) & 1) != 0; };
            for (int i = 0; i <= 5; i++) set(8, i, bit(i));
            set(8, 7, bit(6));
            set(8, 8, bit(7));
            set(7, 8, bit(8));
            for (int i = 9; i < 15; i++) set(14 - i, 8, bit(i));
            for (int i = 0; i < 8; i++) set(size - 1 - i, 8, bit(i));
            for (int i = 8; i < 15; i++) set(8, size - 15 + i, bit(i));
            set(8, size - 8, true);
        }

        static void ApplyMask(bool[,] mod, bool[,] fn, int size, int m)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (fn[y, x]) continue;
                    bool inv;
                    switch (m)
                    {
                        case 0: inv = (x + y) % 2 == 0; break;
                        case 1: inv = y % 2 == 0; break;
                        case 2: inv = x % 3 == 0; break;
                        case 3: inv = (x + y) % 3 == 0; break;
                        case 4: inv = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: inv = x * y % 2 + x * y % 3 == 0; break;
                        case 6: inv = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: inv = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (inv) mod[y, x] = !mod[y, x];
                }
        }

        // 简化的评分:连续同色、2×2 色块、深浅比例(都是越低越好)
        static int Penalty(bool[,] mod, int size)
        {
            int score = 0, dark = 0;
            for (int y = 0; y < size; y++)
            {
                int runX = 1, runY = 1;
                for (int x = 0; x < size; x++)
                {
                    if (mod[y, x]) dark++;
                    if (x > 0)
                    {
                        if (mod[y, x] == mod[y, x - 1]) { runX++; if (runX == 5) score += 3; else if (runX > 5) score++; } else runX = 1;
                        if (mod[x, y] == mod[x - 1, y]) { runY++; if (runY == 5) score += 3; else if (runY > 5) score++; } else runY = 1;
                    }
                    if (x > 0 && y > 0 && mod[y, x] == mod[y, x - 1] && mod[y, x] == mod[y - 1, x] && mod[y, x] == mod[y - 1, x - 1]) score += 3;
                }
            }
            int total = size * size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            score += Math.Max(0, k) * 10;
            return score;
        }

        static byte Mul(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        static byte[] RsDivisor(int degree)
        {
            byte[] r = new byte[degree];
            r[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < r.Length; j++)
                {
                    r[j] = Mul(r[j], root);
                    if (j + 1 < r.Length) r[j] ^= r[j + 1];
                }
                root = Mul(root, 0x02);
            }
            return r;
        }

        static byte[] RsRemainder(byte[] data, byte[] div)
        {
            byte[] r = new byte[div.Length];
            foreach (byte b in data)
            {
                int factor = b ^ r[0];
                Array.Copy(r, 1, r, 0, r.Length - 1);
                r[r.Length - 1] = 0;
                for (int i = 0; i < r.Length; i++) r[i] ^= Mul(div[i], factor);
            }
            return r;
        }

        // 画成图片:每个格子 cell 像素,四周留 4 格白边
        public static Bitmap Render(bool[,] mod, int cell, Color dark, Color light)
        {
            int n = mod.GetLength(0), q = 4;
            Bitmap b = new Bitmap((n + q * 2) * cell, (n + q * 2) * cell, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(light);
                using (SolidBrush br = new SolidBrush(dark))
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                            if (mod[y, x]) g.FillRectangle(br, (x + q) * cell, (y + q) * cell, cell, cell);
            }
            return b;
        }
    }

    // ================================================================ 局域网临时分享(图片、文件、长文字)
    // 只服务当前这一条内容:地址里带随机口令,窗口关掉或 10 分钟后自动停止
    static class ShareServer
    {
        class Entry { public string Name, Path, Mime; public long Size; }

        static System.Net.Sockets.TcpListener listener;
        static string token;
        static string pageTitle, pageText;
        static List<Entry> entries = new List<Entry>();
        static DateTime expires;
        static readonly object gate = new object();

        public static bool Running { get { return listener != null; } }

        // 找本机在局域网里的地址(优先有网关的 Wi-Fi / 有线网卡)
        public static string LanAddress()
        {
            string best = null;
            int bestScore = -1;
            try
            {
                foreach (System.Net.NetworkInformation.NetworkInterface ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;
                    string desc = (ni.Description + " " + ni.Name).ToLowerInvariant();
                    // 虚拟网卡(虚拟机、VPN 等)扣分但不排除:装了 Hyper-V 的电脑,真正的网卡也可能叫 vEthernet
                    bool virt = desc.Contains("virtual") || desc.Contains("vmware") || desc.Contains("vbox") || desc.Contains("wsl") ||
                                desc.Contains("tap-") || desc.Contains("vpn") || desc.Contains("zerotier") || desc.Contains("tailscale") ||
                                desc.Contains("default switch");
                    System.Net.NetworkInformation.IPInterfaceProperties p = ni.GetIPProperties();
                    bool gw = p.GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation ua in p.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                        byte[] a = ua.Address.GetAddressBytes();
                        bool priv = a[0] == 10 || (a[0] == 172 && a[1] >= 16 && a[1] <= 31) || (a[0] == 192 && a[1] == 168);
                        if (!priv) continue;
                        int score = (gw ? 8 : 0) - (virt ? 6 : 0) + (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ? 2 : 0) + (a[0] == 192 ? 1 : 0);
                        if (score > bestScore) { bestScore = score; best = ua.Address.ToString(); }
                    }
                }
            }
            catch { }
            return best;
        }

        static string Mime(string name)
        {
            switch ((System.IO.Path.GetExtension(name) ?? "").ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".webp": return "image/webp";
                case ".pdf": return "application/pdf";
                case ".txt": return "text/plain; charset=utf-8";
                case ".mp4": return "video/mp4";
                case ".mp3": return "audio/mpeg";
                default: return "application/octet-stream";
            }
        }

        // 开始分享。text 不为空时分享一段文字;否则分享文件。返回手机要打开的地址,失败返回 null
        public static string Start(string title, string text, IList<string> files, out string error)
        {
            error = null;
            Stop();
            string ip = LanAddress();
            if (ip == null) { error = "这台电脑没有连接局域网(Wi-Fi 或网线)"; return null; }
            lock (gate)
            {
                entries = new List<Entry>();
                pageTitle = title;
                pageText = text;
                if (files != null)
                    foreach (string f in files)
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(f);
                            if (!fi.Exists) continue;
                            Entry e = new Entry();
                            e.Path = fi.FullName;
                            e.Name = fi.Name;
                            // 剪切板历史里的截图文件名很长(时间_校验值.png),换成好认的名字
                            Match hm = Regex.Match(fi.Name, @"^(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})_\d{3}_[0-9a-f]+(_p)?(\.\w+)$");
                            if (hm.Success)
                                e.Name = "截图 " + hm.Groups[1].Value + "-" + hm.Groups[2].Value + "-" + hm.Groups[3].Value + " " +
                                         hm.Groups[4].Value + "." + hm.Groups[5].Value + "." + hm.Groups[6].Value + hm.Groups[8].Value;
                            e.Size = fi.Length;
                            e.Mime = Mime(fi.Name);
                            entries.Add(e);
                        }
                        catch { }
                    }
                if (text == null && entries.Count == 0) { error = "没有可以分享的内容(文件可能已被移动或删除)"; return null; }
                byte[] rnd = new byte[12];
                using (RandomNumberGenerator g = RandomNumberGenerator.Create()) g.GetBytes(rnd);
                token = BitConverter.ToString(rnd).Replace("-", "").ToLowerInvariant();
                expires = DateTime.Now.AddMinutes(10);
            }
            try
            {
                listener = new System.Net.Sockets.TcpListener(IPAddress.Any, 0);
                listener.Start();
            }
            catch (Exception ex)
            {
                listener = null;
                error = "无法开启分享:" + ex.Message;
                return null;
            }
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            System.Net.Sockets.TcpListener l = listener;
            Thread th = new Thread(delegate() { AcceptLoop(l); });
            th.IsBackground = true;
            th.Start();
            return "http://" + ip + ":" + port + "/" + token;
        }

        public static void Stop()
        {
            System.Net.Sockets.TcpListener l = listener;
            listener = null;
            if (l != null) { try { l.Stop(); } catch { } }
        }

        static void AcceptLoop(System.Net.Sockets.TcpListener l)
        {
            while (true)
            {
                System.Net.Sockets.TcpClient c;
                try { c = l.AcceptTcpClient(); }
                catch { return; }   // 已停止
                ThreadPool.QueueUserWorkItem(delegate { try { Serve(c); } catch { } finally { CloseGently(c); } });
            }
        }

        // 发完再关:先告诉对方"发完了",把对方还没读的数据读掉,再断开。
        // 直接关会让系统发出"重置",手机可能把已经收到的内容整个丢掉(下载显示 0 B)
        static void CloseGently(System.Net.Sockets.TcpClient c)
        {
            try
            {
                c.Client.Shutdown(System.Net.Sockets.SocketShutdown.Send);
                c.Client.ReceiveTimeout = 2000;
                byte[] sink = new byte[4096];
                DateTime until = DateTime.Now.AddSeconds(3);
                while (DateTime.Now < until && c.Client.Receive(sink) > 0) { }
            }
            catch { }
            try { c.Close(); } catch { }
        }

        static void Serve(System.Net.Sockets.TcpClient c)
        {
            c.NoDelay = true;
            c.ReceiveTimeout = 10000;
            c.SendTimeout = 60000;
            System.Net.Sockets.NetworkStream ns = c.GetStream();
            // 读请求头
            byte[] buf = new byte[8192];
            int got = 0;
            while (got < buf.Length)
            {
                int n = ns.Read(buf, got, buf.Length - got);
                if (n <= 0) break;
                got += n;
                if (Encoding.ASCII.GetString(buf, 0, got).Contains("\r\n\r\n")) break;
            }
            string head = Encoding.ASCII.GetString(buf, 0, got);
            int sp1 = head.IndexOf(' '), sp2 = sp1 < 0 ? -1 : head.IndexOf(' ', sp1 + 1);
            if (sp1 < 0 || sp2 < 0) return;
            string method = head.Substring(0, sp1);
            string path = head.Substring(sp1 + 1, sp2 - sp1 - 1);
            int q = path.IndexOf('?');
            string query = "";
            if (q >= 0) { query = path.Substring(q + 1); path = path.Substring(0, q); }
            bool headOnly = method == "HEAD";
            // 分段下载(手机的下载管理器常用):Range: bytes=开始-结束
            long rStart = -1, rEnd = -1;
            Match rm = Regex.Match(head, @"(?im)^Range:\s*bytes=(\d*)-(\d*)\s*$");
            if (rm.Success)
            {
                if (rm.Groups[1].Value.Length > 0) rStart = long.Parse(rm.Groups[1].Value);
                if (rm.Groups[2].Value.Length > 0) rEnd = long.Parse(rm.Groups[2].Value);
            }

            string tok;
            List<Entry> list;
            string title, text;
            DateTime exp;
            lock (gate) { tok = token; list = entries; title = pageTitle; text = pageText; exp = expires; }
            if (tok == null || !(path == "/" + tok || path.StartsWith("/" + tok + "/")))
            {
                Send(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not found"), null, headOnly);
                return;
            }
            if (DateTime.Now > exp)
            {
                Send(ns, 410, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Page("已失效", "<p class='sub'>这个分享已经过期。请在电脑上重新打开二维码。</p>")), null, headOnly);
                return;
            }
            string rest = path.Substring(tok.Length + 1).TrimStart('/');
            if (rest.Length == 0)
            {
                Send(ns, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(BuildPage(tok, title, text, list)), null, headOnly);
                return;
            }
            int idx;
            if (rest.StartsWith("f") && int.TryParse(rest.Substring(1), out idx) && idx >= 0 && idx < list.Count)
            {
                Entry e = list[idx];
                bool dl = query.Contains("dl=1");
                // 文件名:老的下载器只认 filename="英文名",新的认 filename*=UTF-8''中文名,两个都给
                string ext = System.IO.Path.GetExtension(e.Name);
                string ascii = Regex.Replace(System.IO.Path.GetFileNameWithoutExtension(e.Name), @"[^A-Za-z0-9._-]+", "_").Trim('_');
                if (ascii.Length == 0) ascii = "Glean";
                string disp = (dl ? "attachment" : "inline") + "; filename=\"" + ascii + ext + "\"; filename*=UTF-8''" + Uri.EscapeDataString(e.Name);
                using (FileStream fs = new FileStream(e.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long total = fs.Length;
                    long start = 0, end = total - 1;
                    string status = "200 OK", range = "";
                    if (rStart >= 0 || rEnd >= 0)
                    {
                        if (rStart < 0) { start = Math.Max(0, total - rEnd); end = total - 1; }   // bytes=-500:最后 500 字节
                        else { start = rStart; end = rEnd >= 0 ? Math.Min(rEnd, total - 1) : total - 1; }
                        if (start > end || start >= total)
                        {
                            Send(ns, 416, "text/plain", new byte[0], "Content-Range: bytes */" + total + "\r\n", headOnly);
                            return;
                        }
                        status = "206 Partial Content";
                        range = "Content-Range: bytes " + start + "-" + end + "/" + total + "\r\n";
                    }
                    long len = total == 0 ? 0 : end - start + 1;
                    string hdr = "HTTP/1.1 " + status + "\r\nContent-Type: " + e.Mime + "\r\nContent-Length: " + len +
                                 "\r\n" + range + "Accept-Ranges: bytes\r\nContent-Disposition: " + disp +
                                 "\r\nLast-Modified: " + File.GetLastWriteTimeUtc(e.Path).ToString("R", CultureInfo.InvariantCulture) +
                                 "\r\nETag: \"" + tok.Substring(0, 8) + idx + "-" + total + "\"" +
                                 "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
                    byte[] hb = Encoding.UTF8.GetBytes(hdr);
                    ns.Write(hb, 0, hb.Length);
                    if (!headOnly && len > 0)
                    {
                        fs.Position = start;
                        byte[] buf2 = new byte[81920];
                        long left = len;
                        while (left > 0)
                        {
                            int n = fs.Read(buf2, 0, (int)Math.Min(buf2.Length, left));
                            if (n <= 0) break;
                            ns.Write(buf2, 0, n);
                            left -= n;
                        }
                    }
                }
                return;
            }
            Send(ns, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not found"), null, headOnly);
        }

        static void Send(System.Net.Sockets.NetworkStream ns, int code, string type, byte[] body, string extra, bool headOnly)
        {
            string reason = code == 200 ? "OK" : code == 404 ? "Not Found" : code == 416 ? "Range Not Satisfiable" : "Gone";
            string hdr = "HTTP/1.1 " + code + " " + reason + "\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length +
                         "\r\nCache-Control: no-store\r\nConnection: close\r\n" + (extra ?? "") + "\r\n";
            byte[] hb = Encoding.UTF8.GetBytes(hdr);
            ns.Write(hb, 0, hb.Length);
            if (!headOnly) ns.Write(body, 0, body.Length);
        }

        static string Esc(string s)
        {
            return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        static string Size(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1048576) return (b / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            return (b / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        }

        static string Page(string h1, string inner)
        {
            return "<!doctype html><html lang='zh-CN'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>" +
                   "<title>" + Esc(h1) + " · Glean 拾光</title><style>" +
                   ":root{--bg:#f5f2ed;--card:#fff;--text:#26211c;--sub:#887d70;--accent:#e28a2e}" +
                   "@media (prefers-color-scheme:dark){:root{--bg:#1e1c1a;--card:#2e2b28;--text:#f1ece5;--sub:#a2998e}}" +
                   "body{margin:0;background:var(--bg);color:var(--text);font:16px/1.6 -apple-system,'PingFang SC','Microsoft YaHei',sans-serif}" +
                   ".wrap{max-width:720px;margin:0 auto;padding:20px 16px 40px}" +
                   ".brand{color:var(--sub);font-size:13px;margin-bottom:6px}h1{font-size:20px;margin:0 0 14px}" +
                   ".card{background:var(--card);border-radius:14px;padding:14px;margin-bottom:12px;box-shadow:0 1px 3px rgba(0,0,0,.08)}" +
                   "img{max-width:100%;border-radius:10px;display:block}.sub{color:var(--sub);font-size:13px}" +
                   "pre{white-space:pre-wrap;word-break:break-word;margin:0;font:inherit;user-select:text;-webkit-user-select:text}" +
                   ".btn{display:inline-block;background:var(--accent);color:#fff;border:0;border-radius:10px;padding:10px 18px;font-size:15px;text-decoration:none;margin-top:10px}" +
                   ".row{display:flex;justify-content:space-between;align-items:center;gap:12px}.row>div{flex:1;min-width:0}.row .btn{flex-shrink:0;white-space:nowrap;margin-top:0}.name{word-break:break-all}" +
                   "</style></head><body><div class='wrap'><div class='brand'>Glean 拾光 · 从电脑带走</div><h1>" + Esc(h1) + "</h1>" + inner + "</div></body></html>";
        }

        static string BuildPage(string tok, string title, string text, List<Entry> list)
        {
            StringBuilder sb = new StringBuilder();
            if (text != null)
            {
                sb.Append("<div class='card'><pre id='t'>" + Esc(text) + "</pre></div>");
                sb.Append("<button class='btn' onclick=\"var r=document.createRange();r.selectNodeContents(document.getElementById('t'));var s=getSelection();s.removeAllRanges();s.addRange(r);" +
                          "try{if(navigator.clipboard){navigator.clipboard.writeText(document.getElementById('t').innerText).then(function(){this.innerText='已复制'}.bind(this))}else{document.execCommand('copy');this.innerText='已复制'}}catch(e){this.innerText='已全选,长按复制'}\">复制全部文字</button>");
            }
            for (int i = 0; i < list.Count; i++)
            {
                Entry e = list[i];
                string url = "/" + tok + "/f" + i;
                sb.Append("<div class='card'>");
                if (e.Mime.StartsWith("image/")) sb.Append("<img src='" + url + "' alt=''><p class='sub'>长按图片即可保存到相册</p>");
                sb.Append("<div class='row'><div><div class='name'>" + Esc(e.Name) + "</div><div class='sub'>" + Size(e.Size) + "</div></div>" +
                          "<a class='btn' href='" + url + "?dl=1' download='" + Esc(e.Name) + "'>下载</a></div></div>");
            }
            sb.Append("<p class='sub'>这个页面由你电脑上的 Glean 临时提供,只在同一个 Wi-Fi 里能打开,10 分钟后自动失效。</p>");
            return Page(title, sb.ToString());
        }
    }

    // 扫码窗口
    class ShareForm : Form
    {
        Bitmap qr;
        readonly string title, mode, link;
        readonly System.Windows.Forms.Timer expire = new System.Windows.Forms.Timer();
        readonly bool served;

        public ShareForm(string title, bool[,] matrix, string mode, string link, bool served)
        {
            this.title = title;
            this.mode = mode;
            this.link = link;
            this.served = served;
            Text = "扫码带走";
            try { if (Brand.LargeIcon != null) Icon = Brand.LargeIcon; } catch { }
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            KeyPreview = true;
            DoubleBuffered = true;
            Font = Gfx.UI;
            BackColor = SetPal.Back;
            int n = matrix.GetLength(0) + 8;
            int cell = Math.Max(2, Gfx.S(300) / n);
            qr = QrCode.Render(matrix, cell, Color.FromArgb(28, 26, 24), Color.White);
            int w = Math.Max(qr.Width + Gfx.S(80), Gfx.S(400));
            ClientSize = new Size(w, Gfx.S(84) + qr.Width + Gfx.S(150));
            Btn ok = new Btn();
            ok.Text = "完成";
            ok.Primary = true;
            ok.SetBounds(ClientSize.Width - Gfx.S(28 + 96), ClientSize.Height - Gfx.S(52), Gfx.S(96), Gfx.S(34));
            ok.Click += delegate { Close(); };
            Controls.Add(ok);
            if (served)
            {
                expire.Interval = 10 * 60 * 1000;
                expire.Tick += delegate { expire.Stop(); Close(); };
                expire.Start();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(Handle, Theme.Cur.Dark);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            expire.Stop();
            if (served) ShareServer.Stop();
            if (qr != null) { qr.Dispose(); qr = null; }
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Gfx.Text(g, "用手机扫一扫", SetPal.PageTitle, new Rectangle(Gfx.S(28), Gfx.S(16), ClientSize.Width - Gfx.S(56), Gfx.S(40)), t.Text, TextFormatFlags.Left);
            Gfx.Text(g, title, Gfx.Small, new Rectangle(Gfx.S(28), Gfx.S(58), ClientSize.Width - Gfx.S(56), Gfx.S(20)), t.Sub,
                TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (qr != null)
            {
                int x = (ClientSize.Width - qr.Width) / 2, y = Gfx.S(92);
                Gfx.FillRound(g, Color.White, new Rectangle(x - Gfx.S(6), y - Gfx.S(6), qr.Width + Gfx.S(12), qr.Height + Gfx.S(12)), 14);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImageUnscaled(qr, x, y);
                Rectangle tr = new Rectangle(Gfx.S(28), y + qr.Height + Gfx.S(14), ClientSize.Width - Gfx.S(56), Gfx.S(44));
                TextRenderer.DrawText(g, mode, Gfx.Small, tr, t.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                if (link != null)
                    TextRenderer.DrawText(g, link, Gfx.Small, new Rectangle(Gfx.S(28), tr.Bottom, ClientSize.Width - Gfx.S(56), Gfx.S(20)), Theme.Blend(t.Sub, t.Bg, 0.3),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

    // ================================================================ 快递单号
    static class Courier
    {
        public class Company
        {
            public string Name, Mark, Code;
            public Color Color;
            public Company(string name, string mark, string code, Color c) { Name = name; Mark = mark; Code = code; Color = c; }
        }

        // 角标用公司名里的一个字 + 一种颜色(不是各家的商标图案)
        public static readonly Company[] Companies = new Company[]
        {
            new Company("顺丰速运", "顺", "shunfeng", Color.FromArgb(40, 40, 44)),
            new Company("中通快递", "中", "zhongtong", Color.FromArgb(32, 96, 200)),
            new Company("圆通速递", "圆", "yuantong", Color.FromArgb(112, 58, 160)),
            new Company("韵达快递", "韵", "yunda", Color.FromArgb(214, 140, 20)),
            new Company("申通快递", "申", "shentong", Color.FromArgb(222, 92, 40)),
            new Company("极兔速递", "兔", "jtexpress", Color.FromArgb(214, 40, 52)),
            new Company("京东物流", "京", "jd", Color.FromArgb(196, 30, 36)),
            new Company("邮政快递包裹", "邮", "youzhengguonei", Color.FromArgb(0, 128, 72)),
            new Company("EMS", "E", "ems", Color.FromArgb(0, 104, 170)),
            new Company("德邦快递", "德", "debangkuaidi", Color.FromArgb(20, 84, 170)),
            new Company("百世快递", "百", "huitongkuaidi", Color.FromArgb(200, 44, 66)),
            new Company("跨越速运", "跨", "kuayue", Color.FromArgb(120, 64, 168)),
            new Company("顺心捷达", "心", "sxjdfreight", Color.FromArgb(230, 100, 30)),
            new Company("菜鸟速递", "鸟", "danniao", Color.FromArgb(60, 120, 230)),
        };

        static Company Find(string code)
        {
            foreach (Company c in Companies) if (c.Code == code) return c;
            return null;
        }

        // 单号规则:带字母前缀的很确定;纯数字的按目前最常见的电子面单格式(长度 + 开头)区分
        static readonly KeyValuePair<Regex, string>[] Rules = new KeyValuePair<Regex, string>[]
        {
            new KeyValuePair<Regex, string>(new Regex(@"^SF\d{12,15}$"), "shunfeng"),
            new KeyValuePair<Regex, string>(new Regex(@"^YT\d{13,15}$"), "yuantong"),
            new KeyValuePair<Regex, string>(new Regex(@"^JT\d{13,15}$"), "jtexpress"),
            new KeyValuePair<Regex, string>(new Regex(@"^JD[A-Z0-9]{11,18}$"), "jd"),
            new KeyValuePair<Regex, string>(new Regex(@"^DPK\d{9,15}$"), "debangkuaidi"),
            new KeyValuePair<Regex, string>(new Regex(@"^KY\d{10,15}$"), "kuayue"),
            new KeyValuePair<Regex, string>(new Regex(@"^E[A-Z]\d{9}C[NS]$"), "ems"),
            new KeyValuePair<Regex, string>(new Regex(@"^[A-Z]{2}\d{9}CN$"), "ems"),
        };

        static readonly KeyValuePair<Regex, string>[] DigitRules = new KeyValuePair<Regex, string>[]
        {
            new KeyValuePair<Regex, string>(new Regex(@"^77\d{13}$"), "shentong"),                 // 申通:77 开头 15 位
            new KeyValuePair<Regex, string>(new Regex(@"^7[0-9]\d{12}$"), "zhongtong"),             // 中通:7 开头 14 位
            new KeyValuePair<Regex, string>(new Regex(@"^(43|46|31|33|35|39)\d{11}$"), "yunda"),  // 韵达:13 位
            new KeyValuePair<Regex, string>(new Regex(@"^(43|46|31)\d{13}$"), "yunda"),           // 韵达:15 位
            new KeyValuePair<Regex, string>(new Regex(@"^9[5-9]\d{11}$"), "youzhengguonei"),       // 邮政快递包裹:13 位
        };

        // 只在文字里提到快递时才认的:1 开头 13 位也可能是程序里的时间戳,单独复制时不算
        static readonly KeyValuePair<Regex, string>[] ContextOnlyRules = new KeyValuePair<Regex, string>[]
        {
            new KeyValuePair<Regex, string>(new Regex(@"^1\d{12}$"), "ems"),                       // EMS 国内:1 开头 13 位
        };

        public class Hit
        {
            public Company Company;
            public string Number;
            public bool Whole;   // 整段内容就是这个单号
        }

        public static string Number(string text)
        {
            return Regex.Replace((text ?? "").Trim(), @"\s", "").ToUpperInvariant();
        }

        static Company ByRules(string t, bool digitsToo)
        {
            foreach (KeyValuePair<Regex, string> r in Rules)
                if (r.Key.IsMatch(t)) return Find(r.Value);
            if (digitsToo)
                foreach (KeyValuePair<Regex, string> r in DigitRules)
                    if (r.Key.IsMatch(t)) return Find(r.Value);
            return null;
        }

        // 整段文字就是一个单号:返回 [公司名, 快递100 代码]
        public static string[] Detect(string text)
        {
            Company c = DetectCompany(text);
            return c == null ? null : new string[] { c.Name, c.Code };
        }

        public static Company DetectCompany(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 40) return null;
            return ByRules(Number(text), true);
        }

        static readonly Regex Candidate = new Regex(@"(?<![A-Za-z0-9])[A-Za-z]{0,3}\d[A-Za-z0-9]{8,21}(?![A-Za-z0-9])");
        static readonly Regex Context = new Regex("快递|(?<!订)单号|运单|物流|包裹|取件|派送|派件|签收|驿站|丰巢|揽收|已发货|已发出|配送");

        // 在一段文字里找单号:带字母前缀的直接认;纯数字的要文字里提到快递(或快递公司名)才认,避免把订单号、手机号当成单号
        public static Hit Find(string text, bool digitsNeedContext)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string body = text.Length > 3000 ? text.Substring(0, 3000) : text;
            Company named = null;
            foreach (Company c in Companies)
            {
                if (c.Code == "danniao") continue;   // "菜鸟驿站"只是代收点,不代表是哪家快递
                string shortName = c.Name.Length > 2 ? c.Name.Substring(0, 2) : c.Name;
                if (body.IndexOf(shortName, StringComparison.Ordinal) >= 0) { named = c; break; }
            }
            bool context = named != null || Context.IsMatch(body);
            foreach (System.Text.RegularExpressions.Match m in Candidate.Matches(body))
            {
                string t = m.Value.ToUpperInvariant();
                // 优先级:带字母前缀的(确定)> 文字里点名的公司 > 纯数字规则 > 只在提到快递时才认的规则
                Company c = ByRules(t, false);
                bool whole = Number(text) == t;   // 整段就是这个号码:不需要上下文
                if (c == null && named != null && Regex.IsMatch(t, @"^[A-Z]{0,3}\d{10,20}$")) c = named;
                if (c == null && (context || whole || !digitsNeedContext)) c = ByRules(t, true);
                if (c == null && context)
                    foreach (KeyValuePair<Regex, string> r in ContextOnlyRules)
                        if (r.Key.IsMatch(t)) { c = Find(r.Value); break; }
                if (c != null)
                {
                    Hit h = new Hit();
                    h.Company = c;
                    h.Number = t;
                    h.Whole = Number(text) == t;
                    return h;
                }
            }
            return null;
        }

        // 看起来可能是快递单号(字母数字、10~22 位、含至少 8 位数字),用来决定要不要在菜单里提供"标记为快递单号"
        public static bool LooksLikeNumber(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 40) return false;
            string t = Number(text);
            return Regex.IsMatch(t, @"^[A-Z0-9]{10,22}$") && Regex.Matches(t, @"\d").Count >= 8;
        }

        // 一条记录对应的快递:用户手动标记的 > 自动识别的(整段或文字里找到的)
        public static Hit HitOf(Item it)
        {
            if (it == null || it.IsImage || it.IsFiles || it.Text == null) return null;
            if (!string.IsNullOrEmpty(it.CustomTitle) && LooksLikeNumber(it.Text))
                foreach (Company k in Companies)
                    if (k.Name == it.CustomTitle)
                    {
                        Hit h = new Hit();
                        h.Company = k;
                        h.Number = Number(it.Text);
                        h.Whole = true;
                        return h;
                    }
            if (!it.CourierChecked)
            {
                it.CourierHit = Find(it.Text, true);
                it.CourierChecked = true;
            }
            return it.CourierHit;
        }

        public static Company For(Item it)
        {
            Hit h = HitOf(it);
            return h == null ? null : h.Company;
        }

        public static string TrackUrl(string text)
        {
            Company c = DetectCompany(text);
            return TrackUrl(text, c);
        }

        public static string TrackUrl(string text, Company c)
        {
            string nu = Uri.EscapeDataString(Number(text));
            return c != null ? "https://www.kuaidi100.com/chaxun?com=" + c.Code + "&nu=" + nu : "https://www.kuaidi100.com/chaxun?nu=" + nu;
        }

        public static string SearchUrl(string text)
        {
            return "https://www.baidu.com/s?wd=" + Uri.EscapeDataString(Number(text));
        }

        // 角标:公司颜色的圆角方块(上浅下深的渐变)+ 白色的字,大小和其他卡片上的程序图标一样。
        // 字按笔画的实际轮廓居中(中文字体上下留白不一样,按行高居中会偏)
        static FontFamily badgeFamily;

        public static void DrawBadge(Graphics g, Company c, Rectangle r)
        {
            SmoothingMode sm = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float rad = r.Width * 0.24f;
            using (GraphicsPath tile = RoundPx(new RectangleF(r.X, r.Y, r.Width, r.Height), rad))
            {
                // 底下一点点投影
                using (GraphicsPath sh = RoundPx(new RectangleF(r.X, r.Y + Math.Max(1f, r.Height * 0.04f), r.Width, r.Height), rad))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(46, 0, 0, 0)))
                    g.FillPath(sb, sh);
                using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
                    Theme.Blend(c.Color, Color.White, 0.20), Theme.Blend(c.Color, Color.Black, 0.10), LinearGradientMode.Vertical))
                    g.FillPath(lb, tile);
                using (Pen hl = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                    g.DrawPath(hl, tile);
            }
            try
            {
                if (badgeFamily == null) badgeFamily = new FontFamily("Microsoft YaHei UI");
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddString(c.Mark, badgeFamily, (int)FontStyle.Bold, r.Height * 0.56f, new PointF(0, 0), StringFormat.GenericTypographic);
                    RectangleF bb = gp.GetBounds();
                    float k = Math.Min(1f, r.Width * 0.62f / Math.Max(1f, bb.Width));
                    using (Matrix m = new Matrix())
                    {
                        m.Translate(r.X + r.Width / 2f, r.Y + r.Height / 2f);
                        m.Scale(k, k);
                        m.Translate(-(bb.X + bb.Width / 2f), -(bb.Y + bb.Height / 2f));
                        gp.Transform(m);
                    }
                    g.FillPath(Brushes.White, gp);
                }
            }
            catch
            {
                using (Font f = new Font("Microsoft YaHei UI", Math.Max(6f, r.Height * 0.48f), FontStyle.Bold, GraphicsUnit.Pixel))
                    Gfx.SoftText(g, c.Mark, f, r, Color.White, true);
            }
            g.SmoothingMode = sm;
        }

        static GraphicsPath RoundPx(RectangleF r, float rad)
        {
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // ================================================================ 图片转文字
    // 用 Windows 10/11 自带的离线文字识别(Windows.Media.Ocr)。
    // 不在编译时引用它,而是运行时再去找:编译方式不用变,老系统上找不到就自动隐藏这个功能。
    static class Ocr
    {
        static int state;   // 0 还没检查  1 可用  -1 不可用
        public static double Upscale = 0;     // 放大倍数(0 = 按屏幕缩放自动决定)
        static MethodInfo asTaskOp;

        static Type WinRT(string name)
        {
            foreach (string asm in new string[] { "Windows.Foundation", "Windows.Media", "Windows.Storage", "Windows.Graphics", "Windows.Globalization", "Windows" })
            {
                try
                {
                    Type t = Type.GetType(name + ", " + asm + ", ContentType=WindowsRuntime", false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        public static bool Available
        {
            get
            {
                if (state == 0)
                {
                    bool ok = false;
                    try { ok = WinRT("Windows.Media.Ocr.OcrEngine") != null; } catch { ok = false; }
                    state = ok ? 1 : -1;
                }
                return state == 1;
            }
        }

        // 等待 WinRT 的异步操作完成,返回结果
        static object Await(object op, Type resultType)
        {
            if (asTaskOp == null)
            {
                Assembly a = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                Type ext = a.GetType("System.WindowsRuntimeSystemExtensions");
                foreach (MethodInfo m in ext.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "AsTask" || !m.IsGenericMethodDefinition || m.GetGenericArguments().Length != 1) continue;
                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType.Name == "IAsyncOperation`1") { asTaskOp = m; break; }
                }
                if (asTaskOp == null) throw new Exception("找不到系统的异步组件");
            }
            System.Threading.Tasks.Task t = (System.Threading.Tasks.Task)asTaskOp.MakeGenericMethod(resultType).Invoke(null, new object[] { op });
            if (!t.Wait(30000)) throw new Exception("识别超时");
            return t.GetType().GetProperty("Result").GetValue(t, null);
        }

        static void Close(object o)
        {
            IDisposable d = o as IDisposable;
            if (d != null) { try { d.Dispose(); } catch { } }
        }

        // 把异常里真正的原因取出来
        public static string Reason(Exception ex)
        {
            while ((ex is TargetInvocationException || ex is AggregateException) && ex.InnerException != null) ex = ex.InnerException;
            return ex.Message;
        }

        // 中文识别结果常在每个字之间加空格("你 好"),去掉中文之间的空格,英文单词之间的保留
        static readonly Regex CjkGap = new Regex(@"(?<=[\u2E80-\u9FFF\uF900-\uFAFF\uFF00-\uFFEF\u3000-\u303F])[ \t]+(?=[\u2E80-\u9FFF\uF900-\uFAFF\uFF00-\uFFEF\u3000-\u303F])");

        // 全角标点两边多出来的空格
        static readonly Regex PunctGap = new Regex(@"[ \t]+(?=[\uFF0C\u3002\uFF1A\uFF1B\uFF01\uFF1F\u3001\uFF0E\uFF09\u300D\u300F\u201D\u2019])|(?<=[\uFF0C\u3002\uFF1A\uFF1B\uFF01\uFF1F\u3001\uFF0E\uFF08\u300C\u300E\u201C\u2018])[ \t]+");
        // 数字中间被认成全角点或间隔号的小数点:128 ． 5 → 128.5
        static readonly Regex DigitDot = new Regex(@"(?<=\d)[ \t]*[\uFF0E\u00B7\u30FB][ \t]*(?=\d)");

        // 英文里被认成全角的标点:"12345，version" → "12345, version"
        static readonly Regex AsciiPunct = new Regex(@"(?<=[A-Za-z0-9])[ \t]*([\uFF0C\uFF1A\uFF1B\uFF01\uFF1F])[ \t]*(?=[A-Za-z])");
        // 中文后面、半角标点前面多出的空格:"文字 :Windows" → "文字:Windows"
        static readonly Regex CjkAsciiPunct = new Regex(@"(?<=[\u2E80-\u9FFF\uF900-\uFAFF])[ \t]+(?=[:;,.!?])");

        public static string Tidy(string s)
        {
            s = CjkGap.Replace(s, "");
            s = DigitDot.Replace(s, ".");
            s = PunctGap.Replace(s, "");
            s = AsciiPunct.Replace(s, delegate(Match m)
            {
                string c = m.Groups[1].Value;
                string a = c == "\uFF0C" ? "," : c == "\uFF1A" ? ":" : c == "\uFF1B" ? ";" : c == "\uFF01" ? "!" : "?";
                return a + " ";
            });
            s = CjkAsciiPunct.Replace(s, "");
            // 行首的项目符号常被认成全角点
            s = Regex.Replace(s, @"^[\uFF0E\u00B7\u30FB\u2022\u3002][ \t]*(?=\S)", "• ");
            // 中文旁边的英文引号多出的空格:的 " 文件夹 " 变成 → 的"文件夹"变成
            s = Regex.Replace(s, @"(?<=[\u2E80-\u9FFF])[ \t]*""[ \t]*", "\"");
            s = Regex.Replace(s, @"[ \t]*""[ \t]*(?=[\u2E80-\u9FFF])", "\"");
            // 被拆成"偏旁 + 部件"的字合回去(这些偏旁单独出现在正常文字里的情况极少)
            foreach (KeyValuePair<string, string> kv in SplitFix)
                if (s.IndexOf(kv.Key, StringComparison.Ordinal) >= 0) s = s.Replace(kv.Key, kv.Value);
            // 形近的生僻字:"囗"(方框)几乎都是"口"
            s = s.Replace('\u56D7', '\u53E3');
            // 中文之间的全角点,其实是间隔号:拾光．设置 → 拾光 · 设置
            s = Regex.Replace(s, @"(?<=[\u4E00-\u9FFF])[ \t]*[\uFF0E\u30FB][ \t]*(?=[\u4E00-\u9FFF])", " · ");
            // 数字之间:被认成汉字"一"的连字符、全角冒号、被认成逗号的小数点
            s = Regex.Replace(s, @"(?<=\d)[ \t]*[\u4E00\u2014\u2013\uFF0D][ \t]*(?=\d)", "-");
            s = Regex.Replace(s, @"(?<=\d)[ \t]*\uFF1A[ \t]*(?=\d)", ":");
            s = Regex.Replace(s, @"(?<=\d)\uFF0C(?=\d(?!\d\d(?!\d)))", ".");   // 1，000 这种千位分隔不动
            // 日期、时间里被拆开的数字:2026-1 1-03 → 2026-11-03
            s = Regex.Replace(s, @"(?<=[-:/.]\d)[ \t](?=\d[-:/.])", "");
            s = Regex.Replace(s, @"(?<=\d{4}-\d)[ \t](?=\d\b)", "");
            // 图标被认成的"0":行首的 0 / O 后面紧跟中文
            s = Regex.Replace(s, @"^[0Oo\u3007][ \t]+(?=[\u4E00-\u9FFF]|[A-Z][a-z])", "");
            return s.Trim();
        }

        // 只收"单独出现几乎不可能"的偏旁(亻讠氵扌…和右边的刂攵阝),普通字的组合(如"日月")不动,避免误改
        static readonly KeyValuePair<string, string>[] SplitFix = BuildSplitFix(
            "亻王住 忄佥检 木佥检 扌佥捡 亻佥俭 忄金检 亻主住 亻尔你 亻门们 亻也他 亻故做 亻乍作 亻更便 亻吏使 亻立位 亻本体 亻可何 亻旦但 亻氐低 亻氏低 亻言信 亻牛件 亻介价 亻分份 亻壬任 亻木休 亻衣依 亻呆保 亻建健 亻亭停 亻共供 亻直值 亻扁偏 亻昔借 亻专传 亻尤优 亻韦伟 亻列例 亻匕化 亻十什 亻寸付 亻弋代 亻又仅 亻火伙 亻古估 亻圭佳 亻则侧 亻谷俗 彳亍行 彳寺待 彳艮很 彳主往 彳正征 彳走徒 冫欠次 冫令冷 冫隹准 冫夬决 冫咸减 冫兄况 冫水冰 冫中冲 冫争净 冫京凉 讠兑说 讠青请 讠吾语 讠十计 讠己记 讠人认 讠只识 讠舌话 讠周调 讠殳设 讠式试 讠义议 讠寸讨 讠方访 讠吴误 讠卖读 讠果课 讠午许 讠仑论 讠者诸 讠平评 讠羊详 讠炎谈 讠秀诱 讠亥该 讠正证 讠上让 讠川训 讠丁订 讠斥诉 讠司词 讠圣译 讠射谢 讠隹谁 讠胃谓 讠普谱 讠成诚 氵肖消 氵去法 氵青清 氵每海 氵十汁 氵也池 氵由油 氵告浩 氵寅演 氵吉洁 氵良浪 氵则测 氵舌活 氵原源 氵票漂 氵骨滑 氵殳没 氵工江 氵可河 氵先洗 氵皮波 氵主注 氵目泪 氵羊洋 氵又汉 氵少沙 氵齐济 氵胡湖 氵斩渐 氵昆混 氵气汽 氵台治 氵勾沟 氵包泡 扌丁打 扌巴把 扌安按 扌是提 扌妾接 扌由抽 扌白拍 扌乇托 扌巨拒 扌寺持 扌叟搜 扌罢摆 扌番播 扌兆挑 扌立拉 扌合拾 扌支技 扌斤折 扌并拼 扌召招 扌奂换 扌甬捅 扌殳投 扌戈找 扌旨指 扌包抱 扌察擦 扌非排 扌用拥 扌圭挂 扌广扩 扌旦担 扌户护 扌受授 扌卓掉 扌隹推 扌苗描 扌发拨 钅艮银 钅昔错 钅失铁 钅肖销 钅建键 钅连链 钅冈钢 钅中钟 钅戋钱 钅竟镜 钅丑钮 钅十针 纟及级 纟田细 纟冬终 纟会绘 纟吉结 纟色绝 纟合给 纟充统 纟扁编 纟屯纯 纟氏纸 纟千纤 纟工红 纟彖缘 纟东练 纟戋线 纟且组 纟圣经 纟责绩 纟宗综 纟录绿 纟勺约 纟己纪 纟卖续 纟隹维 纟宿缩 纟召绍 饣反饭 饣欠饮 饣并饼 饣包饱 饣官馆 饣我饿 犭苗猫 犭句狗 犭良狼 犭者猪 犭虫独 犭王狂 忄青情 忄亡忙 忄夬快 忄生性 忄曼慢 忄董懂 忄白怕 忄昔惜 忄艮恨 忄感憾 忄不怀 忄肖悄 礻申神 礻兄祝 礻斤祈 礻畐福 礻土社 礻见视 礻且祖 衤寸衬 衤刀初 衤皮被 衤卜补 衤由袖 衤库裤 阝方防 阝东陈 阝余除 阝完院 阝艮限 阝付附 阝人队 阝介阶 阝示际 阝且阻 阝击陆 阝日阳 阝月阴 另刂别 至刂到 歹刂列 禾刂利 冈刂刚 半刂判 居刂剧 亥刂刻 贝刂则 舌刂刮 干刂刊 乘刂剩 畐刂副 害刂割 者阝都 由阝邮 交阝郊 令阝邻 良阝郎 方攵放 己攵改 正攵政 古攵故 交攵效 娄攵数 孝攵教 苟攵敬 求攵救");

        static KeyValuePair<string, string>[] BuildSplitFix(string table)
        {
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            HashSet<string> seen = new HashSet<string>();
            foreach (string tok in table.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (tok.Length != 3) continue;
                string key = tok.Substring(0, 2);
                if (seen.Add(key)) list.Add(new KeyValuePair<string, string>(key, tok.Substring(2)));
            }
            return list.ToArray();
        }

        // 在后台线程调用。noChinese = true 表示系统没有中文识别组件,这次只按其他语言(一般是英文)识别
        public static string Recognize(string imagePath, out bool noChinese)
        {
            noChinese = false;
            Type tEngine = WinRT("Windows.Media.Ocr.OcrEngine");
            Type tResult = WinRT("Windows.Media.Ocr.OcrResult");
            Type tLine = WinRT("Windows.Media.Ocr.OcrLine");
            Type tLang = WinRT("Windows.Globalization.Language");
            Type tFile = WinRT("Windows.Storage.StorageFile");
            Type tMode = WinRT("Windows.Storage.FileAccessMode");
            Type tStream = WinRT("Windows.Storage.Streams.IRandomAccessStream");
            Type tDecoder = WinRT("Windows.Graphics.Imaging.BitmapDecoder");
            Type tSoft = WinRT("Windows.Graphics.Imaging.SoftwareBitmap");
            if (tEngine == null || tResult == null || tLine == null || tLang == null || tFile == null ||
                tMode == null || tStream == null || tDecoder == null || tSoft == null)
                throw new Exception("这台电脑的系统不支持文字识别(需要 Windows 10 或更新版本)");

            // 1) 识别引擎:优先用中文(中文引擎也能认英文),没有就用系统语言
            object engine = null;
            foreach (string tag in new string[] { "zh-Hans-CN", "zh-Hans", "zh-CN", "zh-Hant-TW", "zh-Hant" })
            {
                try
                {
                    object lang = Activator.CreateInstance(tLang, tag);
                    bool sup = (bool)tEngine.GetMethod("IsLanguageSupported").Invoke(null, new object[] { lang });
                    if (!sup) continue;
                    engine = tEngine.GetMethod("TryCreateFromLanguage").Invoke(null, new object[] { lang });
                    if (engine != null) break;
                }
                catch { }
            }
            if (engine == null)
            {
                engine = tEngine.GetMethod("TryCreateFromUserProfileLanguages").Invoke(null, null);
                if (engine == null) throw new Exception("系统里没有可用的文字识别语言");
                noChinese = true;
            }

            // 2) 先按屏幕缩放估一个放大倍数识别一遍,量出字的实际高度;
            //    字太大或太小(容易把"住"拆成"亻主")就按理想字高重新缩放,再识别一遍
            uint maxDim = 10000;
            try { maxDim = Convert.ToUInt32(tEngine.GetProperty("MaxImageDimension").GetValue(null, null)); } catch { }
            using (Bitmap src = Util.LoadBitmap(imagePath))
            {
                bool invert = InvertDark && LooksDark(src);
                double k0 = Upscale > 0 ? Upscale : Math.Max(1.0, Math.Min(2.0, 2.0 / Math.Max(0.5, (double)Gfx.Scale)));
                k0 = Fit(src, k0, maxDim);
                double height;
                List<string> lines = RunOnce(engine, src, k0, invert, out height);
                if (Adaptive && lines.Count > 0 && height > 0)
                {
                    double k1 = Fit(src, Math.Max(0.5, Math.Min(3.0, k0 * TargetHeight / height)), maxDim);
                    if (Math.Abs(k1 / k0 - 1) > 0.15)
                    {
                        double h1;
                        List<string> again = RunOnce(engine, src, k1, invert, out h1);
                        if (again.Count > 0) lines = again;
                    }
                }
                StringBuilder sb = new StringBuilder();
                foreach (string l in lines)
                {
                    string x = Tidy(l ?? "");
                    if (x.Length == 0 || IconNoise.IsMatch(x)) continue;   // 单独一个"0""O"之类,多半是图标
                    if (sb.Length > 0) sb.Append("\r\n");
                    sb.Append(x);
                }
                return sb.ToString();
            }
        }

        static readonly Regex IconNoise = new Regex(@"^[0Oo\u3007\u53E3\u56DE@®©]$");

        public static bool Adaptive = false;      // 按实际字高重新缩放(测试中)
        public static bool InvertDark = false;    // 深色背景先反色成白底黑字(测试中)
        public static double TargetHeight = 24;   // 识别效果最好的字高(像素)

        static double Fit(Bitmap src, double k, uint maxDim)
        {
            double limit = maxDim * 0.98;
            int big = Math.Max(src.Width, src.Height);
            if (big * k > limit) k = limit / big;
            return k;
        }

        // 抽样判断是不是深色背景(深色模式的截图)
        static bool LooksDark(Bitmap b)
        {
            try
            {
                long sum = 0;
                int n = 0;
                int sx = Math.Max(1, b.Width / 48), sy = Math.Max(1, b.Height / 48);
                for (int y = 0; y < b.Height; y += sy)
                    for (int x = 0; x < b.Width; x += sx)
                    {
                        Color c = b.GetPixel(x, y);
                        int lum = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
                        sum += c.A < 40 ? 255 : lum;
                        n++;
                    }
                return n > 0 && sum / n < 110;
            }
            catch { return false; }
        }

        // 按倍数 k 缩放(可选反色)后识别一次;height 返回识别到的字的中位高度(缩放后的像素)
        static List<string> RunOnce(object engine, Bitmap src, double k, bool invert, out double height)
        {
            height = 0;
            Type tEngine = WinRT("Windows.Media.Ocr.OcrEngine");
            Type tResult = WinRT("Windows.Media.Ocr.OcrResult");
            Type tLine = WinRT("Windows.Media.Ocr.OcrLine");
            Type tWord = WinRT("Windows.Media.Ocr.OcrWord");
            Type tFile = WinRT("Windows.Storage.StorageFile");
            Type tMode = WinRT("Windows.Storage.FileAccessMode");
            Type tStream = WinRT("Windows.Storage.Streams.IRandomAccessStream");
            Type tDecoder = WinRT("Windows.Graphics.Imaging.BitmapDecoder");
            Type tSoft = WinRT("Windows.Graphics.Imaging.SoftwareBitmap");

            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Glean-OCR");
            Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, "ocr-" + Guid.NewGuid().ToString("N") + ".png");
            int w = Math.Max(1, (int)(src.Width * k)), h = Math.Max(1, (int)(src.Height * k));
            using (Bitmap dst = new Bitmap(w, h, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(dst))
                {
                    g.Clear(invert ? Color.Black : Color.White);   // 透明的地方当背景色
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    if (invert)
                    {
                        ColorMatrix cm = new ColorMatrix(new float[][]
                        {
                            new float[] { -1, 0, 0, 0, 0 },
                            new float[] { 0, -1, 0, 0, 0 },
                            new float[] { 0, 0, -1, 0, 0 },
                            new float[] { 0, 0, 0, 1, 0 },
                            new float[] { 1, 1, 1, 0, 1 }
                        });
                        using (ImageAttributes ia = new ImageAttributes())
                        {
                            ia.SetColorMatrix(cm);
                            g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                    else g.DrawImage(src, 0, 0, w, h);
                }
                if (invert)
                {
                    // 反色时透明区域(先铺的黑底)也被反成了白色以外的颜色:统一再铺一次白底
                    using (Bitmap flat = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics fg = Graphics.FromImage(flat)) { fg.Clear(Color.White); fg.DrawImageUnscaled(dst, 0, 0); }
                        flat.Save(file, ImageFormat.Png);
                    }
                }
                else dst.Save(file, ImageFormat.Png);
            }

            try
            {
                object storageFile = Await(tFile.GetMethod("GetFileFromPathAsync", new Type[] { typeof(string) }).Invoke(null, new object[] { file }), tFile);
                object stream = Await(tFile.GetMethod("OpenAsync", new Type[] { tMode }).Invoke(storageFile, new object[] { Enum.ToObject(tMode, 0) }), tStream);
                try
                {
                    object decoder = Await(tDecoder.GetMethod("CreateAsync", new Type[] { tStream }).Invoke(null, new object[] { stream }), tDecoder);
                    object soft = Await(tDecoder.GetMethod("GetSoftwareBitmapAsync", Type.EmptyTypes).Invoke(decoder, null), tSoft);
                    try
                    {
                        object result = Await(tEngine.GetMethod("RecognizeAsync").Invoke(engine, new object[] { soft }), tResult);
                        List<string> lines = new List<string>();
                        List<double> hs = new List<double>();
                        try
                        {
                            object ls = tResult.GetProperty("Lines").GetValue(result, null);
                            foreach (object ln in (System.Collections.IEnumerable)ls)
                            {
                                lines.Add((string)tLine.GetProperty("Text").GetValue(ln, null));
                                if (tWord == null) continue;
                                try
                                {
                                    object ws = tLine.GetProperty("Words").GetValue(ln, null);
                                    foreach (object wd in (System.Collections.IEnumerable)ws)
                                    {
                                        object rect = tWord.GetProperty("BoundingRect").GetValue(wd, null);
                                        hs.Add(Convert.ToDouble(rect.GetType().GetProperty("Height").GetValue(rect, null)));
                                    }
                                }
                                catch { }
                            }
                        }
                        catch
                        {
                            lines.Clear();
                            lines.Add((string)tResult.GetProperty("Text").GetValue(result, null) ?? "");
                        }
                        if (hs.Count > 0)
                        {
                            hs.Sort();
                            height = hs[hs.Count / 2];
                        }
                        return lines;
                    }
                    finally { Close(soft); }
                }
                finally { Close(stream); }
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    static class Updater
    {
        public static UpdateInfo Available;   // 检查到的新版本(没有就是 null)
        static int busy;

        public static string Api
        {
            get { return "https://api.github.com/repos/" + Brand.RepoOwner + "/" + Brand.RepoName + "/releases/latest"; }
        }

        public static string ReleasesPage
        {
            get { return "https://github.com/" + Brand.RepoOwner + "/" + Brand.RepoName + "/releases"; }
        }

        // "v4.1"、"4.1.2-beta" 这类写法都能认出来
        public static Version ParseVersion(string s)
        {
            Match m = Regex.Match(s ?? "", @"(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?");
            if (!m.Success) return new Version(0, 0);
            int a = int.Parse(m.Groups[1].Value);
            int b = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            int c = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
            int d = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0;
            return new Version(a, b, c, d);
        }

        public static Version Current { get { return ParseVersion(Config.Version); } }

        static void EnableTls12()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }   // GitHub 只支持 TLS 1.2 以上
        }

        static HttpWebRequest Request(string url)
        {
            EnableTls12();
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Glean-Updater/" + Config.Version;
            req.Timeout = 20000;
            req.ReadWriteTimeout = 30000;
            req.AllowAutoRedirect = true;
            return req;
        }

        // ---- 简单的 JSON 取值(只取几个字段,不需要额外的库)
        static string JsonUnescape(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
                char n = s[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 < s.Length)
                        {
                            sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                            i += 4;
                        }
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }

        static string JsonString(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            return m.Success ? JsonUnescape(m.Groups[1].Value) : null;
        }

        // 联网检查(在后台线程调用)。返回比当前新的版本,没有新版本返回 null;出错抛异常
        public static UpdateInfo Check()
        {
            string json;
            HttpWebRequest req = Request(Api);
            req.Accept = "application/vnd.github+json";
            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    json = sr.ReadToEnd();
            }
            catch (WebException ex)
            {
                HttpWebResponse r = ex.Response as HttpWebResponse;
                if (r != null && r.StatusCode == HttpStatusCode.NotFound)
                    throw new Exception("还没有发布任何版本");
                if (r != null && ((int)r.StatusCode == 403 || (int)r.StatusCode == 429))
                    throw new Exception("GitHub 暂时限制了访问次数,请过一会儿再试");
                throw new Exception("连不上 GitHub,请检查网络");
            }
            // 只有真的查到了才记时间;没联网的话,一小时后会再试,不用等一整天
            Config.LastUpdateCheck = DateTime.Now.Ticks;
            Config.Save();

            UpdateInfo u = new UpdateInfo();
            u.Tag = JsonString(json, "tag_name") ?? "";
            u.Ver = ParseVersion(u.Tag);
            u.Notes = (JsonString(json, "body") ?? "").Trim();
            u.PageUrl = JsonString(json, "html_url") ?? ReleasesPage;

            // 在附件里找安装包(名字以 Setup.exe 结尾),顺便取它的 SHA-256 校验值
            MatchCollection urls = Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"");
            int prevEnd = 0;
            foreach (Match m in urls)
            {
                string url = m.Groups[1].Value;
                if (url.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase))
                {
                    u.Url = url;
                    string assetJson = json.Substring(prevEnd, m.Index - prevEnd);
                    Match dg = Regex.Match(assetJson, "\"digest\"\\s*:\\s*\"sha256:([0-9a-fA-F]{64})\"");
                    if (dg.Success) u.Sha256 = dg.Groups[1].Value.ToLowerInvariant();
                    Match sz = Regex.Match(assetJson, "\"size\"\\s*:\\s*(\\d+)");
                    if (sz.Success) u.Size = long.Parse(sz.Groups[1].Value);
                    break;
                }
                prevEnd = m.Index + m.Length;
            }
            if (u.Ver <= Current) return null;
            if (u.Url == null) throw new Exception("新版本 " + u.Tag + " 里没有找到安装包");
            return u;
        }

        // 在后台检查;结果通过 done 回调(在后台线程上,调用方自己切回界面线程)
        public static void CheckAsync(Action<UpdateInfo, string> done)
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
            {
                try { done(null, "上一次检查还没结束,请稍候"); } catch { }
                return;
            }
            Thread th = new Thread(delegate()
            {
                UpdateInfo u = null;
                string err = null;
                try { u = Check(); }
                catch (Exception ex) { err = ex.Message; }
                if (u != null) Available = u;
                Interlocked.Exchange(ref busy, 0);
                try { done(u, err); } catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        // 下载安装包到临时文件夹,并核对校验值;progress 参数是 0~100
        public static string Download(UpdateInfo u, Action<int> progress)
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Glean-Update");
            Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, "Glean-Setup-" + Regex.Replace(u.Tag, "[^0-9A-Za-z.\\-]", "") + ".exe");
            HttpWebRequest req = Request(u.Url);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream src = resp.GetResponseStream())
            using (FileStream fs = File.Create(file))
            {
                long total = resp.ContentLength > 0 ? resp.ContentLength : u.Size;
                byte[] buf = new byte[81920];
                long got = 0;
                int n, last = -1;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    fs.Write(buf, 0, n);
                    got += n;
                    if (total > 0)
                    {
                        int pct = (int)(got * 100 / total);
                        if (pct != last) { last = pct; progress(pct); }
                    }
                }
                // 网络中途断开时,下载到的文件会不完整
                if (total > 0 && got != total)
                {
                    fs.Close();
                    try { File.Delete(file); } catch { }
                    throw new Exception("下载没有完成(网络中断了),请再试一次");
                }
            }
            // 1) 有 GitHub 的校验值就核对,不一致说明文件被改过或没下完整
            if (u.Sha256 != null)
            {
                string actual;
                using (FileStream fs = File.OpenRead(file))
                using (SHA256 sha = SHA256.Create())
                    actual = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                if (actual != u.Sha256)
                {
                    try { File.Delete(file); } catch { }
                    throw new Exception("下载的文件校验不通过,已删除。请稍后再试");
                }
            }
            // 2) 确认确实是 Glean 的安装包
            FileVersionInfo vi = FileVersionInfo.GetVersionInfo(file);
            if (vi.ProductName == null || vi.ProductName.IndexOf(Brand.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                try { File.Delete(file); } catch { }
                throw new Exception("下载的文件不是 " + Brand.Name + " 安装包,已删除");
            }
            return file;
        }

        // 启动安装包的静默更新模式,然后退出自己;安装包会装好新版并重新打开
        public static void Launch(string setupFile)
        {
            Process.Start(new ProcessStartInfo(setupFile, "/update"));
            Application.Exit();
        }
    }

    // 更新窗口:显示新版本说明,下载进度,一键安装
    // 更新说明:自己画的文字区,细细的圆角滚动条跟随主题色调,不用系统那根白色滚动条
    class NotesView : Control
    {
        string text = "";
        int scroll, contentH;
        bool hot, dragging;
        int dragY, dragScroll;
        const TextFormatFlags Flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                                      TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;

        public NotesView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        // 把 Markdown 记号去掉,显示成干净的文字
        public static string Plain(string s)
        {
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
            s = Regex.Replace(s, @"__(.+?)__", "$1");
            s = Regex.Replace(s, @"`([^`]*)`", "$1");
            s = Regex.Replace(s, @"(?m)^[ \t]*#+[ \t]*", "");
            s = Regex.Replace(s, @"(?m)^([ \t]*)[-*+][ \t]+", "$1•  ");
            s = Regex.Replace(s, @"\r?\n", "\r\n");
            return s.Trim();
        }

        public string Notes
        {
            get { return text; }
            set { text = value ?? ""; scroll = 0; Measure(); Invalidate(); }
        }

        int TextW { get { return Math.Max(10, Width - Gfx.S(18)); } }
        int MaxScroll { get { return Math.Max(0, contentH - Height); } }

        void Measure()
        {
            contentH = text.Length == 0 ? 0 :
                TextRenderer.MeasureText(text, Font, new Size(TextW, int.MaxValue), Flags).Height + Gfx.S(6);
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Measure(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Measure(); }

        Rectangle Thumb()
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            int th = Math.Max(Gfx.S(28), Height * Height / Math.Max(1, contentH));
            int ty = (Height - th) * scroll / MaxScroll;
            int w = Gfx.S(hot || dragging ? 6 : 4);
            return new Rectangle(Width - Gfx.S(3) - w, ty, w, th);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            TextRenderer.DrawText(g, text, Font, new Rectangle(0, -scroll, TextW, contentH + Gfx.S(20)), ForeColor, BackColor, Flags);
            Rectangle th = Thumb();
            if (!th.IsEmpty)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Gfx.FillRound(g, Color.FromArgb(hot || dragging ? 120 : 70, ForeColor), th, hot || dragging ? 3 : 2);
            }
        }

        void ScrollTo(int v)
        {
            v = Math.Max(0, Math.Min(MaxScroll, v));
            if (v != scroll) { scroll = v; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollTo(scroll - e.Delta * Gfx.S(48) / 120);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hot = true; Invalidate();
            Focus();   // 鼠标放上来就能直接滚轮
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hot = false; Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (MaxScroll <= 0 || e.X < Width - Gfx.S(16)) return;
            Rectangle th = Thumb();
            if (e.Y < th.Top || e.Y > th.Bottom)   // 点在滑槽上:跳到那里
                ScrollTo(MaxScroll * (e.Y - th.Height / 2) / Math.Max(1, Height - th.Height));
            dragging = true;
            dragY = e.Y;
            dragScroll = scroll;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            int track = Math.Max(1, Height - Thumb().Height);
            ScrollTo(dragScroll + (e.Y - dragY) * MaxScroll / track);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging) { dragging = false; Capture = false; Invalidate(); }
        }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Up || k == Keys.Down || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int line = Font.Height;
            if (e.KeyCode == Keys.Down) ScrollTo(scroll + line);
            else if (e.KeyCode == Keys.Up) ScrollTo(scroll - line);
            else if (e.KeyCode == Keys.PageDown) ScrollTo(scroll + Height - line);
            else if (e.KeyCode == Keys.PageUp) ScrollTo(scroll - Height + line);
        }
    }

    class UpdateForm : Form
    {
        readonly UpdateInfo info;
        readonly NotesView notes = new NotesView();
        readonly Btn btnGo = new Btn();
        readonly Btn btnLater = new Btn();
        readonly Label status;
        int progress = -1;
        bool working;

        public UpdateForm(UpdateInfo u)
        {
            info = u;
            Theme t = Theme.Cur;
            Text = "软件更新";
            try { if (Brand.LargeIcon != null) Icon = Brand.LargeIcon; } catch { }
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ClientSize = new Size(Gfx.S(520), Gfx.S(420));
            BackColor = SetPal.Back;
            Font = Gfx.UI;
            DoubleBuffered = true;

            AddLabel("发现新版本", Gfx.S(112), Gfx.S(30), SetPal.PageTitle, t.Text);
            AddLabel(Brand.Name + " " + u.Tag.TrimStart('v', 'V') + "  ·  当前 " + Config.Version, Gfx.S(114), Gfx.S(68), Gfx.Small, t.Sub);

            notes.BackColor = SetPal.Card;
            notes.ForeColor = t.Text;
            notes.Font = Gfx.UI;
            notes.SetBounds(Gfx.S(40), Gfx.S(122), ClientSize.Width - Gfx.S(80) + Gfx.S(8), Gfx.S(176));
            notes.Notes = u.Notes.Length > 0 ? NotesView.Plain(u.Notes) : "这个版本没有写更新说明。";
            Controls.Add(notes);

            status = AddLabel("更新后历史记录和设置都会保留。", Gfx.S(32), Gfx.S(318), Gfx.Small, t.Sub);

            btnGo.Primary = true;
            btnGo.Text = "立即更新";
            btnGo.SetBounds(ClientSize.Width - Gfx.S(28 + 112), ClientSize.Height - Gfx.S(54), Gfx.S(112), Gfx.S(34));
            btnGo.Click += delegate { Start(); };
            Controls.Add(btnGo);

            btnLater.Text = "稍后";
            btnLater.SetBounds(ClientSize.Width - Gfx.S(28 + 112 + 10 + 86), ClientSize.Height - Gfx.S(54), Gfx.S(86), Gfx.S(34));
            btnLater.Click += delegate { if (!working) Close(); };
            Controls.Add(btnLater);

            Btn skip = new Btn();
            skip.Text = "跳过此版本";
            skip.SetBounds(Gfx.S(28), ClientSize.Height - Gfx.S(54), Gfx.S(110), Gfx.S(34));
            skip.Click += delegate
            {
                if (working) return;
                Config.SkipVersion = info.Tag;
                Config.Save();
                Close();
            };
            Controls.Add(skip);
        }

        Label AddLabel(string text, int x, int y, Font f, Color c)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = f;
            l.ForeColor = c;
            l.BackColor = SetPal.Back;
            l.AutoSize = true;
            l.MaximumSize = new Size(ClientSize.Width - x - Gfx.S(28), 0);
            l.Location = new Point(x, y);
            Controls.Add(l);
            return l;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DarkTitle(Handle, Theme.Cur.Dark);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Theme t = Theme.Cur;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 图标后面一圈柔和的光
            Rectangle logoR = new Rectangle(Gfx.S(30), Gfx.S(26), Gfx.S(68), Gfx.S(68));
            Gfx.DrawGlow(g, logoR, t.Accent, Gfx.S(22));
            Bitmap logo = Brand.Logo(Gfx.S(68));
            if (logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, logoR);
                logo.Dispose();
            }
            // 更新说明的卡片底
            Rectangle card = new Rectangle(Gfx.S(28), Gfx.S(110), ClientSize.Width - Gfx.S(56), Gfx.S(200));
            Gfx.FillRound(g, SetPal.Card, card, 10);
            // 下载进度条
            if (progress >= 0)
            {
                Rectangle bar = new Rectangle(Gfx.S(32), Gfx.S(346), ClientSize.Width - Gfx.S(64), Gfx.S(6));
                Gfx.FillRound(g, SetPal.Sep, bar, 3);
                int w = Math.Max(Gfx.S(6), bar.Width * Math.Min(100, progress) / 100);
                Gfx.FillRound(g, t.Accent, new Rectangle(bar.X, bar.Y, w, bar.Height), 3);
            }
        }

        void SetStatus(string s)
        {
            status.Text = s;
        }

        void Start()
        {
            if (working) return;
            working = true;
            btnGo.Text = "正在下载…";
            btnGo.Invalidate();
            progress = 0;
            Invalidate();
            Thread th = new Thread(delegate()
            {
                try
                {
                    string file = Updater.Download(info, delegate(int pct)
                    {
                        BeginInvoke(new MethodInvoker(delegate
                        {
                            progress = pct;
                            SetStatus("正在下载新版本… " + pct + "%");
                            Invalidate(new Rectangle(0, Gfx.S(340), ClientSize.Width, Gfx.S(20)));
                        }));
                    });
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        SetStatus("下载完成,正在安装,软件会自动重新打开…");
                        Refresh();
                        Updater.Launch(file);
                    }));
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        working = false;
                        progress = -1;
                        btnGo.Text = "重试";
                        btnGo.Invalidate();
                        SetStatus("更新失败:" + msg);
                        Invalidate();
                    }));
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && !working) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // ================================================================ 主窗口(不可见,只负责收消息)
    class MainForm : Form
    {
        const int HotkeyId = 1;

        History hist;
        readonly ShelfForm shelf;
        readonly NotifyIcon tray;
        ToolStripMenuItem openItem;
        DateTime pausedUntil = DateTime.MinValue;   // 暂停记录到这个时间;MaxValue = 一直暂停
        bool paused { get { return DateTime.Now < pausedUntil; } }
        readonly System.Windows.Forms.Timer pauseTimer = new System.Windows.Forms.Timer();
        bool hotkeyOk;
        bool settingsOpen;
        DateTime ignoreUntil = DateTime.MinValue;
        IntPtr prevWnd = IntPtr.Zero;
        readonly System.Windows.Forms.Timer updateTimer = new System.Windows.Forms.Timer();
        bool updateBalloon;

        public MainForm()
        {
            ShowInTaskbar = false;
            try { hist = new History(Config.DataPath); }
            catch { hist = new History(Config.DefaultDir); }   // 自定义文件夹不可用时回退到默认位置
            hist.Purge(Config.KeepDays);
            shelf = new ShelfForm(this, hist);

            tray = new NotifyIcon();
            tray.Icon = Brand.SmallIcon ?? MakeIcon();
            tray.Text = TrayTip();
            tray.ContextMenuStrip = BuildMenu();
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) TogglePopup(false);
            };
            tray.Visible = true;
            pauseTimer.Tick += delegate
            {
                if (pausedUntil != DateTime.MinValue && !paused) SetPause(TimeSpan.Zero);
            };
            // 退出前,把还能撤销的删除真正完成
            Application.ApplicationExit += delegate { try { hist.FinishPendingDelete(); } catch { } };

            // 如果开过开机自启,把启动项更新成当前这个 exe 的位置(换名字/换文件夹后也能正常自启)
            try { if (Startup.Get()) Startup.Set(true); } catch { }

            // 自动更新:启动 30 秒后检查一次,之后每小时看一眼是否到了"每天一次"
            tray.BalloonTipClicked += delegate
            {
                if (updateBalloon && Updater.Available != null) ShowUpdate(Updater.Available);
            };
            updateTimer.Interval = 30 * 1000;
            updateTimer.Tick += delegate
            {
                updateTimer.Interval = 60 * 60 * 1000;
                if (!Config.AutoUpdate) return;
                if ((DateTime.Now - new DateTime(Math.Max(0, Config.LastUpdateCheck))).TotalHours < 24) return;
                Updater.CheckAsync(delegate(UpdateInfo u, string err)
                {
                    if (u == null || u.Tag == Config.SkipVersion) return;
                    try { BeginInvoke(new MethodInvoker(delegate { NotifyUpdate(u); })); } catch { }
                });
            };
            updateTimer.Start();

            // 启动 3 秒后,在后台把面板的窗口先建好:第一次按 Win + V 不用再等
            System.Windows.Forms.Timer warm = new System.Windows.Forms.Timer();
            warm.Interval = 3000;
            warm.Tick += delegate
            {
                warm.Stop();
                warm.Dispose();
                try { shelf.Prewarm(); } catch { }
            };
            warm.Start();

            IntPtr unused = this.Handle;   // 强制创建窗口句柄,用来接收剪切板和热键消息
        }

        // 始终保持不可见
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        string TrayTip()
        {
            string s = Brand.Name + " " + Brand.NameCn + "  v" + Config.Version + "  ·  " + Config.HotkeyText + (Config.WinV ? " / Win+V" : "");
            if (paused) s = Brand.Name + " " + Brand.NameCn + "  ·  已暂停记录";
            return s.Length > 63 ? s.Substring(0, 63) : s;   // 托盘提示最多 63 个字
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            openItem = new ToolStripMenuItem("打开剪切板历史    " + Config.HotkeyText);
            openItem.Click += delegate { TogglePopup(false); };
            m.Items.Add(openItem);
            m.Items.Add("打开文件夹", null, delegate { shelf.ShowFiles(); });
            m.Items.Add("检查更新…", null, delegate { CheckUpdateNow(); });
            m.Items.Add("设置…", null, delegate { ShowSettings(); });
            m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem pause = new ToolStripMenuItem("暂停记录");
            m.Items.Add(pause);

            ToolStripMenuItem auto = new ToolStripMenuItem("开机自动启动");
            auto.CheckOnClick = true;
            auto.Checked = Startup.Get();
            auto.Click += delegate
            {
                try { Startup.Set(auto.Checked); }
                catch { auto.Checked = Startup.Get(); }
            };
            m.Items.Add(auto);

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("打开数据文件夹", null, delegate { Process.Start("explorer.exe", hist.Dir); });
            m.Items.Add("清空历史(保留置顶)", null, delegate
            {
                if (MessageBox.Show("确定清空历史记录吗?置顶的记录会保留。", Brand.Name,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    hist.Clear(true);
            });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("退出", null, delegate
            {
                tray.Visible = false;
                Application.Exit();
            });

            m.Opening += delegate
            {
                BuildPauseMenu(pause);
                auto.Checked = Startup.Get();
                Theme.Refresh();
                Theme.StyleMenu(m);
            };
            return m;
        }

        // 暂停记录:可以选一段时间,到点自动恢复
        void BuildPauseMenu(ToolStripMenuItem pause)
        {
            pause.DropDownItems.Clear();
            if (paused)
            {
                pause.Text = "已暂停记录" + PauseLeftText();
                pause.Checked = true;
                pause.DropDownItems.Add("恢复记录", null, delegate { SetPause(TimeSpan.Zero); });
                pause.DropDownItems.Add(new ToolStripSeparator());
            }
            else
            {
                pause.Text = "暂停记录";
                pause.Checked = false;
            }
            pause.DropDownItems.Add("暂停 5 分钟", null, delegate { SetPause(TimeSpan.FromMinutes(5)); });
            pause.DropDownItems.Add("暂停 30 分钟", null, delegate { SetPause(TimeSpan.FromMinutes(30)); });
            pause.DropDownItems.Add("暂停 1 小时", null, delegate { SetPause(TimeSpan.FromHours(1)); });
            pause.DropDownItems.Add("暂停到明天", null, delegate { SetPause(DateTime.Today.AddDays(1) - DateTime.Now); });
            pause.DropDownItems.Add("一直暂停(手动恢复)", null, delegate { SetPause(TimeSpan.MaxValue); });
        }

        string PauseLeftText()
        {
            if (pausedUntil == DateTime.MaxValue) return "";
            TimeSpan left = pausedUntil - DateTime.Now;
            if (left.TotalMinutes < 1) return "(不到 1 分钟后恢复)";
            if (left.TotalMinutes < 60) return "(" + (int)Math.Ceiling(left.TotalMinutes) + " 分钟后恢复)";
            return "(" + pausedUntil.ToString("HH:mm", CultureInfo.InvariantCulture) + " 恢复)";
        }

        void SetPause(TimeSpan span)
        {
            pauseTimer.Stop();
            if (span <= TimeSpan.Zero)
            {
                pausedUntil = DateTime.MinValue;
                tray.Text = TrayTip();
                tray.ShowBalloonTip(2000, Brand.Name, "已恢复记录剪切板", ToolTipIcon.Info);
                return;
            }
            pausedUntil = span == TimeSpan.MaxValue ? DateTime.MaxValue : DateTime.Now + span;
            string tip = pausedUntil == DateTime.MaxValue ? "已暂停记录,右键托盘图标可恢复"
                : "已暂停记录,将在 " + pausedUntil.ToString("HH:mm", CultureInfo.InvariantCulture) + " 自动恢复";
            tray.Text = TrayTip();
            tray.ShowBalloonTip(2500, Brand.Name, tip, ToolTipIcon.Info);
            if (pausedUntil != DateTime.MaxValue)
            {
                // 到点自动恢复(每 30 秒看一眼,电脑睡眠醒来也不会错过)
                pauseTimer.Interval = 30 * 1000;
                pauseTimer.Start();
            }
        }

        static Icon MakeIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush body = new SolidBrush(Color.FromArgb(60, 120, 210)))
                    g.FillRectangle(body, 5, 5, 22, 25);
                g.FillRectangle(Brushes.White, 8, 10, 16, 17);
                g.FillRectangle(Brushes.DimGray, 11, 2, 10, 7);
                using (Pen p = new Pen(Color.FromArgb(60, 120, 210), 2))
                {
                    g.DrawLine(p, 11, 15, 21, 15);
                    g.DrawLine(p, 11, 19, 21, 19);
                    g.DrawLine(p, 11, 23, 18, 23);
                }
                return (Icon)Icon.FromHandle(bmp.GetHicon()).Clone();
            }
        }

        // ------------------------------------------------ 窗口消息
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.AddClipboardFormatListener(Handle);
            hotkeyOk = RegisterHotkey();
            UpdateWinV();
            updateBalloon = false;
            if (hotkeyOk)
                tray.ShowBalloonTip(2500, Brand.Name + " " + Brand.NameCn + " 已在后台运行",
                    "按 " + (Config.WinV ? "Win + V 或 " : "") + Config.HotkeyText + " 呼出剪切板历史;右键托盘图标可打开设置", ToolTipIcon.Info);
            else
                tray.ShowBalloonTip(4000, Brand.Name,
                    "热键 " + Config.HotkeyText + " 被其他程序占用,请点击托盘图标打开,或在设置里换一个", ToolTipIcon.Warning);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            WinVHook.Uninstall();
            Native.UnregisterHotKey(Handle, HotkeyId);
            Native.RemoveClipboardFormatListener(Handle);
            base.OnHandleDestroyed(e);
        }

        // 按设置开/关 Win+V;钩子回调在界面线程里执行,直接切换弹窗即可
        void UpdateWinV()
        {
            if (Config.WinV)
            {
                bool ok = false;
                try
                {
                    ok = WinVHook.Install(delegate { BeginInvoke(new MethodInvoker(delegate { TogglePopup(true); })); });
                }
                catch { ok = false; }
                if (!ok)
                    tray.ShowBalloonTip(3000, Brand.Name, "Win + V 接管失败(可能被安全软件拦截),请使用 " + Config.HotkeyText, ToolTipIcon.Warning);
            }
            else WinVHook.Uninstall();
        }

        void NotifyUpdate(UpdateInfo u)
        {
            updateBalloon = true;
            tray.ShowBalloonTip(6000, "发现 " + Brand.Name + " 新版本 " + u.Tag.TrimStart('v', 'V'),
                "点这里查看更新内容,一键升级。", ToolTipIcon.Info);
        }

        bool updateOpen;

        void ShowUpdate(UpdateInfo u)
        {
            if (updateOpen) return;
            updateOpen = true;
            try
            {
                using (UpdateForm f = new UpdateForm(u)) f.ShowDialog();
            }
            finally { updateOpen = false; }
        }

        // 托盘菜单里的"检查更新…":有新版本就打开更新窗口,没有就提示一下
        void CheckUpdateNow()
        {
            Updater.CheckAsync(delegate(UpdateInfo u, string err)
            {
                try
                {
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        updateBalloon = false;
                        if (u != null) ShowUpdate(u);
                        else if (err != null) tray.ShowBalloonTip(3000, Brand.Name, "检查更新失败:" + err, ToolTipIcon.Warning);
                        else tray.ShowBalloonTip(3000, Brand.Name, "已经是最新版本(" + Config.Version + ")", ToolTipIcon.Info);
                    }));
                }
                catch { }
            });
        }

        bool RegisterHotkey()
        {
            Native.UnregisterHotKey(Handle, HotkeyId);
            return Native.RegisterHotKey(Handle, HotkeyId, Config.Modifiers | 0x4000, (uint)Config.Key);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
            }
            else if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                TogglePopup(true);
            }
            base.WndProc(ref m);
        }

        void TogglePopup(bool fromHotkey)
        {
            if (shelf.Visible)
            {
                if (!shelf.BringFrontIfHidden()) shelf.HideShelf();   // 文件夹窗口被挡住了:先叫到前面
                return;
            }
            // 记住当前窗口,选中后好粘贴回去;从托盘点开时不知道目标窗口,只复制
            prevWnd = fromHotkey ? Native.GetForegroundWindow() : IntPtr.Zero;
            hist.Purge(Config.KeepDays);
            shelf.ShowAt();   // 卡片 / 列表两种风格都由同一个面板负责
        }

        // ------------------------------------------------ 设置
        public void ShowSettingsFromPopup()
        {
            BeginInvoke(new MethodInvoker(ShowSettings));
        }

        void ShowSettings()
        {
            if (settingsOpen) return;
            settingsOpen = true;
            try
            {
                Theme.Refresh();
                string oldDir = hist.Dir;
                using (SettingsForm f = new SettingsForm())
                {
                    if (f.ShowDialog() == DialogResult.OK) ApplySettings(oldDir, f.MoveExisting);
                }
            }
            finally { settingsOpen = false; }
        }

        void ApplySettings(string oldDir, bool move)
        {
            Theme.Refresh();
            string newDir = Config.DataPath;
            if (!Util.SamePath(newDir, oldDir))
            {
                string err;
                if (!hist.SwitchTo(newDir, move, out err))
                {
                    MessageBox.Show("无法切换数据文件夹:" + err + "\n将继续使用原来的位置。", Brand.Name,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Config.DataDir = Util.SamePath(oldDir, Config.DefaultDir) ? "" : oldDir;
                    Config.Save();
                }
            }
            hist.Trim();
            hist.Purge(Config.KeepDays);

            hotkeyOk = RegisterHotkey();
            UpdateWinV();
            tray.Text = TrayTip();
            openItem.Text = "打开剪切板历史    " + Config.HotkeyText;
            if (!hotkeyOk)
                tray.ShowBalloonTip(4000, Brand.Name,
                    "热键 " + Config.HotkeyText + " 被其他程序占用,请在设置里换一个", ToolTipIcon.Warning);
        }

        // ------------------------------------------------ 监听剪切板
        void OnClipboardChanged()
        {
            if (paused || DateTime.Now < ignoreUntil) return;
            if (OwnerIgnored()) return;
            try { Util.Retry(ReadClipboard); }
            catch { }
        }

        // 剪切板内容来自"忽略名单"里的程序(如密码管理器)时不记录
        bool OwnerIgnored()
        {
            try
            {
                string list = Config.IgnoreApps;
                if (string.IsNullOrWhiteSpace(list)) return false;
                // 用真正的来源程序判断(远程控制软件转手写入剪切板时,看的是你实际复制的那个程序)
                string path, disp, exe;
                Util.ClipboardSource(out path, out disp, out exe);
                if (exe == null && path != null) exe = System.IO.Path.GetFileNameWithoutExtension(path);
                if (exe == null) return false;
                string name = exe.ToLowerInvariant();
                foreach (string a in list.Split(new char[] { ',', ';', '，', '；' }))
                {
                    string x = a.Trim().ToLowerInvariant();
                    if (x.EndsWith(".exe")) x = x.Substring(0, x.Length - 4);
                    if (x.Length > 0 && x == name) return true;
                }
            }
            catch { }
            return false;
        }

        void ReadClipboard()
        {
            IDataObject d = Clipboard.GetDataObject();
            if (d == null) return;
            // 密码管理器等会标记"不要记录"
            if (d.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing")) return;

            Item added = null;
            if (d.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = d.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) added = hist.AddFiles(files);
            }
            else if (d.GetDataPresent(DataFormats.UnicodeText))
            {
                string t = d.GetData(DataFormats.UnicodeText) as string;
                if (t != null) added = hist.AddText(t);
            }
            else if (Config.RecordImages && d.GetDataPresent(DataFormats.Bitmap))
            {
                // 大截图编码成 PNG 要几十到几百毫秒:放到后台去做,界面和正在打字的程序都不会被卡住
                Image img = Clipboard.GetImage();
                if (img != null)
                {
                    string srcPath, srcName;
                    Util.ClipboardOwnerApp(out srcPath, out srcName);   // 来源程序现在就记下(之后剪切板可能已经变了)
                    DateTime when = DateTime.Now;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        byte[] png = null;
                        try
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                img.Save(ms, ImageFormat.Png);
                                png = ms.ToArray();
                            }
                        }
                        catch { png = null; }
                        finally { img.Dispose(); }
                        if (png == null) return;
                        string hash = Util.Md5Short(png);
                        try
                        {
                            BeginInvoke(new MethodInvoker(delegate
                            {
                                try
                                {
                                    Item it = hist.AddImagePng(png, hash, when);
                                    if (it != null) hist.SetSource(it, srcPath, srcName);
                                }
                                catch { }
                            }));
                        }
                        catch { }
                    });
                }
            }
            // 记下内容来自哪个程序(卡片上显示它的图标和名字)
            if (added != null)
            {
                string appPath, appName;
                Util.ClipboardOwnerApp(out appPath, out appName);
                hist.SetSource(added, appPath, appName);
            }
        }

        public void Balloon(string text, bool warn)
        {
            tray.ShowBalloonTip(warn ? 6000 : 2500, Brand.Name, text, warn ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }

        // 供弹出窗口调用:把一段新文本记入历史(用于"粘贴为…")
        public Item Record(string text)
        {
            try { return hist.AddText(text); }
            catch { return null; }
        }

        // ------------------------------------------------ 选中一条:放入剪切板并(可选)粘贴
        public void Apply(Item it, bool paste)
        {
            ignoreUntil = DateTime.Now.AddMilliseconds(800);   // 避免把自己写入的内容又记一遍
            try
            {
                if (it.IsImage)
                {
                    using (Bitmap bmp = Util.LoadBitmap(it.FilePath))
                        Util.Retry(delegate { Clipboard.SetImage(bmp); });
                }
                else if (it.IsFiles)
                {
                    List<string> exist = it.Paths.Where(x => File.Exists(x) || Directory.Exists(x)).ToList();
                    if (exist.Count == 0)
                    {
                        tray.ShowBalloonTip(2500, Brand.Name, "这些文件已被移动或删除,无法再粘贴", ToolTipIcon.Warning);
                        return;
                    }
                    StringCollection sc = new StringCollection();
                    sc.AddRange(exist.ToArray());
                    DataObject dobj = new DataObject();
                    dobj.SetFileDropList(sc);                                  // 粘贴到资源管理器 = 复制文件
                    dobj.SetText(string.Join("\r\n", exist.ToArray()));        // 粘贴到文本框 = 文件路径
                    Util.Retry(delegate { Clipboard.SetDataObject(dobj, true); });
                }
                else
                {
                    string text = it.Text;
                    Util.Retry(delegate { Clipboard.SetText(text, TextDataFormat.UnicodeText); });
                }
            }
            catch
            {
                tray.ShowBalloonTip(2000, Brand.Name, "写入剪切板失败,请重试", ToolTipIcon.Warning);
                return;
            }
            hist.Touch(it);

            if (paste && Config.AutoPaste && prevWnd != IntPtr.Zero)
            {
                Native.SetForegroundWindow(prevWnd);
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 120;
                t.Tick += delegate
                {
                    t.Stop();
                    t.Dispose();
                    SendCtrlV();
                };
                t.Start();
            }
        }

        static void SendCtrlV()
        {
            Native.keybd_event(0x11, 0, 0, UIntPtr.Zero);   // Ctrl down
            Native.keybd_event(0x56, 0, 0, UIntPtr.Zero);   // V down
            Native.keybd_event(0x56, 0, 2, UIntPtr.Zero);   // V up
            Native.keybd_event(0x11, 0, 2, UIntPtr.Zero);   // Ctrl up
        }
    }

    // ================================================================ 入口
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\Glean_Singleton", out created))
            {
                if (!created) return;   // 已经在运行
                try { Native.SetProcessDPIAware(); } catch { }   // 声明支持高分屏,文字才不会被系统拉伸发糊
                Gfx.InitDpi();
                Config.Load();
                try
                {
                    Directory.CreateDirectory(Config.DefaultDir);
                    System.Runtime.ProfileOptimization.SetProfileRoot(Config.DefaultDir);
                    System.Runtime.ProfileOptimization.StartProfile("startup.jitprofile");
                }
                catch { }
                Theme.Refresh();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
