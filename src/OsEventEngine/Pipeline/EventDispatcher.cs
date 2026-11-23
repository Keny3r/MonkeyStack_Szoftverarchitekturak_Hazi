namespace OsEventEngine.Pipeline;

using System.Diagnostics;
using System.Threading.Channels;
using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.State;

/// <summary>
/// A Half-Sync/Half-Async sor- és szinkron rétege. A monitorok a korlátos csatornába írnak (sorréteg);
/// egyetlen fogyasztó végigviszi a mintákat az interceptor láncon, majd kiküldi őket az
/// <see cref="OsEventNotifier"/>-en. Ha van <see cref="SynchronizationContext"/>, oda postáz (WPF: UI szál),
/// különben a fogyasztó szálon hív. Így az események mindig sorosítva érkeznek.
/// </summary>
internal sealed class EventDispatcher
{
    private readonly Channel<MetricSample> _channel = Channel.CreateBounded<MetricSample>(
        new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly InterceptorChain _chain;
    private readonly OsEventNotifier _notifier;
    private readonly SynchronizationContext? _context;

    public EventDispatcher(InterceptorChain chain, OsEventNotifier notifier, SynchronizationContext? context)
    {
        _chain = chain ?? throw new ArgumentNullException(nameof(chain));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _context = context;
    }

    /// <summary>A monitorok ide írják a mintákat. Telített sornál a legrégebbi minta vész el (csak a legfrissebb számít).</summary>
    public ChannelWriter<MetricSample> Writer => _channel.Writer;

    public Task Completion { get; private set; } = Task.CompletedTask;

    public void Start(CancellationToken cancellationToken)
    {
        Completion = Task.Run(() => RunAsync(cancellationToken));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var sample in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                MetricSample? result;
                try
                {
                    result = _chain.Run(sample);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"[OsEventEngine] Hiba a feldolgozási láncban ({sample.Metric}): {ex}");
                    continue;
                }

                if (result is { } output)
                {
                    Dispatch(output);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Leállítás.
        }
    }

    private void Dispatch(MetricSample sample)
    {
        if (_context is null)
        {
            Publish(sample);
        }
        else
        {
            _context.Post(static state =>
            {
                var (dispatcher, s) = ((EventDispatcher, MetricSample))state!;
                dispatcher.Publish(s);
            }, (this, sample));
        }
    }

    private void Publish(in MetricSample sample)
    {
        try
        {
            EventDataMapper.Publish(_notifier, sample);
        }
        catch (Exception ex)
        {
            // Egy hibás feliratkozó nem állíthatja le a motort (plan.md 6.3, 7. pont).
            Trace.TraceError($"[OsEventEngine] Feliratkozói hiba ({sample.Metric}): {ex}");
        }
    }
}
