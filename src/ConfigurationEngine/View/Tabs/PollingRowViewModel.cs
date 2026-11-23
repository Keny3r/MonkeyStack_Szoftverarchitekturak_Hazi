namespace ConfigurationEngine.View.Tabs;

using System.Globalization;
using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Events;

/// <summary>
/// Egy metrika fix sora a „Mintavételezés” fülön (view.md, "5. Mintavételezés"): a metrika felirata a mértékegységgel,
/// az engedélyezés, a lekérdezési gyakoriság (ms, olvasható alakkal) és a metrikát használó szabályok.
/// </summary>
public sealed class PollingRowViewModel : EditableItemViewModel
{
    public PollingRowViewModel(ConfigurationWindowViewModel owner, MetricPolling polling)
        : base(owner)
    {
        Polling = polling ?? throw new ArgumentNullException(nameof(polling));
        IntervalMs = IntField(nameof(MetricPolling.IntervalMs), () => Polling.IntervalMs, v => Polling.IntervalMs = v);
    }

    public MetricPolling Polling { get; }

    public Metric Metric => Polling.Metric;

    public override ConfigurationItemKind Kind => ConfigurationItemKind.MetricPolling;

    public override string ItemKey => ItemKeys.For(Metric);

    /// <summary>A metrika felirata a mértékegységgel, pl. <c>CPU (%)</c>.</summary>
    public string MetricLabel
    {
        get
        {
            var unit = MetricText.Unit(Metric);
            return unit.Length == 0 ? MetricText.Name(Metric) : $"{MetricText.Name(Metric)} ({unit})";
        }
    }

    /// <summary>
    /// Engedélyezés. Kikapcsolt metrikánál a gyakoriság mező inaktív, de az értéke megmarad, így visszakapcsoláskor
    /// nem kell újra beírni.
    /// </summary>
    public bool IsEnabled
    {
        get => Polling.IsEnabled;
        set
        {
            if (Polling.IsEnabled == value)
            {
                return;
            }

            Polling.IsEnabled = value;
            OnPropertyChanged();
            Edited();
        }
    }

    /// <summary>Lekérdezési gyakoriság, ms (250–60000).</summary>
    public InputField IntervalMs { get; }

    /// <summary>A gyakoriság olvasható alakja, pl. <c>= 30 s</c>, <c>= 1 perc</c>.</summary>
    public string ReadableInterval => Readable(Polling.IntervalMs);

    /// <summary>A metrikát használó szabályok nevei.</summary>
    public IReadOnlyList<string> RuleNames =>
        Owner.Data?.BehaviorRules
            .Where(r => r.Metric == Metric)
            .Select(r => string.IsNullOrWhiteSpace(r.Name) ? "(névtelen szabály)" : r.Name)
            .ToList() ?? [];

    /// <summary>A „Szabályok” oszlop, pl. <c>Kis tűz, Nagy tűz</c>, vagy <c>–</c>.</summary>
    public string RulesText => RuleNames.Count == 0 ? "–" : string.Join(", ", RuleNames);

    /// <summary>Kikapcsolt metrikát szabály használ: ezek a szabályok így sosem teljesülnek.</summary>
    public bool HasUnreachableRules => !Polling.IsEnabled && RuleNames.Count > 0;

    /// <summary>A figyelmeztetés súgója.</summary>
    public string UnreachableRulesToolTip =>
        $"A mintavételezés ki van kapcsolva, így ezek a szabályok sosem teljesülnek: {RulesText}";

    /// <summary>Visszaállítás a séma alapértékeire (engedélyezve, metrikánkénti alapgyakoriság).</summary>
    public void ResetToDefaults()
    {
        var defaults = ConfigurationDefaults.CreateMetricPolling(Metric);
        Polling.IsEnabled = defaults.IsEnabled;
        Polling.IntervalMs = defaults.IntervalMs;
        Owner.SetInputError(Kind, ItemKey, IntervalMs.PropertyName, null);
        IntervalMs.Reset();
        OnPropertyChanged(nameof(IsEnabled));
    }

    /// <summary>A sor feliratainak frissítése egy módosítás után.</summary>
    public void RefreshSummary()
    {
        OnPropertyChanged(nameof(ReadableInterval));
        OnPropertyChanged(nameof(RuleNames));
        OnPropertyChanged(nameof(RulesText));
        OnPropertyChanged(nameof(HasUnreachableRules));
        OnPropertyChanged(nameof(UnreachableRulesToolTip));
    }

    internal static string Readable(int milliseconds)
    {
        if (milliseconds >= 60_000 && milliseconds % 60_000 == 0)
        {
            return string.Create(CultureInfo.CurrentCulture, $"= {milliseconds / 60_000} perc");
        }

        return string.Create(CultureInfo.CurrentCulture, $"= {milliseconds / 1000.0:0.##} s");
    }
}
