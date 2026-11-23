namespace OsEventEngine.Tests;

using MonkeyStack.Core.Events;
using OsEventEngine.Sources;

/// <summary>
/// Hamis mérési forrás: a megadott függvény értékét adja, és számolja a hívásokat.
/// </summary>
internal sealed class FakeSource(Metric metric, Func<int?> value) : IMetricSource
{
    private int _calls;

    public FakeSource(Metric metric, int value)
        : this(metric, () => value)
    {
    }

    public Metric Metric => metric;

    public int Calls => Volatile.Read(ref _calls);

    public int? Sample()
    {
        Interlocked.Increment(ref _calls);
        return value();
    }
}

internal static class Eventually
{
    /// <summary>
    /// Vár, amíg a feltétel teljesül (valós időben legfeljebb <paramref name="timeoutMs"/> ms).
    /// Ha van <paramref name="tick"/>, minden körben meghívja (pl. a hamis idő léptetésére), így nem számít,
    /// hogy a háttérciklus mikor regisztrálja a következő várakozását.
    /// </summary>
    public static async Task<bool> UntilAsync(Func<bool> condition, Action? tick = null, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            tick?.Invoke();
            await Task.Delay(10);
        }

        return condition();
    }
}
