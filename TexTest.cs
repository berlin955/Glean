using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
static class TexTest
{
    [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, uint flags);
    static int Measure(Form f, TextBox tb)
    {
        Application.DoEvents();
        System.Threading.Thread.Sleep(300);
        Application.DoEvents();
        Rectangle r = tb.RectangleToScreen(tb.ClientRectangle);
        using (Bitmap b = new Bitmap(r.Width, r.Height))
        {
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
            int darker = 0, mid = 0, ink = 0;
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    Color c = b.GetPixel(x, y);
                    int s = c.R + c.G + c.B;
                    if (s < 60) darker++;                 // 比字的颜色(38,33,28 → 99)还黑
                    else if (s < 140) ink++;
                    else if (s < 700) mid++;              // 抗锯齿的过渡像素
                }
            return darker * 100000 + mid;
        }
    }
    static void Run(bool redraw)
    {
        Form f = new Form();
        f.FormBorderStyle = FormBorderStyle.None;
        f.AllowTransparency = true;
        f.BackColor = Color.White;
        f.Bounds = new Rectangle(100, 100, 600, 300);
        TextBox tb = new TextBox();
        tb.Multiline = true; tb.BorderStyle = BorderStyle.None; tb.ReadOnly = true;
        tb.BackColor = Color.White; tb.ForeColor = Color.FromArgb(38, 33, 28);
        tb.Font = new Font("Microsoft YaHei UI", 20f);
        tb.Text = "3316980649089088956 测试文字";
        tb.Bounds = new Rectangle(20, 20, 560, 260);
        f.Controls.Add(tb);
        f.Opacity = 0;
        f.Show();
        for (int k = 1; k <= 10; k++) { f.Opacity = k / 10.0; Application.DoEvents(); System.Threading.Thread.Sleep(20); }
        if (redraw) RedrawWindow(f.Handle, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0004 | 0x0080 | 0x0400);
        int m = Measure(f, tb);
        System.IO.File.AppendAllText("out.txt", "FADE redraw=" + redraw + " darkerThanInk=" + (m / 100000) + " smoothEdge=" + (m % 100000) + "\r\n");
        f.Close();
    }
    [STAThread]
    static int Main()
    {
        Run(false);
        Run(true);
        return 0;
    }
}
