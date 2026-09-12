# 屏幕准星 ScreenCross

Windows 桌面准星叠加（Overlay）工具：在屏幕中心显示可自定义的准星，点击穿透、不抢焦点、不影响任何正常操作。适用于 FPS 训练、屏幕中心定位与演示标注。

## 功能

- 6 种准星样式：十字 / 十字+中心点+圆圈 / 圆点 / 圆圈 / T 型 / X 型
- 参数自定义：线长、粗细、中心间距、颜色（预设 + 自定义）、不透明度、对比色描边
- 位置微调：快捷键或设置窗口按像素偏移；多显示器可选
- 系统托盘：显示/隐藏、设置、开机自启、退出
- 配置自动保存在 `%APPDATA%\ScreenCross\config.json`，重启恢复
- 静止时零渲染循环、零轮询，CPU 占用趋近 0%

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| F8 | 显示 / 隐藏准星 |
| Ctrl + 方向键 | 按像素微调准星位置 |

> 注：全局热键在系统范围内生效，编辑文本时 Ctrl+方向键 会被本程序优先接收（词跳转失效）。

## 使用

双击 `ScreenCross.exe` 即可（绿色版，无需安装）。首次启动会有使用提示。

- 左键点击托盘图标：打开设置
- 右键点击托盘图标：显示/隐藏、开机自启、退出
- 全屏独占（Exclusive Fullscreen）模式下系统合成被绕过，准星不可见；请将游戏改为"无边框窗口 / 全屏窗口化"

## 构建

需要 .NET 8 SDK（Windows）：

```bash
dotnet build -c Release
# 输出：bin/Release/net8.0-windows/ScreenCross.exe

# 发布为绿色版单文件（依赖目标机装有 .NET 8 桌面运行时）
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# 或自包含单文件（无需目标机安装运行时，体积较大）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## 安全与合规

- 本软件为纯桌面叠加层：不注入任何进程、不读写他人内存、不安装全局键盘/鼠标钩子（热键使用 `RegisterHotKey`）、不采集屏幕画面、不模拟输入、不联网。
- 部分游戏或竞技平台可能限制第三方覆盖层，使用前请自行确认游戏条款，风险自担。

## 已知限制

- 全屏独占模式下不可见（见上文）。
- 多显示器混合 DPI 场景下定位以 WinForms Screen 坐标为准，个别极端组合可能需要微调偏移。
- 准星预设暂不支持导入导出（规划中的 P1 功能）。
