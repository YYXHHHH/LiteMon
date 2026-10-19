using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using LibreHardwareMonitor.Hardware;

namespace HwProbe;

internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== LiteMon M0 HwProbe ===");
        Console.WriteLine($"OS      : {Environment.OSVersion.VersionString} (build {Environment.OSVersion.Version.Build})");
        Console.WriteLine($"Runtime : {Environment.Version}");
        Console.WriteLine($"CPU     : {Environment.ProcessorCount} logical cores");
        Console.WriteLine($"Admin   : {Native.IsUserAnAdmin()}");
        Console.WriteLine($"Time    : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        Console.WriteLine();

        RunLhm();
        RunNvml();
        RunNvme();
        RunSystemCounters();
    }

    // ---------------- S1 ----------------
    private static void RunLhm()
    {
        Console.WriteLine("== S1 LibreHardwareMonitor (PawnIO) ==");
        Computer? computer = null;
        try
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = false,
                IsMemoryEnabled = false,
                IsGpuEnabled = false,
                IsNetworkEnabled = false,
            };

            var sw = Stopwatch.StartNew();
            computer.Open();
            Console.WriteLine($"  Open() = {sw.Elapsed.TotalMilliseconds:F0} ms");

            int temps = 0, fans = 0;
            foreach (var hw in computer.Hardware)
                DumpHardware(hw, ref temps, ref fans);

            Console.WriteLine($"  summary: temperature={temps}, fan={fans}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAILED {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            computer?.Close();
        }
    }

    private static void DumpHardware(IHardware hw, ref int temps, ref int fans, int depth = 0)
    {
        var sw = Stopwatch.StartNew();
        try { hw.Update(); }
        catch (Exception ex)
        {
            Console.WriteLine($"  {hw.Name} update failed: {ex.Message}");
            return;
        }
        sw.Stop();

        Console.WriteLine($"  {hw.HardwareType,-14} {hw.Name}  update={sw.Elapsed.TotalMilliseconds:F1}ms");

        foreach (var s in hw.Sensors)
        {
            if (s.Value is not { } v) continue;
            if (s.SensorType is not (SensorType.Temperature or SensorType.Fan or SensorType.Power or SensorType.Voltage or SensorType.Clock)) continue;
            if (s.SensorType == SensorType.Temperature) temps++;
            if (s.SensorType == SensorType.Fan) fans++;
            Console.WriteLine($"      {s.SensorType,-12} {s.Name,-34} {v,8:F1}");
        }

        foreach (var sub in hw.SubHardware)
            DumpHardware(sub, ref temps, ref fans, depth + 1);
    }

    // ---------------- S2 ----------------
    private static void RunNvml()
    {
        Console.WriteLine();
        Console.WriteLine("== S2 NVML (NVIDIA) ==");
        try
        {
            int rc = Native.nvmlInit_v2();
            Console.WriteLine($"  nvmlInit_v2 = {rc}");
            if (rc != 0) return;

            try
            {
                Native.nvmlDeviceGetCount_v2(out var count);
                Console.WriteLine($"  device count = {count}");

                for (uint i = 0; i < count; i++)
                {
                    Native.nvmlDeviceGetHandleByIndex_v2(i, out var dev);

                    var name = new StringBuilder(96);
                    Native.nvmlDeviceGetName(dev, name, (uint)name.Capacity);
                    Native.nvmlDeviceGetTemperature(dev, 0, out var temp);
                    Native.nvmlDeviceGetPowerUsage(dev, out var mw);
                    Native.nvmlDeviceGetUtilizationRates(dev, out var util);
                    Native.nvmlDeviceGetMemoryInfo(dev, out var mem);

                    Console.WriteLine($"  [{i}] {name} | {temp} C | {mw / 1000.0:F1} W | GPU {util.Gpu}% | MEM {mem.Used / 1048576.0:F0}/{mem.Total / 1048576.0:F0} MB");
                }
            }
            finally
            {
                Native.nvmlShutdown();
            }
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("  nvml.dll 不存在（未安装 NVIDIA 驱动）");
        }
        catch (EntryPointNotFoundException ex)
        {
            Console.WriteLine($"  NVML 入口缺失: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAILED {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ---------------- S3 ----------------
    private static void RunNvme()
    {
        Console.WriteLine();
        Console.WriteLine("== S3 NVMe temperature (IOCTL) ==");

        for (int i = 0; i < 8; i++)
        {
            string path = @"\\.\PhysicalDrive" + i.ToString();
            try
            {
                using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                var query = new Native.STORAGE_PROPERTY_QUERY
                {
                    PropertyId = Native.StorageDeviceTemperatureProperty,
                    QueryType = Native.PropertyStandardQuery,
                };

                int inSize = Marshal.SizeOf<Native.STORAGE_PROPERTY_QUERY>();
                IntPtr inBuf = Marshal.AllocHGlobal(inSize);
                IntPtr outBuf = Marshal.AllocHGlobal(512);
                try
                {
                    Marshal.StructureToPtr(query, inBuf, false);
                    bool ok = Native.DeviceIoControl(handle, Native.IOCTL_STORAGE_QUERY_PROPERTY,
                        inBuf, (uint)inSize, outBuf, 512, out var returned, IntPtr.Zero);
                    if (!ok)
                    {
                        Console.WriteLine($"  PhysicalDrive{i}: IOCTL failed (win32={Marshal.GetLastWin32Error()})");
                        continue;
                    }

                    var desc = Marshal.PtrToStructure<Native.STORAGE_TEMPERATURE_DATA_DESCRIPTOR>(outBuf);
                    double c = desc.Info0.Temperature - 273.15;
                    Console.WriteLine($"  PhysicalDrive{i}: {c:F0} C  (critical={desc.CriticalTemperature - 273.15:F0} C, warning={desc.WarningTemperature - 273.15:F0} C, info={desc.InfoCount}, returned={returned})");
                }
                finally
                {
                    Marshal.FreeHGlobal(inBuf);
                    Marshal.FreeHGlobal(outBuf);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"  PhysicalDrive{i}: access denied (需要管理员)");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"  PhysicalDrive{i}: IOException {ex.Message}");
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  PhysicalDrive{i}: {ex.GetType().Name} {ex.Message}");
            }
        }
    }

    // ---------------- S4 ----------------
    private static void RunSystemCounters()
    {
        Console.WriteLine();
        Console.WriteLine("== S4 system counters ==");

        using var pdh = new PdhSet();
        bool ok = pdh.Open(
            ("cpu", @"\Processor Information(_Total)\% Processor Utility"),
            ("diskRead", @"\PhysicalDisk(_Total)\Disk Read Bytes/sec"),
            ("diskWrite", @"\PhysicalDisk(_Total)\Disk Write Bytes/sec"));
        Console.WriteLine($"  PDH open = {ok}");

        var (prevIn, prevOut) = NetTotals();
        long prevTs = Stopwatch.GetTimestamp();

        for (int i = 0; i < 3; i++)
        {
            Thread.Sleep(1000);
            pdh.Collect();

            var (usedPct, usedGb, totalGb) = ReadMemory();
            var (nowIn, nowOut) = NetTotals();
            long nowTs = Stopwatch.GetTimestamp();

            double dt = (nowTs - prevTs) / (double)Stopwatch.Frequency;
            double down = dt <= 0 ? 0 : (nowIn - prevIn) / dt / 1048576.0;
            double up = dt <= 0 ? 0 : (nowOut - prevOut) / dt / 1048576.0;

            Console.WriteLine(
                $"  #{i + 1} CPU {pdh.Get("cpu")?.ToString("F1") ?? "--"}% | " +
                $"MEM {usedPct:F1}% ({usedGb:F1}/{totalGb:F1} GB) | " +
                $"DISK R {pdh.Get("diskRead") / 1048576.0 ?? 0:F2} MB/s " +
                $"W {pdh.Get("diskWrite") / 1048576.0 ?? 0:F2} MB/s | " +
                $"NET down {down:F2} / up {up:F2} MB/s");

            prevIn = nowIn;
            prevOut = nowOut;
            prevTs = nowTs;
        }
    }

    private static (double usedPercent, double usedGb, double totalGb) ReadMemory()
    {
        var m = new Native.MEMORYSTATUSEX { Length = (uint)Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        if (!Native.GlobalMemoryStatusEx(ref m) || m.TotalPhys == 0) return (0, 0, 0);
        return (100.0 * (m.TotalPhys - m.AvailPhys) / m.TotalPhys,
                (m.TotalPhys - m.AvailPhys) / 1073741824.0,
                m.TotalPhys / 1073741824.0);
    }

    private static (long inBytes, long outBytes) NetTotals()
    {
        long inBytes = 0, outBytes = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)) continue;
            try
            {
                var s = nic.GetIPStatistics();
                inBytes += s.BytesReceived;
                outBytes += s.BytesSent;
            }
            catch
            {
                // 某些虚拟网卡不提供统计
            }
        }
        return (inBytes, outBytes);
    }
}

