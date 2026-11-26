using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
static class TexTest
{
    static void Out(string s) { File.AppendAllText("out.txt", s + "\r\n"); }
    [STAThread]
    static int Main()
    {
        Glean.Embedded.Hook();
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "glean-clip-test.png");
            using (Bitmap b = new Bitmap(400, 300, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); g.FillEllipse(Brushes.Red, 100, 50, 200, 200); }
                b.SetResolution(300, 300);
                b.Save(path, ImageFormat.Png);
            }
            byte[] raw = File.ReadAllBytes(path);
            DataObject d = Glean.ClipImage.Build(path);
            Clipboard.SetDataObject(d, true);
            IDataObject back = Clipboard.GetDataObject();
            Out("formats: " + string.Join(", ", back.GetFormats()));
            MemoryStream png = back.GetData("PNG") as MemoryStream;
            byte[] pb = png == null ? null : png.ToArray();
            Out("PNG same as file: " + (pb != null && pb.Length == raw.Length && Convert.ToBase64String(pb) == Convert.ToBase64String(raw)));
            MemoryStream dib = back.GetData(DataFormats.Dib) as MemoryStream;
            byte[] db = dib.ToArray();
            Out("DIB w=" + BitConverter.ToInt32(db, 4) + " h=" + BitConverter.ToInt32(db, 8) + " bpp=" + BitConverter.ToInt16(db, 14) + " dpi=" + Math.Round(BitConverter.ToInt32(db, 24) * 0.0254));
            // 左下角(透明处)像素:位图从下往上存,第一行就是最底下一行
            Out("DIB corner pixel BGR=" + db[40] + "," + db[41] + "," + db[42]);
            int stride = (400 * 3 + 3) & ~3;
            int row = 300 - 1 - 150, off = 40 + row * stride + 200 * 3;
            Out("DIB center pixel BGR=" + db[off] + "," + db[off + 1] + "," + db[off + 2]);
            Image gi = Clipboard.GetImage();
            Out("GetImage " + (gi == null ? "null" : gi.Width + "x" + gi.Height));
            using (MemoryStream ms = new MemoryStream(pb)) using (Image im = Image.FromStream(ms))
                Out("PNG back: " + im.Width + "x" + im.Height + " dpi " + im.HorizontalResolution + " alpha at corner " + ((Bitmap)im).GetPixel(5, 5).A);
            string named = Glean.ClipImage.NamedCopy(path, new DateTime(2026, 11, 26, 15, 30, 0));
            Out("named copy: " + Path.GetFileName(named) + " exists " + File.Exists(named) + " in temp " + named.StartsWith(Path.GetTempPath()));
            // JPG 原图也能给出 PNG
            string jp = Path.Combine(Path.GetTempPath(), "glean-clip-test.jpg");
            using (Bitmap b = new Bitmap(200, 100)) { using (Graphics g = Graphics.FromImage(b)) g.Clear(Color.SteelBlue); b.Save(jp, ImageFormat.Jpeg); }
            DataObject d2 = Glean.ClipImage.Build(jp);
            Out("jpg -> formats: " + string.Join(", ", d2.GetFormats()) + " png bytes " + ((MemoryStream)d2.GetData("PNG")).Length);
        }
        catch (Exception ex) { Out("ERROR " + ex); }
        return 0;
    }
}
