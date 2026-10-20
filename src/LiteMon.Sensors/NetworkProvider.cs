using System.Net.NetworkInformation;
using LiteMon.Core.Metrics;
using LiteMon.Core.Sensors;

namespace LiteMon.Sensors;

/// <summary>网络速率：GetIPStatistics 累计值差分。切换网卡时自动重建基线。</summary>
public sealed class NetworkProvider : ISensorProvider
{
    private long _prevIn;
    private long _prevOut;
    private long _prevTs;
    private bool _primed;
    private NetworkInterface[]? _nics;
    private long _refreshAt;

    public string Name => "network";

    public ProviderCadence Cadence => ProviderCadence.Fast;

    public ProviderCapabilities Capabilities { get; } = new(
        new HashSet<MetricId>
        {
            MetricId.NetDownBps, MetricId.NetUpBps,
            MetricId.NetTotalIn, MetricId.NetTotalOut,
        },
        RequiresAdmin: false,
        RequiresDriver: false);

    public bool IsEnabled => true;

    public void Initialize()
    {
    }

    public void Poll(IMetricWriter writer, long nowMs)
    {
        var (inBytes, outBytes) = Totals();

        if (!_primed || nowMs <= _prevTs || inBytes < _prevIn || outBytes < _prevOut)
        {
            _prevIn = inBytes;
            _prevOut = outBytes;
            _prevTs = nowMs;
            _primed = true;
            writer.Write(MetricId.NetTotalIn, inBytes);
            writer.Write(MetricId.NetTotalOut, outBytes);
            return;
        }

        double seconds = (nowMs - _prevTs) / 1000.0;
        if (seconds <= 0)
        {
            return;
        }

        writer.Write(MetricId.NetDownBps, Math.Max(0, (inBytes - _prevIn) / seconds));
        writer.Write(MetricId.NetUpBps, Math.Max(0, (outBytes - _prevOut) / seconds));
        writer.Write(MetricId.NetTotalIn, inBytes);
        writer.Write(MetricId.NetTotalOut, outBytes);

        _prevIn = inBytes;
        _prevOut = outBytes;
        _prevTs = nowMs;
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// 枚举网卡很贵（本机实测 ~28 ms/次），必须缓存；每 60 秒或取数失败时重建。
    /// </summary>
    private (long In, long Out) Totals()
    {
        long now = Environment.TickCount64;
        if (_nics is null || now >= _refreshAt)
        {
            _nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                              && nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                .ToArray();
            _refreshAt = now + 60_000;
        }

        long inBytes = 0;
        long outBytes = 0;

        foreach (var nic in _nics)
        {
            try
            {
                var stats = nic.GetIPStatistics();
                inBytes += stats.BytesReceived;
                outBytes += stats.BytesSent;
            }
            catch (NetworkInformationException)
            {
                _nics = null;   // 网卡失效：下次重建
            }
        }

        return (inBytes, outBytes);
    }
}