internal sealed class PdhSet : IDisposable
{
    private IntPtr _query;
    private readonly Dictionary<string, IntPtr> _counters = new();

    public bool Open(params (string Name, string Path)[] counters)
    {
        uint rc = Native.PdhOpenQueryW(null, IntPtr.Zero, out _query);
        Console.WriteLine($"    PdhOpenQuery rc=0x{rc:X8}");
        if (rc != 0) return false;

        foreach (var (name, path) in counters)
        {
            uint addRc = Native.PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out var counter);
            if (addRc != 0)
            {
                uint fallback = Native.PdhAddCounterW(_query, path, IntPtr.Zero, out counter);
                Console.WriteLine($"    add {name}: english=0x{addRc:X8} fallback=0x{fallback:X8} path={path}");
                continue;
            }
            _counters[name] = counter;
        }
        return _counters.Count > 0;
    }

    public void Collect() => Native.PdhCollectQueryData(_query);

    public double? Get(string name)
    {
        if (!_counters.TryGetValue(name, out var counter)) return null;
        if (Native.PdhGetFormattedCounterValue(counter, Native.PDH_FMT_DOUBLE, out _, out var value) != 0) return null;
        return value.CStatus == 0 ? value.DoubleValue : null;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero) Native.PdhCloseQuery(_query);
        _query = IntPtr.Zero;
    }
}
