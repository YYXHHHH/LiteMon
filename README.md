# LiteMon · 轻量硬件监控

参考 [Diorser/LiteMonitor](https://github.com/Diorser/LiteMonitor) 的 Windows 桌面硬件监控项目规划。

## 文档

- **[技术方案](docs/技术方案.md)** —— 选型、架构、数据采集矩阵、性能预算、UI 设计、工程化、路线图、风险与合规。
- **[开发手册](docs/开发手册.md)** —— 从零搭环境到发布的全流程：工程初始化、编码规范、M0 验证实验、逐模块实现、测试用例、调试手册、CI/CD、评审与质量门禁。
- **[M0 验证报告](docs/M0-验证报告.md)** —— 实测数据、三个被推翻的假设（WPF 内存、NVMe 权限、LHM 返回值）与 Go/No-Go 结论。

## 一分钟版

| 项 | 结论 |
|---|---|
| 技术栈 | C# / **.NET 10 LTS** + **WPF 自绘** + LibreHardwareMonitorLib + 厂商 GPU SDK |
| 体积目标 | 3–6 MB（框架依赖） / 55–70 MB（自包含） |
| 常驻开销 | 内存 ≤ 60 MB，空闲 CPU ≤ 0.3%，1 s 刷新 |
| 驱动策略 | 默认 **无驱动兼容模式**；温度/风扇/电压需用户显式开启 **PawnIO** 增强模式（不用 WinRing0） |
| 进程模型 | M1 单进程；M3 视需要拆为「采集服务 + 低权限 UI」 |
| 周期 | M0 验证 1 周 + M1 MVP 3 周 + M2 打磨 2 周 + M3 发布 2 周 ≈ 8 周 |

## 下一步（M0）

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
