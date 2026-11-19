namespace ConfigurationEngine.Tests;

using System.IO;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using ConfigurationEngine.Validation;
using Microsoft.Data.Sqlite;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

public sealed class SqliteConfigurationStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MonkeyStackTests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public SqliteConfigurationStoreTests()
    {
        _databasePath = Path.Combine(_directory, "config.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact]
    public async Task FirstLoad_CreatesDatabaseWithDefaults()
    {
        var store = new SqliteConfigurationStore(_databasePath);

        var first = await store.LoadAsync();
        var second = await store.LoadAsync();

        Assert.True(first.DatabaseWasReset);
        Assert.False(second.DatabaseWasReset);
        Assert.True(File.Exists(_databasePath));
        Assert.Empty(first.DroppedItems);
        Assert.Equal(100, first.Data.Character.CharacterWidth);
        Assert.Equal(Enum.GetValues<Metric>(), first.Data.MetricPollings.Select(p => p.Metric));
        Assert.Equal(1000, first.Data.MetricPollings.Single(p => p.Metric == Metric.Cpu).IntervalMs);
    }

    [Fact]
    public async Task SaveThenLoad_ReturnsSameConfiguration()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        var saved = TestData.ValidConfiguration();

        await store.SaveAsync(saved);
        var loaded = (await store.LoadAsync()).Data;

        Assert.Equal(Describe(saved), Describe(loaded));
    }

    [Fact]
    public async Task Save_AssignsIdsToNewItemsAndRemapsTemporaryReferences()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        var data = TestData.ValidConfiguration();
        var newSource = new AnimationSource { Id = -1, Name = "Alvás", SourceType = AnimationSourceType.Gif, Path = "sleep.gif" };
        var newRule = TestData.Rule(0, "Alszik", Metric.Inactivity, ComparisonOperator.Gte, 10, sourceId: -1);
        data.AnimationSources.Add(newSource);
        data.BehaviorRules.Add(newRule);
        data.MovementAnimations.Add(new MovementAnimation { MovementState = MovementState.Idle, AnimationSourceId = -1, FramesPerSecond = 6 });

        await store.SaveAsync(data);

        Assert.True(newSource.Id > 0);
        Assert.True(newRule.Id > 0);
        Assert.Equal(newSource.Id, newRule.AnimationSourceId);
        Assert.Equal(newSource.Id, data.MovementAnimations.Single(m => m.MovementState == MovementState.Idle).AnimationSourceId);

        var loaded = (await store.LoadAsync()).Data;
        Assert.Equal(newSource.Id, loaded.BehaviorRules.Single(r => r.Name == "Alszik").AnimationSourceId);
    }

    [Fact]
    public async Task Save_TrimsTextAndRaisesChanged()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        var changed = 0;
        store.Changed += (_, _) => changed++;
        var data = TestData.ValidConfiguration();
        data.BehaviorRules[0].Name = "  Kis tűz  ";
        data.BehaviorRules[2].ExclusiveGroup = "   ";

        await store.SaveAsync(data);
        var loaded = (await store.LoadAsync()).Data;

        Assert.Equal(1, changed);
        Assert.Equal("Kis tűz", loaded.BehaviorRules[0].Name);
        Assert.Null(loaded.BehaviorRules[2].ExclusiveGroup);

        // Sikeres mentés után a hívó objektumai is a tárolt (trimmelt) értéket mutatják.
        Assert.Equal("Kis tűz", data.BehaviorRules[0].Name);
        Assert.Null(data.BehaviorRules[2].ExclusiveGroup);
    }

    [Fact]
    public async Task Save_RejectedConfigurationLeavesCallerObjectsUnchanged()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        var data = TestData.ValidConfiguration();
        data.AnimationSources.Add(new AnimationSource { Id = -1, Name = " Új ", SourceType = AnimationSourceType.Gif, Path = "new.gif" });
        data.BehaviorRules[0].Name = "  Kis tűz  ";
        data.BehaviorRules[0].AnimationSourceId = -1;
        data.BehaviorRules[2].ExclusiveGroup = "   ";
        data.BehaviorRules[1].FramesPerSecond = 0;

        await Assert.ThrowsAsync<ConfigurationValidationException>(() => store.SaveAsync(data));

        Assert.Equal(-1, data.AnimationSources[3].Id);
        Assert.Equal(" Új ", data.AnimationSources[3].Name);
        Assert.Equal("  Kis tűz  ", data.BehaviorRules[0].Name);
        Assert.Equal(-1, data.BehaviorRules[0].AnimationSourceId);
        Assert.Equal("   ", data.BehaviorRules[2].ExclusiveGroup);
    }

    [Fact]
    public async Task Save_RecreatesDatabaseDeletedSinceLastOperation()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        await store.SaveAsync(TestData.ValidConfiguration());

        SqliteConnection.ClearAllPools();
        foreach (var file in Directory.GetFiles(_directory))
        {
            File.Delete(file);
        }

        await store.SaveAsync(TestData.ValidConfiguration());
        var result = await store.LoadAsync();

        Assert.Equal(3, result.Data.AnimationSources.Count);
        Assert.Empty(result.DroppedItems);
    }

    [Fact]
    public async Task Save_RemovesDeletedItems()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        var data = TestData.ValidConfiguration();
        await store.SaveAsync(data);

        data.BehaviorRules.RemoveAll(r => r.Name == "Pizsama");
        data.AnimationSources.RemoveAll(s => s.Name == "Pizsama");
        await store.SaveAsync(data);
        var loaded = (await store.LoadAsync()).Data;

        Assert.DoesNotContain(loaded.AnimationSources, s => s.Name == "Pizsama");
        Assert.DoesNotContain(loaded.BehaviorRules, r => r.Name == "Pizsama");
        Assert.Equal(2, loaded.AnimationSources.Count);
    }

    [Fact]
    public async Task Save_RejectsInvalidConfigurationAndKeepsStoredState()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        var changed = 0;
        store.Changed += (_, _) => changed++;
        var data = TestData.ValidConfiguration();
        await store.SaveAsync(data);

        // A "Tűz" forrást két szabály is használja, ezért nem törölhető.
        data.AnimationSources.RemoveAll(s => s.Name == "Tűz");
        var exception = await Assert.ThrowsAsync<ConfigurationValidationException>(() => store.SaveAsync(data));
        var loaded = (await store.LoadAsync()).Data;

        Assert.Contains(exception.Result.Errors, e => e.PropertyName == nameof(BehaviorRule.AnimationSourceId));
        Assert.Equal(3, loaded.AnimationSources.Count);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Load_DropsInvalidRowWithReason()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        await store.SaveAsync(TestData.ValidConfiguration());
        Execute("UPDATE BehaviorRule SET Threshold = 150 WHERE Id = 1");

        var result = await store.LoadAsync();

        var dropped = Assert.Single(result.DroppedItems);
        Assert.Equal(ConfigurationItemKind.BehaviorRule, dropped.Kind);
        Assert.Equal("Kis tűz", dropped.DisplayName);
        Assert.Contains(dropped.Reasons, r => r.PropertyName == nameof(BehaviorRule.Threshold));
        Assert.Equal(["Nagy tűz", "Pizsama"], result.Data.BehaviorRules.Select(r => r.Name));
    }

    [Fact]
    public async Task Load_InvalidSourceAlsoDropsItsDependents()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        await store.SaveAsync(TestData.ValidConfiguration());
        Execute("UPDATE AnimationSource SET FrameCount = 99 WHERE Name = 'Tűz'");

        var result = await store.LoadAsync();

        Assert.Equal(3, result.DroppedItems.Count);
        Assert.Contains(result.DroppedItems, d => d.Kind == ConfigurationItemKind.AnimationSource && d.DisplayName == "Tűz");
        Assert.All(
            result.DroppedItems.Where(d => d.Kind == ConfigurationItemKind.BehaviorRule),
            d => Assert.Contains(d.Reasons, r => r.PropertyName == nameof(BehaviorRule.AnimationSourceId)));
        Assert.Equal(["Pizsama"], result.Data.BehaviorRules.Select(r => r.Name));
    }

    [Fact]
    public async Task Load_DropsRowWithUnknownEnumText()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        await store.SaveAsync(TestData.ValidConfiguration());
        Execute("UPDATE BehaviorRule SET Metric = 'Gpu' WHERE Name = 'Pizsama'");

        var result = await store.LoadAsync();

        var dropped = Assert.Single(result.DroppedItems);
        Assert.Equal("Pizsama", dropped.DisplayName);
        Assert.Contains(dropped.Reasons, r => r.PropertyName == nameof(BehaviorRule.Metric));
    }

    [Fact]
    public async Task Load_UsesDefaultsForInvalidOrMissingPolling()
    {
        var store = new SqliteConfigurationStore(_databasePath);
        await store.LoadAsync();
        Execute("UPDATE MetricPolling SET IntervalMs = 5 WHERE Metric = 'Cpu'");
        Execute("DELETE FROM MetricPolling WHERE Metric = 'Disk'");

        var result = await store.LoadAsync();

        var dropped = Assert.Single(result.DroppedItems);
        Assert.Equal(ConfigurationItemKind.MetricPolling, dropped.Kind);
        Assert.Equal(nameof(Metric.Cpu), dropped.ItemKey);
        Assert.Equal(1000, result.Data.MetricPollings.Single(p => p.Metric == Metric.Cpu).IntervalMs);
        Assert.Equal(30000, result.Data.MetricPollings.Single(p => p.Metric == Metric.Disk).IntervalMs);
        Assert.Equal(7, result.Data.MetricPollings.Count);
    }

    [Fact]
    public async Task Load_ResetsCorruptDatabaseAndKeepsBackup()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_databasePath, "ez nem egy SQLite adatbázis, csak szemét");
        var store = new SqliteConfigurationStore(_databasePath);

        var result = await store.LoadAsync();

        Assert.True(result.DatabaseWasReset);
        Assert.True(File.Exists(_databasePath + ".bak"));
        Assert.Equal(7, result.Data.MetricPollings.Count);
        Assert.False((await store.LoadAsync()).DatabaseWasReset);
    }

    [Fact]
    public async Task Load_ResetKeepsEarlierBackup()
    {
        Directory.CreateDirectory(_directory);
        const string Garbage = "ez nem egy SQLite adatbázis, csak szemét";
        await File.WriteAllTextAsync(_databasePath, Garbage);
        await File.WriteAllTextAsync(_databasePath + ".bak", "korábbi mentés");
        var store = new SqliteConfigurationStore(_databasePath);

        var result = await store.LoadAsync();

        Assert.True(result.DatabaseWasReset);
        Assert.Equal(Garbage, await File.ReadAllTextAsync(_databasePath + ".bak"));
        // Szűrés kézzel: a Windows a "config.db.bak.*" mintára magát a config.db.bak-ot is visszaadná.
        var earlier = Assert.Single(Directory.GetFiles(_directory), f => Path.GetFileName(f).StartsWith("config.db.bak.", StringComparison.Ordinal));
        Assert.Equal("korábbi mentés", await File.ReadAllTextAsync(earlier));
    }

    // A naplók áthelyezését közvetlenül teszteljük: egy kitalált (érvénytelen) -wal fájlt az SQLite már megnyitáskor eldob.
    [Fact]
    public async Task MoveWithSidecars_MovesJournalsAndDropsStaleOnes()
    {
        Directory.CreateDirectory(_directory);
        var backupPath = _databasePath + ".bak";
        await File.WriteAllTextAsync(_databasePath, "adatbázis");
        await File.WriteAllTextAsync(_databasePath + "-wal", "napló");
        await File.WriteAllTextAsync(_databasePath + "-shm", "index");
        await File.WriteAllTextAsync(backupPath + "-journal", "régi, máshoz tartozó napló");

        SqliteConfigurationStore.MoveWithSidecars(_databasePath, backupPath);

        Assert.Equal("adatbázis", await File.ReadAllTextAsync(backupPath));
        Assert.Equal("napló", await File.ReadAllTextAsync(backupPath + "-wal"));
        Assert.False(File.Exists(backupPath + "-journal"));
        Assert.False(File.Exists(_databasePath));
        Assert.False(File.Exists(_databasePath + "-wal"));
        Assert.False(File.Exists(_databasePath + "-shm"));
    }

    [Fact]
    public async Task Load_EmptyDatabaseFileIsReportedAsReset()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(_databasePath, []);
        var store = new SqliteConfigurationStore(_databasePath);

        var result = await store.LoadAsync();

        Assert.True(result.DatabaseWasReset);
        Assert.Equal(7, result.Data.MetricPollings.Count);
    }

    [Fact]
    public async Task Load_DropsSourcesFailingQuickMediaCheck()
    {
        await new SqliteConfigurationStore(_databasePath).SaveAsync(TestData.ValidConfiguration());
        var store = new SqliteConfigurationStore(_databasePath, mediaValidator: new RejectingMediaValidator("Pizsama"));

        var result = await store.LoadAsync();

        Assert.Equal(2, result.DroppedItems.Count);
        Assert.DoesNotContain(result.Data.AnimationSources, s => s.Name == "Pizsama");
        Assert.DoesNotContain(result.Data.BehaviorRules, r => r.Name == "Pizsama");
    }

    private void Execute(string sql)
    {
        using var connection = new SqliteConnection(ConfigurationDbContext.ConnectionString(_databasePath));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Összehasonlítható, szöveges leírás a modellről (a mezőnkénti egyezés ellenőrzéséhez).</summary>
    private static string Describe(ConfigurationData data) => string.Join(
        Environment.NewLine,
        data.AnimationSources.Select(s => $"S {s.Id} {s.Name} {s.SourceType} {s.Path} {s.FrameWidth} {s.FrameHeight} {s.Columns} {s.Rows} {s.FrameCount}")
            .Concat(data.MovementAnimations.Select(m => $"M {m.MovementState} {m.AnimationSourceId} {m.FramesPerSecond}"))
            .Concat(data.BehaviorRules.Select(r => $"R {r.Id} {r.Name} {r.IsEnabled} {r.Metric} {r.Operator} {r.Threshold} {r.ThresholdUpper} {r.AnimationSourceId} {r.FramesPerSecond} {r.ZIndex} {r.ExclusiveGroup} {r.Priority}"))
            .Append($"C {data.Character.CharacterWidth} {data.Character.CharacterHeight} {data.Character.WalkSpeedPxPerSec} {data.Character.GravityPxPerSec2} {data.Character.FollowCursor}")
            .Concat(data.MetricPollings.Select(p => $"P {p.Metric} {p.IsEnabled} {p.IntervalMs}")));

    private sealed class RejectingMediaValidator(string rejectedName) : IMediaValidator
    {
        public Task<IReadOnlyList<ValidationError>> ValidateAsync(
            AnimationSource source,
            MediaValidationDepth depth,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ValidationError> errors = source.Name == rejectedName
                ? [new ValidationError(ConfigurationItemKind.AnimationSource, source.Id.ToString(), nameof(AnimationSource.Path), "A fájl nem található.")]
                : [];
            return Task.FromResult(errors);
        }
    }
}
