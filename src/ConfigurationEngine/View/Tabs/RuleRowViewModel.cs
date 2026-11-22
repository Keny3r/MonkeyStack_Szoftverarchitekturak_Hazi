namespace ConfigurationEngine.View.Tabs;

using System.Globalization;
using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Events;

/// <summary>
/// Egy viselkedési szabály a „Szabályok” fülön (gui.md, "3. Viselkedési szabályok (rétegek)"): a rétegek listájának
/// sora és a részletek panel mezői. A módosítások a szerkesztett másolatba kerülnek, és Mentéskor tárolódnak.
/// </summary>
public sealed class RuleRowViewModel : EditableItemViewModel
{
    private double _previewFramesPerSecond;

    public RuleRowViewModel(ConfigurationWindowViewModel owner, BehaviorRule rule)
        : base(owner)
    {
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        Name = Field(nameof(BehaviorRule.Name), () => Rule.Name, text =>
        {
            Rule.Name = text;
            return null;
        });
        Threshold = Field(
            nameof(BehaviorRule.Threshold),
            () => MetricText.FormatValue(Rule.Metric, Rule.Threshold),
            ThresholdWriter(v => Rule.Threshold = v!.Value, required: true));
        ThresholdUpper = Field(
            nameof(BehaviorRule.ThresholdUpper),
            () => Rule.ThresholdUpper is { } upper ? MetricText.FormatValue(Rule.Metric, upper) : string.Empty,
            ThresholdWriter(v => Rule.ThresholdUpper = v, required: false));
        FramesPerSecond = DoubleField(nameof(BehaviorRule.FramesPerSecond), () => Rule.FramesPerSecond, v => Rule.FramesPerSecond = v);
        ZIndex = IntField(nameof(BehaviorRule.ZIndex), () => Rule.ZIndex, v => Rule.ZIndex = v);
        Priority = IntField(nameof(BehaviorRule.Priority), () => Rule.Priority, v => Rule.Priority = v);
        Preview = owner.Previews.Create();
        _previewFramesPerSecond = ConfigurationLimits.FramesPerSecond.Contains(rule.FramesPerSecond)
            ? rule.FramesPerSecond
            : new BehaviorRule().FramesPerSecond;
    }

    public BehaviorRule Rule { get; }

    public override ConfigurationItemKind Kind => ConfigurationItemKind.BehaviorRule;

    public override string ItemKey =>
        ItemKeys.For(Rule, Owner.Data?.BehaviorRules.IndexOf(Rule) ?? 0);

    // Alap

    /// <summary>Név, 1–64 karakter.</summary>
    public InputField Name { get; }

    /// <summary>Engedélyezés; a listából is kapcsolható.</summary>
    public bool IsEnabled
    {
        get => Rule.IsEnabled;
        set
        {
            if (Rule.IsEnabled == value)
            {
                return;
            }

            Rule.IsEnabled = value;
            OnPropertyChanged();
            Edited();
        }
    }

    // Triggerfeltétel

    /// <summary>
    /// A metrika. Váltáskor a küszöbértékek megmaradnak (ha az új tartományon kívül esnek, a mező hibát jelez),
    /// csak az alakjuk változik (pl. <c>HH:mm</c>).
    /// </summary>
    public Metric Metric
    {
        get => Rule.Metric;
        set
        {
            if (Rule.Metric == value)
            {
                return;
            }

            Rule.Metric = value;
            ResetField(Threshold);
            ResetField(ThresholdUpper);
            OnPropertyChanged();
            OnPropertyChanged(nameof(UnitText));
            OnPropertyChanged(nameof(IsTime));
            Edited();
        }
    }

