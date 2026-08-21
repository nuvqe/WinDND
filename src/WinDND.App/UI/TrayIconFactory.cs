using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WinDND.UI;

internal static class TrayIconFactory
{
    public static Icon Create(bool muted, bool scheduleEnabled)
    {
        using Bitmap bitmap = new(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        Color background = scheduleEnabled ? Color.FromArgb(38, 114, 184) : Color.FromArgb(100, 100, 100);
        using SolidBrush backgroundBrush = new(background);
        graphics.FillEllipse(backgroundBrush, 1, 1, 30, 30);

        using Pen whitePen = new(Color.White, 2.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        PointF[] speaker =
        [
            new(8, 13), new(12, 13), new(17, 9), new(17, 23), new(12, 19), new(8, 19)
        ];
        graphics.DrawLines(whitePen, speaker);

        if (muted)
        {
            using Pen mutePen = new(Color.FromArgb(255, 224, 84, 84), 3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawLine(mutePen, 21, 12, 27, 20);
            graphics.DrawLine(mutePen, 27, 12, 21, 20);
        }
        else
        {
            graphics.DrawArc(whitePen, 16, 11, 8, 10, -65, 130);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
