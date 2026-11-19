namespace ConfigurationEngine.Validation;

using System.Globalization;
using ConfigurationEngine.Model;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>
/// Mező- és kereszthivatkozási szintű validálás (schema.md, "Validálás" és a táblák "Érvényes érték" oszlopai).
/// Gyors, mellékhatás nélküli, a fájlrendszert nem éri el; a médiafájlokat az <see cref="IMediaValidator"/> ellenőrzi.
/// </summary>
public sealed class ConfigurationValidator : IConfigurationValidator
{
    /// <summary>A forrásnevek egyediségét kis- és nagybetűtől (ékezetes betűket is beleértve) függetlenül nézzük.</summary>
    private static readonly StringComparer NameComparer = StringComparer.InvariantCultureIgnoreCase;

    public ValidationResult Validate(ConfigurationData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var errors = new ErrorCollector();
        var sourceIds = ValidateSources(data.AnimationSources, errors);
        ValidateMovementAnimations(data.MovementAnimations, sourceIds, errors);
        ValidateRules(data.BehaviorRules, sourceIds, errors);
        ValidateCharacterSettings(data.Character, errors);
        ValidateMetricPollings(data.MetricPollings, errors);

        return errors.ToResult();
    }

    private static HashSet<int> ValidateSources(IReadOnlyList<AnimationSource> sources, ErrorCollector errors)
    {
        var ids = new HashSet<int>();
        var names = new HashSet<string>(NameComparer);

        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];

            // Ismétlődő azonosítónál a kulcs a listabeli index, különben a hiba mindkét elemre mutatna.
            var duplicateId = source.Id != 0 && !ids.Add(source.Id);
            var add = errors.For(ConfigurationItemKind.AnimationSource, duplicateId ? ItemKeys.ForIndex(i) : ItemKeys.For(source, i));

            if (duplicateId)
            {
                add(nameof(AnimationSource.Id), "Az azonosító többször szerepel.");
            }

            if (CheckName(source.Name, add) && !names.Add(source.Name.Trim()))
            {
                add(nameof(AnimationSource.Name), $"Már van „{source.Name.Trim()}” nevű animációs forrás.");
            }

            if (!Enum.IsDefined(source.SourceType))
            {
                add(nameof(AnimationSource.SourceType), "Ismeretlen forrástípus.");
            }

            if (string.IsNullOrWhiteSpace(source.Path))
            {
                add(nameof(AnimationSource.Path), "Az útvonal megadása kötelező.");
            }
            else if (source.Path.Any(char.IsControl))
            {
                add(nameof(AnimationSource.Path), "Az útvonal nem tartalmazhat vezérlőkaraktert.");
            }

