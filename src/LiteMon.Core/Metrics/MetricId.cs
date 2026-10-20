namespace LiteMon.Core.Metrics;

/// <summary>
/// 指标字典。ID 一旦发布不可修改（配置、主题、历史库都会引用），只能新增。
/// 段位：100 CPU / 200 GPU / 300 内存 / 400 磁盘 / 500 网络 / 600 风扇电压 / 700 电源 / 800 系统。
/// </summary>
public enum MetricId : short
{
    CpuLoad = 100,
    CpuTemp = 101,
    CpuClockMhz = 102,
    CpuPowerW = 103,
    CpuCoreMax = 104,
    CpuCoreAvg = 105,
    CpuTempSource = 106,   // 1=LHM 内核, 2=ACPI 热区

    GpuLoad = 200,
    GpuTemp = 201,
    GpuMemUsedMb = 202,
    GpuMemTotalMb = 203,
    GpuPowerW = 204,
    GpuFanPercent = 205,
    GpuClockMhz = 206,
    GpuHotspotTemp = 207,

    MemUsedPercent = 300,
    MemUsedBytes = 301,
    MemTotalBytes = 302,
    MemAvailableBytes = 303,

    DiskReadBps = 400,
    DiskWriteBps = 401,
    DiskTemp = 402,
    DiskUsedPercent = 403,
    DiskFreeBytes = 404,

    NetUpBps = 500,
    NetDownBps = 501,
    NetTotalIn = 502,
    NetTotalOut = 503,
    NetLinkDownBps = 504,
    NetLinkUpBps = 505,

    FanRpm = 600,
    Voltage = 601,

    BatteryPercent = 700,
    AcOnline = 701,

    UptimeSeconds = 800,
    ProcessCount = 801,

    /// <summary>哨兵：数组长度使用，不是有效指标。</summary>
    Count = 900,
}
