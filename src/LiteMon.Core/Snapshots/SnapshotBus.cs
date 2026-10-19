using LiteMon.Core.Metrics;

namespace LiteMon.Core.Snapshots;

public interface ISnapshotSource
{
    Snapshot Current { get; }

    event EventHandler<Snapshot>? Updated;
}

/// <summary>快照发布总线。Publish 在采样线程调用，订阅方自行切回 UI 线程。</summary>
public sealed class SnapshotBus : ISnapshotSource
{
    private Snapshot _current = Snapshot.Empty;

    public Snapshot Current => Volatile.Read(ref _current);

    public event EventHandler<Snapshot>? Updated;

    public void Publish(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
        Updated?.Invoke(this, snapshot);
    }
}