    /// <summary>
    /// Az operátor. <c>között</c>-ről másra váltáskor a felső határ törlődik, <c>között</c>-re váltáskor kötelezővé válik.
    /// </summary>
    public ComparisonOperator Operator
    {
        get => Rule.Operator;
        set
        {
            if (Rule.Operator == value)
            {
                return;
            }

            if (Rule.Operator == ComparisonOperator.Between)
            {
                Rule.ThresholdUpper = null;
            }

            Rule.Operator = value;
            ResetField(ThresholdUpper);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsBetween));
            Edited();
        }
    }

    /// <summary>Küszöbérték (<c>között</c> operátornál az alsó határ), a metrika mértékegységével.</summary>
    public InputField Threshold { get; }

    /// <summary>Felső határ; csak <c>között</c> operátornál látható.</summary>
    public InputField ThresholdUpper { get; }

    public bool IsBetween => Rule.Operator == ComparisonOperator.Between;

    /// <summary>Az <c>Idő</c> metrika küszöbei <c>HH:mm</c> alakban szerkeszthetők.</summary>
    public bool IsTime => Rule.Metric == Metric.Time;

    /// <summary>A küszöbök mértékegysége (<c>%</c>, <c>db</c>, <c>perc</c>).</summary>
    public string UnitText => MetricText.Unit(Rule.Metric);

    /// <summary><c>Idő</c> + <c>között</c>, és az alsó határ későbbi, mint a felső.</summary>
    public bool CrossesMidnight =>
        IsTime && IsBetween && Rule.ThresholdUpper is { } upper && Rule.Threshold > upper;

    // Animáció

    /// <summary>
    /// Az overlay animáció forrása. Új szabálynál nincs kiválasztva (0); a legördülő lista üres kijelölése (<c>null</c>)
    /// nem írja felül.
    /// </summary>
    public int? AnimationSourceId
    {
        get => Rule.AnimationSourceId;
        set
        {
            if (value is not { } id || id == Rule.AnimationSourceId)
            {
                return;
            }

            Rule.AnimationSourceId = id;
            OnPropertyChanged();
            Edited();
        }
    }

    /// <summary>Lejátszási sebesség, fps (1–60).</summary>
    public InputField FramesPerSecond { get; }

    public PreviewViewModel Preview { get; }

    // Réteg

    /// <summary>Rétegsorrend, 0–100. A lista a mező elhagyásakor rendeződik újra, nem gépelés közben.</summary>
    public InputField ZIndex { get; }

    // Kizárás

    /// <summary>
    /// Kizáró csoport (szerkeszthető legördülő lista). Üres vagy csak szóközből álló név = nincs csoport;
    /// ilyenkor a prioritás inaktív. A végleges trimmelést a tároló végzi.
    /// </summary>
    public string? ExclusiveGroup
    {
        get => Rule.ExclusiveGroup;
        set
        {
            var group = string.IsNullOrEmpty(value) ? null : value;
            if (Rule.ExclusiveGroup == group)
            {
                return;
            }

            Rule.ExclusiveGroup = group;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasGroup));
            Edited();
        }
    }

    public bool HasGroup => !string.IsNullOrWhiteSpace(Rule.ExclusiveGroup);

    /// <summary>Csoporton belüli prioritás, 0–100; csak csoport megadásakor aktív.</summary>
    public InputField Priority { get; }

    // A lista sora

    /// <summary>A név a listában.</summary>
    public string DisplayName => Rule.Name;

    /// <summary>A feltétel rövid alakja, pl. <c>CPU &gt; 80 %</c> vagy <c>20:30–06:00</c>.</summary>
    public string ConditionText => MetricText.Condition(Rule);

    public string ZIndexText => string.Create(CultureInfo.CurrentCulture, $"Z = {Rule.ZIndex}");

    /// <summary>
    /// A szabály metrikájának mintavételezése ki van kapcsolva, így a szabály sosem teljesül
    /// (gui.md, "Rétegek listája" és "5. Mintavételezés").
    /// </summary>
    public bool IsMetricPollingDisabled =>
        Owner.Data?.MetricPollings.FirstOrDefault(p => p.Metric == Rule.Metric) is { IsEnabled: false };

    /// <summary>A lista sorának és a részletek feliratainak frissítése egy módosítás után.</summary>
    public void RefreshSummary()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ConditionText));
        OnPropertyChanged(nameof(ZIndexText));
        OnPropertyChanged(nameof(IsMetricPollingDisabled));
        OnPropertyChanged(nameof(CrossesMidnight));
        OnPropertyChanged(nameof(HasGroup));
    }

    /// <summary>
    /// Az előnézet a szerkesztett forrást és sebességet mutassa; tartományon kívüli sebességnél az utolsó érvényessel.
    /// Forrás nélkül (új szabály) nincs előnézet.
    /// </summary>
    public void RefreshPreview()
    {
        if (ConfigurationLimits.FramesPerSecond.Contains(Rule.FramesPerSecond))
        {
            _previewFramesPerSecond = Rule.FramesPerSecond;
        }

        Preview.Show(Owner.FindSource(Rule.AnimationSourceId), _previewFramesPerSecond);
    }

    /// <summary>A sor törlésekor: a beviteli hibái megszűnnek, az előnézete eldobódik.</summary>
    public override void OnRemoved()
    {
        base.OnRemoved();
        Preview.Release();
    }

    /// <summary>A küszöbérték: az <c>Idő</c> metrikánál <c>HH:mm</c>, egyébként egész szám.</summary>
    private Func<string, string?> ThresholdWriter(Action<int?> set, bool required) => text =>
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            if (required)
            {
                return "A mező nem lehet üres.";
            }

            set(null);
            return null;
        }

        if (Rule.Metric == Metric.Time)
        {
            if (!MetricText.TryParseTime(trimmed, out var minutes))
            {
                return "Az időt ÓÓ:PP alakban add meg (pl. 20:30).";
            }

            set(minutes);
            return null;
        }

        if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
            && !int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return "Egész számot adj meg.";
        }

        set(value);
        return null;
    };

    /// <summary>A mező beviteli hibájának elvetése, és a modell értékének visszaírása (pl. metrikaváltáskor).</summary>
    private void ResetField(InputField field)
    {
        Owner.SetInputError(Kind, ItemKey, field.PropertyName, null);
        field.Reset();
    }
}
