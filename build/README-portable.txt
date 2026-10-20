LiteMon 轻量硬件监控
====================

双击 LiteMon.exe 启动。

说明
----
* 面板置顶，可拖动；右上角 × 或 Esc 退出；右键有菜单。
* CPU 温度默认走 ACPI 热区（不需要管理员）。
* 想读取 CPU 内核温度：右键 -> “以管理员身份重启（读取内核温度）”，
  在 UAC 弹窗点“是”。成功后底部显示“温度：LHM 内核”。
* 窗口位置等设置暂未持久化，重启后回到屏幕右上角。
* 不写注册表、不装服务，删除文件夹即卸载。

数据来源
--------
CPU 使用率/频率   PDH + CallNtPowerInformation
CPU 温度          LibreHardwareMonitor（内核，需管理员）/ ACPI 热区（回退）
GPU               NVIDIA NVML
内存              GlobalMemoryStatusEx
磁盘              PDH PhysicalDisk
网络              主网卡 GetIPStatistics
硬件型号          注册表 / WMI

常见问题
--------
Q: 双击没反应？
A: 本包是自包含版本，不需要安装 .NET；若被 SmartScreen 拦截，
   点“更多信息”->“仍要运行”。企业电脑可能禁止运行未签名程序。

Q: 温度显示“--”？
A: 该机器没有 ACPI 热区，且未以管理员身份运行。

Q: CPU 占用？
A: 空闲约 1.5%–2.5%（单核），内存约 180 MB（WPF 基线）。
