namespace ConfigurationEngine.View.Tabs;

using System.Globalization;
using System.IO;
using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;

/// <summary>
/// Egy animációs forrás a „Források” fülön (view.md, "4. Animációs források"): a lista sora és a részletek panel mezői.
/// Itt csak a forrás betöltési módja állítható; a sebesség és a réteg a felhasználás helyén van. A típust a tallózás
/// határozza meg, ezért a részletekben csak felirat.
/// </summary>
public sealed class SourceRowViewModel : EditableItemViewModel
{
    /// <summary>A Források fül előnézetének sebessége (a forrásnak nincs saját sebessége; a mozgási animáció alapértéke).</summary>
    private static readonly double PreviewFramesPerSecond = new MovementAnimation().FramesPerSecond;

    private static readonly string[] GridProperties =
    [
        nameof(AnimationSource.FrameWidth),
        nameof(AnimationSource.FrameHeight),
        nameof(AnimationSource.Columns),
        nameof(AnimationSource.Rows),
        nameof(AnimationSource.FrameCount)
    ];

    private int _checkVersion;
    private bool _isChecking;
    private int? _lastGridProduct;

    public SourceRowViewModel(ConfigurationWindowViewModel owner, AnimationSource source)
        : base(owner)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Name = Field(nameof(AnimationSource.Name), () => Source.Name, text =>
        {
            Source.Name = text;
            return null;
        });
        FrameWidth = OptionalIntField(nameof(AnimationSource.FrameWidth), () => Source.FrameWidth, v => Source.FrameWidth = v);
        FrameHeight = OptionalIntField(nameof(AnimationSource.FrameHeight), () => Source.FrameHeight, v => Source.FrameHeight = v);
        Columns = OptionalIntField(nameof(AnimationSource.Columns), () => Source.Columns, v => Source.Columns = v);
        Rows = OptionalIntField(nameof(AnimationSource.Rows), () => Source.Rows, v => Source.Rows = v);
        FrameCount = OptionalIntField(nameof(AnimationSource.FrameCount), () => Source.FrameCount, v => Source.FrameCount = v);
        Preview = owner.Previews.Create();
        _lastGridProduct = Source.Columns * Source.Rows;
    }

    public AnimationSource Source { get; }

    public override ConfigurationItemKind Kind => ConfigurationItemKind.AnimationSource;

    public override string ItemKey =>
        ItemKeys.For(Source, Owner.Data?.AnimationSources.IndexOf(Source) ?? 0);

    /// <summary>Név, 1–64 karakter, egyedi.</summary>
    public InputField Name { get; }

    /// <summary>Egy képkocka szélessége (px, 1–1024); csak sprite sheetnél.</summary>
    public InputField FrameWidth { get; }

    /// <summary>Egy képkocka magassága (px, 1–1024); csak sprite sheetnél.</summary>
    public InputField FrameHeight { get; }

    /// <summary>A rács oszlopai (1–64); csak sprite sheetnél.</summary>
    public InputField Columns { get; }

    /// <summary>A rács sorai (1–64); csak sprite sheetnél.</summary>
    public InputField Rows { get; }

    /// <summary>A képkockák száma (1 – <c>Columns × Rows</c>); alapértéke <c>Columns × Rows</c>; csak sprite sheetnél.</summary>
    public InputField FrameCount { get; }

    public PreviewViewModel Preview { get; }

    public bool IsSpriteSheet => Source.SourceType == AnimationSourceType.SpriteSheet;

    public bool IsFolder => Source.SourceType == AnimationSourceType.ImageFolder;

    /// <summary>A típus felirata: GIF, Sprite sheet, Képmappa.</summary>
    public string TypeText => Source.SourceType switch
    {
        AnimationSourceType.Gif => "GIF",
        AnimationSourceType.SpriteSheet => "Sprite sheet",
        AnimationSourceType.ImageFolder => "Képmappa",
        _ => Source.SourceType.ToString()
    };

    /// <summary>A név a listában és a részletek fejlécében.</summary>
    public string DisplayName => Source.Name;

    /// <summary>A teljes útvonal (súgóként).</summary>
    public string FullPath => Source.Path;

    /// <summary>Az útvonal rövid alakja a listában, pl. <c>…\fire_big.gif</c> vagy <c>…\pyjama\</c>.</summary>
    public string PathText => ShortPath(includeRoot: false);

    /// <summary>Az útvonal a részletek panelen, a meghajtóval, pl. <c>C:\…\fire_big.gif</c> (view.md, "4. Animációs források").</summary>
    public string DetailsPathText => ShortPath(includeRoot: true);

    /// <summary>A forrást használó mozgásállapotok és szabályok a szerkesztett másolat alapján.</summary>
    public IReadOnlyList<string> UsedBy
    {
        get
        {
            if (Owner.Data is not { } data)
            {
                return [];
            }

            return
            [
                .. data.MovementAnimations.Where(m => m.AnimationSourceId == Source.Id).Select(m => m.MovementState.ToString()),
                .. data.BehaviorRules.Where(r => r.AnimationSourceId == Source.Id)
                    .Select(r => string.IsNullOrWhiteSpace(r.Name) ? "(névtelen szabály)" : r.Name)
            ];
        }
    }

    /// <summary>A „Használja” oszlop, pl. <c>Walking, Kis tűz</c>, vagy <c>–</c>.</summary>
    public string UsedByText => UsedBy.Count == 0 ? "–" : string.Join(", ", UsedBy);

    /// <summary>Amíg mozgási animáció vagy szabály használja, a forrás nem törölhető.</summary>
    public bool IsUsed => UsedBy.Count > 0;

    /// <summary>A tallózás vagy csere utáni teljes médiaellenőrzés fut; a sor „ellenőrzés…” állapotot mutat.</summary>
    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    /// <summary>
    /// Sprite sheetnél a rácsból számolt képméret (<c>Columns × FrameWidth</c> × <c>Rows × FrameHeight</c>), hogy eltérés
    /// esetén látszódjon, melyik értéket kell javítani.
    /// </summary>
    public string GridSizeText =>
        Source is { FrameWidth: { } w, FrameHeight: { } h, Columns: { } c, Rows: { } r }
            ? string.Create(CultureInfo.CurrentCulture, $"A rácsból számolt képméret: {c * w} × {r * h} px")
            : string.Empty;

    /// <summary>A lista sorának és a részletek feliratainak frissítése egy módosítás után.</summary>
    public void RefreshSummary()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(PathText));
        OnPropertyChanged(nameof(DetailsPathText));
        OnPropertyChanged(nameof(FullPath));
        OnPropertyChanged(nameof(UsedBy));
        OnPropertyChanged(nameof(UsedByText));
        OnPropertyChanged(nameof(IsUsed));
        OnPropertyChanged(nameof(GridSizeText));
    }

    /// <summary>Az előnézet a szerkesztett forrást mutassa (útvonal, sprite sheet rács).</summary>
    public void RefreshPreview() => Preview.Show(Source, PreviewFramesPerSecond);

    /// <summary>„Csere…”: ugyanilyen típusú fájl vagy mappa; utána a teljes médiaellenőrzés a háttérben fut.</summary>
    public void ReplacePath(string path)
    {
        Source.Path = path;
        Edited();
        StartMediaCheck();
    }

    /// <summary>
    /// A teljes (<c>Full</c>) médiaellenőrzés a háttérben (view.md, "Tallózás"); közben a sor „ellenőrzés…” állapotú,
    /// a felület nem blokkolódik. Ha közben újabb ellenőrzés indul, a régebbi eredménye elvész.
    /// </summary>
    public void StartMediaCheck() => _ = CheckMediaAsync();

    /// <summary>A sor törlésekor: a beviteli hibái megszűnnek, az előnézete eldobódik, a futó ellenőrzés eredménye elvész.</summary>
    public override void OnRemoved()
    {
        _checkVersion++;
        IsChecking = false;
        base.OnRemoved();
        Preview.Release();
    }

    protected override void OnFieldCommitted(string propertyName)
    {
        if (!GridProperties.Contains(propertyName))
        {
            return;
        }

        // A FrameCount alapértéke Columns × Rows, amíg a felhasználó nem írt be mást.
        if (propertyName is nameof(AnimationSource.Columns) or nameof(AnimationSource.Rows))
        {
            var product = Source.Columns * Source.Rows;
            if (product is { } p && (Source.FrameCount is null || Source.FrameCount == _lastGridProduct))
            {
                Source.FrameCount = p;
                FrameCount.Refresh();
            }

            _lastGridProduct = product;
        }

        // A teljes médiaellenőrzés tallózás / csere után és mentéskor fut (view.md, "Mezőnkénti hibajelzés"). A rács
        // módosítása után a korábbi eredmény (pl. rácseltérés) elavult, ezért törlődik; addig az előnézet mutatja,
        // ha a forrás így nem tölthető be, mentéskor pedig az ellenőrzés újra lefut.
        _checkVersion++;
        IsChecking = false;
        Owner.ClearMediaErrors(Source.Id);
    }

    /// <summary>
    /// Az útvonal rövid alakja: a fájl vagy mappa neve <c>…\</c> után (mappánál záró <c>\</c>-sel), a részletek panelen
    /// a meghajtóval együtt. A relatív (szállított) útvonal és a közvetlenül a meghajtón lévő fájl változatlan marad.
    /// </summary>
    private string ShortPath(bool includeRoot)
    {
        var path = Source.Path;
        if (!Path.IsPathRooted(path))
        {
            return path;
        }

        var trimmed = path.TrimEnd('\\', '/');
        var root = Path.GetPathRoot(trimmed) ?? string.Empty;
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(name) || string.Equals(Path.GetDirectoryName(trimmed), root, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var suffix = IsFolder ? "\\" : string.Empty;
        return includeRoot ? $"{root.TrimEnd('\\', '/')}\\…\\{name}{suffix}" : $"…\\{name}{suffix}";
    }

    private async Task CheckMediaAsync()
    {
        var version = ++_checkVersion;
        var snapshot = PreviewViewModel.Snapshot(Source);
        IsChecking = true;
        IReadOnlyList<ValidationError> errors;
        try
        {
            errors = await Owner.ValidateMediaAsync(snapshot);
        }
        catch (Exception ex)
        {
            errors = [new ValidationError(Kind, ItemKey, nameof(AnimationSource.Path), ex.Message)];
        }

        if (version != _checkVersion)
        {
            return;
        }

        // Az „ellenőrzés…” állapot csak akkor ér véget, amikor az eredmény már a hibák között van.
        Owner.SetMediaErrors(snapshot.Id, errors);
        IsChecking = false;
    }
}
