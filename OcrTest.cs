using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
namespace OcrTestNs
{
    static class TestMain
    {
        static string Norm(string s)
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c)) continue;
                char d = c;
                if (d == '“' || d == '”') d = '"';
                if (d == '：') d = ':';
                if (d == '，') d = ',';
                b.Append(d);
            }
            return b.ToString();
        }

        static int Lev(string a, string b)
        {
            int[] prev = new int[b.Length + 1], cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                int[] t = prev; prev = cur; cur = t;
            }
            return prev[b.Length];
        }

        static int Main()
        {
            string truth = "Glean 拾光 0.6\n\n这一版让列表模式下的“文件夹”变成了一个能长期摆在桌面上的小窗口。\n\n• 移动:按住窗口顶部或底部的空白处就能拖着走,靠近屏幕边缘会自动贴齐\n• 调整大小:拖窗口的四边或四个角\n• 记住位置:下次打开文件夹,还在上次放的地方、还是那么大;剪切板列表照旧在鼠标旁弹出\n• 图钉:右上角的图钉默认钉住(一直在最前面);点掉后像普通窗口一样,会出现在任务栏\n复制的文字:Windows 10 自带识别,价格 128.5 元。你好世界,识别测试,他们很快就会处理这个问题。";
            float[] sizes = { 10.5f, 12f, 15f, 18f, 22f };
            string[] cfgs = { "old", "adaptive", "adaptive+invert", "adaptive+invert t20", "adaptive+invert t28" };
            Dictionary<string, double> total = new Dictionary<string, double>();
            foreach (string c in cfgs) total[c] = 0;
            int n = 0;
            foreach (bool dark in new bool[] { true, false })
            foreach (float fs in sizes)
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "t-" + dark + fs + ".png");
                using (Font f = new Font("Microsoft YaHei UI", fs))
                {
                    int width = (int)(fs * 52);
                    SizeF sz;
                    using (Bitmap m = new Bitmap(1, 1)) using (Graphics mg = Graphics.FromImage(m)) sz = mg.MeasureString(truth, f, width);
                    using (Bitmap b = new Bitmap(width + 60, (int)sz.Height + 50))
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(dark ? Color.FromArgb(43, 40, 38) : Color.FromArgb(250, 248, 245));
                        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                        using (SolidBrush br = new SolidBrush(dark ? Color.FromArgb(240, 236, 230) : Color.FromArgb(38, 33, 28)))
                            g.DrawString(truth, f, br, new RectangleF(30, 25, width, sz.Height + 10));
                        b.Save(path, ImageFormat.Png);
                    }
                }
                n++;
                foreach (string c in cfgs)
                {
                    Glean.Ocr.Adaptive = c != "old";
                    Glean.Ocr.InvertDark = c.Contains("invert");
                    Glean.Ocr.TargetHeight = c.EndsWith("t20") ? 20 : c.EndsWith("t28") ? 28 : 24;
                    string r;
                    try { bool nz; r = Glean.Ocr.Recognize(path, out nz); }
                    catch (Exception ex) { r = "ERR " + Glean.Ocr.Reason(ex); }
                    string a = Norm(truth), o = Norm(r);
                    double cer = Lev(a, o) / (double)a.Length;
                    total[c] += cer;
                    Console.WriteLine(string.Format("RES {0,-5} {1,4} {2,-20} CER {3:P1}", dark ? "dark" : "light", fs, c, cer));
                    if (dark && fs == 18f) Console.WriteLine("TXT " + c + ": " + r.Replace("\r\n", " | "));
                }
            }
            foreach (string c in cfgs) Console.WriteLine(string.Format("AVG {0,-20} {1:P1}", c, total[c] / n));
            return 0;
        }
    }
}
