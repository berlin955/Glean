using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
static class TexTest
{
    [STAThread]
    static int Main()
    {
        Directory.CreateDirectory("dl");
        byte[] big = new byte[20 * 1024 * 1024];
        new Random(5).NextBytes(big);
        string f1 = Path.GetFullPath("20261103_143205_123_abcdef0123456789.png");
        File.WriteAllBytes(f1, big);
        string f2 = Path.GetFullPath("会议纪要 最终版.pdf");
        File.WriteAllBytes(f2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        string err;
        string url = Glean.ShareServer.Start("测试", null, new List<string> { f1, f2 }, out err);
        File.WriteAllText("url.txt", url ?? ("ERR " + err));
        Thread.Sleep(90000);   // 让外面的 curl 测试
        Glean.ShareServer.Stop();
        return 0;
    }
}
