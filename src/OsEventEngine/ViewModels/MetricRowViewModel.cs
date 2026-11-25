namespace OsEventEngine.ViewModels;

using MonkeyStack.Core.Events;
using OsEventEngine.Monitoring;
using OsEventEngine.Simulation;

/// <summary>
/// Az OS Mock View egy sora: egy metrika módja, csúszkája, kiküldött és élő értéke, monitorállapota.
/// A sor csak a <see cref="MetricDescriptor"/>-ból dolgozik, így a View-ban nincs metrika-specifikus kód.
/// </summary>
internal sealed class MetricRowViewModel : ObservableObject
{
    private readonly OsEventMonitorService _service;
    private bool _isRefreshing;
    private bool _isSimulated;
    private double _sliderValue;
    private string _sentText = "–";
    private string _liveText = "–";
    private string _originText = string.Empty;
    private string _monitorInfo = string.Empty;

    public MetricRowViewModel(MetricDescriptor descriptor, OsEventMonitorService service)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _sliderValue = service.SimulationController.GetSimulatedValue(descriptor.Metric);
    }

    public MetricDescriptor Descriptor { get; }

    public string DisplayName => Descriptor.DisplayName;

    public double Minimum => Descriptor.Min;

    public double Maximum => Descriptor.Max;

    /// <summary>Szimulált mód be/ki. Bekapcsoláskor az utolsó élő értékről indul (nincs ugrás).</summary>
    public bool IsSimulated
    {
        get => _isSimulated;
        set
        {
            if (SetProperty(ref _isSimulated, value) && !_isRefreshing)
            {
                _service.Simulation.SetMode(Descriptor.Metric, value ? SourceMode.Simulated : SourceMode.Live);
                Refresh();
            }
        }
    }

    /// <summary>A csúszka értéke. Mozgatása szimulált módba vált, és azonnal mintát kér.</summary>
    public double SliderValue
    {
        get => _sliderValue;
        set
        {
            if (!SetProperty(ref _sliderValue, Math.Round(value)))
            {
                return;
            }

            OnPropertyChanged(nameof(SliderText));
            if (!_isRefreshing)
            {
                _service.Simulation.SetSimulatedValue(Descriptor.Metric, (int)_sliderValue);
                SetProperty(ref _isSimulated, true, nameof(IsSimulated));
            }
        }
    }

    public string SliderText => Format((int)_sliderValue);

    /// <summary>A legutóbb kiküldött érték (amit a Karakter is megkapott).</summary>
    public string SentText
    {
        get => _sentText;
        private set => SetProperty(ref _sentText, value);
    }

    /// <summary>Az utolsó élő mérés. Szimulált módban nem frissül, mert ilyenkor nem mérünk.</summary>
    public string LiveText
    {
        get => _liveText;
        private set => SetProperty(ref _liveText, value);
    }

    /// <summary>A kiküldött érték forrása: élő vagy szimulált.</summary>
    public string OriginText
    {
        get => _originText;
        private set => SetProperty(ref _originText, value);
    }

    public string MonitorInfo
    {
        get => _monitorInfo;
        private set => SetProperty(ref _monitorInfo, value);
    }

    /// <summary>Frissítés a modellből (a View időzítője hívja). A felhasználó által húzott csúszkát nem írja felül.</summary>
    public void Refresh()
    {
        var metric = Descriptor.Metric;
        _isRefreshing = true;
        try
        {
            IsSimulated = _service.Simulation.GetMode(metric) == SourceMode.Simulated;
            int? live = _service.SimulationController.GetLastLiveValue(metric);
            if (!IsSimulated && live is int liveValue)
            {
                // Élő módban a csúszka követi a mért értéket, így szimulációra váltva onnan indul.
                SliderValue = Math.Clamp(liveValue, Descriptor.Min, Descriptor.Max);
            }
            else if (IsSimulated)
            {
                // Szimulált módban a csúszka a modellben beállított értéket mutatja, így a preset vagy más komponens
                // (pl. teszt) által beállított érték is látszik. A húzott csúszka értéke már a modellben van, ezért nem ugrik vissza.
                SliderValue = Math.Clamp(_service.SimulationController.GetSimulatedValue(metric), Descriptor.Min, Descriptor.Max);
            }

            LiveText = live is int l ? Format(l) + (IsSimulated ? " (utolsó)" : string.Empty) : "–";
            if (_service.TryGetSentSample(metric, out var sent))
            {
                SentText = Format(sent.Value);
                OriginText = sent.Origin == SampleOrigin.Simulated ? "szimulált" : "élő";
            }

            MonitorInfo = _service.GetMonitorInfo(metric);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private string Format(int value) => Descriptor.Metric == Metric.Time
        ? $"{value / 60:00}:{value % 60:00}"
        : $"{value} {Descriptor.Unit}";
}
