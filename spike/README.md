# spike —— 早期技术验证探针

这些程序**不参与 LiteMon.sln 构建**，只用来在开发早期验证技术假设（详见 [M0 验证报告](../docs/M0-验证报告.md)）。保留在此是为了让结论可复现。

| 探针 | 用途 | 结论 |
|---|---|---|
| `HwProbe` | PDH / NVML / LHM / WMI 能否读到硬件数据 | 写入 M0 报告 |
| `PerfProbe` | 各 Provider 单轮耗时 | 定位到网卡枚举 28.5 ms 的热点 |
| `RenderProbe` | WPF 自绘 vs 控件渲染的帧耗时 | 选择自绘 + 定时动画 |
| `WinFormsProbe` | 对比 WinForms 方案 | 未采用 |
| `BackdropProbe` | 透明窗口方案（DWM 亚克力 / 分层窗口） | 证伪 DWM 方案，确认分层窗口 |

运行方式（示例）：

```powershell
dotnet run --project spike/PerfProbe -c Release
```
