namespace OsEventEngine.Monitoring;

using System.Diagnostics;
using System.Threading.Channels;
using MonkeyStack.Core.Events;
using OsEventEngine.Sources;

/// <summary>
/// Egy metrika mintavételező ciklusa (Half-Sync/Half-Async: az aszinkron réteg eleme).
/// Saját szála nincs: egy async ciklus a thread poolon, ami a periódus leteltekor vagy
/// <see cref="TriggerNow"/> hívásra mintát vesz, és a közös csatornába írja.
/// Component Configurator: <see cref="Init"/> / <see cref="Suspend"/> / <see cref="Resume"/> / <see cref="Fini"/>
/// futás közben, újraindítás nélkül hívható. Hiba esetén <see cref="MonitorStatus.Faulted"/>, exponenciális visszalépéssel.
/// </summary>
internal sealed class MetricMonitor : IMetricMonitor, IConfigurableComponent
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly IMetricSource _source;
    private readonly ChannelWriter<MetricSample> _output;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly object _sync = new();
    private MonitorOptions _options;
    private MonitorStatus _status = MonitorStatus.Stopped;
    private bool _suspended;
    private int _consecutiveFailures;
    private CancellationTokenSource? _cts;
    private Task _loop = Task.CompletedTask;

    public MetricMonitor(IMetricSource source, ChannelWriter<MetricSample> output, TimeProvider timeProvider)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = new MonitorOptions(false, MetricCatalog.DefaultInterval(source.Metric));
    }

    public Metric Metric => _source.Metric;

    public MonitorStatus Status
    {
        get { lock (_sync) { return _status; } }
    }

    /// <summary>A ciklus befejeződése (<see cref="Fini"/> után), a leállítás megvárásához.</summary>
    public Task Completion
    {
        get { lock (_sync) { return _loop; } }
    }

    public void Init(MonitorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        bool isRestart;
        lock (_sync)
        {
            _options = options;
            _suspended = !options.Enabled;
            isRestart = _cts is not null;
            if (!isRestart)
            {
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _loop = Task.Run(() => RunAsync(token));
            }

            _status = _suspended ? MonitorStatus.Suspended
                : _consecutiveFailures > 0 ? MonitorStatus.Faulted
                : MonitorStatus.Running;
        }

        // Első indításkor a ciklus amúgy is azonnal mér; újrakonfiguráláskor felébresztjük, hogy az új periódus azonnal érvényes legyen.
        if (isRestart)
        {
            TriggerNow();
        }
    }

    public void Suspend()
    {
        lock (_sync)
        {
            _suspended = true;
            if (_cts is not null)
            {
                _status = MonitorStatus.Suspended;
            }
        }

        TriggerNow();
    }

    public void Resume()
    {
        lock (_sync)
        {
            _suspended = false;
            if (_cts is not null)
            {
                _status = _consecutiveFailures > 0 ? MonitorStatus.Faulted : MonitorStatus.Running;
            }
        }

        TriggerNow();
    }

    public void Fini()
    {
        lock (_sync)
        {
            _cts?.Cancel();
            _status = MonitorStatus.Stopped;
        }

        _wake.Writer.TryComplete();
    }

    public void TriggerNow() => _wake.Writer.TryWrite(true);

    public string Info()
    {
        lock (_sync)
        {
            string info = $"{_status} @ {_options.Interval.TotalMilliseconds:0} ms";
            return _consecutiveFailures > 0 ? $"{info} ({_consecutiveFailures} hiba)" : info;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool suspended;
            TimeSpan interval;
            lock (_sync)
            {
                suspended = _suspended;
                interval = _options.Interval;
            }

            TimeSpan wait = suspended ? Timeout.InfiniteTimeSpan : SampleOnce(interval);
            await WaitAsync(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <returns>Mennyit kell várni a következő mintáig (hibánál a visszalépési idő).</returns>
    private TimeSpan SampleOnce(TimeSpan interval)
    {
        try
        {
            int? value = _source.Sample();
            lock (_sync)
            {
                _consecutiveFailures = 0;
                if (!_suspended && _status == MonitorStatus.Faulted)
                {
                    _status = MonitorStatus.Running;
                }
            }

            if (value is int v)
            {
                _output.TryWrite(new MetricSample(Metric, MetricCatalog.Clamp(Metric, v), SampleOrigin.Live, _timeProvider.GetUtcNow()));
            }

            return interval;
        }
        catch (Exception ex)
        {
            int failures;
            lock (_sync)
            {
                failures = ++_consecutiveFailures;
                if (!_suspended)
                {
                    _status = MonitorStatus.Faulted;
                }
            }

            Trace.TraceWarning($"[OsEventEngine] {Metric} mérési hiba ({failures}.): {ex.Message}");
            double factor = Math.Pow(2, Math.Min(failures - 1, 16));
            return TimeSpan.FromMilliseconds(Math.Min(interval.TotalMilliseconds * factor, MaxBackoff.TotalMilliseconds));
        }
    }

    /// <summary>Vár a periódus leteltéig vagy egy <see cref="TriggerNow"/> jelzésig, amelyik előbb jön.</summary>
    private async Task WaitAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        using var iteration = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var wake = _wake.Reader.WaitToReadAsync(iteration.Token).AsTask();
        var delay = wait == Timeout.InfiniteTimeSpan
            ? Task.Delay(Timeout.Infinite, iteration.Token)
            : Task.Delay(wait, _timeProvider, iteration.Token);

        await Task.WhenAny(wake, delay).ConfigureAwait(false);
        iteration.Cancel();
        _wake.Reader.TryRead(out _);
    }
}
