using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenCross;

/// <summary>
/// 根据配置构建准星图形（WPF 保留模式：仅在参数变化时重建一次，静止时零 CPU 占用）。
/// 所有尺寸参数按"物理像素"理解；dpiScale &gt; 1 时换算为更小的 DIU，
/// 保证高 DPI 下准星物理大小不变。
/// </summary>
public static class CrosshairRenderer
{
    public static FrameworkElement Build(AppConfig cfg, double dpiScale = 1.0)
    {
        double s = dpiScale <= 0 ? 1.0 : dpiScale;
        double u = 1.0 / s; // 物理像素 -> DIU
        double len = Math.Max(1, cfg.LineLength) * u;
        double th = Math.Max(1, cfg.Thickness) * u;
        double gap = Math.Max(0, cfg.Gap) * u;

        Color main = cfg.Color;
        Color outline = IsDark(main) ? Colors.White : Colors.Black;
        var mainBrush = new SolidColorBrush(main);
        var outlineBrush = new SolidColorBrush(outline);
        if (mainBrush.CanFreeze) mainBrush.Freeze();
        if (outlineBrush.CanFreeze) outlineBrush.Freeze();

        var canvas = new Canvas
        {
            Width = 200 * u,
            Height = 200 * u,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = Math.Clamp(cfg.Opacity, 0.05, 1.0),
            IsHitTestVisible = false
        };
        var g = new Canvas { IsHitTestVisible = false };
        Canvas.SetLeft(g, 100 * u);
        Canvas.SetTop(g, 100 * u);

        void Line(double x1, double y1, double x2, double y2)
        {
            if (cfg.Outline)
                g.Children.Add(new Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    Stroke = outlineBrush, StrokeThickness = th + 2 * u,
                    StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat
                });
            g.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = mainBrush, StrokeThickness = th,
                StrokeStartLineCap = PenLineCap.Flat, StrokeEndLineCap = PenLineCap.Flat
            });
        }

        void Dot()
        {
            double d = Math.Max(3 * u, (cfg.Thickness * 2 + 2) * u);
            if (cfg.Outline) AddEllipse(g, d + 2 * u, outlineBrush, filled: true);
            AddEllipse(g, d, mainBrush, filled: true);
        }

        void Ring(double radius)
        {
            double d = 2 * radius;
            if (cfg.Outline) AddEllipse(g, d + 2 * u, outlineBrush, filled: false, stroke: th + 2 * u);
            AddEllipse(g, d, mainBrush, filled: false, stroke: th);
        }

        switch (cfg.Style)
        {
            case CrossStyle.Dot:
                Dot();
                break;
            case CrossStyle.Circle:
                Ring(Math.Max(3 * u, len));
                break;
            case CrossStyle.TShape:
                Line(-gap - len, 0, -gap, 0);
                Line(gap, 0, gap + len, 0);
                Line(0, gap, 0, gap + len);
                break;
            case CrossStyle.XShape:
            {
                const double k = 0.7071067811865476;
                double s0 = gap * k, e0 = (gap + len) * k;
                Line(-s0, -s0, -e0, -e0);
                Line(s0, s0, e0, e0);
                Line(-s0, s0, -e0, e0);
                Line(s0, -s0, e0, -e0);
                break;
            }
            case CrossStyle.CrossDotCircle:
                Line(-gap - len, 0, -gap, 0);
                Line(gap, 0, gap + len, 0);
                Line(0, -gap - len, 0, -gap);
                Line(0, gap, 0, gap + len);
                Dot();
                Ring(gap + len + 4 * u);
                break;
            default: // Cross
                Line(-gap - len, 0, -gap, 0);
                Line(gap, 0, gap + len, 0);
                Line(0, -gap - len, 0, -gap);
                Line(0, gap, 0, gap + len);
                break;
        }

        canvas.Children.Add(g);
        return canvas;
    }

    private static void AddEllipse(Canvas parent, double size, Brush brush, bool filled, double stroke = 1)
    {
        var e = new Ellipse { Width = size, Height = size, StrokeThickness = stroke };
        if (filled) e.Fill = brush;
        else e.Stroke = brush;
        Canvas.SetLeft(e, -size / 2);
        Canvas.SetTop(e, -size / 2);
        parent.Children.Add(e);
    }

    private static bool IsDark(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0 < 0.15;
}
