using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class BuildIcons
{
    private static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    private static Bitmap Draw(int size, Color top, Color bottom)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size / 64f, size / 64f);
            using (var tile = Round(new RectangleF(2, 2, 60, 60), 16))
            using (var gradient = new LinearGradientBrush(new PointF(5, 4), new PointF(57, 62), top, bottom))
                g.FillPath(gradient, tile);
            using (var pen = new Pen(Color.FromArgb(248, 255, 251), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawArc(pen, 14, 15, 36, 36, -55, 290);
                g.DrawLine(pen, 32, 10, 32, 23);
                g.DrawLines(pen, new[] { new PointF(32, 29), new PointF(32, 34), new PointF(40, 39) });
            }
        }
        return bitmap;
    }
    private static void SaveIco(string path, Color top, Color bottom)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 };
        var data = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
            using (var bitmap = Draw(sizes[i], top, bottom))
            using (var stream = new MemoryStream())
            {
                if (sizes[i] == 256) bitmap.Save(stream, ImageFormat.Png);
                else WriteDib(stream, bitmap);
                data[i] = stream.ToArray();
            }
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(data[i].Length); writer.Write(offset); offset += data[i].Length;
            }
            foreach (byte[] item in data) writer.Write(item);
        }
    }
    private static void WriteDib(Stream stream, Bitmap bitmap)
    {
        int size = bitmap.Width;
        int maskRow = (size + 31) / 32 * 4;
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(40); writer.Write(size); writer.Write(size * 2);
            writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(0);
            writer.Write(size * size * 4 + maskRow * size);
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for (int y = size - 1; y >= 0; y--)
                for (int x = 0; x < size; x++)
                {
                    Color c = bitmap.GetPixel(x, y);
                    writer.Write(c.B); writer.Write(c.G); writer.Write(c.R); writer.Write(c.A);
                }
            for (int y = size - 1; y >= 0; y--)
            {
                byte[] mask = new byte[maskRow];
                for (int x = 0; x < size; x++)
                    if (bitmap.GetPixel(x, y).A == 0) mask[x / 8] |= (byte)(128 >> (x % 8));
                writer.Write(mask);
            }
        }
    }
    private static int Main(string[] args)
    {
        string output = args[0]; Directory.CreateDirectory(output);
        Color greenTop = Color.FromArgb(89, 190, 155), greenBottom = Color.FromArgb(31, 130, 104);
        SaveIco(Path.Combine(output, "app.ico"), greenTop, greenBottom);
        SaveIco(Path.Combine(output, "paused.ico"), Color.FromArgb(172, 190, 181), Color.FromArgb(114, 137, 124));
        SaveIco(Path.Combine(output, "countdown.ico"), Color.FromArgb(242, 192, 103), Color.FromArgb(193, 131, 49));
        using (var png = Draw(512, greenTop, greenBottom)) png.Save(Path.Combine(output, "软件图标.png"));
        Console.WriteLine("Generated 10 icon sizes and three status colors."); return 0;
    }
}
