using System.Collections.Concurrent;

namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class Profiler(string measurementTarget) : IDisposable
{
    private readonly ThreadLocal<SingleThreadedProfiler> _threadLocalProfiler = new(() =>
        new ProfilerStopwatch(measurementTarget)
    );

    public ConcurrentDictionary<string, float> Metrics { get; } = [];
    public float TotalImpact { get; private set; }

    public ConcurrentDictionary<string, float> OffThreadMetrics { get; } = [];

    private float _offThreadTotalImpact;
    public float OffThreadTotalImpact => _offThreadTotalImpact;

    public void Start(string category)
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            return;
        }

        _threadLocalProfiler.Value.Start(category);
    }

    public float Stop(string category)
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            return 0f;
        }

        var ms = _threadLocalProfiler.Value.Stop(category, out var actualCategory);

        if (LoadingProgressMod.instance.StartupImpact.IsActiveThread())
        {
            TotalImpact += ms;

            _ = Metrics.TryGetValue(actualCategory, out var total);
            total += ms;
            Metrics[actualCategory] = total;
        }
        else
        {
            InterlockedAdd(ref _offThreadTotalImpact, ms);

            _ = OffThreadMetrics.AddOrUpdate(actualCategory, ms, (_, total) => total + ms);
        }

        return ms;
    }

    public void Dispose()
    {
        _threadLocalProfiler.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void InterlockedAdd(ref float location, float value)
    {
        float initial;
        float computed;
        do
        {
            initial = location;
            computed = initial + value;
        } while (initial != Interlocked.CompareExchange(ref location, computed, initial));
    }
}
