using System.Runtime.InteropServices;

namespace LiteMon.Sensors.Interop;

internal static class NativeMethods
{
    public const uint PDH_FMT_DOUBLE = 0x00000200;

    // ---------- 内存 ----------
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    // ---------- CPU 频率 ----------
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESSOR_POWER_INFORMATION
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    [DllImport("powrprof.dll")]
    public static extern uint CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferSize,
        IntPtr outputBuffer,
        uint outputBufferSize);

    public static bool TryGetAverageMhz(out double averageMhz)
    {
        averageMhz = 0;
        int count = Environment.ProcessorCount;
        int size = Marshal.SizeOf<PROCESSOR_POWER_INFORMATION>();
        IntPtr buffer = Marshal.AllocHGlobal(size * count);
        try
        {
            uint status = CallNtPowerInformation(11, IntPtr.Zero, 0, buffer, (uint)(size * count));
            if (status != 0)
            {
                return false;
            }

            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PROCESSOR_POWER_INFORMATION>(IntPtr.Add(buffer, i * size));
                sum += info.CurrentMhz;
            }

            averageMhz = sum / count;
            return averageMhz > 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // ---------- NVML ----------
    [StructLayout(LayoutKind.Sequential)]
    public struct NvmlUtilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NvmlMemory
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nvmlInit_v2();

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nvmlShutdown();

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nvmlDeviceGetCount_v2(out uint count);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nvmlDeviceGetTemperature(IntPtr device, uint sensor, out uint celsius);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint milliwatts);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization utilization);

    [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out NvmlMemory memory);
}