            CheckGrid(source, add);
        }

        return ids;
    }

    private static void CheckGrid(AnimationSource source, Action<string?, string> add)
    {
        // Ismeretlen típusnál a rács szabályai nem értelmezhetők; elég a típus hibája.
        if (!Enum.IsDefined(source.SourceType))
        {
            return;
        }

        if (source.SourceType != AnimationSourceType.SpriteSheet)
        {
            foreach (var (name, value) in GridFields(source))
            {
                if (value is not null)
                {
                    add(name, "Csak sprite sheet esetén adható meg.");
                }
            }

            return;
        }

        CheckRequiredRange(source.FrameWidth, ConfigurationLimits.FramePx, nameof(AnimationSource.FrameWidth), "A képkocka szélessége", "px", add);
        CheckRequiredRange(source.FrameHeight, ConfigurationLimits.FramePx, nameof(AnimationSource.FrameHeight), "A képkocka magassága", "px", add);
        var columnsOk = CheckRequiredRange(source.Columns, ConfigurationLimits.GridCells, nameof(AnimationSource.Columns), "Az oszlopok száma", null, add);
        var rowsOk = CheckRequiredRange(source.Rows, ConfigurationLimits.GridCells, nameof(AnimationSource.Rows), "A sorok száma", null, add);

        if (source.FrameCount is not { } frameCount)
        {
            add(nameof(AnimationSource.FrameCount), "A képkockák számának megadása kötelező.");
        }
        else if (columnsOk && rowsOk)
        {
            var max = source.Columns!.Value * source.Rows!.Value;
            if (frameCount < 1 || frameCount > max)
            {
                add(nameof(AnimationSource.FrameCount), $"A képkockák száma 1 és {max} (oszlopok × sorok) között lehet.");
            }
        }
    }

    private static IEnumerable<(string Name, int? Value)> GridFields(AnimationSource source)
    {
        yield return (nameof(AnimationSource.FrameWidth), source.FrameWidth);
        yield return (nameof(AnimationSource.FrameHeight), source.FrameHeight);
        yield return (nameof(AnimationSource.Columns), source.Columns);
        yield return (nameof(AnimationSource.Rows), source.Rows);
        yield return (nameof(AnimationSource.FrameCount), source.FrameCount);
    }

    private static void ValidateMovementAnimations(
        IReadOnlyList<MovementAnimation> movementAnimations,
        HashSet<int> sourceIds,
        ErrorCollector errors)
    {
        var states = new HashSet<MovementState>();

        foreach (var movement in movementAnimations)
        {
            var add = errors.For(ConfigurationItemKind.MovementAnimation, ItemKeys.For(movement.MovementState));

            if (!Enum.IsDefined(movement.MovementState))
            {
                add(nameof(MovementAnimation.MovementState), "Ismeretlen mozgásállapot.");
            }
            else if (!states.Add(movement.MovementState))
            {
                add(nameof(MovementAnimation.MovementState), "Ehhez a mozgásállapothoz már van animáció.");
            }

            CheckSourceReference(movement.AnimationSourceId, sourceIds, nameof(MovementAnimation.AnimationSourceId), add);
            CheckRange(movement.FramesPerSecond, ConfigurationLimits.FramesPerSecond, nameof(MovementAnimation.FramesPerSecond), "A sebesség", "képkocka/s", add);
        }
    }

    private static void ValidateRules(IReadOnlyList<BehaviorRule> rules, HashSet<int> sourceIds, ErrorCollector errors)
    {
        var ids = new HashSet<int>();

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];

            // Ismétlődő azonosítónál a kulcs a listabeli index, különben a hiba mindkét elemre mutatna.
            var duplicateId = rule.Id != 0 && !ids.Add(rule.Id);
            var add = errors.For(ConfigurationItemKind.BehaviorRule, duplicateId ? ItemKeys.ForIndex(i) : ItemKeys.For(rule, i));

            if (duplicateId)
            {
                add(nameof(BehaviorRule.Id), "Az azonosító többször szerepel.");
            }

            CheckName(rule.Name, add);
            CheckCondition(rule, add);
            CheckSourceReference(rule.AnimationSourceId, sourceIds, nameof(BehaviorRule.AnimationSourceId), add);
            CheckRange(rule.FramesPerSecond, ConfigurationLimits.FramesPerSecond, nameof(BehaviorRule.FramesPerSecond), "A sebesség", "képkocka/s", add);
            CheckRange(rule.ZIndex, ConfigurationLimits.ZIndex, nameof(BehaviorRule.ZIndex), "A rétegsorrend", null, add);
            CheckRange(rule.Priority, ConfigurationLimits.Priority, nameof(BehaviorRule.Priority), "A prioritás", null, add);
            CheckExclusiveGroup(rule.ExclusiveGroup, add);
        }
    }

    private static void CheckCondition(BehaviorRule rule, Action<string?, string> add)
    {
        var metricOk = Enum.IsDefined(rule.Metric);
        if (!metricOk)
        {
            add(nameof(BehaviorRule.Metric), "Ismeretlen metrika.");
        }

        // Ismeretlen operátornál a felső határ szabálya nem értelmezhető; elég az operátor hibája.
        if (!Enum.IsDefined(rule.Operator))
        {
            add(nameof(BehaviorRule.Operator), "Ismeretlen operátor.");
        }
        else if (rule.Operator != ComparisonOperator.Between)
        {
            if (rule.ThresholdUpper is not null)
            {
                add(nameof(BehaviorRule.ThresholdUpper), "Felső határ csak a „között” operátornál adható meg.");
            }
        }
        else if (rule.ThresholdUpper is null)
        {
            add(nameof(BehaviorRule.ThresholdUpper), "A „között” operátorhoz a felső határ megadása kötelező.");
        }

        if (!metricOk)
        {
            return;
        }

        var range = ConfigurationLimits.MetricRange(rule.Metric);
        var thresholdOk = CheckRange(rule.Threshold, range, nameof(BehaviorRule.Threshold), "A küszöbérték", null, add);

        if (rule.Operator == ComparisonOperator.Between && rule.ThresholdUpper is { } upper)
        {
            var upperOk = CheckRange(upper, range, nameof(BehaviorRule.ThresholdUpper), "A felső határ", null, add);

            // Az időnél megengedett az éjfélen átnyúló intervallum (pl. 20:30–06:00), máshol nem.
            if (thresholdOk && upperOk && rule.Metric != Metric.Time && upper < rule.Threshold)
            {
                add(nameof(BehaviorRule.ThresholdUpper), "A felső határ nem lehet kisebb az alsónál.");
            }
        }
    }

    private static void CheckExclusiveGroup(string? group, Action<string?, string> add)
    {
        // Az üres vagy csak szóközből álló név "nincs csoport"; mentéskor null lesz belőle.
        if (string.IsNullOrWhiteSpace(group))
        {
            return;
        }

        if (group.Trim().Length > ConfigurationLimits.ExclusiveGroupMaxLength)
        {
            add(nameof(BehaviorRule.ExclusiveGroup), $"A csoport neve legfeljebb {ConfigurationLimits.ExclusiveGroupMaxLength} karakter lehet.");
        }
        else if (group.Any(char.IsControl))
        {
            add(nameof(BehaviorRule.ExclusiveGroup), "A csoport neve nem tartalmazhat vezérlőkaraktert.");
        }
    }

    private static void ValidateCharacterSettings(CharacterSettings? settings, ErrorCollector errors)
    {
        var add = errors.For(ConfigurationItemKind.CharacterSettings, ItemKeys.CharacterSettings);

        if (settings is null)
        {
            add(null, "A karakter beállítások hiányoznak.");
            return;
        }

        if (settings.Id != 1)
        {
            add(nameof(CharacterSettings.Id), "A karakter beállítások azonosítója csak 1 lehet.");
        }

        CheckRange(settings.CharacterWidth, ConfigurationLimits.CharacterSizePx, nameof(CharacterSettings.CharacterWidth), "A szélesség", "px", add);
        CheckRange(settings.CharacterHeight, ConfigurationLimits.CharacterSizePx, nameof(CharacterSettings.CharacterHeight), "A magasság", "px", add);
        CheckRange(settings.WalkSpeedPxPerSec, ConfigurationLimits.WalkSpeedPxPerSec, nameof(CharacterSettings.WalkSpeedPxPerSec), "A séta sebessége", "px/s", add);
        CheckRange(settings.GravityPxPerSec2, ConfigurationLimits.GravityPxPerSec2, nameof(CharacterSettings.GravityPxPerSec2), "A gravitáció", "px/s²", add);
    }

    private static void ValidateMetricPollings(IReadOnlyList<MetricPolling> pollings, ErrorCollector errors)
    {
        var metrics = new HashSet<Metric>();

        foreach (var polling in pollings)
        {
            var add = errors.For(ConfigurationItemKind.MetricPolling, ItemKeys.For(polling.Metric));

            if (!Enum.IsDefined(polling.Metric))
            {
                add(nameof(MetricPolling.Metric), "Ismeretlen metrika.");
            }
            else if (!metrics.Add(polling.Metric))
            {
                add(nameof(MetricPolling.Metric), "Ehhez a metrikához már van mintavételezési beállítás.");
            }

            CheckRange(polling.IntervalMs, ConfigurationLimits.PollingIntervalMs, nameof(MetricPolling.IntervalMs), "A lekérdezési gyakoriság", "ms", add);
        }
    }

    /// <returns>Igaz, ha a név formailag rendben van (az egyediséget a hívó nézi).</returns>
    private static bool CheckName(string? name, Action<string?, string> add)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            add("Name", "A név megadása kötelező.");
            return false;
        }

        if (name.Trim().Length > ConfigurationLimits.NameMaxLength)
        {
            add("Name", $"A név legfeljebb {ConfigurationLimits.NameMaxLength} karakter lehet.");
            return false;
        }

        if (name.Any(char.IsControl))
        {
            add("Name", "A név nem tartalmazhat vezérlőkaraktert.");
            return false;
        }

        return true;
    }

    private static void CheckSourceReference(int sourceId, HashSet<int> sourceIds, string propertyName, Action<string?, string> add)
    {
        if (sourceId == 0)
        {
            add(propertyName, "Animációs forrás kiválasztása kötelező.");
        }
        else if (!sourceIds.Contains(sourceId))
        {
            add(propertyName, "A hivatkozott animációs forrás nem létezik.");
        }
    }

    private static bool CheckRequiredRange(int? value, IntRange range, string propertyName, string label, string? unit, Action<string?, string> add)
    {
        if (value is null)
        {
            add(propertyName, $"{label} megadása kötelező.");
            return false;
        }

        return CheckRange(value.Value, range, propertyName, label, unit, add);
    }

    private static bool CheckRange(int value, IntRange range, string propertyName, string label, string? unit, Action<string?, string> add)
    {
        if (range.Contains(value))
        {
            return true;
        }

        add(propertyName, RangeMessage(label, range.Min.ToString(CultureInfo.CurrentCulture), range.Max.ToString(CultureInfo.CurrentCulture), unit));
        return false;
    }

    private static bool CheckRange(double value, DoubleRange range, string propertyName, string label, string? unit, Action<string?, string> add)
    {
        if (range.Contains(value))
        {
            return true;
        }

        add(propertyName, RangeMessage(label, range.Min.ToString(CultureInfo.CurrentCulture), range.Max.ToString(CultureInfo.CurrentCulture), unit));
        return false;
    }

    private static string RangeMessage(string label, string min, string max, string? unit) =>
        unit is null ? $"{label} {min} és {max} között lehet." : $"{label} {min} és {max} {unit} között lehet.";

    /// <summary>A hibák gyűjtése elemenként, hogy a hívó kódja rövid maradjon.</summary>
    private sealed class ErrorCollector
    {
        private readonly List<ValidationError> _errors = [];

        public Action<string?, string> For(ConfigurationItemKind kind, string itemKey) =>
            (propertyName, message) => _errors.Add(new ValidationError(kind, itemKey, propertyName, message));

        public ValidationResult ToResult() => _errors.Count == 0 ? ValidationResult.Success : new ValidationResult(_errors);
    }
}
