namespace ConfigurationEngine.Storage;

using ConfigurationEngine.Model;
using Microsoft.EntityFrameworkCore;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

/// <summary>
/// A konfiguráció SQLite adatbázisa (schema.md, ER-diagram). A modellosztályokat közvetlenül képezi le:
/// a felsorolások szövegként tárolódnak, az animációs forrásra mutató hivatkozások törlése tiltott (RESTRICT).
/// Kívülről nem érhető el, csak az <see cref="SqliteConfigurationStore"/> használja (Wrapper Facade).
/// </summary>
internal sealed class ConfigurationDbContext : DbContext
{
    private const int EnumTextMaxLength = 32;

    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AnimationSource> AnimationSources => Set<AnimationSource>();

    public DbSet<MovementAnimation> MovementAnimations => Set<MovementAnimation>();

    public DbSet<BehaviorRule> BehaviorRules => Set<BehaviorRule>();

    public DbSet<CharacterSettings> CharacterSettings => Set<CharacterSettings>();

    public DbSet<MetricPolling> MetricPollings => Set<MetricPolling>();

    /// <summary>Létrehozza a kontextust egy adatbázisfájlhoz.</summary>
    public static ConfigurationDbContext Create(string databasePath) =>
        new(new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseSqlite(ConnectionString(databasePath))
            .Options);

    /// <summary>
    /// Kapcsolat-készlet nélkül: a fájl a művelet végén felszabadul, így sérült adatbázisnál
    /// félre lehet tenni, és a tesztek is törölhetik.
    /// </summary>
    public static string ConnectionString(string databasePath) => $"Data Source={databasePath};Pooling=False";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnimationSource>(entity =>
        {
            entity.ToTable("AnimationSource");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(ConfigurationLimits.NameMaxLength);
            entity.Property(x => x.SourceType)
                .HasConversion(new TolerantEnumConverter<AnimationSourceType>())
                .HasMaxLength(EnumTextMaxLength);
            entity.Property(x => x.Path).IsRequired();
            entity.HasData(ConfigurationDefaults.CreateMovementSources());
        });

        modelBuilder.Entity<MovementAnimation>(entity =>
        {
            entity.ToTable("MovementAnimation");
            entity.HasKey(x => x.MovementState);
            entity.Property(x => x.MovementState)
                .HasConversion(new TolerantEnumConverter<MovementState>())
                .HasMaxLength(EnumTextMaxLength)
                .ValueGeneratedNever();
            entity.HasOne<AnimationSource>()
                .WithMany()
                .HasForeignKey(x => x.AnimationSourceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasData(ConfigurationDefaults.CreateMovementAnimations());
        });

        modelBuilder.Entity<BehaviorRule>(entity =>
        {
            entity.ToTable("BehaviorRule");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(ConfigurationLimits.NameMaxLength);
            entity.Property(x => x.Metric)
                .HasConversion(new TolerantEnumConverter<Metric>())
                .HasMaxLength(EnumTextMaxLength);
            entity.Property(x => x.Operator)
                .HasConversion(new TolerantEnumConverter<ComparisonOperator>())
                .HasMaxLength(EnumTextMaxLength);
            entity.Property(x => x.ExclusiveGroup).HasMaxLength(ConfigurationLimits.ExclusiveGroupMaxLength);
            entity.HasOne<AnimationSource>()
                .WithMany()
                .HasForeignKey(x => x.AnimationSourceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CharacterSettings>(entity =>
        {
            entity.ToTable("CharacterSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.HasData(ConfigurationDefaults.CreateCharacterSettings());
        });

        modelBuilder.Entity<MetricPolling>(entity =>
        {
            entity.ToTable("MetricPolling");
            entity.HasKey(x => x.Metric);
            entity.Property(x => x.Metric)
                .HasConversion(new TolerantEnumConverter<Metric>())
                .HasMaxLength(EnumTextMaxLength)
                .ValueGeneratedNever();
            entity.HasData(ConfigurationDefaults.CreateMetricPollings());
        });
    }
}
