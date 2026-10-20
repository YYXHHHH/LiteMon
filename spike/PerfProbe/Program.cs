using System.Diagnostics;
using LiteMon.Core.Sensors;
using LiteMon.Core.Metrics;
using LiteMon.Sensors;

var providers = new ISensorProvider[]
{
    new CpuProvider(),
    new MemoryProvider(),
    new NetworkProvider(),
    new DiskProvider(),
    new NvmlProvider(),
    new LhmProvider(),
    new UptimeProvider(),
};

Console.WriteLine("=== init ===");
foreach (var p in providers)
{
    var sw = Stopwatch.StartNew();
    try { p.Initialize(); } catch (Exception ex) { Console.WriteLine($"{p.Name}: init failed {ex.GetType().Name}"); }
    sw.Stop();
    Console.WriteLine($"{p.Name,-10} enabled={p.IsEnabled,-5} init={sw.Elapsed.TotalMilliseconds,8:F1} ms");
}

var builder = new SnapshotBuilder();
var stats = providers.ToDictionary(p => p.Name, _ => new List<double>());
Console.WriteLine();
Console.WriteLine("=== polling 20 rounds ===");

for (int round = 0; round < 20; round++)
{
    long now = Environment.TickCount64;
    builder.Begin(now);

    foreach (var p in providers)
    {
        if (!p.IsEnabled) continue;
        var sw = Stopwatch.StartNew();
        try { p.Poll(builder, now); } catch (Exception ex) { Console.WriteLine($"{p.Name} poll failed: {ex.Message}"); }
        sw.Stop();
        stats[p.Name].Add(sw.Elapsed.TotalMilliseconds);
    }

    builder.Build();
    Thread.Sleep(1000);
}

Console.WriteLine();
Console.WriteLine("provider      avg(ms)   max(ms)   total(ms)");
foreach (var (name, values) in stats)
{
    if (values.Count == 0) { Console.WriteLine($"{name,-12} (disabled)"); continue; }
    Console.WriteLine($"{name,-12} {values.Average(),8:F2} {values.Max(),9:F2} {values.Sum(),10:F1}");
}

foreach (var p in providers) p.Dispose();
