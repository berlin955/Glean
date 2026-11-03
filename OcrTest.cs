using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
namespace OcrTestNs
{
    static class TestMain
    {
        static int Main()
        {
            Console.WriteLine("Available: " + Glean.Ocr.Available);
            try
            {
                Type te = Type.GetType("Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime");
                Type tl = Type.GetType("Windows.Globalization.Language, Windows.Foundation, ContentType=WindowsRuntime");
                object langs = te.GetProperty("AvailableRecognizerLanguages").GetValue(null, null);
                foreach (object l in (System.Collections.IEnumerable)langs)
                    Console.WriteLine("lang: " + tl.GetProperty("LanguageTag").GetValue(l, null));
            }
            catch (Exception ex) { Console.WriteLine("lang list error: " + ex); }
            string[] samples = { "Hello Glean OCR test 12345", "拾光 识别测试 你好世界" };
            for (int i = 0; i < samples.Length; i++)
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ocr-sample-" + i + ".png");
                using (Bitmap b = new Bitmap(900, 120))
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.Clear(Color.White);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (Font f = new Font("Microsoft YaHei", 28f)) g.DrawString(samples[i], f, Brushes.Black, 20, 30);
                    b.Save(path, ImageFormat.Png);
                }
                try
                {
                    bool noZh;
                    string r = Glean.Ocr.Recognize(path, out noZh);
                    Console.WriteLine("sample " + i + " noChinese=" + noZh + " => [" + r + "]");
                }
                catch (Exception ex) { Console.WriteLine("sample " + i + " ERROR: " + Glean.Ocr.Reason(ex) + "\n" + ex); }
            }
            return 0;
        }
    }
}
