namespace ConfigurationEngine.Tests.View;

using ConfigurationEngine.View;

/// <summary>
/// Közös segédek a konfigurációs ablak tesztjeihez.
/// </summary>
internal static class ViewTestHelpers
{
    /// <summary>Az ablak fülei által kért összes előnézet (a motorban ennyi előnézeti animáció van).</summary>
    public static IReadOnlyList<PreviewViewModel> TabPreviews(ConfigurationWindowViewModel viewModel) =>
    [
        .. viewModel.CharacterTab.MovementRows.Select(r => r.Preview),
        .. viewModel.RulesTab.Rules.Where(r => viewModel.FindSource(r.Rule.AnimationSourceId) is not null).Select(r => r.Preview),
        .. viewModel.SourcesTab.Sources.Select(r => r.Preview)
    ];

    /// <summary>Megvárja, amíg minden megadott előnézet betöltődik.</summary>
    public static async Task WaitForPreviewsAsync(Func<IEnumerable<PreviewViewModel>> previews)
    {
        for (var i = 0; i < 200 && !AllLoaded(); i++)
        {
            await Task.Delay(25);
        }

        Assert.True(AllLoaded());

        bool AllLoaded() => previews().All(p => p is { IsLoading: false, Animation: not null });
    }
}
