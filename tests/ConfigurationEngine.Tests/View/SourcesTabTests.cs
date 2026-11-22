namespace ConfigurationEngine.Tests.View;

using System.IO;
using ConfigurationEngine.Model;
using ConfigurationEngine.Tests.Media;
using ConfigurationEngine.View;
using ConfigurationEngine.View.Fakes;
using ConfigurationEngine.View.Tabs;
using MonkeyStack.Core.Character;

/// <summary>
/// A „Források” fül (gui.md, "4. Animációs források") a minta konfigurációval: a négy szállított forrást a
/// mozgásállapotok és a szabályok is használják.
/// </summary>
public sealed class SourcesTabTests : IDisposable
{
    private readonly FakeConfigurationEditor _editor = new();
    private readonly TestBrowser _browser = new();
    private readonly ConfigurationWindowViewModel _viewModel;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MonkeyStackSourcesTab_" + Guid.NewGuid().ToString("N"));

    public SourcesTabTests()
    {
        _viewModel = new ConfigurationWindowViewModel(_editor, new AcceptingDialogs(), _browser);
        Directory.CreateDirectory(_directory);
    }

    private SourcesTabViewModel Tab => _viewModel.SourcesTab;

    public void Dispose()
    {
        _editor.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task List_ShowsTypePathAndUsers()
    {
        await _viewModel.LoadAsync();

        var idle = Tab.Sources[0];
        Assert.Equal(4, Tab.Sources.Count);
        Assert.Equal("Képmappa", idle.TypeText);
        Assert.Equal("movement/idle", idle.PathText);
        Assert.Equal("Idle, Pizsama", idle.UsedByText);
        Assert.Same(idle, Tab.SelectedSource);
    }

    [Fact]
    public async Task UsedSource_CannotBeDeletedAndTheToolTipSaysWhy()
    {
        await _viewModel.LoadAsync();

        Assert.False(Tab.DeleteCommand.CanExecute(null));
        Assert.Equal("Nem törölhető, használja: Idle, Pizsama", Tab.DeleteToolTip);
    }

    [Fact]
    public async Task BrowsedFolder_BecomesANewSelectedSourceWithTemporaryIdAndIsChecked()
    {
        await _viewModel.LoadAsync();
        _browser.Folder = Path.Combine(AppContext.BaseDirectory, "assets", "movement", "walking");

        Tab.BrowseFolderCommand.Execute(null);
        var row = Tab.SelectedSource!;
        await WaitUntilAsync(() => !row.IsChecking);

        Assert.Equal(-1, row.Source.Id);
        Assert.Equal("walking", row.Source.Name);
        Assert.Equal(AnimationSourceType.ImageFolder, row.Source.SourceType);
        Assert.Equal("–", row.UsedByText);
        Assert.False(row.HasAnyError);
        Assert.True(Tab.DeleteCommand.CanExecute(null));
        Assert.Contains(_viewModel.SourceOptions, o => o.Id == -1 && o.Name == "walking");
        Assert.True(_viewModel.IsDirty);
    }

    [Fact]
    public async Task NameCollision_GetsANumberRegardlessOfCase()
    {
        await _viewModel.LoadAsync();
        _browser.Folder = Path.Combine(AppContext.BaseDirectory, "assets", "movement", "walking");
        Tab.BrowseFolderCommand.Execute(null);
        _browser.Folder = Path.Combine(AppContext.BaseDirectory, "assets", "movement", "WALKING");

        Tab.BrowseFolderCommand.Execute(null);

        Assert.Equal("WALKING (2)", Tab.SelectedSource!.Source.Name);
        Assert.Equal(-2, Tab.SelectedSource.Source.Id);
    }

    [Fact]
    public async Task BrowsedFile_TypeFollowsTheExtension()
    {
        await _viewModel.LoadAsync();
        _browser.File = Path.Combine(_directory, "tuz.gif");

        Tab.BrowseFileCommand.Execute(null);
        Assert.Equal(AnimationSourceType.Gif, Tab.SelectedSource!.Source.SourceType);
        Assert.Equal("tuz", Tab.SelectedSource.Source.Name);

        _browser.File = Path.Combine(_directory, "seta.png");
        Tab.BrowseFileCommand.Execute(null);
        Assert.Equal(AnimationSourceType.SpriteSheet, Tab.SelectedSource!.Source.SourceType);
        Assert.True(Tab.SelectedSource.IsSpriteSheet);
    }

    [Fact]
    public async Task UnloadableFile_ShowsTheMediaErrorBeforeSaving()
    {
        await _viewModel.LoadAsync();
        _browser.File = Path.Combine(_directory, "nincs.gif");

        Tab.BrowseFileCommand.Execute(null);
        var row = Tab.SelectedSource!;
        await WaitUntilAsync(() => !row.IsChecking);

        Assert.True(row.HasAnyError);
        Assert.StartsWith("Források (", _viewModel.SourcesTabHeader);
        Assert.False(_viewModel.CanSave);
    }

    [Fact]
    public async Task SpriteSheet_GridDefaultsFrameCountAndShowsComputedSize()
    {
        await _viewModel.LoadAsync();
        var sheet = Path.Combine(_directory, "lap.png");
        TestImages.WritePng(sheet, 64, 32, TestImages.Red);
        _browser.File = sheet;
        Tab.BrowseFileCommand.Execute(null);
        var row = Tab.SelectedSource!;

        row.FrameWidth.Text = "32";
        row.FrameHeight.Text = "32";
        row.Columns.Text = "2";
        row.Rows.Text = "1";
        await WaitUntilAsync(() => !row.IsChecking);

        Assert.Equal(2, row.Source.FrameCount);
        Assert.Equal("2", row.FrameCount.Text);
        Assert.Equal("A rácsból számolt képméret: 64 × 32 px", row.GridSizeText);
        Assert.False(row.HasAnyError);

        // A rács módosítása után a médiaellenőrzés nem fut újra (csak tallózás / csere után és mentéskor):
        // a rácseltérést addig az előnézet jelzi, mentéskor pedig a mezőknél jelenik meg.
        row.Columns.Text = "3";
        await WaitUntilAsync(() => !row.Preview.IsLoading);

        Assert.Equal(3, row.Source.FrameCount);
        Assert.False(row.IsChecking);
        Assert.False(row.HasAnyError);
        Assert.True(row.Preview.HasError);

        Assert.False(await _viewModel.SaveAsync());
        Assert.True(row.HasAnyError);
        Assert.Equal(0, _editor.SaveCount);
    }

    [Fact]
    public async Task ChangedFrameCount_IsNotOverwrittenByTheGrid()
    {
        await _viewModel.LoadAsync();
        _browser.File = Path.Combine(_directory, "lap.png");
        Tab.BrowseFileCommand.Execute(null);
        var row = Tab.SelectedSource!;
        row.Columns.Text = "4";
        row.Rows.Text = "2";
        row.FrameCount.Text = "7";

        row.Rows.Text = "3";

        Assert.Equal(7, row.Source.FrameCount);
    }

    [Fact]
    public async Task NewSource_CanBeUsedBeforeSavingAndThenCannotBeDeleted()
    {
        await _viewModel.LoadAsync();
        _browser.Folder = Path.Combine(AppContext.BaseDirectory, "assets", "movement", "walking");
        Tab.BrowseFolderCommand.Execute(null);

        _viewModel.CharacterTab.MovementRows.Single(r => r.State == MovementState.Walking).AnimationSourceId = -1;

        Assert.Equal("Walking", Tab.SelectedSource!.UsedByText);
        Assert.False(Tab.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeletedUnusedSource_LeavesTheCopyAndItsErrors()
    {
        await _viewModel.LoadAsync();
        _browser.File = Path.Combine(_directory, "nincs.gif");
        Tab.BrowseFileCommand.Execute(null);
        var row = Tab.SelectedSource!;
        await WaitUntilAsync(() => !row.IsChecking);

        Tab.DeleteCommand.Execute(null);

        Assert.DoesNotContain(row.Source, _viewModel.Data!.AnimationSources);
        Assert.DoesNotContain(row, Tab.Sources);
        Assert.Equal(0, _viewModel.ErrorCount);
        Assert.DoesNotContain(_viewModel.SourceOptions, o => o.Id == row.Source.Id);
        Assert.False(_viewModel.IsDirty);
    }

    [Fact]
    public async Task Replace_KeepsTheTypeAndChecksTheNewPath()
    {
        await _viewModel.LoadAsync();
        _browser.Folder = Path.Combine(_directory, "nincs-ilyen-mappa");

        Tab.ReplaceCommand.Execute(null);
        var row = Tab.SelectedSource!;
        await WaitUntilAsync(() => !row.IsChecking);

        Assert.Equal(_browser.Folder, row.Source.Path);
        Assert.Equal(AnimationSourceType.ImageFolder, row.Source.SourceType);
        Assert.True(row.HasAnyError);
    }

    [Fact]
    public async Task SavedNewSource_GetsARealIdAndReferencesFollow()
    {
        await _viewModel.LoadAsync();
        _browser.Folder = Path.Combine(AppContext.BaseDirectory, "assets", "movement", "walking");
        Tab.BrowseFolderCommand.Execute(null);
        await WaitUntilAsync(() => !Tab.SelectedSource!.IsChecking);
        _viewModel.CharacterTab.MovementRows.Single(r => r.State == MovementState.Walking).AnimationSourceId = -1;

        Assert.True(await _viewModel.SaveAsync());

        var saved = Tab.Sources.Single(s => s.Source.Name == "walking");
        Assert.True(saved.Source.Id > 0);
        Assert.Equal("Walking", saved.UsedByText);
        Assert.DoesNotContain(_viewModel.SourceOptions, o => o.Id < 0);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private sealed class TestBrowser : IMediaBrowser
    {
        public string? File { get; set; }

        public string? Folder { get; set; }

        public string? PickFile(IReadOnlyCollection<string> extensions) => File;

        public string? PickFolder() => Folder;
    }

    private sealed class AcceptingDialogs : IUserDialogs
    {
        public bool Confirm(string title, string message) => true;

        public CloseDecision AskUnsavedChanges() => CloseDecision.Discard;
    }
}
