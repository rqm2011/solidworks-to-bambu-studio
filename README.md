# SolidWorks → Bambu Studio

这是一个面向 **SOLIDWORKS 2025（64 位）** 的 C# COM 加载项。打开零件后，点击工具栏上的“发送到 Bambu Studio”，插件会自动完成：

1. 将当前零件直接导出为轻量 3MF 到本地缓存；
2. 按“月份/时间+文件名”自动保存一份长期备份；
3. 直接用 Bambu Studio 打开该模型；
4. 定期清理旧缓存。

用户不需要手动“另存为 3MF”、打开“导入”对话框或寻找文件。Bambu Studio 中仍会保留打印机、耗材、摆放、支撑、切片和最终打印确认，避免错误参数直接下发打印机。

## 当前功能

- 仅在活动文档为 `.SLDPRT` 零件时启用；
- 支持未保存的新零件和不同配置；
- 导出整个零件，不受当前面/实体选择影响；
- 静默导出 3MF，并在启动 Bambu Studio 前检查文件存在且非空；
- 临时关闭 3MF 外观、材料和贴图输出以减小文件、加快导入，结束后恢复用户原有设置；
- 允许终端安全软件透明加密临时 3MF，不尝试解包或绕开加密；
- 每次发送前自动备份 3MF；按 `yyyy-MM` 建立月度文件夹，并以 `yyyyMMdd_HHmmss_文件名.3mf` 命名；
- 自动查找 Bambu Studio，也可在设置窗口中手动指定；
- 默认保留最近 7 天的缓存，避免 Bambu Studio 尚未读完时源文件被删除；
- 提供安装、卸载和日志。

> 技术说明：Bambu Studio 不能直接读取 SOLIDWORKS 的 B-Rep/特征树，因此插件会在 `%LOCALAPPDATA%\SolidWorksToBambu\exports` 下生成临时 3MF。发送前会将文件复制到 `%USERPROFILE%\Documents\SolidWorksToBambu\backups\yyyy-MM`；临时文件可自动清理，月度备份不会被缓存清理删除。

## 安装

### 推荐：安装包

从 [GitHub Releases](https://github.com/rqm2011/solidworks-to-bambu-studio/releases/latest) 下载 `SolidWorksToBambu-Setup-v0.3.0.exe`。关闭 SOLIDWORKS 后双击安装，接受 Windows 管理员权限提示即可；不需要 Visual Studio、MSBuild 或 SOLIDWORKS API 开发环境。

安装包会将插件部署到 `%USERPROFILE%\Documents\SolidWorksToBambu\addin`，同步系统与当前用户的 64 位 COM 注册，并在 Windows“应用和功能”中登记卸载入口。

### 从源码安装

### 前提

- Windows 10/11 64 位；
- SOLIDWORKS 2025 64 位；
- Bambu Studio；
- 推荐安装 Visual Studio 2022 或 Build Tools 2022 的“.NET 桌面生成工具”和 .NET Framework 4.8 Developer Pack；如果未安装，脚本会自动使用 Windows 自带的 64 位 C# 编译器进入兼容构建模式。

### 一键编译并注册

先关闭 SOLIDWORKS，然后在本项目目录打开 PowerShell：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install.ps1
```

脚本会请求管理员权限，这是因为 SOLIDWORKS 按官方加载项机制从 `HKEY_LOCAL_MACHINE\SOFTWARE\SolidWorks\Addins` 发现 COM 加载项。
安装器也会同步 64 位系统和当前用户 COM 注册，避免旧的用户级 `CodeBase` 覆盖新安装路径，导致加载项勾选后立即取消。

如果 SOLIDWORKS 安装在自定义位置：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install.ps1 -SolidWorksDirectory "D:\SOLIDWORKS Corp\SOLIDWORKS"
```

安装后启动 SOLIDWORKS 2025：

1. 打开 `工具 > 插件`；
2. 确认 `SolidWorks → Bambu Studio` 的“当前启用”和“启动”已勾选；
3. 打开任意零件；
4. 点击 Bambu Studio 工具栏按钮，或使用 `工具 > Bambu Studio > 发送到 Bambu Studio`。

首次找不到 Bambu Studio 时，插件会提示选择 `bambu-studio.exe`。

如果加载项仍在勾选后立即取消，请先关闭 SOLIDWORKS，再运行注册修复：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Repair-SystemRegistration.ps1
```

## 卸载

先关闭 SOLIDWORKS：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Uninstall.ps1
```

同时删除设置、日志、临时导出缓存和月度备份：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Uninstall.ps1 -RemoveUserData
```

## 目录与日志

- 插件安装目录：`%USERPROFILE%\Documents\SolidWorksToBambu\addin`
- 临时导出：`%LOCALAPPDATA%\SolidWorksToBambu\exports`
- 月度备份：`%USERPROFILE%\Documents\SolidWorksToBambu\backups\yyyy-MM`
- 日志：`%LOCALAPPDATA%\SolidWorksToBambu\SolidWorksToBambu.log`
- 设置：`HKEY_CURRENT_USER\Software\SolidWorksToBambu`

## 开发与编译

仅编译：

```powershell
.\scripts\Build.ps1 -Configuration Release
```

生成单文件安装包：

```powershell
.\scripts\Build-Installer.ps1
```

安装包和 SHA-256 校验文件会生成到 `artifacts` 目录。

编译时使用 SOLIDWORKS 安装目录的三个官方互操作程序集，并把所需 COM 类型嵌入插件 DLL；部署时只需插件本体和官方 `SolidWorksTools.dll`：

- `SolidWorks.Interop.sldworks.dll`
- `SolidWorks.Interop.swconst.dll`
- `SolidWorks.Interop.swpublished.dll`

实现依据：

- [SOLIDWORKS 2025 ISwAddin 接口](https://help.solidworks.com/2025/english/api/swpublishedapi/SolidWorks.Interop.swpublished~SolidWorks.Interop.swpublished.ISwAddin.html)
- [SOLIDWORKS 2025 加载项注册与回调机制](https://help.solidworks.com/2025/english/api/sldworksapiprogguide/overview/using_swaddin_to_create_a_solidworks_addin.htm)
- [IModelDocExtension.SaveAs3 导出接口](https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDocExtension~SaveAs3.html)
- [Bambu Studio 官方命令行用法](https://github.com/bambulab/BambuStudio/wiki/Command-Line-Usage)

## 已知边界

- 目前只发送单个零件；装配体的“合并成一个对象”或“各零件分对象”需要单独设计交互。
- 为提高速度，当前临时 3MF 只传递可打印网格，不传递 SOLIDWORKS 外观、材料和贴图。
- 插件负责把几何送入 Bambu Studio，不自动点击“切片/打印”。完全自动下发需要明确的打印机、喷嘴、耗材、热床和工艺预设，并涉及 Bambu Studio/打印机认证。
