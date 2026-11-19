namespace ConfigurationEngine.Storage;

using System.IO;
using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MonkeyStack.Core.Events;

/// <summary>
/// A konfiguráció tárolása helyi SQLite adatbázisban, EF Core-ral (Wrapper Facade: a hívó nem látja az EF Core-t
/// és az SQLite-ot). Az EF Core minden lekérdezést paraméterezve küld, nyers SQL-t nem írunk (SQL injection ellen).
/// A műveleteket egy zár sorosítja, így bármelyik szálról hívható (Thread-Safe Interface).
/// </summary>
/// <remarks>
/// Betöltés: migráció, beolvasás, validálás, a hibás sorok kihagyása (a rájuk hivatkozókkal együtt), hiányzó
/// vagy hibás beállítások helyett alapértékek. Sérült adatbázisfájlnál a fájl <c>.bak</c> kiterjesztéssel félrekerül,
/// és alapértékekkel új jön létre (schema.md, "Validálás"). Mentés: validálás, majd egy tranzakcióban a teljes
/// tárolt állapot lecserélése.
/// </remarks>
public sealed class SqliteConfigurationStore : IConfigurationStore
{
    /// <summary>SQLite hibakódok, amelyeknél a fájl nem használható adatbázisként.</summary>
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADatabase = 26;

    private readonly IConfigurationValidator _validator;
    private readonly IMediaValidator? _mediaValidator;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="databasePath">Az adatbázisfájl; ha <c>null</c>, <see cref="DefaultDatabasePath"/>.</param>
    /// <param name="validator">Ha <c>null</c>, <see cref="ConfigurationValidator"/>.</param>
    /// <param name="mediaValidator">
    /// Betöltéskor a gyors médiaellenőrzéshez (<see cref="MediaValidationDepth.Quick"/>); ha <c>null</c>, ez a lépés kimarad.
    /// </param>
    public SqliteConfigurationStore(
        string? databasePath = null,
        IConfigurationValidator? validator = null,
        IMediaValidator? mediaValidator = null)
    {
        DatabasePath = Path.GetFullPath(databasePath ?? DefaultDatabasePath);
        _validator = validator ?? new ConfigurationValidator();
        _mediaValidator = mediaValidator;
    }

    /// <summary><c>%LOCALAPPDATA%\MonkeyStack\config.db</c> (nem kell hozzá rendszergazdai jog).</summary>
    public static string DefaultDatabasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonkeyStack", "config.db");

    public string DatabasePath { get; }

    public event EventHandler? Changed;

    public async Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        ConfigurationData stored;
        bool wasReset;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            wasReset = !File.Exists(DatabasePath);
            try
            {
                stored = await ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsCorruptDatabase(ex))
            {
                ResetDatabase();
                wasReset = true;
                stored = await ReadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }

