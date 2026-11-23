namespace ConfigurationEngine.Rules;

using ConfigurationEngine.Model;
using MonkeyStack.Core.Events;

/// <summary>
/// A viselkedési szabályok kiértékelése egy registry-állapotra (schema.md, "Kiértékelés").
/// Állapot nélküli, tiszta függvények: ugyanarra a bemenetre mindig ugyanazt adják, bármelyik szálról hívhatók.
/// </summary>
internal static class RuleEvaluator
{
    /// <summary>
    /// Kiválasztja az aktív szabályokat, rétegsorrendben (<see cref="BehaviorRule.ZIndex"/> szerint növekvő,
    /// egyenlőségnél a kisebb <see cref="BehaviorRule.Id"/> előbb):
    /// <list type="number">
    /// <item>csak a bekapcsolt szabályok, bekapcsolt mintavételezésű metrikával;</item>
    /// <item>a metrikához még nem érkezett adat: a szabály nem teljesül;</item>
    /// <item>kizáró csoportonként csak a legnagyobb <see cref="BehaviorRule.Priority"/>-jú teljesülő szabály marad
    /// (egyenlőségnél a kisebb <see cref="BehaviorRule.Id"/>); a csoport nélküliek mind megmaradnak. A csoportnév
    /// trimmelve, kis- és nagybetűtől függetlenül számít; az üres vagy csak szóközből álló név csoport nélküli.</item>
    /// </list>
    /// </summary>
    /// <param name="candidates">A kiértékelendő elemek; mindegyikhez <paramref name="ruleOf"/> adja a szabályt.</param>
    /// <param name="ruleOf">Az elem szabálya.</param>
    /// <param name="registry">A rendszeresemény-metrikák aktuális értékei.</param>
    /// <param name="disabledMetrics">A kikapcsolt mintavételezésű metrikák; ezekre a szabály nem teljesül.</param>
    public static List<T> SelectActive<T>(
        IEnumerable<T> candidates,
        Func<T, BehaviorRule> ruleOf,
        OsEventDataRegistry registry,
        IReadOnlySet<Metric> disabledMetrics)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(ruleOf);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(disabledMetrics);

        var active = new List<T>();
        var groupWinners = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            var rule = ruleOf(candidate);
            if (!rule.IsEnabled || disabledMetrics.Contains(rule.Metric) ||
                !TryReadMetric(registry, rule.Metric, out int value) || !IsSatisfied(rule, value))
            {
                continue;
            }

            // A validátorhoz igazodva: a betöltés normalizál, de a kiértékelés bármilyen tárolóval helyes marad.
            if (string.IsNullOrWhiteSpace(rule.ExclusiveGroup))
            {
                active.Add(candidate);
                continue;
            }

            string group = rule.ExclusiveGroup.Trim();
            if (!groupWinners.TryGetValue(group, out int index))
            {
                groupWinners[group] = active.Count;
                active.Add(candidate);
            }
            else if (Beats(rule, ruleOf(active[index])))
            {
                active[index] = candidate;
            }
        }

        // A List.Sort nem stabil, ezért az Id is a rendezési kulcs része.
        active.Sort((a, b) =>
        {
            var (ra, rb) = (ruleOf(a), ruleOf(b));
            int byZIndex = ra.ZIndex.CompareTo(rb.ZIndex);
            return byZIndex != 0 ? byZIndex : ra.Id.CompareTo(rb.Id);
        });
        return active;
    }

    /// <summary>
    /// Teljesül-e a szabály feltétele a metrika <paramref name="value"/> értékére.
    /// <see cref="ComparisonOperator.Between"/>: zárt intervallum; <see cref="Metric.Time"/> esetén, ha az alsó határ
    /// nagyobb a felsőnél, éjfélen átnyúló intervallum (pl. 1230–360: 20:30-tól 06:00-ig).
    /// </summary>
    public static bool IsSatisfied(BehaviorRule rule, int value)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule.Operator switch
        {
            ComparisonOperator.Gt => value > rule.Threshold,
            ComparisonOperator.Gte => value >= rule.Threshold,
            ComparisonOperator.Lt => value < rule.Threshold,
            ComparisonOperator.Lte => value <= rule.Threshold,
            ComparisonOperator.Between when rule.ThresholdUpper is { } upper =>
                rule.Metric == Metric.Time && rule.Threshold > upper
                    ? value >= rule.Threshold || value <= upper
                    : value >= rule.Threshold && value <= upper,
            _ => false
        };
    }

    /// <summary>
    /// A metrika értéke a registry-ből (schema.md, "Metrikák és értéktartományok"); <c>false</c>, ha még nem érkezett adat.
    /// </summary>
    public static bool TryReadMetric(OsEventDataRegistry registry, Metric metric, out int value)
    {
        ArgumentNullException.ThrowIfNull(registry);

        int? read = metric switch
        {
            Metric.Cpu => registry.Cpu?.UsagePercent,
            Metric.Memory => registry.Memory?.UsagePercent,
            Metric.Disk => registry.Disk?.UsagePercent,
            Metric.Battery => registry.Battery?.ChargePercent,
            Metric.WindowCount => registry.WindowCount?.WindowCount,
            Metric.Time => registry.Time is { } time ? (time.Hour * 60) + time.Minute : null,
            Metric.Inactivity => registry.Inactivity?.InactivityMinutes,
            _ => null
        };

        value = read ?? 0;
        return read.HasValue;
    }

    private static bool Beats(BehaviorRule challenger, BehaviorRule holder) =>
        challenger.Priority != holder.Priority ? challenger.Priority > holder.Priority : challenger.Id < holder.Id;
}
