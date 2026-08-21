using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
string outputPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "WinDND.ico"));

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
List<byte[]> images = sizes.Select(RenderPng).ToList();

using FileStream output = File.Create(outputPath);
using BinaryWriter writer = new(output);
writer.Write((ushort)0);
writer.Write((ushort)1);
writer.Write((ushort)images.Count);

int imageOffset = 6 + (16 * images.Count);
for (int index = 0; index < images.Count; index++)
{
    int size = sizes[index];
    byte dimension = size == 256 ? (byte)0 : (byte)size;
    writer.Write(dimension);
    writer.Write(dimension);
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write(images[index].Length);
    writer.Write(imageOffset);
    imageOffset += images[index].Length;
}

foreach (byte[] image in images)
{
    writer.Write(image);
}

static byte[] RenderPng(int size)
{
    using Bitmap bitmap = new(size, size, PixelFormat.Format32bppArgb);
    using Graphics graphics = Graphics.FromImage(bitmap);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.CompositingQuality = CompositingQuality.HighQuality;
    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    graphics.Clear(Color.Transparent);

    float scale = size / 256f;
    using GraphicsPath background = RoundedRectangle(new RectangleF(8 * scale, 8 * scale, 240 * scale, 240 * scale), 52 * scale);
    using LinearGradientBrush backgroundBrush = new(
        new PointF(32 * scale, 24 * scale),
        new PointF(224 * scale, 232 * scale),
        Color.FromArgb(39, 124, 201),
        Color.FromArgb(20, 75, 137));
    graphics.FillPath(backgroundBrush, background);

    using Pen border = new(Color.FromArgb(70, 255, 255, 255), Math.Max(1f, 5 * scale));
    graphics.DrawPath(border, background);

    PointF[] speaker =
    [
        new(48 * scale, 105 * scale),
        new(88 * scale, 105 * scale),
        new(139 * scale, 64 * scale),
        new(139 * scale, 192 * scale),
        new(88 * scale, 151 * scale),
        new(48 * scale, 151 * scale)
    ];
    using SolidBrush white = new(Color.White);
    graphics.FillPolygon(white, speaker);

    using Pen wave = new(Color.White, Math.Max(2f, 13 * scale))
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    graphics.DrawArc(wave, 125 * scale, 82 * scale, 75 * scale, 92 * scale, -62, 124);

    using Pen slashShadow = new(Color.FromArgb(120, 45, 26, 37), Math.Max(3f, 29 * scale))
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    using Pen slash = new(Color.FromArgb(245, 83, 91), Math.Max(2f, 21 * scale))
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    graphics.DrawLine(slashShadow, 172 * scale, 74 * scale, 220 * scale, 184 * scale);
    graphics.DrawLine(slash, 172 * scale, 74 * scale, 220 * scale, 184 * scale);

    using MemoryStream stream = new();
    bitmap.Save(stream, ImageFormat.Png);
    return stream.ToArray();
}

static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
{
    float diameter = radius * 2;
    GraphicsPath path = new();
    path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
    path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
    path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
    path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
    path.CloseFigure();
    return path;
}
