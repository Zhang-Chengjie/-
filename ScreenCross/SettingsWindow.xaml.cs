using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SWF = System.Windows.Forms;

namespace ScreenCross;

/// <summary>
/// 设置窗口：所有修改实时应用并自动保存（所见即所得）。
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly List<KeyValuePair<string, CrossStyle>> StyleItems = new()
    {
        new("十字", CrossStyle.Cross),
        new("十字 + 中心点 + 圆圈", CrossStyle.CrossDotCircle),
        new("圆点", CrossStyle.Dot),
        new("圆圈", CrossStyle.Circle),
        new("T 型", CrossStyle.TShape),
        new("X 型", CrossStyle.XShape),
    };

    private static readonly string[] Palette =
    {
        "#FF00FF00", "#FFFFFFFF", "#FFFF3131", "#FF00B0FF", "#FFFFE100",
        "#FF00FFFF", "#FFFF00FF", "#FFFF8C00", "#FFFF6EC7", "#FF000000"
    };

    private readonly AppConfig _cfg;
    private readonly OverlayWindow _overlay;
    private bool _loading = true;

    public SettingsWindow(AppConfig cfg, OverlayWindow overlay)
    {
        InitializeComponent();
        _cfg = cfg;
        _overlay = overlay;
        _overlay.ConfigExternallyChanged += OnConfigExternallyChanged;
        Closed += (_, _) => _overlay.ConfigExternallyChanged -= OnConfigExternallyChanged;
        LoadFromConfig();
        _loading = false;
    }

    private void LoadFromConfig()
    {
        StyleBox.ItemsSource = StyleItems;
        StyleBox.SelectedValue = _cfg.Style;

        LengthSlider.Value = _cfg.LineLength;
        ThicknessSlider.Value = _cfg.Thickness;
        GapSlider.Value = _cfg.Gap;
        OpacitySlider.Value = Math.Clamp(_cfg.Opacity, 0.1, 1);

        OutlineCheck.IsChecked = _cfg.Outline;

        var screens = SWF.Screen.AllScreens;
        var items = new List<string>();
        for (int i = 0; i < screens.Length; i++)
            items.Add($"显示器 {i + 1}（{screens[i].Bounds.Width}×{screens[i].Bounds.Height}）");
        MonitorBox.ItemsSource = items;
        MonitorBox.SelectedIndex = Math.Clamp(_cfg.MonitorIndex, 0, items.Count - 1);

        AutoStartCheck.IsChecked = AutoStartHelper.IsEnabled();

        OffsetXBox.Text = _cfg.OffsetX.ToString();
        OffsetYBox.Text = _cfg.OffsetY.ToString();

        BuildColorPanel();
        UpdateLabels();
        UpdatePreview();
    }

    private void OnStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (StyleBox.SelectedValue is CrossStyle style)
        {
            _cfg.Style = style;
            Apply();
        }
    }

    private void OnParamChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _cfg.LineLength = (int)LengthSlider.Value;
        _cfg.Thickness = (int)ThicknessSlider.Value;
        _cfg.Gap = (int)GapSlider.Value;
        _cfg.Opacity = OpacitySlider.Value;
        Apply();
    }

    private void OnOutlineChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _cfg.Outline = OutlineCheck.IsChecked == true;
        Apply();
    }

    private void OnOffsetChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (int.TryParse(OffsetXBox.Text, out int x)) _cfg.OffsetX = Math.Clamp(x, -500, 500);
        if (int.TryParse(OffsetYBox.Text, out int y)) _cfg.OffsetY = Math.Clamp(y, -500, 500);
        Apply();
    }

    private void OnMonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (MonitorBox.SelectedIndex >= 0)
        {
            _cfg.MonitorIndex = MonitorBox.SelectedIndex;
            Apply();
        }
    }

    private void OnAutoStartChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool on = AutoStartCheck.IsChecked == true;
        AutoStartHelper.SetEnabled(on);
        _cfg.StartWithWindows = on;
        Apply();
    }

    private void OnCustomColorClick(object sender, RoutedEventArgs e)
    {
        var c = _cfg.Color;
        using var dlg = new SWF.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B)
        };
        if (dlg.ShowDialog() == SWF.DialogResult.OK)
        {
            _cfg.ColorHex = $"#{dlg.Color.A:X2}{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            BuildColorPanel();
            Apply();
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        var d = new AppConfig();
        _cfg.Style = d.Style;
        _cfg.LineLength = d.LineLength;
        _cfg.Thickness = d.Thickness;
        _cfg.Gap = d.Gap;
        _cfg.ColorHex = d.ColorHex;
        _cfg.Opacity = d.Opacity;
        _cfg.Outline = d.Outline;
        _cfg.OffsetX = 0;
        _cfg.OffsetY = 0;
        LoadFromConfig();
        Apply();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void Apply()
    {
        _overlay.ApplyConfigChange();
        UpdatePreview();
        UpdateLabels();
    }

    private void OnConfigExternallyChanged()
    {
        if (!OffsetXBox.IsFocused) OffsetXBox.Text = _cfg.OffsetX.ToString();
        if (!OffsetYBox.IsFocused) OffsetYBox.Text = _cfg.OffsetY.ToString();
    }

    private void BuildColorPanel()
    {
        ColorPanel.Children.Clear();
        foreach (var hex in Palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            bool selected = color.Equals(_cfg.Color);
            var btn = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(color),
                BorderThickness = new Thickness(selected ? 2 : 1),
                BorderBrush = selected ? SystemColors.HighlightBrush : Brushes.Gray,
                Tag = hex
            };
            btn.Click += (_, _) =>
            {
                _cfg.ColorHex = hex;
                BuildColorPanel();
                Apply();
            };
            ColorPanel.Children.Add(btn);
        }
    }

    private void UpdateLabels()
    {
        LengthValue.Text = ((int)LengthSlider.Value).ToString();
        ThicknessValue.Text = ((int)ThicknessSlider.Value).ToString();
        GapValue.Text = ((int)GapSlider.Value).ToString();
        OpacityValue.Text = $"{Math.Round(OpacitySlider.Value * 100)}%";
    }

    private void UpdatePreview()
    {
        PreviewHost.Children.Clear();
        PreviewHost.Children.Add(CrosshairRenderer.Build(_cfg, 1.0));
    }
}
