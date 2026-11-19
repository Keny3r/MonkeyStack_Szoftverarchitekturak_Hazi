namespace ConfigurationEngine.Storage;

using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Csak a <c>dotnet ef</c> eszköznek kell a migrációk generálásához; futás közben nem használjuk.
/// Új migráció: <c>dotnet ef migrations add &lt;Név&gt; --project src/ConfigurationEngine --output-dir Storage/Migrations</c>.
/// </summary>
internal sealed class ConfigurationDbContextFactory : IDesignTimeDbContextFactory<ConfigurationDbContext>
{
    public ConfigurationDbContext CreateDbContext(string[] args) => ConfigurationDbContext.Create("design-time.db");
}
