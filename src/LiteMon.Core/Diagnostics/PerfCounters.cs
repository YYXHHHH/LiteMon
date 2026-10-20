namespace LiteMon.Core.Diagnostics;

/// <summary>轻量性能计数：采样耗时与渲染耗时，供 --diag 模式输出。</summary>
public static class PerfCounters
{
    private static long _pollTicks;
    private static long _renderTicks;
    private static long _frames;
    private static long _animFrames;
    private static long _snapshots;

    public static void AddPoll(long ticks) => Interlocked.Add(ref _pollTicks, ticks);

    public static void AddRender(long ticks)
    {
        Interlocked.Add(ref _renderTicks, ticks);
        Interlocked.Increment(ref _frames);
    }

    public static void AddAnimationFrame() => Interlocked.Increment(ref _animFrames);

    public static void AddSnapshot() => Interlocked.Increment(ref _snapshots);

    public static (long PollTicks, long RenderTicks, long Frames, long AnimFrames, long Snapshots) Read()
        => (Interlocked.Read(ref _pollTicks), Interlocked.Read(ref _renderTicks),
            Interlocked.Read(ref _frames), Interlocked.Read(ref _animFrames), Interlocked.Read(ref _snapshots));
}
