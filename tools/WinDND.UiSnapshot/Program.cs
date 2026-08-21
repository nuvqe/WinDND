using System.Drawing.Imaging;
using WinDND.Core.Models;
using WinDND.Core.Scheduling;
using WinDND.UI;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: WinDND.UiSnapshot <output.png> [--hardware]");
    return 2;
}

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

AppSettings sampleSettings = new()
{
    Rules =
    [
        new ScheduleRule { Action = "25", Cron = "30 7 * * 1-5" },
        new ScheduleRule { Action = "mute", Cron = "0 22 * * 1-5" },
        new ScheduleRule { Action = "unmute", Cron = "0 9 * * 6,0" }
    ],
    Hardware = new HardwareControllerSettings { Enabled = true }
};

using SettingsForm form = new(sampleSettings, new CronScheduleService(), "No compatible USB controller connected.")
{
    StartPosition = FormStartPosition.Manual,
    Location = new Point(20, 20)
};
form.Show();
Application.DoEvents();
if (args.Length == 2 && args[1].Equals("--hardware", StringComparison.OrdinalIgnoreCase))
{
    FindControls(form).OfType<TabControl>().Single().SelectedIndex = 1;
    Application.DoEvents();
}
form.PerformLayout();

Label status = FindControls(form)
    .OfType<Label>()
    .Single(control =>
        control.Text.StartsWith("Later rows win", StringComparison.Ordinal) ||
        control.Text.StartsWith("Muted and unmuted indicators", StringComparison.Ordinal));
Button save = FindControls(form).OfType<Button>().Single(control => control.Text == "Save");
Button cancel = FindControls(form).OfType<Button>().Single(control => control.Text == "Cancel");
Rectangle statusBounds = GetFormBounds(form, status);
Rectangle saveBounds = GetFormBounds(form, save);
Rectangle cancelBounds = GetFormBounds(form, cancel);

string outputPath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
using Bitmap bitmap = new(form.Width, form.Height);
form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
bitmap.Save(outputPath, ImageFormat.Png);

Console.WriteLine(
    $"Client={form.ClientRectangle}; Status={statusBounds}; Save={saveBounds}; Cancel={cancelBounds}; " +
    $"TextHeight={TextRenderer.MeasureText(status.Text, status.Font).Height}");
return new[] { statusBounds, saveBounds, cancelBounds }.All(form.ClientRectangle.Contains) ? 0 : 1;

static Rectangle GetFormBounds(Form form, Control control) =>
    new(form.PointToClient(control.PointToScreen(Point.Empty)), control.Size);

static IEnumerable<Control> FindControls(Control parent)
{
    foreach (Control child in parent.Controls)
    {
        yield return child;
        foreach (Control descendant in FindControls(child))
        {
            yield return descendant;
        }
    }
}