        return await RemoveInvalidItemsAsync(stored, wasReset, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(ConfigurationData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        ModelCopies.Normalize(data);
        var validation = _validator.Validate(data);
        if (!validation.IsValid)
        {
            throw new ConfigurationValidationException(validation);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private ConfigurationDbContext OpenContext()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        return ConfigurationDbContext.Create(DatabasePath);
    }

    private async Task<ConfigurationData> ReadAsync(CancellationToken cancellationToken)
    {
        await using var context = OpenContext();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        // AsNoTracking: az ismeretlen felsorolás-értékű kulcsok (-1) ütközhetnének a követésben.
        return new ConfigurationData
        {
            AnimationSources = await context.AnimationSources.AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false),
            MovementAnimations = await context.MovementAnimations.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false),
            BehaviorRules = await context.BehaviorRules.AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false),
            Character = await context.CharacterSettings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, cancellationToken).ConfigureAwait(false)
                ?? ConfigurationDefaults.CreateCharacterSettings(),
            MetricPollings = await context.MetricPollings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// A tárolt állapot lecserélése egy tranzakcióban. A szabályokra, a mozgási animációkra és a beállításokra semmi
    /// nem hivatkozik, ezért ezek törlés után újra bekerülnek (a meglévő azonosítók megmaradnak). A forrásoknál a
    /// törölteket töröljük, a meglévőket frissítjük, az újakat beszúrjuk, majd a negatív ideiglenes azonosítókra mutató
    /// hivatkozásokat a kapott azonosítókra cseréljük. A meglévő azonosítójú sorok mindig az újak előtt kerülnek be,
    /// különben egy generált azonosító ütközhetne egy még be nem szúrt meglévővel.
    /// </summary>
    private async Task WriteAsync(ConfigurationData data, CancellationToken cancellationToken)
    {
        await using var context = OpenContext();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await context.BehaviorRules.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MovementAnimations.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.CharacterSettings.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MetricPollings.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        var keptSourceIds = data.AnimationSources.Where(s => s.Id > 0).Select(s => s.Id).ToList();
        await context.AnimationSources.Where(s => !keptSourceIds.Contains(s.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var existingSourceIds = (await context.AnimationSources.Select(s => s.Id).ToListAsync(cancellationToken).ConfigureAwait(false)).ToHashSet();

        var newSources = new List<(AnimationSource Original, AnimationSource Entity)>();
        foreach (var source in data.AnimationSources)
        {
            var entity = source.Copy();
            if (source.Id <= 0)
            {
                entity.Id = 0;
                newSources.Add((source, entity));
            }
            else if (existingSourceIds.Contains(source.Id))
            {
                context.AnimationSources.Update(entity);
            }
            else
            {
                context.AnimationSources.Add(entity);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.AnimationSources.AddRange(newSources.Select(x => x.Entity));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var sourceIdMap = newSources
            .Where(x => x.Original.Id < 0)
            .ToDictionary(x => x.Original.Id, x => x.Entity.Id);
        int MapSourceId(int id) => id < 0 ? sourceIdMap[id] : id;

        var newRules = new List<(BehaviorRule Original, BehaviorRule Entity)>();
        foreach (var rule in data.BehaviorRules)
        {
            var entity = rule.Copy();
            entity.AnimationSourceId = MapSourceId(rule.AnimationSourceId);
            if (rule.Id <= 0)
            {
                entity.Id = 0;
                newRules.Add((rule, entity));
            }
            else
            {
                context.BehaviorRules.Add(entity);
            }
        }

        foreach (var movement in data.MovementAnimations)
        {
            var entity = movement.Copy();
            entity.AnimationSourceId = MapSourceId(movement.AnimationSourceId);
            context.MovementAnimations.Add(entity);
        }

        context.CharacterSettings.Add(data.Character.Copy());
        context.MetricPollings.AddRange(data.MetricPollings.Select(p => p.Copy()));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.BehaviorRules.AddRange(newRules.Select(x => x.Entity));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        // Csak sikeres commit után frissítjük a hívó objektumait.
        foreach (var (original, entity) in newSources)
        {
            original.Id = entity.Id;
        }

        foreach (var (original, entity) in newRules)
        {
            original.Id = entity.Id;
        }

        foreach (var rule in data.BehaviorRules)
        {
            rule.AnimationSourceId = MapSourceId(rule.AnimationSourceId);
        }

        foreach (var movement in data.MovementAnimations)
        {
            movement.AnimationSourceId = MapSourceId(movement.AnimationSourceId);
        }
    }

    /// <summary>
    /// A betöltött adatból kihagyja az érvénytelen elemeket. Egy érvénytelen forrás a rá hivatkozó szabályokat és
    /// mozgási animációkat is viszi; a hibás karakter- és mintavételezési beállítások helyett alapértékek kerülnek be.
    /// </summary>
    private async Task<ConfigurationLoadResult> RemoveInvalidItemsAsync(
        ConfigurationData stored,
        bool wasReset,
        CancellationToken cancellationToken)
    {
        var errorsByItem = _validator.Validate(stored).Errors.ToLookup(e => (e.Kind, e.ItemKey));
        var dropped = new List<DroppedItem>();
        var result = new ConfigurationData();

        var droppedSourceNames = new Dictionary<int, string>();
        for (var i = 0; i < stored.AnimationSources.Count; i++)
        {
            var source = stored.AnimationSources[i];
            var key = ItemKeys.For(source, i);
            var reasons = errorsByItem[(ConfigurationItemKind.AnimationSource, key)].ToList();

            if (reasons.Count == 0 && _mediaValidator is not null)
            {
                reasons.AddRange(await _mediaValidator
                    .ValidateAsync(source, MediaValidationDepth.Quick, cancellationToken)
                    .ConfigureAwait(false));
            }

            if (reasons.Count > 0)
            {
                dropped.Add(new DroppedItem(ConfigurationItemKind.AnimationSource, key, source.Name, reasons));
                droppedSourceNames[source.Id] = source.Name;
            }
            else
            {
                result.AnimationSources.Add(source);
            }
        }

        List<ValidationError> ReasonsWithSource(ConfigurationItemKind kind, string key, int sourceId, string propertyName)
        {
            var reasons = errorsByItem[(kind, key)].ToList();
            if (droppedSourceNames.TryGetValue(sourceId, out var sourceName))
            {
                reasons.Add(new ValidationError(kind, key, propertyName, $"A hivatkozott animációs forrás („{sourceName}”) érvénytelen."));
            }

            return reasons;
        }

        foreach (var movement in stored.MovementAnimations)
        {
            var key = ItemKeys.For(movement.MovementState);
            var reasons = ReasonsWithSource(ConfigurationItemKind.MovementAnimation, key, movement.AnimationSourceId, nameof(MovementAnimation.AnimationSourceId));
            if (reasons.Count > 0)
            {
                dropped.Add(new DroppedItem(ConfigurationItemKind.MovementAnimation, key, key, reasons));
            }
            else
            {
                result.MovementAnimations.Add(movement);
            }
        }

        for (var i = 0; i < stored.BehaviorRules.Count; i++)
        {
            var rule = stored.BehaviorRules[i];
            var key = ItemKeys.For(rule, i);
            var reasons = ReasonsWithSource(ConfigurationItemKind.BehaviorRule, key, rule.AnimationSourceId, nameof(BehaviorRule.AnimationSourceId));
            if (reasons.Count > 0)
            {
                dropped.Add(new DroppedItem(ConfigurationItemKind.BehaviorRule, key, rule.Name, reasons));
            }
            else
            {
                result.BehaviorRules.Add(rule);
            }
        }

        var characterErrors = errorsByItem[(ConfigurationItemKind.CharacterSettings, ItemKeys.CharacterSettings)].ToList();
        if (characterErrors.Count > 0)
        {
            characterErrors.Add(DefaultsApplied(ConfigurationItemKind.CharacterSettings, ItemKeys.CharacterSettings));
            dropped.Add(new DroppedItem(ConfigurationItemKind.CharacterSettings, ItemKeys.CharacterSettings, "Karakter beállítások", characterErrors));
            result.Character = ConfigurationDefaults.CreateCharacterSettings();
        }
        else
        {
            result.Character = stored.Character;
        }

        foreach (var polling in stored.MetricPollings)
        {
            var key = ItemKeys.For(polling.Metric);
            var reasons = errorsByItem[(ConfigurationItemKind.MetricPolling, key)].ToList();
            if (reasons.Count == 0)
            {
                result.MetricPollings.Add(polling);
                continue;
            }

            if (Enum.IsDefined(polling.Metric))
            {
                reasons.Add(DefaultsApplied(ConfigurationItemKind.MetricPolling, key));
            }

            dropped.Add(new DroppedItem(ConfigurationItemKind.MetricPolling, key, key, reasons));
        }

        // A hiányzó (vagy hibás) metrikák alapértéket kapnak, így mindig mind a hét szerepel.
        foreach (var metric in Enum.GetValues<Metric>())
        {
            if (!result.MetricPollings.Any(p => p.Metric == metric))
            {
                result.MetricPollings.Add(ConfigurationDefaults.CreateMetricPolling(metric));
            }
        }

        result.MetricPollings.Sort((a, b) => a.Metric.CompareTo(b.Metric));
        return new ConfigurationLoadResult(result, dropped, wasReset);
    }

    private static ValidationError DefaultsApplied(ConfigurationItemKind kind, string key) =>
        new(kind, key, null, "Helyette az alapértékek érvényesek.");

    private static bool IsCorruptDatabase(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is SqliteException { SqliteErrorCode: SqliteCorrupt or SqliteNotADatabase })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A sérült fájlt (és a kísérő naplófájlokat) félreteszi; a következő megnyitás új adatbázist hoz létre.</summary>
    private void ResetDatabase()
    {
        SqliteConnection.ClearAllPools();
        File.Move(DatabasePath, DatabasePath + ".bak", overwrite: true);

        foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
        {
            var sidecar = DatabasePath + suffix;
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }
    }
}
