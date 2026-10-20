using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using LiteMon.Sensors.Interop;
using Microsoft.Win32;

namespace LiteMon.Sensors;

public sealed record DeviceInfo(string CpuName, string GpuName, string MemoryText, string DiskName, string NicName);

/// <summary>启动时采集一次硬件型号，用于面板分组标题。</summary>
public static class DeviceInfoCollector
{
    public static DeviceInfo Collect()
        => new(ReadCpuName(), ReadGpuName(), ReadMemory(), ReadDiskName(), ReadNicName());

    private static string ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            string? name = key?.GetValue("ProcessorNameString") as string;
            return string.IsNullOrWhiteSpace(name) ? "CPU" : name.Trim();
        }
        catch (Exception)
        {
            return "CPU";
        }
    }

    private static string ReadGpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\cimv2", "SELECT Name FROM Win32_VideoController");
            var names = new List<string>();

            foreach (ManagementObject row in searcher.Get())
            {
                using (row)
                {
                    string? name = row["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name) && !name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                    {
                        names.Add(name.Trim());
                    }
                }
            }

            return names.FirstOrDefault(n => n.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                   ?? names.FirstOrDefault(n => n.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                   ?? names.FirstOrDefault()
                   ?? "GPU";
        }
        catch (Exception)
        {
            return "GPU";
        }
    }

    private static string ReadMemory()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            Length = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>(),
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            return "内存";
        }

        string capacity = $"{status.TotalPhys / 1073741824.0:F1} GB";

        try
        {
            using var searcher = new ManagementObjectSearcher("root\\cimv2", "SELECT Manufacturer, Capacity FROM Win32_PhysicalMemory");
            string? maker = null;

            foreach (ManagementObject row in searcher.Get())
            {
                using (row)
                {
                    if (maker is null && row["Manufacturer"] is { } value)
                    {
                        string text = value.ToString()?.Trim() ?? string.Empty;
                        if (text.Length > 0 && text is not "Unknown")
                        {
                            maker = text;
                        }
                    }
                }
            }

            return maker is null ? capacity : $"{maker} {capacity}";
        }
        catch (Exception)
        {
            return capacity;
        }
    }

    private static string ReadDiskName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\cimv2",
                "SELECT Model FROM Win32_DiskDrive WHERE MediaType = 'Fixed hard disk media'");

            foreach (ManagementObject row in searcher.Get())
            {
                using (row)
                {
                    string? model = row["Model"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(model))
                    {
                        return model.Trim();
                    }
                }
            }
        }
        catch (Exception)
        {
            // WMI 不可用时退回通用名称
        }

        return "磁盘";
    }

    private static string ReadNicName()
    {
        try
        {
            return NetworkSelection.Primary()?.Description ?? "网络";
        }
        catch (Exception)
        {
            return "网络";
        }
    }
}
