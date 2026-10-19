using System.Runtime.InteropServices;

namespace LiteMon.Sensors.Interop;

/// <summary>PDH 计数器集合。句柄只创建一次，之后每轮只 Collect。</summary>
internal sealed class PdhQuery : IDisposable
{
    private IntPtr _query;
    private readonly Dictionary<string, IntPtr> _counters = new();

    public bool Open(params (string Name, string Path)[] counters)
    {
        if (PdhOpenQueryW(null, IntPtr.Zero, out _query) != 0)
        {
            _query = IntPtr.Zero;
            return false;
        }

        foreach (var (name, path) in counters)
        {
            if (PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out var counter) != 0)
            {
                continue;
            }

            _counters[name] = counter;
        }

        return _counters.Count > 0;
    }

    public void Collect()
    {
        if (_query != IntPtr.Zero)
        {
            _ = PdhCollectQueryData(_query);
        }
    }

    public double? Get(string name)
    {
        if (!_counters.TryGetValue(name, out var counter))
        {
            return null;
        }

        if (PdhGetFormattedCounterValue(counter, NativeMethods.PDH_FMT_DOUBLE, out _, out var value) != 0)
        {
            return null;
        }

        return value.CStatus == 0 ? value.DoubleValue : null;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            _ = PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PDH_FMT_COUNTERVALUE value);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
