using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace ScreenCross;

public enum CrossStyle
{
    Cross,          // 十字
    CrossDotCircle, // 十字 + 中心点 + 圆圈
    Dot,            // 圆点
    Circle,         // 圆圈
    TShape,         // T 型
    XShape          // X 型
}

/// <summary>应用配置，JSON 持久化于 %APPDATA%\ScreenCross\config.json。</summary>
public class AppConfig
{
    public CrossStyle Style { get; set; } = CrossStyle.Cross;
    public int LineLength { get; set; } = 12;
    public int Thickness { get; set; } = 2;
    public int Gap { get; set; } = 4;
    public string ColorHex { get; set; } = "#FF00FF00";
    public double Opacity { get; set; } = 0.9;
    public bool Outline { get; set; } = true;
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;
    public int MonitorIndex { get; set; } = 0;
    public bool StartWithWindows { get; set; } = false;
    public bool FirstRunNoticeShown { get; set; } = false;

    [JsonIgnore]
    public Color Color
    {
        get
        {
            try { return (Color)ColorConverter.ConvertFromString(ColorHex); }
            catch { return Colors.Lime; }
        }
    }

    private static string DirPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenCross");
    private static string FilePath => Path.Combine(DirPath, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), JsonOptions);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // 配置损坏时回退到默认值
        }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DirPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // 保存失败不致命，下次退出再试
        }
    }
}
