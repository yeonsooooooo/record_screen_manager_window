using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiskMonitor;

public class AppSettings
{
    public string FolderPath { get; set; } = "";
    public int IntervalMinutes { get; set; } = 5;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ThresholdKind ThresholdKind { get; set; } = ThresholdKind.UsedPercent;

    public double ThresholdValue { get; set; } = 90;
    public bool IsRunning { get; set; } = false;

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiskMonitor");

    private static string SettingsPath =>
        Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return loaded ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // 설정 저장 실패는 무시 (UI에서 안내하거나 무시)
        }
    }
}
