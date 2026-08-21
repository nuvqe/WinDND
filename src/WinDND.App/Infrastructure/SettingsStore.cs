using System.Text.Json;
using System.Text.Json.Serialization;
using WinDND.Core.Models;

namespace WinDND.Infrastructure;

internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IAppLogger _logger;
    private readonly string _directory;
    private readonly string _path;

    public SettingsStore(IAppLogger logger)
    {
        _logger = logger;
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinDND");
        _path = Path.Combine(_directory, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(_path);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Rules ??= [];
            settings.Hardware ??= new HardwareControllerSettings();
            settings.Hardware.ProductIds ??= [0x0002, 0x0003];
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.Error($"Could not load settings from '{_path}'. Defaults will be used.", ex, 2100);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        string temporaryPath = _path + ".tmp";

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
