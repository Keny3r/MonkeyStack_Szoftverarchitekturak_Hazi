namespace OsEventEngine.Simulation;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.Sources;

/// <summary>
/// Egy metrika forrása, ami élő és szimulált mód között váltható (Strategy + Proxy).
/// Élő módban átlátszóan továbbítja a hívást a valódi forrásnak; szimulált módban a beállított értéket adja,
/// és a valódi forrást <b>nem hívja</b>, így a gépet sem terheli (plan.md, ADR-5).
/// A monitor és a lánc nem tud róla, hogy szimulált értéket kap: ugyanazon az úton megy, mint az élő.
/// </summary>
internal sealed class SwitchableSource : IMetricSource, ISimulatable
{
    private readonly IMetricSource _live;
    private readonly MetricDescriptor _descriptor;
    private readonly object _sync = new();
    private SourceMode _mode = SourceMode.Live;
    private int _simulatedValue;
    private int? _lastLiveValue;

    public SwitchableSource(IMetricSource live, MetricDescriptor descriptor)
    {
        _live = live ?? throw new ArgumentNullException(nameof(live));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        if (live.Metric != descriptor.Metric)
        {
            throw new ArgumentException("A leíró másik metrikához tartozik.", nameof(descriptor));
        }

        _simulatedValue = descriptor.Min;
    }

    public Metric Metric => _live.Metric;

    /// <summary>
    /// A forrás módja. Élőből szimuláltba váltáskor a szimulált érték az utolsó élő érték lesz, így nincs ugrás.
    /// </summary>
    public SourceMode Mode
    {
        get { lock (_sync) { return _mode; } }
        set
        {
            lock (_sync)
            {
                if (value == SourceMode.Simulated && _mode == SourceMode.Live && _lastLiveValue is int live)
                {
                    _simulatedValue = ClampToDescriptor(live);
                }

                _mode = value;
            }
        }
    }

    /// <summary>A szimulált érték, a leíró tartományára vágva.</summary>
    public int SimulatedValue
    {
        get { lock (_sync) { return _simulatedValue; } }
        set { lock (_sync) { _simulatedValue = ClampToDescriptor(value); } }
    }

    /// <summary>Az utolsó élő mérés (szimulált módban nem frissül, mert ilyenkor nem mérünk).</summary>
    public int? LastLiveValue
    {
        get { lock (_sync) { return _lastLiveValue; } }
    }

    public int? Sample() => SampleWithOrigin().Value;

    /// <summary>Egy minta a forrásával együtt, hogy a lánc tudja, élő vagy szimulált.</summary>
    public (int? Value, SampleOrigin Origin) SampleWithOrigin()
    {
        lock (_sync)
        {
            if (_mode == SourceMode.Simulated)
            {
                return (_simulatedValue, SampleOrigin.Simulated);
            }
        }

        // A valódi mérés a lockon kívül fut, hogy a UI-ból jövő módváltás ne várjon rá.
        int? value = _live.Sample();
        if (value is int v)
        {
            lock (_sync)
            {
                _lastLiveValue = v;
            }
        }

        return (value, SampleOrigin.Live);
    }

    private int ClampToDescriptor(int value) => Math.Clamp(value, _descriptor.Min, _descriptor.Max);
}
