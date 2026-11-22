namespace ConfigurationEngine.View;

using System.Globalization;
using ConfigurationEngine.Model;
using MonkeyStack.Core.Events;

/// <summary>Egy metrika a legördülő listában.</summary>
public sealed record MetricOption(Metric Value, string Name);

/// <summary>Egy összehasonlító operátor a legördülő listában, jellel (gui.md, "Részletek").</summary>
public sealed record OperatorOption(ComparisonOperator Value, string Symbol);

/// <summary>
/// A metrikák és operátorok megjelenítése (gui.md, "3. Viselkedési szabályok" és "5. Mintavételezés";
/// schema.md, "Metrikák és értéktartományok"): név, mértékegység, és az <c>Idő</c> metrika <c>HH:mm</c> alakja.
/// </summary>
public static class MetricText
{
    public static IReadOnlyList<MetricOption> MetricOptions { get; } =
        Enum.GetValues<Metric>().Select(m => new MetricOption(m, Name(m))).ToList();

    public static IReadOnlyList<OperatorOption> OperatorOptions { get; } =
        Enum.GetValues<ComparisonOperator>().Select(o => new OperatorOption(o, Symbol(o))).ToList();

    public static string Name(Metric metric) => metric switch
    {
        Metric.Cpu => "CPU",
        Metric.Memory => "Memória",
        Metric.Disk => "Háttértár",
        Metric.Battery => "Akkumulátor",
        Metric.WindowCount => "Ablakok száma",
        Metric.Time => "Idő",
        Metric.Inactivity => "Inaktivitás",
        _ => metric.ToString()
    };

    /// <summary>A metrika mértékegysége: <c>%</c>, <c>db</c>, <c>perc</c>; az <c>Idő</c> <c>HH:mm</c> alakú, nincs egysége.</summary>
    public static string Unit(Metric metric) => metric switch
    {
        Metric.Cpu or Metric.Memory or Metric.Disk or Metric.Battery => "%",
        Metric.WindowCount => "db",
        Metric.Inactivity => "perc",
        _ => string.Empty
    };

    public static string Symbol(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Gt => ">",
        ComparisonOperator.Gte => "≥",
        ComparisonOperator.Lt => "<",
        ComparisonOperator.Lte => "≤",
        ComparisonOperator.Between => "között",
        _ => op.ToString()
    };

    /// <summary>A küszöbérték szövegként: az <c>Idő</c> metrikánál <c>HH:mm</c>, egyébként egész szám.</summary>
    public static string FormatValue(Metric metric, int value) =>
        metric == Metric.Time ? FormatTime(value) : value.ToString(CultureInfo.CurrentCulture);

    /// <summary>Éjfél óta eltelt percek <c>HH:mm</c> alakban.</summary>
    public static string FormatTime(int minutes) =>
        string.Create(CultureInfo.InvariantCulture, $"{minutes / 60:00}:{Math.Abs(minutes % 60):00}");

    /// <summary><c>H:mm</c> vagy <c>HH:mm</c> alakú idő (00:00–23:59) éjfél óta eltelt percekre.</summary>
    public static bool TryParseTime(string text, out int minutes)
    {
        minutes = 0;
        var parts = text.Trim().Split(':');
        if (parts.Length != 2
            || parts[0].Length is < 1 or > 2
            || parts[1].Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var mins)
            || hours > 23
            || mins > 59)
        {
            return false;
        }

        minutes = (hours * 60) + mins;
        return true;
    }

    /// <summary>
    /// A feltétel rövid szöveges alakja a szabálylistában (gui.md, "Rétegek listája"), pl. <c>CPU &gt; 80 %</c>,
    /// <c>20:30–06:00</c>.
    /// </summary>
    public static string Condition(BehaviorRule rule)
    {
        var unit = Unit(rule.Metric);
        var suffix = unit.Length == 0 ? string.Empty : " " + unit;
        var lower = FormatValue(rule.Metric, rule.Threshold);
        if (rule.Operator == ComparisonOperator.Between)
        {
            var upper = rule.ThresholdUpper is { } u ? FormatValue(rule.Metric, u) : "?";
            return rule.Metric == Metric.Time
                ? $"{lower}–{upper}"
                : $"{Name(rule.Metric)} {lower}–{upper}{suffix}";
        }

        return $"{Name(rule.Metric)} {Symbol(rule.Operator)} {lower}{suffix}";
    }
}
