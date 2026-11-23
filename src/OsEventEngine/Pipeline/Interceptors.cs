namespace OsEventEngine.Pipeline;

using System.Diagnostics;
using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.State;

/// <summary>
/// Az interceptorok sorban futtatása (Interceptor minta). Ha egy elem eldobja a mintát, a lánc megszakad.
/// Egyetlen fogyasztó szálon fut, ezért az elemek belső állapota lock nélkül biztonságos.
/// </summary>
internal sealed class InterceptorChain(IReadOnlyList<IEventInterceptor> interceptors)
{
    public MetricSample? Run(in MetricSample sample)
    {
        MetricSample current = sample;
        foreach (var interceptor in interceptors)
        {
            if (interceptor.Intercept(current) is not { } next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }
}

/// <summary>
/// Exponenciális mozgóátlag az élő CPU-értékre, hogy a pillanatnyi tüskék ne villogtassák az overlay-t.
/// Szimulált mintát nem simít, és a simítást újraindítja, így élőre visszaváltva nincs elhúzódó átmenet.
/// </summary>
internal sealed class SmoothingInterceptor(Func<bool> isEnabled, double alpha = 0.5) : IEventInterceptor
{
    private double? _average;

    public MetricSample? Intercept(in MetricSample sample)
    {
        if (sample.Metric != Metric.Cpu)
        {
            return sample;
        }

        if (sample.Origin != SampleOrigin.Live || !isEnabled())
        {
            _average = null;
            return sample;
        }

        _average = _average is double previous ? previous + (alpha * (sample.Value - previous)) : sample.Value;
        return sample with { Value = (int)Math.Round(_average.Value) };
    }
}

/// <summary>
/// Csak a változást engedi tovább: ha |új − utolsó küldött| ≥ deadband. Metrikánként az első minta,
/// és élő ↔ szimulált váltás után az első minta mindig átjut (kezdeti pillanatkép, plan.md 6.3).
/// </summary>
internal sealed class ChangeFilterInterceptor(Func<Metric, int> deadband) : IEventInterceptor
{
    private readonly Dictionary<Metric, MetricSample> _lastSent = [];

    public MetricSample? Intercept(in MetricSample sample)
    {
        if (_lastSent.TryGetValue(sample.Metric, out var last)
            && last.Origin == sample.Origin
            && Math.Abs(sample.Value - last.Value) < Math.Max(1, deadband(sample.Metric)))
        {
            return null;
        }

        _lastSent[sample.Metric] = sample;
        return sample;
    }
}

/// <summary>
/// A kiküldésre kerülő értékeket a <see cref="MetricSnapshotStore"/>-ba írja (OS Mock View, <c>GetSnapshot</c>).
/// </summary>
internal sealed class SnapshotInterceptor(MetricSnapshotStore store) : IEventInterceptor
{
    public MetricSample? Intercept(in MetricSample sample)
    {
        store.Update(sample);
        return sample;
    }
}

/// <summary>
/// Kimenő események naplózása a Debug/Trace kimenetre, ha be van kapcsolva.
/// </summary>
internal sealed class LoggingInterceptor(Func<bool> isEnabled) : IEventInterceptor
{
    public MetricSample? Intercept(in MetricSample sample)
    {
        if (isEnabled())
        {
            Trace.WriteLine($"[OsEventEngine] {sample.Timestamp:HH:mm:ss.fff} {sample.Metric} = {sample.Value} ({sample.Origin})");
        }

        return sample;
    }
}
