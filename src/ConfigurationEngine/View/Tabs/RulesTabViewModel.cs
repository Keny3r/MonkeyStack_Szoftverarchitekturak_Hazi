namespace ConfigurationEngine.View.Tabs;

using System.Collections.ObjectModel;
using ConfigurationEngine.Model;

/// <summary>
/// A „Szabályok” fül (view.md, "3. Viselkedési szabályok (rétegek)"): felül a rétegek listája, alatta a kijelölt
/// szabály részletei. A lista csökkenő <c>ZIndex</c> szerint rendezett (a legfelső réteg áll legelöl); azonos
/// <c>ZIndex</c>-nél a kirajzolási sorrend fordítottja dönt, így mindig a felül rajzolt réteg áll elöl.
/// A betöltéskor kihagyott szabályok nem itt, hanem a figyelmeztető sáv Részletek paneljén jelennek meg.
/// </summary>
public sealed class RulesTabViewModel : TabViewModel
{
    private RuleRowViewModel? _selectedRule;

    public RulesTabViewModel(ConfigurationWindowViewModel owner)
        : base(owner)
    {
        NewRuleCommand = new RelayCommand(AddRule, () => Owner.Data is not null);
        DeleteCommand = new RelayCommand(DeleteSelected, () => SelectedRule is not null);
    }

    public override int TabIndex => ConfigurationWindowViewModel.RulesTabIndex;

    /// <summary>A rétegek listája, rendezve.</summary>
    public ObservableCollection<RuleRowViewModel> Rules { get; } = [];

    /// <summary>A kijelölt szabály; a részletek panel ezt mutatja.</summary>
    public RuleRowViewModel? SelectedRule
    {
        get => _selectedRule;
        set
        {
            if (SetProperty(ref _selectedRule, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                DeleteCommand.RaiseCanExecuteChanged();
                SyncGroupNames();
            }
        }
    }

    public bool HasSelection => SelectedRule is not null;

    public ObservableCollection<SourceOption> SourceOptions => Owner.SourceOptions;

    public IReadOnlyList<MetricOption> MetricOptions => MetricText.MetricOptions;

    public IReadOnlyList<OperatorOption> OperatorOptions => MetricText.OperatorOptions;

    /// <summary>A kizáró csoport legördülő listája: a többi szabály által már használt csoportnevek.</summary>
    public ObservableCollection<string> GroupNames { get; } = [];

    /// <summary>„+ Új szabály”.</summary>
    public RelayCommand NewRuleCommand { get; }

    /// <summary>„Törlés”: a kijelölt szabály törlése megerősítés nélkül (az Elvetés visszahozza).</summary>
    public RelayCommand DeleteCommand { get; }

    protected override IEnumerable<EditableItemViewModel> Items => Rules;

    /// <summary>
    /// A lista újrarendezése: csökkenő <c>ZIndex</c>, azonosnál a kirajzolási sorrend fordítottja. A kirajzolás
    /// növekvő <c>ZIndex</c>, azon belül növekvő <c>Id</c> szerint történik; a még nem mentett szabályok mentéskor a
    /// meglévőknél nagyobb azonosítót kapnak, a felvétel sorrendjében. A <c>ZIndex</c> mező elhagyásakor is fut.
    /// </summary>
    public void Resort()
    {
        if (Owner.Data is not { } data)
        {
            return;
        }

        var ordered = Rules
            .OrderByDescending(r => r.Rule.ZIndex)
            .ThenByDescending(r => r.Rule.Id <= 0)
            .ThenByDescending(r => r.Rule.Id <= 0 ? data.BehaviorRules.IndexOf(r.Rule) : r.Rule.Id)
            .ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var current = Rules.IndexOf(ordered[i]);
            if (current != i)
            {
                Rules.Move(current, i);
            }
        }
    }

    protected override void Rebuild(DataReloadReason reason)
    {
        var selectedId = SelectedRule?.Rule.Id;
        Rules.Clear();
        SelectedRule = null;
        if (Owner.Data is not { } data)
        {
            return;
        }

        foreach (var rule in data.BehaviorRules)
        {
            Rules.Add(new RuleRowViewModel(Owner, rule));
        }

        Resort();
        SelectedRule = Rules.FirstOrDefault(r => r.Rule.Id == selectedId) ?? Rules.FirstOrDefault();
        NewRuleCommand.RaiseCanExecuteChanged();
    }

    protected override void RefreshPreviews()
    {
        foreach (var row in Rules)
        {
            row.RefreshSummary();
            row.RefreshPreview();
        }

        SyncGroupNames();
    }

    protected override void Select(EditableItemViewModel item) => SelectedRule = item as RuleRowViewModel;

    /// <summary>
    /// Új szabály a lista legfelső rétegeként (<c>ZIndex</c> = a legnagyobb meglévő + 1, legfeljebb 100), alapértékekkel
    /// és kijelölve. Forrás nincs kiválasztva. Negatív ideiglenes azonosítót kap, így a hibái kulcsa a sorrendtől
    /// függetlenül egyértelmű (implementation.md, "Ideiglenes azonosítók").
    /// </summary>
    private void AddRule()
    {
        if (Owner.Data is not { } data)
        {
            return;
        }

        var topZIndex = data.BehaviorRules.Count == 0 ? -1 : data.BehaviorRules.Max(r => r.ZIndex);
        var rule = new BehaviorRule
        {
            Id = Math.Min(0, data.BehaviorRules.Select(r => r.Id).DefaultIfEmpty(0).Min()) - 1,
            ZIndex = Math.Min(topZIndex + 1, ConfigurationLimits.ZIndex.Max)
        };
        data.BehaviorRules.Add(rule);

        var row = new RuleRowViewModel(Owner, rule);
        Rules.Add(row);
        Resort();
        SelectedRule = row;
        Owner.NotifyEdited();
    }

    /// <summary>A kijelölt szabály törlése a szerkesztett másolatból; a kijelölés a következő sorra lép.</summary>
    private void DeleteSelected()
    {
        if (SelectedRule is not { } row || Owner.Data is not { } data)
        {
            return;
        }

        var index = Rules.IndexOf(row);
        row.OnRemoved();
        data.BehaviorRules.Remove(row.Rule);
        Rules.Remove(row);
        SelectedRule = Rules.Count == 0 ? null : Rules[Math.Min(index, Rules.Count - 1)];
        Owner.NotifyEdited();
    }

    /// <summary>
    /// A csoportnevek helyben frissítése, hogy a szerkeszthető legördülő lista szövege ne ugráljon. A motorhoz
    /// igazodva a kis- és nagybetű nem számít (<c>Fire</c> és <c>fire</c> ugyanaz a csoport), ezért egyszer szerepel.
    /// </summary>
    private void SyncGroupNames()
    {
        var names = Rules
            .Where(r => r != SelectedRule && !string.IsNullOrWhiteSpace(r.Rule.ExclusiveGroup))
            .Select(r => r.Rule.ExclusiveGroup!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCulture)
            .ToList();
        if (names.SequenceEqual(GroupNames))
        {
            return;
        }

        GroupNames.Clear();
        foreach (var name in names)
        {
            GroupNames.Add(name);
        }
    }
}
