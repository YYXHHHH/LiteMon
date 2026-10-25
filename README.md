# LiteMon · 轻量硬件监控

参考 [Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor) 的 Windows 桌面硬件监控项目规划。

## 文档

- **[技术方案](docs/技术方案.md)** —— 选型、架构、数据采集矩阵、性能预算、UI 设计、工程化、路线图、风险与合规。
- **[开发手册](docs/开发手册.md)** —— 从零搭环境到发布的全流程：工程初始化、编码规范、M0 验证实验、逐模块实现、测试用例、调试手册、CI/CD、评审与质量门禁。
- **[M0 验证报告](docs/M0-验证报告.md)** —— 实测数据、三个被推翻的假设（WPF 内存、NVMe 权限、LHM 返回值）与 Go/No-Go 结论。
- **[进度记录](docs/进度.md)** —— 每周进展与性能数据。

## 当前状态（2026-10-20）

可运行的面板 MVP：

```powershell
dotnet run --project src/LiteMon.App -c Release
```

| 项 | 实测 |
|---|---|
| CPU 占用 | **1.6%（单核）**，优化前 11–14% |
| 内存 | 工作集 ~185 MB / 私有 ~122 MB（WPF 基线，见 M0 报告） |
| 刷新 | **可调 1/2/5/10 秒或自定义 1–60 秒**（右键 → 刷新间隔），12 项指标（CPU 使用率/频率、GPU 使用率/温度/显存、内存、磁盘读写、网络上下行、运行时长） |
| 硬件型号 | 分组标题显示 CPU/GPU/内存/磁盘/网卡型号（如 Ryzen 7 8845H、RTX 4060 Laptop、Crucial 31.3 GB、YMTC PC411、MediaTek Wi-Fi 6E） |
| 温度 | **CPU 70 ℃ 可读**（ACPI 热区回退，无需管理员）；内核温度需右键菜单提权（LHM/PawnIO） |
| 交互 | 置顶、拖动、Esc/×/右键退出；右键菜单可切换刷新间隔（**选择持久化**到 `LiteMon.settings.json`） |
| 固定悬浮窗 | 右上角**图钉**：点一下钉在当前屏幕位置（此时拖不动），再点解锁；位置与固定状态**重启后保留**，换分辨率/拔副屏自动回到可见区 |

已知问题：无托盘、无开机自启；ACPI 温度为热区近似值，内核温度需管理员。

## 直接用（已打包）

| 包 | 体积 | 适用 |
|---|---|---|
| `release/LiteMon-portable-win-x64/`（zip 63.6 MB） | 149 MB | **自包含版**：不需要装 .NET，解压后双击 `LiteMon.exe` |
| `release/LiteMon-lite-win-x64/`（zip 2.0 MB） | 4.9 MB | 轻量版：需要 .NET 10 桌面运行时 |

启动约 4 秒（WPF 冷启动，已接受）。重新打包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/publish.ps1
```

> `release/` 已加入 `.gitignore`，二进制不进仓库；`build/publish.ps1` 可随时重建。

## 一分钟版

| 项 | 结论 |
|---|---|
| 技术栈 | C# / **.NET 10 LTS** + **WPF 自绘** + LibreHardwareMonitorLib + 厂商 GPU SDK |
| 体积目标 | 3–6 MB（框架依赖） / 55–70 MB（自包含） |
| 常驻开销 | 内存 ≤ 60 MB，空闲 CPU ≤ 0.3%，1 s 刷新 |
| 驱动策略 | 默认 **无驱动兼容模式**；温度/风扇/电压需用户显式开启 **PawnIO** 增强模式（不用 WinRing0） |
| 进程模型 | M1 单进程；M3 视需要拆为「采集服务 + 低权限 UI」 |
| 周期 | M0 验证 1 周 + M1 MVP 3 周 + M2 打磨 2 周 + M3 发布 2 周 ≈ 8 周 |

## M0 验证清单（已完成）

1. 用 LibreHardwareMonitorLib + PawnIO 在 Win11 实机读取 CPU 温度、主板风扇，记录权限、耗时、体积。
2. 用 NVML 读取 NVIDIA GPU（温度/功耗/显存），验证无管理员权限可用。
3. 用 IOCTL 读取 NVMe 温度，验证 Win10 / Win11 权限差异。
4. WPF 自绘 12 项指标，测单帧耗时与工作集内存。
5. 输出《M0 验证报告》，据此确认或调整选型。

## 目录规划

```
src/LiteMon.App          # WPF 壳
src/LiteMon.Core         # 指标模型 / 快照 / 调度 / 告警
src/LiteMon.Sensors      # PDH / NVML / ADLX / IGCL / LHM / NVMe / 网络 / WMI
src/LiteMon.Rendering    # 自绘布局 / 主题 / 文本缓存
src/LiteMon.Settings     # settings.json + Schema + 迁移
src/LiteMon.Diagnostics  # 日志 / 诊断包
tests/ themes/ docs/ build/
```
