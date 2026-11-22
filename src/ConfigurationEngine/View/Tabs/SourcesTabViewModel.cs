namespace ConfigurationEngine.View.Tabs;

using System.Collections.ObjectModel;
using System.IO;
using ConfigurationEngine.Model;

/// <summary>
/// A „Források” fül (gui.md, "4. Animációs források"): az animációs források könyvtára lista + részletek elrendezésben.
/// Tallózással új forrás vehető fel; a használt forrás nem törölhető.
/// </summary>
public sealed class SourcesTabViewModel : TabViewModel
{
    private readonly IMediaBrowser? _browser;
    private SourceRowViewModel? _selectedSource;

    public SourcesTabViewModel(ConfigurationWindowViewModel owner, IMediaBrowser? browser)
        : base(owner)
    {
        _browser = browser;
        BrowseFileCommand = new RelayCommand(BrowseFile, () => Owner.Data is not null && _browser is not null);
        BrowseFolderCommand = new RelayCommand(BrowseFolder, () => Owner.Data is not null && _browser is not null);
        ReplaceCommand = new RelayCommand(Replace, () => SelectedSource is not null && _browser is not null);
        DeleteCommand = new RelayCommand(DeleteSelected, () => SelectedSource is { IsUsed: false });
    }

    public override int TabIndex => ConfigurationWindowViewModel.SourcesTabIndex;

    public ObservableCollection<SourceRowViewModel> Sources { get; } = [];

    public SourceRowViewModel? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                RefreshCommands();
            }
        }
    }

    public bool HasSelection => SelectedSource is not null;

    /// <summary>„+ Fájl tallózása…”: <c>.gif</c> esetén GIF, egyébként sprite sheet típusú forrás jön létre.</summary>
    public RelayCommand BrowseFileCommand { get; }

    /// <summary>„+ Mappa tallózása…”: képsorozat-mappa típusú forrás jön létre.</summary>
    public RelayCommand BrowseFolderCommand { get; }

    /// <summary>„Csere…”: ugyanilyen típusú fájl vagy mappa választása a kijelölt forráshoz.</summary>
    public RelayCommand ReplaceCommand { get; }

    /// <summary>„Törlés”: csak akkor aktív, ha a kijelölt forrást semmi nem használja.</summary>
    public RelayCommand DeleteCommand { get; }

    /// <summary>A Törlés gomb súgója használt forrásnál, pl. „Nem törölhető, használja: Walking, Kis tűz”.</summary>
    public string? DeleteToolTip =>
        SelectedSource is { IsUsed: true } source ? $"Nem törölhető, használja: {source.UsedByText}" : null;

    protected override IEnumerable<EditableItemViewModel> Items => Sources;

    /// <summary>
    /// Új forrás a tallózott fájlból vagy mappából: a neve a fájl vagy mappa neve (foglalt névnél sorszámmal, kis- és
    /// nagybetűtől függetlenül), egyedi negatív ideiglenes azonosítót kap, kijelölődik, és a háttérben lefut a teljes
    /// médiaellenőrzése.
    /// </summary>
    public SourceRowViewModel? AddSource(string path, AnimationSourceType type)
    {
        if (Owner.Data is not { } data)
        {
            return null;
        }

        var source = new AnimationSource
        {
            Id = Math.Min(0, data.AnimationSources.Select(s => s.Id).DefaultIfEmpty(0).Min()) - 1,
            Name = UniqueName(BaseName(path, type), data),
            SourceType = type,
            Path = path
        };
        data.AnimationSources.Add(source);

        var row = new SourceRowViewModel(Owner, source);
        Sources.Add(row);
        SelectedSource = row;
        Owner.NotifyEdited();
        row.StartMediaCheck();
        return row;
    }

    protected override void Rebuild(DataReloadReason reason)
    {
        var selectedId = SelectedSource?.Source.Id;
        Sources.Clear();
        SelectedSource = null;
        if (Owner.Data is not { } data)
        {
            return;
        }

        foreach (var source in data.AnimationSources)
        {
            Sources.Add(new SourceRowViewModel(Owner, source));
        }

        SelectedSource = Sources.FirstOrDefault(s => s.Source.Id == selectedId) ?? Sources.FirstOrDefault();
        RefreshCommands();
    }

    protected override void RefreshPreviews()
    {
        foreach (var row in Sources)
        {
            row.RefreshSummary();
            row.RefreshPreview();
        }

        RefreshCommands();
    }

    protected override void Select(EditableItemViewModel item) => SelectedSource = item as SourceRowViewModel;

    /// <summary>A tallózott fájl vagy mappa neve (kiterjesztés nélkül).</summary>
    private static string BaseName(string path, AnimationSourceType type)
    {
        var trimmed = path.TrimEnd('\\', '/');
        return type == AnimationSourceType.ImageFolder
            ? Path.GetFileName(trimmed)
            : Path.GetFileNameWithoutExtension(trimmed);
    }

    /// <summary>Foglalt névnél sorszám: <c>fire (2)</c>, <c>fire (3)</c>, … (kis- és nagybetűtől függetlenül).</summary>
    private static string UniqueName(string name, ConfigurationData data)
    {
        var taken = data.AnimationSources.Select(s => s.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name))
        {
            return name;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{name} ({i})";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static IReadOnlyCollection<string> ExtensionsFor(AnimationSourceType type) => type switch
    {
        AnimationSourceType.Gif => [.. ConfigurationLimits.GifExtensions],
        AnimationSourceType.SpriteSheet => [.. ConfigurationLimits.ImageExtensions],
        _ => [.. ConfigurationLimits.GifExtensions, .. ConfigurationLimits.ImageExtensions]
    };

    private void BrowseFile()
    {
        var path = _browser?.PickFile([.. ConfigurationLimits.GifExtensions, .. ConfigurationLimits.ImageExtensions]);
        if (path is null)
        {
            return;
        }

        var type = ConfigurationLimits.GifExtensions.Contains(Path.GetExtension(path))
            ? AnimationSourceType.Gif
            : AnimationSourceType.SpriteSheet;
        AddSource(path, type);
    }

    private void BrowseFolder()
    {
        if (_browser?.PickFolder() is { } path)
        {
            AddSource(path, AnimationSourceType.ImageFolder);
        }
    }

    private void Replace()
    {
        if (SelectedSource is not { } row || _browser is null)
        {
            return;
        }

        var path = row.IsFolder ? _browser.PickFolder() : _browser.PickFile(ExtensionsFor(row.Source.SourceType));
        if (path is not null)
        {
            row.ReplacePath(path);
        }
    }

    /// <summary>A kijelölt, nem használt forrás törlése a szerkesztett másolatból; a kijelölés a következő sorra lép.</summary>
    private void DeleteSelected()
    {
        if (SelectedSource is not { IsUsed: false } row || Owner.Data is not { } data)
        {
            return;
        }

        var index = Sources.IndexOf(row);
        row.OnRemoved();
        data.AnimationSources.Remove(row.Source);
        Sources.Remove(row);
        SelectedSource = Sources.Count == 0 ? null : Sources[Math.Min(index, Sources.Count - 1)];
        Owner.NotifyEdited();
    }

    private void RefreshCommands()
    {
        BrowseFileCommand.RaiseCanExecuteChanged();
        BrowseFolderCommand.RaiseCanExecuteChanged();
        ReplaceCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(DeleteToolTip));
    }
}
