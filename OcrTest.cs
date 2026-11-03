using System;
using System.Drawing;
using System.Drawing.Imaging;
namespace OcrTestNs
{
    static class TestMain
    {
        static int Main()
        {
            string text = "复制的文字:Windows 10 自带识别,价格 128.5 元。\n拾光 识别测试 你好世界,剪切板历史记录\nHello Glean OCR test 12345, version 0.10";
            float[] sizes = { 9f, 10.5f, 14f, 28f };
            double[] ups = { 1.0, 1.5, 2.0, 3.0 };
            foreach (float fs in sizes)
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ocr-" + fs + ".png");
                using (Font f = new Font("Microsoft YaHei UI", fs))
                {
                    Size sz;
                    using (Bitmap m = new Bitmap(1, 1)) using (Graphics mg = Graphics.FromImage(m)) sz = Size.Ceiling(mg.MeasureString(text, f));
                    using (Bitmap b = new Bitmap(sz.Width + 40, sz.Height + 30))
                    using (Graphics g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.White);
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        g.DrawString(text, f, Brushes.Black, 20, 15);
                        b.Save(path, ImageFormat.Png);
                    }
                }
                foreach (double u in ups)
                {
                    Glean.Ocr.Upscale = u;
                    try
                    {
                        bool noZh;
                        string r = Glean.Ocr.Recognize(path, out noZh);
                        Console.WriteLine("font " + fs + " up " + u + " => " + r.Replace("\r\n", " | "));
                    }
                    catch (Exception ex) { Console.WriteLine("font " + fs + " up " + u + " ERROR " + Glean.Ocr.Reason(ex)); }
                }
            }
            return 0;
        }
    }
}
