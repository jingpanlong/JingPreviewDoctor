# 景览 · 图片预览修复助手 (JingPreviewDoctor)

![景览 1.2.1 封面横幅](images/cover.png)

> 让图片重新可见 —— 缩略图修复 / 格式诊断 / 本地预览

「景览」是一款面向设计师与摄影工作者的 Windows 本地工具，专注解决一个常见问题：**文件有图标却没有缩略图**。它提供一键修复当前用户的显示设置、清理可释放的缩略图缓存、诊断系统格式处理程序，并内置轻量图片预览与批量转存能力。

- 当前版本：**1.2.1**
- 运行环境：Windows 7 SP1 – Windows 11，需 .NET Framework 4.0 或更高
- 性质：**本地运行、无联网、无遥测**

---

## ✨ 功能特性

- **修复中心**：一键修复当前用户的四个显示设置，自动备份后再启用缩略图、预览处理程序与缓存；清理可删除的 `thumbcache_*.db` 并通知 Shell 刷新。
- **诊断报告**：逐文件实际尝试内置 / 系统解码，生成可读性诊断报告，可导出日志。
- **格式与处理程序**：列出 42 种扩展名的缩略图与预览窗格注册情况，区分「应用内预览 / 系统缩略图 / 预览窗格」三种能力。
- **图片预览与转存**：打开或拖入图片，等比例查看、左/右旋转、翻转、适应窗口；支持 PNG / JPEG 转存（最长边限制、JPEG 质量 1–100、透明区填白），可调用外部 ImageMagick 处理更多格式。
- **批量工作台**：多文件 / 文件夹（含子目录）扫描、路径去重、任务队列、并行解码（独立进程、30 秒超时）、结果筛选与 CSV 导出。
- **任务持久化**：队列可保存为 `.jingqueue`，自动记录上次队列，支持「继续未完成 / 转存选中项 / 移除成功项」。
- **设置与备份**：手动备份、备份恢复、日志与输出目录集中管理、Shell 刷新；每次修复前自动备份，恢复仅作用于设置。

## 🖼 界面预览

| 修复中心（首页） | 图片预览与转存 | 批量工作台 |
| --- | --- | --- |
| ![修复中心](images/home-repair-center.png) | ![图片预览与转存](images/image-preview.png) | ![批量工作台](images/batch-workbench.png) |

> 左：一键修复与诊断；中：等比例查看、旋转翻转、PNG/JPEG 转存；右：批量扫描、解码检测与结果导出。

## 📥 下载

请前往 **Releases** 获取最新版本：

- **单文件版** `JingPreviewDoctor.exe`：无需随附 DLL，双击即可运行（仍需系统 .NET Framework 4.0+）。
- **安装版** `JingPreviewDoctor-Setup.exe`：当前用户安装，含开始菜单入口与系统应用列表卸载入口。

> GitHub Releases 页面：<https://github.com/jingpanlong/JingPreviewDoctor/releases>

如需校验完整性，随包提供 `SHA256.txt` 清单。程序与安装包未做数字签名，Windows 可能提示未知发布者，请按需核验。

## 🚀 使用步骤

1. 运行主程序，点击「诊断并生成报告」。
2. 点击「一键修复」：自动备份当前用户显示设置后启用缩略图、预览处理程序与缓存，并清理可删除的缓存。
3. 重新打开文件夹并切换为中 / 大图标；`Alt + P` 切换预览窗格。被资源管理器占用的缓存会跳过，可注销再登录后重试（程序不结束 Explorer）。
4. 专有格式仍无预览时，在「格式与处理程序」查看现有注册；未安装处理程序的格式无法仅靠设置修复。
5. 「恢复设置备份」选择 `backup-*.xml`，仅恢复设置（缓存可由系统重建）。

## 📐 支持与限制（请务必阅读）

没有「所有格式都能修复」的真实保证。本程序**不安装 Shell 扩展**，不为系统新增 AI / CDR / PDF / RAW 等解码能力。

- **内置支持**：JPG / JPEG / PNG / BMP / GIF / TIFF / ICO（GDI+，动画 / 多页仅首帧）；PSD 仅 v1、8 位 RGB / 灰度、原始或 PackBits RLE 的合成图；PNM / PPM / PGM / PBM 支持 P1–P6（8/16 位采样，不支持 PAM / PFM）。
- **非内置**：PSB、AI、EPS、PDF、RAW 系列、CDR、EXR、HDR、WEBP、HEIC、AVIF、SVG、TGA、DDS 等。可尝试已安装的 Shell 处理程序或外部 ImageMagick；能力取决于文件版本、构建与依赖，未逐格式保证。
- **输入限制**：内置预览输入上限 512 MB / 2400 万像素；Shell 缩略图最高 1024 px；外部预览最高 1600 px。转存的是预览图，并非无损原图导出或 RAW 显影。
- 系统策略禁止预览时不修改设置，请联系管理员；文件损坏、权限不足、云文件未下载、编解码器崩溃不在一键修复范围。

## 🔧 从源码构建

无需 NuGet，使用系统自带的 .NET Framework C# 编译器即可。

```powershell
# 在源码目录中执行（需要 .NET Framework 4.0+）
powershell -ExecutionPolicy Bypass -File build.ps1
```

产物位于 `dist/`：

- `dist/JingPreviewDoctor.exe` —— 单文件主程序
- `dist/JingPreviewDoctor-Setup.exe` —— 当前用户安装版
- `dist/使用说明.txt` —— 使用说明（亦嵌入安装包）

自检与验证（开发者）：

```text
JingPreviewDoctor.exe --self-test 报告路径
JingPreviewDoctor.exe --ui-smoke-test 报告路径
JingPreviewDoctor.exe --screenshot 输出PNG路径 [页签名 窗口大小]
JingPreviewDoctor-Setup.exe --self-test 报告路径
```

## 🔒 隐私与位置

- 主程序**无网络请求、无遥测**；原始图片不被自动修改，转存目标路径由用户选择。
- 日志与备份：`%LOCALAPPDATA%\JingPreviewDoctor`
- 安装目录：`%LOCALAPPDATA%\Programs\JingPreviewDoctor`
- 卸载保留日志、设置备份与已修复的设置；如需回滚设置，请在卸载前恢复备份。

## 📄 开源许可

本项目以 [MIT License](./LICENSE) 开源。

## 📮 联系作者

- 微信公众号：**景同志**
- 小红书：**景同学**
- 项目仓库与 Issue：<https://github.com/jingpanlong/JingPreviewDoctor>

---

*景览 · 让设置问题有修复入口，让格式限制有清楚说明。*
